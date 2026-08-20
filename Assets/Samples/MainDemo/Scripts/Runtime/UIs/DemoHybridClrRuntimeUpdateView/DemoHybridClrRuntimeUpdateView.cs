/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoHybridClrRuntimeUpdateView.cs
 * author:    taoye
 * created:   2026/8/19
 * descrip:   HybridCLR 3.4 — 固定当前 Manifest 的运行时增量热更新完整演示
 ***************************************************************/

using System;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// HybridCLR Demo 3.4：按当前会话 Manifest 执行 Tag 下载、DLL 加载、业务入口与场景加载。
    /// 不刷新资源版本或 Manifest，避免运行过程中切换资源视图。
    /// </summary>
    public sealed class DemoHybridClrRuntimeUpdateView : BaseDemoView
    {
        private const string c_RuntimeTag = "demo_runtime_hotupdate";
        private const string c_DllLocation = "NovaFramework.Samples.Running.dll";
        private const string c_EntryTypeName = "NovaFramework.Samples.Running.RuntimeHotUpdateEntry";
        private const string c_EntryMethodName = "Activate";
        private const string c_SceneLocation = "DemoRuntimeHotUpdateContent";

        /// <summary>
        /// 执行运行时增量热更新按钮。
        /// </summary>
        [SerializeField] private Button m_RunButton;

        /// <summary>
        /// 当前增量流程取消令牌源；关闭页面时取消尚未完成的下载或加载。
        /// </summary>
        private CancellationTokenSource m_RunCts;

        /// <summary>
        /// 已加载的增量场景句柄；页面关闭时按资源句柄协议卸载。
        /// </summary>
        private ISceneHandle m_RuntimeSceneHandle;

        /// <summary>
        /// 初始化演示页并绑定运行时增量热更新按钮。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        protected override void OnInit(object userData)
        {
            base.OnInit(userData);
            SetTitle("运行时增量热更新");

            if (m_RunButton != null)
            {
                m_RunButton.onClick.AddListener(OnRunButtonClick);
                SetButtonApiHint(m_RunButton, "CreateDownloaderByTags(tags) -> LoadGameAssemblyAsync(dll) -> Entry -> LoadSceneAsync(scene)");
            }
        }

        /// <summary>
        /// 打开页面时展示本演示采用的固定 Manifest 与 AOT 前置约束。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            ClearFeedback();
            AppendFeedback("本次运行固定使用启动阶段已加载的 Manifest，不调用 RefreshManifestAsync。", FeedbackLevel.Info);
            AppendFeedback("AOT Metadata 已由 ProcedureLoadDll 在启动阶段统一补充。", FeedbackLevel.Info);
            AppendFeedback("执行顺序：Tag 下载 -> Running DLL -> 业务入口 -> 增量场景。", FeedbackLevel.Info);
        }

        /// <summary>
        /// 关闭页面时取消流程，并在非应用关闭场景下卸载增量场景。
        /// </summary>
        /// <param name="isShutdown">是否因应用关闭触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            CancelRun();
            if (!isShutdown)
            {
                UnloadRuntimeSceneAsync().Forget();
            }
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 按钮点击回调，启动运行时增量热更新流程。
        /// </summary>
        private void OnRunButtonClick()
        {
            RunRuntimeUpdateAsync().Forget();
        }

        /// <summary>
        /// 基于当前 Manifest 完成 Tag 下载、独立 DLL 加载、入口激活和增量场景加载。
        /// </summary>
        private async UniTaskVoid RunRuntimeUpdateAsync()
        {
            if (Nova.Asset == null)
            {
                AppendFeedback("Nova.Asset 未初始化。", FeedbackLevel.Error);
                return;
            }

            CancelRun();
            m_RunCts = new CancellationTokenSource();
            CancellationToken ct = m_RunCts.Token;

            try
            {
                await UnloadRuntimeSceneAsync();

                IAssetDownloader downloader = Nova.Asset.CreateDownloaderByTags(new[] { c_RuntimeTag });
                AppendFeedback(string.Format("[1] Tag={0} -> 文件数={1}", c_RuntimeTag, downloader.TotalCount), FeedbackLevel.Info);
                if (!downloader.IsEmpty)
                {
                    bool downloaded = await downloader.RunAsync(ct);
                    if (!downloaded)
                    {
                        AppendFeedback("[1] 增量内容下载失败。", FeedbackLevel.Error);
                        return;
                    }
                    AppendFeedback("[1] 当前 Manifest 对应增量内容下载完成。", FeedbackLevel.Success);
                }
                else
                {
                    AppendFeedback("[1] 当前环境无需下载，继续验证本地缓存内容。", FeedbackLevel.Info);
                }

                Assembly assembly = await Util.HybridCLR.LoadGameAssemblyAsync(c_DllLocation);
                ct.ThrowIfCancellationRequested();
                if (assembly == null)
                {
                    throw new InvalidOperationException("运行时 DLL 未加载：" + c_DllLocation);
                }
                AppendFeedback("[2] Running DLL loaded -> " + assembly.GetName().Name, FeedbackLevel.Success);

                Type entryType = assembly.GetType(c_EntryTypeName, throwOnError: false);
                MethodInfo entryMethod = entryType?.GetMethod(c_EntryMethodName, BindingFlags.Public | BindingFlags.Static);
                if (entryMethod == null)
                {
                    throw new MissingMethodException(c_EntryTypeName, c_EntryMethodName);
                }
                string entryResult = entryMethod.Invoke(null, null) as string;
                AppendFeedback("[3] Entry -> " + entryResult, FeedbackLevel.Success);

                m_RuntimeSceneHandle = await Nova.Asset.LoadSceneAsync(c_SceneLocation, LoadSceneMode.Additive, ct);
                ct.ThrowIfCancellationRequested();
                if (m_RuntimeSceneHandle == null || !m_RuntimeSceneHandle.IsValid)
                {
                    throw new InvalidOperationException("增量场景加载失败：" + c_SceneLocation);
                }
                AppendFeedback("[4] Scene loaded -> " + c_SceneLocation, FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("运行时增量热更新已取消。", FeedbackLevel.Warn);
            }
            catch (TargetInvocationException e)
            {
                Exception cause = e.InnerException ?? e;
                AppendFeedback("业务入口执行失败：" + cause.Message, FeedbackLevel.Error);
            }
            catch (Exception e)
            {
                AppendFeedback("运行时增量热更新失败：" + e.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 取消当前运行时增量任务并释放取消令牌源。
        /// </summary>
        private void CancelRun()
        {
            m_RunCts?.Cancel();
            m_RunCts?.Dispose();
            m_RunCts = null;
        }

        /// <summary>
        /// 卸载已加载的增量场景并释放场景句柄。
        /// </summary>
        private async UniTask UnloadRuntimeSceneAsync()
        {
            ISceneHandle handle = m_RuntimeSceneHandle;
            m_RuntimeSceneHandle = null;
            if (handle != null && handle.IsValid)
            {
                await handle.UnloadAsync();
            }
        }
    }
}
