/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.Runner.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 纯执行引擎（UI 与 CLI 共用）
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            /// <summary>
            /// 纯执行引擎：按 Batch Item 顺序执行，任一步 throw 即中断。
            /// 不依赖 UI，不做 CLI 解析；由外部 public 入口对外暴露。
            /// </summary>
            internal static class Runner
            {
                /// <summary>
                /// 执行 Batch。
                /// </summary>
                /// <param name="batch">待执行 Batch。</param>
                /// <param name="reporter">进度汇报宿主。</param>
                /// <param name="overrides">参数覆盖字典（可为 null）。</param>
                /// <param name="ct">取消令牌。</param>
                public static async UniTask RunBatchAsync(Batch batch, IPipifyProgressReporter reporter, IReadOnlyDictionary<string, string> overrides, CancellationToken ct)
                {
                    if (batch == null) throw new ArgumentNullException(nameof(batch));
                    if (reporter == null) throw new ArgumentNullException(nameof(reporter));

                    Stopwatch total = Stopwatch.StartNew();
                    reporter.BeginBatch(batch.Name, batch.Items.Count);
                    bool success = false;
                    // 不冻结 Domain Reload 也不进入 Asset Editing：
                    // LockReloadAssemblies 期间 SBP BuildCache 看到的 .bytes contentHash 不会随 ImportAsset 同步刷新，
                    // 导致 bundlebuilder.build 命中陈旧 cache 复用上一轮 bundle 产物。
                    // batch 流程不向 Assets 写 .cs，单纯 .bytes / .json 写入只触发 importer，不会触发 cs 编译 + Domain Reload。
                    try
                    {
                        for (int i = 0; i < batch.Items.Count; i++)
                        {
                            ct.ThrowIfCancellationRequested();
                            BatchItem item = batch.Items[i];
                            PipifyStepInfo info = Registry.FindById(item.StepId);
                            if (info == null) throw new InvalidOperationException(string.Format("{0} 未注册的 StepId：{1}", c_LogPrefix, item.StepId));

                            object paramsInstance = null;
                            if (info.ParamsType != null)
                            {
                                paramsInstance = string.IsNullOrEmpty(item.ParamsJson)
                                    ? Activator.CreateInstance(info.ParamsType)
                                    : Util.Json.Deserialize(item.ParamsJson, info.ParamsType);
                                ApplyOverridesForItem(info, i, paramsInstance, overrides);
                            }

                            PipifyContext ctx = new PipifyContext
                            {
                                BatchName = batch.Name,
                                CurrentStepIndex = i,
                                TotalSteps = batch.Items.Count,
                                Reporter = reporter,
                                CancellationToken = ct
                            };

                            if (reporter.ReportStep(i, info.DisplayName, 0f)) throw new OperationCanceledException(ct);

                            Stopwatch sw = Stopwatch.StartNew();
                            try
                            {
                                object[] args = info.ParamsType == null ? new object[] { ctx } : new object[] { ctx, paramsInstance };
                                UniTask invoked = (UniTask)info.Method.Invoke(null, args);
                                await invoked;
                                sw.Stop();
                                reporter.EndStep(i, true, sw.Elapsed, null);
                            }
                            catch (TargetInvocationException tie)
                            {
                                sw.Stop();
                                Exception toThrow = tie.InnerException ?? tie;
                                reporter.EndStep(i, false, sw.Elapsed, toThrow);
                                ExceptionDispatchInfo.Capture(toThrow).Throw();
                                throw; // 编译器可达性占位，实际不执行
                            }
                            catch (Exception ex)
                            {
                                sw.Stop();
                                reporter.EndStep(i, false, sw.Elapsed, ex);
                                throw;
                            }
                        }
                        success = true;
                    }
                    finally
                    {
                        // 故意不在这里调 AssetDatabase.Refresh()：Refresh 会立刻触发 C# 编译 + Domain Reload，
                        // 若 Console 勾选 "Clear on Recompile" 会把本批次所有日志一次性清空。
                        // 产物会在 Unity 下次获得焦点时由 AutoRefresh 自动扫描，用户可先看完日志再切焦点触发编译。
                        total.Stop();
                        reporter.EndBatch(success, total.Elapsed);
                    }
                }
            }
        }
    }
}
