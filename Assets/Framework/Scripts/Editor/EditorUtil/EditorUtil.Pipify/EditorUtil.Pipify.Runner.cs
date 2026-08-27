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
                /// 防止同一 Unity Editor 进程并发执行两个会共享全局工作区与 AssetDatabase 的 Batch。
                /// </summary>
                private static bool s_IsRunning;

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
                    if (s_IsRunning) throw new InvalidOperationException($"{c_LogPrefix} 已有 Batch 正在执行，不能并发启动第二个 Batch。");
                    if (!Config.WorkspaceActive.TryGetPersistedConfigMaster(
                            out _, out string frozenMasterGuid, out _, out string masterError))
                    {
                        throw new InvalidOperationException($"{c_LogPrefix} {masterError}");
                    }
                    if (!Config.WorkspaceActive.TryGetPersistedPipifySettings(
                            out PipifySettingsSO frozenSettings, out string frozenPipifyGuid, out _, out string pipifyError))
                    {
                        throw new InvalidOperationException($"{c_LogPrefix} {pipifyError}");
                    }

                    Stopwatch total = Stopwatch.StartNew();
                    reporter.BeginBatch(batch.Name, batch.Items.Count);
                    bool success = false;
                    s_IsRunning = true;
                    // 不冻结 Domain Reload 也不进入 Asset Editing：
                    // LockReloadAssemblies 期间 SBP BuildCache 看到的 .bytes contentHash 不会随 ImportAsset 同步刷新，
                    // 导致 bundlebuilder.build 命中陈旧 cache 复用上一轮 bundle 产物。
                    // batch 流程不向 Assets 写 .cs，单纯 .bytes / .json 写入只触发 importer，不会触发 cs 编译 + Domain Reload。
                    try
                    {
                        PipifySettingsSO settings = FindSettingsContaining(batch);
                        if (!ReferenceEquals(settings, frozenSettings))
                        {
                            throw new InvalidOperationException($"{c_LogPrefix} Batch 所属 PipifySettings 与冻结工作区不一致。");
                        }
                        for (int i = 0; i < batch.Items.Count; i++)
                        {
                            ct.ThrowIfCancellationRequested();
                            ValidateFrozenWorkspace(frozenMasterGuid, frozenPipifyGuid);
                            BatchItem item = batch.Items[i];
                            PipifyStepInfo info = Registry.FindById(item.StepId);
                            if (info == null) throw new InvalidOperationException(string.Format("{0} 未注册的 StepId：{1}", c_LogPrefix, item.StepId));

                            object paramsInstance = ResolveParamsForRun(
                                info,
                                i,
                                item,
                                settings,
                                overrides);

                            PipifyContext ctx = new PipifyContext
                            {
                                BatchName = batch.Name,
                                CurrentStepIndex = i,
                                TotalSteps = batch.Items.Count,
                                Reporter = reporter,
                                CancellationToken = ct
                            };

                            bool? followingDevelopmentBuild = IsHybridCLRStep(item.StepId)
                                ? ResolveFollowingPackageDevelopmentBuild(batch, i, settings, overrides)
                                : null;
                            bool previousDevelopmentBuild = EditorUserBuildSettings.development;
                            try
                            {
                                if (followingDevelopmentBuild.HasValue)
                                {
                                    EditorUserBuildSettings.development = followingDevelopmentBuild.Value;
                                    Log.Debug(LogTag.Editor,
                                        "{0} 对齐 HybridCLR 开发构建：step={1}, developmentBuild={2}",
                                        c_LogPrefix, item.StepId, followingDevelopmentBuild.Value);
                                }

                                if (reporter.ReportStep(i, info.DisplayName, 0f)) throw new OperationCanceledException(ct);

                                Stopwatch sw = Stopwatch.StartNew();
                                try
                                {
                                    object[] args = info.ParamsType == null ? new object[] { ctx } : new object[] { ctx, paramsInstance };
                                    UniTask invoked = (UniTask)info.Method.Invoke(null, args);
                                    await invoked;
                                    ValidateFrozenWorkspace(frozenMasterGuid, frozenPipifyGuid);
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
                            finally
                            {
                                if (followingDevelopmentBuild.HasValue)
                                {
                                    EditorUserBuildSettings.development = previousDevelopmentBuild;
                                }
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
                        s_IsRunning = false;
                        reporter.EndBatch(success, total.Elapsed);
                    }
                }

                /// <summary>
                /// 验证 Batch 启动时冻结的 Master/Pipify GUID 在步骤前后均未变化。
                /// 检测到场景切换或外部改写时立即中断，防止后续 Step 混用另一工作区。
                /// </summary>
                /// <param name="masterGuid">Batch 启动时的 Master GUID。</param>
                /// <param name="pipifyGuid">Batch 启动时的 Pipify GUID。</param>
                private static void ValidateFrozenWorkspace(string masterGuid, string pipifyGuid)
                {
                    if (!Config.WorkspaceActive.TryGetPersistedConfigMaster(
                            out _, out string activeMasterGuid, out _, out string masterError))
                    {
                        throw new InvalidOperationException($"{c_LogPrefix} Batch 执行期间工作区失效：{masterError}");
                    }
                    if (!Config.WorkspaceActive.TryGetPersistedPipifySettings(
                            out _, out string activePipifyGuid, out _, out string pipifyError))
                    {
                        throw new InvalidOperationException($"{c_LogPrefix} Batch 执行期间工作区失效：{pipifyError}");
                    }
                    if (!string.Equals(masterGuid, activeMasterGuid, StringComparison.OrdinalIgnoreCase) ||
                        !string.Equals(pipifyGuid, activePipifyGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"{c_LogPrefix} Batch 执行期间活动工作区发生变化，已中断以避免跨工作区混合产物。");
                    }
                }

                /// <summary>
                /// 解析当前步骤之后首个 Player 打包项的 DevelopmentBuild（含 CLI 覆盖）。
                /// HybridCLR 的预生成命令读取全局 EditorUserBuildSettings，必须与最终 BuildPlayer 选项一致。
                /// </summary>
                /// <returns>找到后续 build.package 时返回最终开发构建值；没有后续 Player 构建时返回 null。</returns>
                internal static bool? ResolveFollowingPackageDevelopmentBuild(
                    Batch batch,
                    int currentStepIndex,
                    PipifySettingsSO settings,
                    IReadOnlyDictionary<string, string> overrides)
                {
                    if (batch == null) throw new ArgumentNullException(nameof(batch));

                    for (int i = currentStepIndex + 1; i < batch.Items.Count; i++)
                    {
                        BatchItem candidate = batch.Items[i];
                        if (!string.Equals(candidate.StepId, "build.package", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        PipifyStepInfo packageInfo = Registry.FindById(candidate.StepId);
                        if (packageInfo == null)
                        {
                            throw new InvalidOperationException(string.Format(
                                "{0} 未注册的 StepId：{1}",
                                c_LogPrefix,
                                candidate.StepId));
                        }

                        PipifySteps.PackageParams packageParams = ResolveParamsForRun(
                            packageInfo,
                            i,
                            candidate,
                            settings,
                            overrides) as PipifySteps.PackageParams;
                        if (packageParams == null)
                        {
                            throw new InvalidOperationException(string.Format(
                                "{0} StepId {1} 的参数类型必须是 {2}",
                                c_LogPrefix,
                                candidate.StepId,
                                typeof(PipifySteps.PackageParams).FullName));
                        }

                        return packageParams.DevelopmentBuild;
                    }

                    return null;
                }

                private static bool IsHybridCLRStep(string stepId)
                {
                    return !string.IsNullOrEmpty(stepId)
                           && stepId.StartsWith("hybridclr.", StringComparison.Ordinal);
                }
            }
        }
    }
}
