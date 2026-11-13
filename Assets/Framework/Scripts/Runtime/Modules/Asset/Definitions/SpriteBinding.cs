/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SpriteBinding.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   显式拥有图片与图集租约的可选绑定助手
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.UI;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 显式选择使用的图片绑定助手；每个 Image 或 SpriteRenderer 同时只有一个拥有者。
    /// 只有显式创建本助手才启用；不会扫描、接管或修改旧项目中的图片或既有加载路径。
    /// 普通业务通过默认构造使用 Nova.Asset，页面关闭或对象回池时主动 Dispose 或 Clear。
    /// 禁用对象不会释放图片；目标 GameObject 销毁会自动结束绑定。
    /// 从未 Awake 的目标或宿主由本绑定短期观察，在销毁后的下一次 Update 收尾；Awake 后只保留原生销毁令牌。
    /// </summary>
    /// <remarks>
    /// 创建、绑定、清空和释放必须在 Unity 主线程执行；调用方取消令牌可以从其他线程取消。
    /// 不接管其他代码持有的句柄，也不销毁直接加载的 Sprite。显式使用期间应由本助手统一写入目标 sprite。
    /// </remarks>
    public sealed class SpriteBinding : IDisposable
    {
        private static readonly Dictionary<int, SpriteBinding> s_Owners = new Dictionary<int, SpriteBinding>();
        private readonly Component m_Target;
        private readonly int m_TargetId;
        private readonly IAssetManager m_AssetManager;
        private CancellationTokenRegistration m_DestroyRegistration;
        private BindingRequest m_Pending;
        private SpriteLease m_Current;

        /// <summary>绑定是否已经结束；结束后不可再发起加载。</summary>
        public bool IsDisposed { get; private set; }

        /// <summary>在 Unity 主线程为指定 Image 创建拥有者，加载通过 Nova.Asset 门面执行。</summary>
        /// <param name="target">由本助手独占写入 sprite 的目标。</param>
        public SpriteBinding(Image target) : this((Component)target, null, false) { }

        /// <summary>在 Unity 主线程为指定 SpriteRenderer 创建拥有者，加载通过 Nova.Asset 门面执行。</summary>
        /// <param name="target">由本助手独占写入 sprite 的目标。</param>
        public SpriteBinding(SpriteRenderer target) : this((Component)target, null, false) { }

        /// <summary>为框架扩展或隔离测试显式注入 IAssetManager，普通业务使用不带 Manager 的构造。</summary>
        /// <param name="target">由本助手独占写入 sprite 的 Image。</param>
        /// <param name="assetManager">负责返回独立句柄租约的资源接口，不得为 null。</param>
        public SpriteBinding(Image target, IAssetManager assetManager) : this((Component)target, assetManager, true) { }

        /// <summary>为框架扩展或隔离测试显式注入 IAssetManager，普通业务使用不带 Manager 的构造。</summary>
        /// <param name="target">由本助手独占写入 sprite 的 SpriteRenderer。</param>
        /// <param name="assetManager">负责返回独立句柄租约的资源接口，不得为 null。</param>
        public SpriteBinding(SpriteRenderer target, IAssetManager assetManager) : this((Component)target, assetManager, true) { }

        /// <summary>核对 Unity 线程和单一拥有者，并订阅目标自身或本 GameObject 的销毁令牌。</summary>
        private SpriteBinding(Component target, IAssetManager assetManager, bool injected)
        {
            EnsureMainThread();
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (injected && assetManager == null) throw new ArgumentNullException(nameof(assetManager));
            m_Target = target;
            m_TargetId = target.GetInstanceID();
            m_AssetManager = assetManager;
            if (s_Owners.ContainsKey(m_TargetId))
                throw new InvalidOperationException("此图片目标已经由另一个 SpriteBinding 持有，请先 Dispose 原拥有者。");

            MonoBehaviour lifetimeOwner;
            if (target is Image image)
                lifetimeOwner = image;
            else
            {
                // SpriteRenderer 本身不是 MonoBehaviour；仅在它自己的 GameObject 上订阅原生销毁令牌。
                SpriteBindingLifetime owner = target.GetComponent<SpriteBindingLifetime>();
                if (owner == null) owner = target.gameObject.AddComponent<SpriteBindingLifetime>();
                lifetimeOwner = owner;
            }
            s_Owners.Add(m_TargetId, this);
            try
            {
                m_DestroyRegistration = lifetimeOwner.destroyCancellationToken.Register(OnTargetDestroyed, this);
                // Unity 对从未 Awake 的对象不保证触发原生销毁令牌；只为显式创建的本绑定补足短期观察。
                if (!IsDisposed && !lifetimeOwner.didAwake)
                    PlayerLoopHelper.AddAction(PlayerLoopTiming.Update, new UnawakenedTargetMonitor(this, lifetimeOwner));
            }
            catch
            {
                // 构造未完成时尚未接管资源或图片，不得用 ClearCore 清掉原有业务图片。
                IsDisposed = true;
                s_Owners.Remove(m_TargetId);
                m_DestroyRegistration.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 异步绑定普通 Sprite；只有本目标最新且未取消的请求可以写图。
        /// 加载失败保留旧图并抛异常；被替代、取消或关闭返回 false，成功返回 true。
        /// 淘汰旧请求时取消回调抛错，本次不调度加载并返回失败任务；若本次也已被取消或替代，保持 false 并记录该错误。
        /// </summary>
        /// <param name="location">默认资源包中的 Sprite 地址。</param>
        /// <param name="ct">只结束本次请求等待的取消令牌，不影响其他加载租约。</param>
        public UniTask<bool> BindAsync(string location, CancellationToken ct = default)
        {
            return BeginRequest(location, null, ct);
        }

        /// <summary>
        /// 加载 SpriteAtlas 并通过 GetSprite 取得指定克隆；本助手同时拥有克隆和图集句柄。
        /// 图集或名称无效时保留旧图；替换、清空或释放时先断开图片引用并销毁克隆，再释放图集句柄。
        /// 淘汰旧请求的取消回调失败时沿用普通图入口的结算与错误报告方式。
        /// </summary>
        /// <param name="atlasLocation">默认资源包中的 SpriteAtlas 地址。</param>
        /// <param name="spriteName">传给 SpriteAtlas.GetSprite 的精确图片名称。</param>
        /// <param name="ct">只结束本次请求等待的取消令牌。</param>
        public UniTask<bool> BindAtlasAsync(string atlasLocation, string spriteName, CancellationToken ct = default)
        {
            EnsureMainThread();
            if (string.IsNullOrWhiteSpace(spriteName)) throw new ArgumentException("图集图片名称不能为空。", nameof(spriteName));
            return BeginRequest(atlasLocation, spriteName, ct);
        }

        /// <summary>立即淘汰未完成请求、清空目标并释放当前租约；仍可再次绑定，重复调用不会重复释放。</summary>
        public void Clear()
        {
            EnsureMainThread();
            if (!IsDisposed) ClearCore();
        }

        /// <summary>永久结束拥有权并清空图片；重复调用无效果，原有释放异常不会造成重复释放。</summary>
        public void Dispose()
        {
            EnsureMainThread();
            if (IsDisposed) return;
            IsDisposed = true;
            s_Owners.Remove(m_TargetId);
            m_DestroyRegistration.Dispose();
            ClearCore();
        }

        /// <summary>先发布新请求再结束旧请求，避免旧 await 的同步续体重入后被旧调用覆盖。</summary>
        private UniTask<bool> BeginRequest(string location, string spriteName, CancellationToken ct)
        {
            EnsureMainThread();
            if (IsDisposed) throw new ObjectDisposedException(nameof(SpriteBinding));
            if (m_Target == null) throw new MissingReferenceException("图片目标已经销毁，请 Dispose 此 SpriteBinding。");
            if (string.IsNullOrWhiteSpace(location)) throw new ArgumentException("资源地址不能为空。", nameof(location));
            if (ct.IsCancellationRequested) return UniTask.FromResult(false);

            var request = new BindingRequest(this, location, spriteName, ct);
            BindingRequest previous = m_Pending;
            m_Pending = request;
            try { previous?.Cancel(); }
            catch (Exception exception) { return RejectUnstartedRequest(request, exception); }
            // Cancel 可同步恢复调用方，调用方可能再次 Bind、Clear 或 Dispose。
            if (IsCurrent(request)) RunRequestAsync(request).Forget();
            else
            {
                if (ReferenceEquals(m_Pending, request)) m_Pending = null;
                request.Close();
                request.Completion.TrySetResult(false);
            }
            return request.Completion.Task;
        }

        /// <summary>取消旧请求失败时结束尚未调度的新请求，释放注册；已经被重入取消或替代的结果仍保持 false。</summary>
        /// <param name="request">已经发布但没有启动底层加载的新请求，不拥有资源句柄。</param>
        /// <param name="failure">旧取消回调抛出的真实异常，不能静默丢弃或自动重放。</param>
        /// <returns>仍有效的新请求返回失败任务；已取消或被替代时返回 false，并可见记录无法交给该任务的错误。</returns>
        private UniTask<bool> RejectUnstartedRequest(BindingRequest request, Exception failure)
        {
            bool current = IsCurrent(request);
            if (ReferenceEquals(m_Pending, request)) m_Pending = null;
            try { request.Close(); }
            catch (Exception exception) { failure = Combine(failure, exception); }
            if (current)
            {
                if (!request.Completion.TrySetException(failure)) Debug.LogException(failure);
            }
            else
            {
                request.Completion.TrySetResult(false);
                Debug.LogException(failure);
            }
            return request.Completion.Task;
        }

        /// <summary>观察每笔底层加载至结束，即使自定义 Manager 忽略取消，也归还晚到句柄并观察晚到异常。</summary>
        private async UniTask RunRequestAsync(BindingRequest request)
        {
            IAssetHandle handle = null;
            SpriteLease candidate = null;
            Exception failure = null;
            bool applied = false;
            try
            {
                if (request.SpriteName == null)
                    handle = await LoadAsync<Sprite>(request.Location, request.Token);
                else
                    handle = await LoadAsync<SpriteAtlas>(request.Location, request.Token);

                await UniTask.SwitchToMainThread();
                if (!IsCurrent(request)) return;
                if (handle == null || !handle.IsValid || !handle.IsDone)
                    throw new InvalidOperationException($"图片资源句柄无效或尚未完成：{request.Location}");

                Sprite sprite;
                bool cloned = request.SpriteName != null;
                if (cloned)
                {
                    SpriteAtlas atlas = (handle as IAssetHandle<SpriteAtlas>)?.Asset;
                    if (atlas == null) throw new InvalidOperationException($"图集加载结果为空：{request.Location}");
                    sprite = atlas.GetSprite(request.SpriteName);
                    if (sprite == null) throw new InvalidOperationException($"图集中找不到图片：{request.Location} / {request.SpriteName}");
                }
                else
                {
                    sprite = (handle as IAssetHandle<Sprite>)?.Asset;
                    if (sprite == null) throw new InvalidOperationException($"图片加载结果为空：{request.Location}");
                }

                candidate = new SpriteLease(handle, sprite, cloned);
                handle = null;
                // Asset getter 可以由扩展实现触发重入；写入前再次核对请求身份。
                if (!IsCurrent(request)) return;
                SpriteLease incoming = candidate;
                candidate = null;
                applied = ApplyLease(request, incoming);
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                try { candidate?.Release(); }
                catch (Exception exception) { failure = Combine(failure, exception); }
                try { handle?.Release(); }
                catch (Exception exception) { failure = Combine(failure, exception); }

                bool current = IsCurrent(request);
                if (ReferenceEquals(m_Pending, request)) m_Pending = null;
                request.Close();
                // 取消或替代已经向调用方返回 false；晚到的加载错误只在这里观察，不改动新请求或旧图。
                if (failure != null && current) request.Completion.TrySetException(failure);
                else request.Completion.TrySetResult(current && applied);
            }
        }

        /// <summary>普通业务每次通过现有静态门面加载；仅显式注入时使用资源接口，不缓存其他模块组件。</summary>
        private UniTask<IAssetHandle<T>> LoadAsync<T>(string location, CancellationToken ct) where T : UnityEngine.Object
        {
            return m_AssetManager != null
                ? m_AssetManager.LoadAsync<T>(location, ct)
                : Nova.Asset.LoadAsync<T>(location, ct);
        }

        /// <summary>交接当前租约并处理赋图回调重入；旧资源释放异常不会撤销已接管的新租约。</summary>
        private bool ApplyLease(BindingRequest request, SpriteLease incoming)
        {
            SpriteLease previous = m_Current;
            Sprite previousSprite = ReadSprite();
            m_Current = incoming;
            try
            {
                WriteSprite(incoming.Sprite);
            }
            catch (Exception exception)
            {
                Exception failure = exception;
                if (ReferenceEquals(m_Current, incoming))
                {
                    m_Current = previous;
                    previous = null;
                    try { WriteSprite(previousSprite); }
                    catch (Exception restoreException) { failure = Combine(failure, restoreException); }
                }
                try { incoming.Release(); }
                catch (Exception releaseException) { failure = Combine(failure, releaseException); }
                try { previous?.Release(); }
                catch (Exception releaseException) { failure = Combine(failure, releaseException); }
                throw failure;
            }
            previous?.Release();
            return IsCurrent(request) && ReferenceEquals(m_Current, incoming);
        }

        /// <summary>先断开内部当前状态和目标引用，再释放旧资源和取消旧请求；回调产生的新状态不会被清掉。</summary>
        private void ClearCore()
        {
            BindingRequest pending = m_Pending;
            SpriteLease lease = m_Current;
            m_Pending = null;
            m_Current = null;
            Exception failure = null;
            try { WriteSprite(null); }
            catch (Exception exception) { failure = exception; }
            try { lease?.Release(); }
            catch (Exception exception) { failure = Combine(failure, exception); }
            try { pending?.Cancel(); }
            catch (Exception exception) { failure = Combine(failure, exception); }
            if (failure != null) throw failure;
        }

        /// <summary>检查请求仍归当前目标所有，并把取消标志纳入每次写回判断。</summary>
        private bool IsCurrent(BindingRequest request)
        {
            return !IsDisposed && m_Target != null && ReferenceEquals(m_Pending, request) && !request.Token.IsCancellationRequested;
        }

        /// <summary>读取本地图片引用，仅在 Unity 主线程及目标仍存在时使用。</summary>
        private Sprite ReadSprite()
        {
            return m_Target is Image image ? image.sprite : ((SpriteRenderer)m_Target).sprite;
        }

        /// <summary>写入仍存在的本地目标；目标销毁时仅执行租约清理。</summary>
        private void WriteSprite(Sprite sprite)
        {
            if (m_Target == null) return;
            if (m_Target is Image image) image.sprite = sprite;
            else ((SpriteRenderer)m_Target).sprite = sprite;
        }

        /// <summary>令牌允许从工作线程取消，但取消结果和任何调用方续体都切回 Unity 主线程后完成。</summary>
        private async UniTask CompleteCancellationAsync(BindingRequest request)
        {
            await UniTask.SwitchToMainThread();
            if (ReferenceEquals(m_Pending, request)) m_Pending = null;
            request.Completion.TrySetResult(false);
        }

        /// <summary>目标销毁时结束本拥有者；清理异常明确记录，避免中断同一销毁令牌上的其他订阅。</summary>
        private static void OnTargetDestroyed(object state)
        {
            try { ((SpriteBinding)state).Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        /// <summary>使用 UniTask 在 Unity 初始化阶段记录的主线程身份，禁止把任意创建线程当成 Unity 线程。</summary>
        private static void EnsureMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
                throw new InvalidOperationException("SpriteBinding 必须在 Unity 主线程创建和调用。");
        }

        /// <summary>保留首个异常，并在多项清理失败时同时携带全部错误。</summary>
        private static Exception Combine(Exception first, Exception next)
        {
            return first == null ? next : new AggregateException(first, next);
        }

        /// <summary>仅观察本绑定尚未 Awake 的目标与宿主；结束或 Awake 后立即停止，不扫描其他对象。</summary>
        private sealed class UnawakenedTargetMonitor : IPlayerLoopItem
        {
            private SpriteBinding m_Owner;
            private MonoBehaviour m_LifetimeOwner;

            /// <summary>记录显式创建的本绑定与其真实宿主；Image 宿主就是 Image 组件自身。</summary>
            internal UnawakenedTargetMonitor(SpriteBinding owner, MonoBehaviour lifetimeOwner)
            {
                m_Owner = owner;
                m_LifetimeOwner = lifetimeOwner;
            }

            /// <summary>在下一次 Update 处理从未 Awake 的对象销毁；先断开观察引用再释放，防止重入复用旧状态。</summary>
            /// <returns>仅当本绑定尚未结束、目标仍存在且宿主仍未 Awake 时继续观察。</returns>
            public bool MoveNext()
            {
                SpriteBinding owner = m_Owner;
                MonoBehaviour lifetimeOwner = m_LifetimeOwner;
                if (owner == null || owner.IsDisposed) return Stop();
                if (owner.m_Target == null || lifetimeOwner == null)
                {
                    Stop();
                    OnTargetDestroyed(owner);
                    return false;
                }
                return lifetimeOwner.didAwake ? Stop() : true;
            }

            /// <summary>切断监测器对绑定和 Unity 宿主的引用，确保停止或销毁回调重入都不会再观察旧拥有者。</summary>
            private bool Stop()
            {
                m_Owner = null;
                m_LifetimeOwner = null;
                return false;
            }
        }

        /// <summary>单次绑定请求；其取消源只控制本次加载租约，底层完成前始终保留观察链。</summary>
        private sealed class BindingRequest
        {
            internal readonly string Location;
            internal readonly string SpriteName;
            internal readonly UniTaskCompletionSource<bool> Completion = new UniTaskCompletionSource<bool>();
            internal readonly CancellationToken Token;
            private readonly SpriteBinding m_Owner;
            private readonly CancellationTokenSource m_Cancellation;
            private CancellationTokenRegistration m_Registration;
            private bool m_Closed;

            /// <summary>为本次请求链接调用方取消，同时缓存令牌，关闭取消源后也不访问已释放源。</summary>
            internal BindingRequest(SpriteBinding owner, string location, string spriteName, CancellationToken ct)
            {
                m_Owner = owner;
                Location = location;
                SpriteName = spriteName;
                m_Cancellation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                Token = m_Cancellation.Token;
                m_Registration = Token.Register(OnCancelled, this);
            }

            /// <summary>淘汰本次请求；不取消其他请求的令牌源。</summary>
            internal void Cancel()
            {
                if (!m_Closed) m_Cancellation.Cancel();
            }

            /// <summary>底层观察链结束后移除令牌注册；重复关闭不重复释放。</summary>
            internal void Close()
            {
                if (m_Closed) return;
                m_Closed = true;
                m_Registration.Dispose();
                m_Cancellation.Dispose();
            }

            /// <summary>把任意线程的取消回调交回主线程完成，不在令牌回调内读写 Unity 对象。</summary>
            private static void OnCancelled(object state)
            {
                var request = (BindingRequest)state;
                request.m_Owner.CompleteCancellationAsync(request).Forget();
            }
        }

        /// <summary>当前图片的唯一租约；字段先断开后清理，保证回调重入及释放异常不会重复使用池化句柄。</summary>
        private sealed class SpriteLease
        {
            internal readonly Sprite Sprite;
            private IAssetHandle m_Handle;
            private Sprite m_Clone;

            /// <summary>接管已完成句柄；仅 GetSprite 创建的图片归本租约销毁。</summary>
            internal SpriteLease(IAssetHandle handle, Sprite sprite, bool cloned)
            {
                m_Handle = handle;
                Sprite = sprite;
                m_Clone = cloned ? sprite : null;
            }

            /// <summary>恰好一次销毁图集克隆并释放句柄；直接加载的 Sprite 始终由其句柄管理。</summary>
            internal void Release()
            {
                IAssetHandle handle = m_Handle;
                Sprite clone = m_Clone;
                m_Handle = null;
                m_Clone = null;
                try
                {
                    if (clone != null)
                    {
                        if (Application.isPlaying) UnityEngine.Object.Destroy(clone);
                        else UnityEngine.Object.DestroyImmediate(clone);
                    }
                }
                finally { handle?.Release(); }
            }
        }
    }
}
