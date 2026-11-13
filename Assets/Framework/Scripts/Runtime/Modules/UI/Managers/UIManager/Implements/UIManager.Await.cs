/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManager.Await.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   显式等待单次页面打开的终态
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class UIManager
    {
        // 旧入口不分配等待表或令牌；已结算但底层尚未结束的请求仍保留序号及实例归属。
        private Dictionary<int, AwaitedUIViewOpen> m_AwaitedUIViews;
        private int m_AwaitAllocationDepth;

        /// <summary>按类型查表并等待本次打开完成；不改变旧序号入口。</summary>
        /// <typeparam name="T">注册且预挂的页面类型。</typeparam>
        /// <param name="userData">原样交给页面的业务数据。</param>
        /// <param name="cancellationToken">只作用于本次请求的令牌。</param>
        /// <returns>准确的单次终态，不拥有成功视图。</returns>
        public override UniTask<UIViewOpenResult> OpenUIViewAwaitAsync<T>(object userData = null, CancellationToken cancellationToken = default)
        {
            EnsureAwaitMainThread();
            return OpenAwaitedUIView(cancellationToken.IsCancellationRequested || m_IsShutdown ? null : GetUIViewRow<T>(), typeof(T), userData, cancellationToken);
        }

        /// <summary>按精确表名等待打开，所有分组和池化行为仍来自表。</summary>
        /// <param name="uiViewName">UI 表 Name。</param>
        /// <param name="userData">本次业务数据。</param>
        /// <param name="cancellationToken">本次请求令牌。</param>
        /// <returns>单次打开的终态。</returns>
        public override UniTask<UIViewOpenResult> OpenUIViewAwaitAsync(string uiViewName, object userData = null, CancellationToken cancellationToken = default)
        {
            EnsureAwaitMainThread();
            return OpenAwaitedUIView(cancellationToken.IsCancellationRequested || m_IsShutdown ? null : GetUIViewRowByName(uiViewName), null, userData, cancellationToken);
        }

        /// <summary>按唯一资源地址查表并等待打开，歧义不会触发加载。</summary>
        /// <param name="assetLocation">精确资源地址。</param>
        /// <param name="userData">本次业务数据。</param>
        /// <param name="cancellationToken">本次请求令牌。</param>
        /// <returns>单次打开的终态。</returns>
        public override UniTask<UIViewOpenResult> OpenUIViewByAssetAwaitAsync(string assetLocation, object userData = null, CancellationToken cancellationToken = default)
        {
            EnsureAwaitMainThread();
            return OpenAwaitedUIView(cancellationToken.IsCancellationRequested || m_IsShutdown ? null : GetUIViewRowByAssetLocation(assetLocation), null, userData, cancellationToken);
        }

        /// <summary>冻结配置和资源拥有者，在同步池命中或资源回调前登记完成源；登记失败也直接结算。</summary>
        private UniTask<UIViewOpenResult> OpenAwaitedUIView(IUIViewRow entry, Type expectedType, object userData, CancellationToken token)
        {
            if (token.IsCancellationRequested)
                return UniTask.FromResult(new UIViewOpenResult(UIViewOpenStatus.Cancelled, -1, errorMessage: "请求在打开前已取消。"));
            if (m_IsShutdown)
                return UniTask.FromResult(new UIViewOpenResult(UIViewOpenStatus.Shutdown, -1, errorMessage: "UI 管理器已经关闭。"));
            if (entry == null)
                return UniTask.FromResult(new UIViewOpenResult(UIViewOpenStatus.Failed, -1, errorMessage: "未找到唯一的 UI 注册配置。"));

            AwaitedUIViewOpen request = null;
            UIViewInstanceObject acquired = null;
            IObjectPool<UIViewInstanceObject> pool = null;
            try
            {
                // 先读完外部行属性，避免 Get 到池实例后再由配置 getter 抛错而丢失租约。
                string location = entry.AssetLocation;
                string groupName = entry.UIGroupName;
                bool pooled = entry.InObjectPools;
                bool pauseCovered = entry.PauseCoveredUIView;
                pool = m_InstancePool;
                IPrefabManager owner = m_PrefabManager;
                m_AwaitAllocationDepth++;
                int serialID;
                UIGroup group;
                try { PrepareOpenUIView(location, groupName, pooled, out serialID, out group, out _, out acquired); }
                finally { m_AwaitAllocationDepth--; }

                request = new AwaitedUIViewOpen(serialID, location, group, pooled, pauseCovered,
                    expectedType, userData, token, owner, pool, acquired);
                acquired = null;
                (m_AwaitedUIViews ??= new Dictionary<int, AwaitedUIViewOpen>()).Add(serialID, request);
                AwaitedUIViewOpen captured = request;
                CancellationTokenRegistration registration = token.Register(() =>
                {
                    if (PlayerLoopHelper.IsMainThread) CancelAwaitedOpen(captured);
                    else PlayerLoopHelper.AddContinuation(PlayerLoopTiming.Update, () => CancelAwaitedOpen(captured));
                });
                if (IsCurrentAwaitedOpen(request)) request.Registration = registration;
                else registration.Dispose();

                if (!MayContinueAwaitedOpen(request))
                {
                    CleanupAwaitedTarget(request);
                    RetireAwaitedOpen(request);
                    return request.Completion.Task;
                }
                if (request.Instance != null)
                {
                    request.Instance.CapturePrefabOwner(owner);
                    FinishAwaitedOpen(request, request.Target, false);
                }
                else
                {
                    Transform parent = ((MonoBehaviour)group.Helper).transform;
                    m_UIViewsBeingLoaded.Add(serialID, location);
                    request.Loading = true;
                    LoadAwaitedUIViewAsync(request, parent).Forget();
                }
                return request.Completion.Task;
            }
            catch (Exception exception)
            {
                if (request != null)
                {
                    FailAwaitedOpen(request, exception);
                    return request.Completion.Task;
                }
                string message = exception.Message;
                if (acquired != null)
                {
                    object target = acquired.Target;
                    try { pool.Put(target); }
                    catch (Exception cleanupError) { message += "；实例归还：" + cleanupError.Message; }
                }
                return UniTask.FromResult(new UIViewOpenResult(UIViewOpenStatus.Failed, -1, errorMessage: message));
            }
        }

        /// <summary>持续观察捕获的加载接口；晚到实例始终归原拥有者，销毁异常不重试未知结果。</summary>
        private async UniTask LoadAwaitedUIViewAsync(AwaitedUIViewOpen request, Transform parent)
        {
            GameObject target = null;
            Exception failure = null;
            try { target = await request.PrefabOwner.InstantiateAsync(request.AssetLocation, parent); }
            catch (Exception exception) { failure = exception; }
            await UniTask.SwitchToMainThread();
            request.Loading = false;
            m_UIViewsBeingLoaded.Remove(request.SerialID);
            m_UIViewsToReleaseOnLoad.Remove(request.SerialID);
            request.Target = target;
            request.OwnsTarget = !ReferenceEquals(target, null);
            if (failure != null)
            {
                FailAwaitedOpen(request, failure);
                return;
            }
            if (!MayContinueAwaitedOpen(request))
            {
                CleanupAwaitedTarget(request);
                RetireAwaitedOpen(request);
                return;
            }
            if (target == null)
            {
                FailAwaitedOpen(request, new InvalidOperationException("异步加载返回空页面实例。"));
                return;
            }
            FinishAwaitedOpen(request, target, true);
        }

        /// <summary>逐阶段核对请求，终态已通知也不免除仍归本次所有的实例清理责任。</summary>
        private void FinishAwaitedOpen(AwaitedUIViewOpen request, GameObject target, bool isNew)
        {
            request.Target = target;
            request.OwnsTarget = true;
            request.Opening = true;
            try
            {
                if (!MayContinueAwaitedOpen(request)) return;
                if (target == null) throw new InvalidOperationException("视图实例已经失效。");
                UIView view = target.GetComponent<UIView>();
                request.View = view;
                if (view == null || request.ExpectedType != null && !request.ExpectedType.IsInstanceOfType(view))
                    throw new InvalidOperationException("页面 Prefab 的 UIView 类型与请求不匹配。");
                if (isNew && request.Pooled)
                {
                    request.Instance = UIViewInstanceObject.Create(GetInstanceName(request.AssetLocation), target, request.PrefabOwner);
                    // 默认池在容量回收回调前已经登记目标；后续清理始终使用捕获的池和目标。
                    request.RegisteredInPool = true;
                    request.Pool.Register(request.Instance, true);
                }
                if (!MayContinueAwaitedOpen(request)) return;
                request.LifecycleStarted = true;
                InternalOpenUIView(request.SerialID, request.AssetLocation, request.Group, target,
                    request.PauseCovered, request.Pooled, isNew, 0f, request.UserData, true,
                    () => MayContinueAwaitedOpen(request));
                if (!MayContinueAwaitedOpen(request)) return;
                if (!m_UIViewIndex.TryGetValue(request.SerialID, out IUIView current) || !ReferenceEquals(current, view))
                {
                    MarkAwaitedTerminal(request, UIViewOpenStatus.Closed, null, "页面在打开期间已经关闭。");
                    return;
                }
                // 唤醒业务前交接给既有正常关闭链，续体内关闭、回池和重开不再归旧请求清理。
                request.OwnsTarget = false;
                request.Instance = null;
                MarkAwaitedTerminal(request, UIViewOpenStatus.Opened, view, null);
            }
            catch (Exception exception)
            {
                if (!request.Completed) MarkAwaitedTerminal(request, UIViewOpenStatus.Failed, null, exception.Message);
            }
            finally
            {
                request.Opening = false;
                if (request.Completed && request.Status != UIViewOpenStatus.Opened) CleanupAwaitedTarget(request);
                PublishAwaitedTerminal(request);
                RetireAwaitedOpen(request);
            }
        }

        /// <summary>失败先固定终态，再逐项清理仍拥有的目标；未登记请求也直接返回 Failed。</summary>
        private void FailAwaitedOpen(AwaitedUIViewOpen request, Exception exception)
        {
            if (!request.Completed) MarkAwaitedTerminal(request, UIViewOpenStatus.Failed, null, exception.Message);
            m_UIViewsBeingLoaded.Remove(request.SerialID);
            m_UIViewsToReleaseOnLoad.Remove(request.SerialID);
            if (!request.Opening && !request.Loading) CleanupAwaitedTarget(request);
            PublishAwaitedTerminal(request);
            RetireAwaitedOpen(request);
        }

        /// <summary>取消只结束本请求；生命周期执行中由阶段检查停止，退出当前调用栈后收尾。</summary>
        private void CancelAwaitedOpen(AwaitedUIViewOpen request)
        {
            if (!IsCurrentAwaitedOpen(request)) return;
            MarkAwaitedTerminal(request, UIViewOpenStatus.Cancelled, null, "页面打开请求已取消。");
            m_UIViewsBeingLoaded.Remove(request.SerialID);
            if (!request.Opening && !request.Loading) CleanupAwaitedTarget(request);
            PublishAwaitedTerminal(request);
            RetireAwaitedOpen(request);
        }

        /// <summary>在初始化、OnOpen 和分组回调之间检查身份、原始令牌和管理器状态。</summary>
        private bool MayContinueAwaitedOpen(AwaitedUIViewOpen request)
        {
            if (!IsCurrentAwaitedOpen(request)) return false;
            if (m_IsShutdown)
            {
                MarkAwaitedTerminal(request, UIViewOpenStatus.Shutdown, null, "UI 管理器已经关闭。");
                return false;
            }
            if (request.Token.IsCancellationRequested)
            {
                CancelAwaitedOpen(request);
                return false;
            }
            if (request.LifecycleStarted && request.Target == null)
            {
                MarkAwaitedTerminal(request, UIViewOpenStatus.Failed, null, "页面在打开期间已经销毁。");
                return false;
            }
            return true;
        }

        /// <summary>只对尚在可选打开中的实例隔离关闭异常；成功交接和旧入口仍用原关闭路径。</summary>
        private bool TryCloseAwaitedView(IUIView view, object userData)
        {
            AwaitedUIViewOpen request = FindAwaitedOwner(view);
            if (request == null) return false;
            MarkAwaitedTerminal(request, UIViewOpenStatus.Closed, null, "页面在打开完成前已关闭。");
            if (!request.CloseStarted)
            {
                // 先标记，避免 OnCover、OnPause 或作用域清理重入相同关闭流程。
                request.CloseStarted = true;
                try { request.Group.RemoveUIView(view); }
                catch (Exception exception) { AppendAwaitedCleanupError(request, "移出分组", exception); }
                try { request.Group.RemoveUIViewWithoutCallbacks(view); }
                catch (Exception exception) { AppendAwaitedCleanupError(request, "强制移出分组", exception); }
                RemoveAwaitedIndex(request);
                try { request.View?.EndOpenScope(); }
                catch (Exception exception) { AppendAwaitedCleanupError(request, "页面作用域", exception); }
                try { view.OnClose(m_IsShutdown, userData); }
                catch (Exception exception) { AppendAwaitedCleanupError(request, "页面关闭", exception); }
                if (!m_IsShutdown)
                {
                    try { request.Group.Refresh(); }
                    catch (Exception exception) { AppendAwaitedCleanupError(request, "分组刷新", exception); }
                }
            }
            if (!request.Opening && !request.Loading) CleanupAwaitedTarget(request);
            PublishAwaitedTerminal(request);
            RetireAwaitedOpen(request);
            return true;
        }

        /// <summary>逐项结束索引、作用域和生命周期，再通过捕获的池或实例拥有者归还；失败不重放未知释放。</summary>
        private void CleanupAwaitedTarget(AwaitedUIViewOpen request)
        {
            if (!request.OwnsTarget || request.Cleaning || request.Loading || request.Opening) return;
            request.Cleaning = true;
            try
            {
                GameObject target = request.Target;
                UIView view = request.View;
                if (view != null && request.LifecycleStarted && view.SerialID > 0 && view.SerialID != request.SerialID)
                {
                    request.OwnsTarget = false;
                    request.Instance = null;
                    return;
                }
                RemoveAwaitedIndex(request);
                if (view != null)
                {
                    if (request.Group.HasUIView(request.SerialID))
                    {
                        try { request.Group.RemoveUIView(view); }
                        catch (Exception exception) { AppendAwaitedCleanupError(request, "移出分组", exception); }
                    }
                    try { request.Group.RemoveUIViewWithoutCallbacks(view); }
                    catch (Exception exception) { AppendAwaitedCleanupError(request, "强制移出分组", exception); }
                    try { view.EndOpenScope(); }
                    catch (Exception exception) { AppendAwaitedCleanupError(request, "页面作用域", exception); }
                    if (request.LifecycleStarted && !request.CloseStarted && view.SerialID == request.SerialID)
                    {
                        request.CloseStarted = true;
                        try { view.OnClose(m_IsShutdown, request.UserData); }
                        catch (Exception exception) { AppendAwaitedCleanupError(request, "页面关闭", exception); }
                    }
                    if (request.LifecycleStarted && !request.Recycled && view.SerialID == request.SerialID)
                    {
                        request.Recycled = true;
                        try { view.OnRecycle(); }
                        catch (Exception exception) { AppendAwaitedCleanupError(request, "页面回收", exception); }
                    }
                }
                // Put 可能同步释放并归还包装；先交出归属，后续只使用冻结的 target，禁止再读取包装。
                request.OwnsTarget = false;
                request.Instance = null;
                if (request.RegisteredInPool && !ReferenceEquals(target, null))
                {
                    try
                    {
                        request.Pool.Put((object)target);
                        if (view == null || view.SerialID <= 0 || view.SerialID == request.SerialID || !request.LifecycleStarted)
                            request.Pool.ReleaseObject((object)target);
                    }
                    catch (Exception exception) { AppendAwaitedCleanupError(request, "池实例释放", exception); }
                }
                else if (target != null)
                {
                    try { request.PrefabOwner.Destroy(target); }
                    catch (Exception exception) { AppendAwaitedCleanupError(request, "实例销毁", exception); }
                }
            }
            finally { request.Cleaning = false; }
        }

        /// <summary>只删除仍指向本请求视图的索引，不改相同实例新一次打开的序号。</summary>
        private void RemoveAwaitedIndex(AwaitedUIViewOpen request)
        {
            if (m_UIViewIndex.TryGetValue(request.SerialID, out IUIView current) && ReferenceEquals(current, request.View))
                m_UIViewIndex.Remove(request.SerialID);
        }

        /// <summary>按视图和序号查找仍持有该实例的打开记录，已成功交接页面不走此分支。</summary>
        private AwaitedUIViewOpen FindAwaitedOwner(IUIView view)
        {
            if (view == null || m_AwaitedUIViews == null || !m_AwaitedUIViews.TryGetValue(view.SerialID, out AwaitedUIViewOpen request)) return null;
            return request.OwnsTarget && ReferenceEquals(request.View, view) ? request : null;
        }

        /// <summary>固定首个终态并断开取消注册；终态和实例拥有权分开，已结算仍可执行必要清理。</summary>
        private bool MarkAwaitedTerminal(AwaitedUIViewOpen request, UIViewOpenStatus status, IUIView view, string message)
        {
            if (request.Completed) return false;
            request.Completed = true;
            request.Status = status;
            request.ResultView = view;
            request.Message = message;
            request.Registration.Dispose();
            return true;
        }

        /// <summary>一次性通知已经固定的结果，业务重入不能改写终态。</summary>
        private void PublishAwaitedTerminal(AwaitedUIViewOpen request)
        {
            if (request.Completed)
                request.Completion.TrySetResult(new UIViewOpenResult(request.Status, request.SerialID, request.ResultView, request.Message));
        }

        /// <summary>底层加载、生命周期及实例拥有权全部结束后才释放本次序号保留。</summary>
        private void RetireAwaitedOpen(AwaitedUIViewOpen request)
        {
            if (!request.Completed || request.Loading || request.Opening || request.OwnsTarget || request.Cleaning) return;
            if (m_AwaitedUIViews != null && m_AwaitedUIViews.TryGetValue(request.SerialID, out AwaitedUIViewOpen current) && ReferenceEquals(current, request))
                m_AwaitedUIViews.Remove(request.SerialID);
        }

        /// <summary>按序号结算关闭，但保留尚需清理晚到实例的资源记录。</summary>
        private void CompleteAwaitedOpen(int serialID, UIViewOpenStatus status, IUIView view, string message)
        {
            if (m_AwaitedUIViews == null || !m_AwaitedUIViews.TryGetValue(serialID, out AwaitedUIViewOpen request)) return;
            MarkAwaitedTerminal(request, status, view, message);
            if (!request.Loading && !request.Opening) CleanupAwaitedTarget(request);
            PublishAwaitedTerminal(request);
            RetireAwaitedOpen(request);
        }

        /// <summary>先固定全部终态再逐项通知，原池和实例接口保留到观察链结束，不依赖 Shutdown 已清空字段。</summary>
        private void CompleteAllAwaitedOpens(UIViewOpenStatus status, string message)
        {
            if (m_AwaitedUIViews == null || m_AwaitedUIViews.Count == 0) return;
            var pending = new List<AwaitedUIViewOpen>(m_AwaitedUIViews.Values);
            foreach (AwaitedUIViewOpen request in pending)
            {
                MarkAwaitedTerminal(request, status, null, message);
                m_UIViewsBeingLoaded.Remove(request.SerialID);
            }
            foreach (AwaitedUIViewOpen request in pending)
            {
                if (!request.Loading && !request.Opening) CleanupAwaitedTarget(request);
                PublishAwaitedTerminal(request);
                RetireAwaitedOpen(request);
            }
        }

        /// <summary>仅接受仍登记且尚未结算的相同请求对象。</summary>
        private bool IsCurrentAwaitedOpen(AwaitedUIViewOpen request)
            => !request.Completed && m_AwaitedUIViews != null && m_AwaitedUIViews.TryGetValue(request.SerialID, out AwaitedUIViewOpen current) && ReferenceEquals(current, request);

        /// <summary>清理异常追加到结果；结果已经通知后单独记录身份和阶段，不吞掉晚到释放失败。</summary>
        private static void AppendAwaitedCleanupError(AwaitedUIViewOpen request, string stage, Exception exception)
        {
            request.Message = (request.Message ?? string.Empty) + "；" + stage + "：" + exception.Message;
            if (request.Completion.Task.Status.IsCompleted())
                Log.Error(LogTag.UI, "页面请求 [{0}] '{1}' 的{2}失败；释放结果未知，未自动重试：{3}", request.SerialID, request.AssetLocation, stage, exception);
        }

        /// <summary>新入口接触注册表、对象池和 Unity 对象前拒绝后台调用。</summary>
        private static void EnsureAwaitMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread) throw new InvalidOperationException("页面打开必须在 Unity 主线程发起。");
        }

        /// <summary>单次可选打开的冻结参数、终态和独立资源归属，不使用旧引用池信封。</summary>
        private sealed class AwaitedUIViewOpen
        {
            internal readonly int SerialID;
            internal readonly string AssetLocation;
            internal readonly UIGroup Group;
            internal readonly bool Pooled;
            internal readonly bool PauseCovered;
            internal readonly Type ExpectedType;
            internal readonly object UserData;
            internal readonly CancellationToken Token;
            internal readonly IPrefabManager PrefabOwner;
            internal readonly IObjectPool<UIViewInstanceObject> Pool;
            internal readonly UniTaskCompletionSource<UIViewOpenResult> Completion = new UniTaskCompletionSource<UIViewOpenResult>();
            internal CancellationTokenRegistration Registration;
            internal UIViewInstanceObject Instance;
            internal GameObject Target;
            internal UIView View;
            internal IUIView ResultView;
            internal UIViewOpenStatus Status;
            internal string Message;
            internal bool Completed;
            internal bool Loading;
            internal bool Opening;
            internal bool OwnsTarget;
            internal bool RegisteredInPool;
            internal bool LifecycleStarted;
            internal bool CloseStarted;
            internal bool Recycled;
            internal bool Cleaning;

            /// <summary>仅赋值已经冻结的数据，Get 池实例后不再读取可能抛错或重入的配置 getter。</summary>
            internal AwaitedUIViewOpen(int serialID, string location, UIGroup group, bool pooled, bool pauseCovered,
                Type expectedType, object userData, CancellationToken token, IPrefabManager owner,
                IObjectPool<UIViewInstanceObject> pool, UIViewInstanceObject instance)
            {
                SerialID = serialID;
                AssetLocation = location;
                Group = group;
                Pooled = pooled;
                PauseCovered = pauseCovered;
                ExpectedType = expectedType;
                UserData = userData;
                Token = token;
                PrefabOwner = owner;
                Pool = pool;
                Instance = instance;
                Target = instance?.Target as GameObject;
                OwnsTarget = instance != null;
                RegisteredInPool = instance != null;
            }
        }
    }
}
