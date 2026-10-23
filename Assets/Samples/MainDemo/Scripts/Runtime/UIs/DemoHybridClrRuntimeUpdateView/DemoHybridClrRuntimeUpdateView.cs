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
        /// 独立加载既有 Content 场景按钮，不执行下载、Running DLL 或业务入口。
        /// </summary>
        [SerializeField] private Button m_LoadContentButton;

        /// <summary>
        /// 卸载本页面持有的 Content 场景；加载未完成时请求取消并等待原加载链清场。
        /// </summary>
        [SerializeField] private Button m_UnloadContentButton;

        /// <summary>
        /// 当前增量流程取消令牌源；关闭页面时取消尚未完成的下载或加载。
        /// </summary>
        private CancellationTokenSource m_RunCts;

        /// <summary>
        /// 已加载的增量场景句柄；页面关闭时按资源句柄协议卸载。
        /// </summary>
        private ISceneHandle m_RuntimeSceneHandle;

        /// <summary>
        /// 独立 Content 加载请求的取消令牌源，与原增量热更新任务分开管理。
        /// </summary>
        private CancellationTokenSource m_ContentLoadCts;

        /// <summary>
        /// 独立 Content 场景的唯一持有句柄；卸载前摘走，归池后不再访问。
        /// </summary>
        private ISceneHandle m_ContentSceneHandle;

        /// <summary>
        /// 当前页面代际，关闭或重开后旧请求只清场，不更新新页面。
        /// </summary>
        private int m_ContentGeneration;

        /// <summary>
        /// 尚未完成的 Content 加载或卸载数；清场结束前拒绝新的加载和原 Run。
        /// </summary>
        private int m_ContentOperations;

        /// <summary>
        /// 页面是否已关闭；关闭或销毁后的请求不得接管句柄或写入反馈。
        /// </summary>
        private bool m_ContentClosed = true;

        /// <summary>
        /// 卸载抛错后保持阻断，避免对状态不明的场景重复加载或双重卸载。
        /// </summary>
        private bool m_ContentCleanupFailed;

        /// <summary>
        /// 原增量流程尚未退出 finally；取消不提前清零，等待其局部句柄清场完成。
        /// </summary>
        private bool m_RunBusy;

        /// <summary>
        /// 初始化演示页，分别绑定原增量热更新与独立 Content 加载、卸载按钮。
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

            if (m_LoadContentButton != null)
            {
                m_LoadContentButton.onClick.AddListener(OnLoadContentButtonClick);
                SetButtonApiHint(m_LoadContentButton, "Nova.Asset.LoadSceneAsync(location, LoadSceneMode.Additive, ct)");
            }

            if (m_UnloadContentButton != null)
            {
                m_UnloadContentButton.onClick.AddListener(OnUnloadContentButtonClick);
                SetButtonApiHint(m_UnloadContentButton, "ISceneHandle.UnloadAsync()");
            }
        }

        /// <summary>
        /// 打开页面时推进 Content 代际，展示原增量流程与独立场景入口的边界。
        /// </summary>
        /// <param name="userData">用户自定义数据，本 View 不使用。</param>
        public override void OnOpen(object userData)
        {
            base.OnOpen(userData);
            m_ContentClosed = false;
            m_ContentGeneration++;
            ClearFeedback();
            AppendFeedback("本次运行固定使用启动阶段已加载的 Manifest，不调用 RefreshManifestAsync。", FeedbackLevel.Info);
            AppendFeedback("AOT Metadata 已由 ProcedureLoadDll 在启动阶段统一补充。", FeedbackLevel.Info);
            AppendFeedback("执行顺序：Tag 下载 -> Running DLL -> 业务入口 -> 增量场景。", FeedbackLevel.Info);
            AppendFeedback("独立 Content 按钮只以 Default Package 的既有地址叠加加载场景；卸载按钮在加载中用于取消。", FeedbackLevel.Info);
        }

        /// <summary>
        /// 关闭页面时使两条场景链的旧代际失效，取消加载并唯一卸载已经持有的句柄。
        /// </summary>
        /// <param name="isShutdown">是否因应用关闭触发。</param>
        /// <param name="userData">用户自定义数据。</param>
        public override void OnClose(bool isShutdown, object userData)
        {
            CloseContentOwner();
            CancelRun();
            UnloadRuntimeSceneAsync().Forget();
            base.OnClose(isShutdown, userData);
        }

        /// <summary>
        /// 启动原运行时增量热更新；独立 Content 尚在加载、持有或清场时拒绝并行进入。
        /// </summary>
        private void OnRunButtonClick()
        {
            if (m_ContentOperations > 0 || m_ContentSceneHandle != null || m_ContentCleanupFailed)
            {
                AppendFeedback("独立 Content 尚在使用、清场或卸载失败，不能同时运行增量流程。", FeedbackLevel.Warn);
                return;
            }

            RunRuntimeUpdateAsync().Forget();
        }

        /// <summary>
        /// 加载按钮只启动现有 Content 场景的独立所有权链，不进入 Running DLL 流程。
        /// </summary>
        private void OnLoadContentButtonClick()
        {
            LoadContentSceneAsync().Forget();
        }

        /// <summary>
        /// 卸载按钮结束已持有场景；加载尚未返回时仅取消该请求，由原链完成句柄清场。
        /// </summary>
        private void OnUnloadContentButtonClick()
        {
            UnloadContentSceneAsync().Forget();
        }

        /// <summary>
        /// 从运行时默认 Package 以 Additive 加载既有 Content 场景，唯一持有成功返回的句柄。
        /// 已返回但取消、关闭或代际过期的局部句柄在 finally 卸载，不交给新页面。
        /// </summary>
        private async UniTaskVoid LoadContentSceneAsync()
        {
            if (m_ContentClosed || this == null)
            {
                return;
            }
            if (Nova.Asset == null)
            {
                AppendFeedback("Nova.Asset 未初始化。", FeedbackLevel.Error);
                return;
            }
            if (m_RunBusy || m_RuntimeSceneHandle != null)
            {
                AppendFeedback("原增量流程仍在执行、持有场景或清场；完成卸载后才能加载独立 Content。", FeedbackLevel.Warn);
                return;
            }
            if (m_ContentOperations > 0 || m_ContentSceneHandle != null || m_ContentCleanupFailed)
            {
                AppendFeedback("Content 已加载、操作尚未完成或卸载失败，不能重复加载。", FeedbackLevel.Warn);
                return;
            }

            int generation = m_ContentGeneration;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            CancellationToken ct = cancellation.Token;
            m_ContentLoadCts = cancellation;
            m_ContentOperations++;
            ISceneHandle handle = null;
            try
            {
                handle = await Nova.Asset.LoadSceneAsync(c_SceneLocation, LoadSceneMode.Additive, ct);
                ct.ThrowIfCancellationRequested();
                if (!IsCurrentContentGeneration(generation))
                {
                    throw new OperationCanceledException(ct);
                }
                if (handle == null || !handle.IsValid)
                {
                    throw new InvalidOperationException("Content 场景加载失败：" + c_SceneLocation);
                }

                m_ContentSceneHandle = handle;
                handle = null;
                AppendFeedback("Content 已加载并由本页面持有 -> " + c_SceneLocation, FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                if (IsCurrentContentGeneration(generation))
                {
                    AppendFeedback("Content 加载已取消，继续完成该请求的句柄清场。", FeedbackLevel.Warn);
                }
            }
            catch (Exception e)
            {
                if (IsCurrentContentGeneration(generation))
                {
                    AppendFeedback("Content 加载失败：" + e.Message, FeedbackLevel.Error);
                }
                else
                {
                    Log.Error(LogTag.UI, "Content 加载退出异常：{0}", e.Message);
                }
            }
            finally
            {
                try
                {
                    // Handle 尚未交给字段时由本请求唯一卸载；返回前的取消由 AssetManager 收束。
                    if (handle != null)
                    {
                        ISceneHandle abandoned = handle;
                        handle = null;
                        await UnloadContentHandleAsync(abandoned);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.UI, "Content 取消或失败后的卸载异常：{0}", e.Message);
                    if (IsCurrentContentGeneration(generation))
                    {
                        AppendFeedback("Content 清场失败，已阻断再次加载：" + e.Message, FeedbackLevel.Error);
                    }
                }
                finally
                {
                    if (ReferenceEquals(m_ContentLoadCts, cancellation))
                    {
                        m_ContentLoadCts = null;
                    }
                    cancellation.Dispose();
                    m_ContentOperations--;
                }
            }
        }

        /// <summary>
        /// 结束本页面持有的 Content 场景；先摘走唯一引用，再等待恰好一次 UnloadAsync。
        /// 尚在加载时只请求取消，加载链完成前仍保留操作门，不能将取消请求当成卸载完成。
        /// </summary>
        private async UniTaskVoid UnloadContentSceneAsync()
        {
            int generation = m_ContentGeneration;
            ISceneHandle handle = m_ContentSceneHandle;
            if (handle == null)
            {
                if (m_ContentLoadCts != null)
                {
                    m_ContentLoadCts.Cancel();
                    if (IsCurrentContentGeneration(generation))
                    {
                        AppendFeedback("已请求取消 Content 加载；等待原加载链完成卸载，尚未宣称清场完成。", FeedbackLevel.Warn);
                    }
                }
                else if (IsCurrentContentGeneration(generation))
                {
                    AppendFeedback(m_ContentOperations > 0 ? "Content 卸载尚未完成。" : "当前没有本页面持有的 Content 场景。", FeedbackLevel.Warn);
                }
                return;
            }

            m_ContentSceneHandle = null;
            m_ContentOperations++;
            try
            {
                await UnloadContentHandleAsync(handle);
                if (IsCurrentContentGeneration(generation))
                {
                    AppendFeedback("Content 已卸载；句柄已归还，不再访问。", FeedbackLevel.Success);
                }
            }
            catch (Exception e)
            {
                Log.Error(LogTag.UI, "Content 卸载异常：{0}", e.Message);
                if (IsCurrentContentGeneration(generation))
                {
                    AppendFeedback("Content 卸载失败，已阻断再次加载：" + e.Message, FeedbackLevel.Error);
                }
            }
            finally
            {
                m_ContentOperations--;
            }
        }

        /// <summary>
        /// 对一个已摘走的场景句柄执行唯一卸载；失败时阻断重试，不重复访问或卸载该句柄。
        /// </summary>
        /// <param name="handle">仅由当前请求或本页面持有字段转交的句柄，调用后不得再次访问。</param>
        /// <returns>场景卸载与句柄归还完成；底层异常保持外抛。</returns>
        private async UniTask UnloadContentHandleAsync(ISceneHandle handle)
        {
            try
            {
                await handle.UnloadAsync();
            }
            catch
            {
                m_ContentCleanupFailed = true;
                throw;
            }
        }

        /// <summary>
        /// 判断异步反馈是否仍属于当前打开页面；旧代际只完成清场。
        /// </summary>
        /// <param name="generation">发起当前操作时冻结的页面代际。</param>
        /// <returns>对象仍存在、页面仍打开且代际一致时返回 true。</returns>
        private bool IsCurrentContentGeneration(int generation)
        {
            return this != null && !m_ContentClosed && generation == m_ContentGeneration;
        }

        /// <summary>
        /// 关闭独立 Content 所有者：使旧请求失效、取消未返回的加载，并卸载已持有的句柄。
        /// 操作计数不在关闭时重置，重开页面仍等待原请求实际清场完成。
        /// </summary>
        private void CloseContentOwner()
        {
            m_ContentClosed = true;
            m_ContentGeneration++;
            m_ContentLoadCts?.Cancel();
            if (m_ContentSceneHandle != null)
            {
                UnloadContentSceneAsync().Forget();
            }
        }

        /// <summary>
        /// 对象销毁时结束独立 Content 所有权；已由关闭摘走的句柄不会重复卸载。
        /// </summary>
        private void OnDestroy()
        {
            CloseContentOwner();
            CancelRun();
            UnloadRuntimeSceneAsync().Forget();
        }

        /// <summary>
        /// 基于当前 Manifest 按原顺序完成 Tag 下载、DLL 加载、入口激活和场景加载。
        /// 令牌与页面代际固定到本次请求；场景先局部持有，取消或旧代际返回的句柄在 finally 卸载。
        /// </summary>
        private async UniTaskVoid RunRuntimeUpdateAsync()
        {
            if (m_ContentClosed || this == null)
            {
                return;
            }
            if (m_RunBusy || m_ContentOperations > 0 || m_ContentSceneHandle != null || m_ContentCleanupFailed)
            {
                AppendFeedback("已有场景流程或清场未完成，不能重复进入增量流程。", FeedbackLevel.Warn);
                return;
            }
            if (Nova.Asset == null)
            {
                AppendFeedback("Nova.Asset 未初始化。", FeedbackLevel.Error);
                return;
            }

            int generation = m_ContentGeneration;
            CancellationTokenSource cancellation = new CancellationTokenSource();
            CancellationToken ct = cancellation.Token;
            m_RunCts = cancellation;
            m_RunBusy = true;
            ISceneHandle handle = null;

            try
            {
                await UnloadRuntimeSceneAsync();
                ct.ThrowIfCancellationRequested();
                if (!IsCurrentContentGeneration(generation))
                {
                    throw new OperationCanceledException(ct);
                }

                IAssetDownloader downloader = Nova.Asset.CreateDownloaderByTags(new[] { c_RuntimeTag });
                AppendFeedback(string.Format("[1] Tag={0} -> 文件数={1}", c_RuntimeTag, downloader.TotalCount), FeedbackLevel.Info);
                if (!downloader.IsEmpty)
                {
                    bool downloaded = await downloader.RunAsync(ct);
                    ct.ThrowIfCancellationRequested();
                    if (!IsCurrentContentGeneration(generation))
                    {
                        throw new OperationCanceledException(ct);
                    }
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
                if (!IsCurrentContentGeneration(generation))
                {
                    throw new OperationCanceledException(ct);
                }
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

                handle = await Nova.Asset.LoadSceneAsync(c_SceneLocation, LoadSceneMode.Additive, ct);
                ct.ThrowIfCancellationRequested();
                if (!IsCurrentContentGeneration(generation))
                {
                    throw new OperationCanceledException(ct);
                }
                if (handle == null || !handle.IsValid)
                {
                    throw new InvalidOperationException("增量场景加载失败：" + c_SceneLocation);
                }
                m_RuntimeSceneHandle = handle;
                handle = null;
                AppendFeedback("[4] Scene loaded -> " + c_SceneLocation, FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                if (IsCurrentContentGeneration(generation))
                {
                    AppendFeedback("运行时增量热更新已取消。", FeedbackLevel.Warn);
                }
            }
            catch (TargetInvocationException e)
            {
                Exception cause = e.InnerException ?? e;
                if (IsCurrentContentGeneration(generation))
                {
                    AppendFeedback("业务入口执行失败：" + cause.Message, FeedbackLevel.Error);
                }
            }
            catch (Exception e)
            {
                if (IsCurrentContentGeneration(generation))
                {
                    AppendFeedback("运行时增量热更新失败：" + e.Message, FeedbackLevel.Error);
                }
                else
                {
                    Log.Error(LogTag.UI, "旧增量流程退出异常：{0}", e.Message);
                }
            }
            finally
            {
                try
                {
                    if (handle != null)
                    {
                        ISceneHandle abandoned = handle;
                        handle = null;
                        await UnloadContentHandleAsync(abandoned);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(LogTag.UI, "增量场景局部句柄卸载异常：{0}", e.Message);
                }
                finally
                {
                    if (ReferenceEquals(m_RunCts, cancellation))
                    {
                        m_RunCts = null;
                    }
                    cancellation.Dispose();
                    m_RunBusy = false;
                }
            }
        }

        /// <summary>
        /// 只请求取消原增量流程；令牌释放和 busy 清零由该请求的 finally 完成。
        /// </summary>
        private void CancelRun()
        {
            m_RunCts?.Cancel();
        }

        /// <summary>
        /// 卸载原增量流程持有的场景：先摘唯一引用，使用共同卸载门并保持操作占用直到完成。
        /// </summary>
        private async UniTask UnloadRuntimeSceneAsync()
        {
            ISceneHandle handle = m_RuntimeSceneHandle;
            m_RuntimeSceneHandle = null;
            if (handle != null)
            {
                m_ContentOperations++;
                try
                {
                    await UnloadContentHandleAsync(handle);
                }
                finally
                {
                    m_ContentOperations--;
                }
            }
        }
    }
}
