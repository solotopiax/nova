/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIView.Scope.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   页面单次打开作用域的显式接入
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    public abstract partial class UIView
    {
        private UIViewScope m_OpenScope;
        private CancellationTokenRegistration m_OpenScopeDestroyRegistration;
        private bool m_OpenScopeAvailable;

        /// <summary>主动取得本次打开的作用域；旧页面不访问时不创建令牌、订阅或清理记录。</summary>
        /// <remarks>只在管理器调用 OnOpen 期间或打开后可用；隐藏和暂停不结束，关闭、回收及销毁结束。</remarks>
        public UIViewScope OpenScope
        {
            get
            {
                if (!Cysharp.Threading.Tasks.PlayerLoopHelper.IsMainThread)
                    throw new InvalidOperationException("页面作用域只能在 Unity 主线程获取。");
                if (!m_OpenScopeAvailable)
                    throw new InvalidOperationException("页面尚未打开或已经关闭，不能取得本次打开作用域。");
                if (m_OpenScope == null)
                {
                    m_OpenScope = new UIViewScope();
                    m_OpenScopeDestroyRegistration = destroyCancellationToken.Register(EndOpenScope);
                    // 从未 Awake 的非激活对象销毁时，原生令牌可能不通知；只为显式取得的本次作用域补足观察。
                    if (!didAwake)
                        PlayerLoopHelper.AddAction(PlayerLoopTiming.Update, new UnawakenedOpenScopeMonitor(this, m_OpenScope));
                }
                return m_OpenScope;
            }
        }

        /// <summary>标记管理器已进入本次打开；不主动开启或接管旧页面任何工作。</summary>
        internal void BeginOpenScope()
        {
            EndOpenScope();
            m_OpenScopeAvailable = true;
        }

        /// <summary>先切断作用域归属，再结束主动登记项；允许关闭、回收和销毁重复到达。</summary>
        internal void EndOpenScope()
        {
            m_OpenScopeAvailable = false;
            UIViewScope scope = m_OpenScope;
            m_OpenScope = null;
            m_OpenScopeDestroyRegistration.Dispose();
            m_OpenScopeDestroyRegistration = default;
            scope?.DisposeFromOwner();
        }

        /// <summary>仅在显式接入且尚未 Awake 时短期观察本次作用域；Awake 后立即交回原生销毁令牌。</summary>
        private sealed class UnawakenedOpenScopeMonitor : IPlayerLoopItem
        {
            private UIView m_Owner;
            private UIViewScope m_Scope;

            /// <summary>捕获本次打开身份，不给其他页面添加组件，也不创建全局页面扫描。</summary>
            internal UnawakenedOpenScopeMonitor(UIView owner, UIViewScope scope)
            {
                m_Owner = owner;
                m_Scope = scope;
            }

            /// <summary>旧作用域结束或重开时停止；组件已经销毁时仅访问其托管字段并结束捕获的作用域。</summary>
            public bool MoveNext()
            {
                try
                {
                    UIView owner = m_Owner;
                    UIViewScope scope = m_Scope;
                    if (scope == null || scope.IsDisposed || !ReferenceEquals(owner.m_OpenScope, scope)) return Stop();
                    if (owner == null)
                    {
                        // Unity 原生对象已销毁，但托管包装及 EndOpenScope 的字段清理仍可安全访问。
                        owner.EndOpenScope();
                        return Stop();
                    }
                    if (owner.didAwake) return Stop();
                    return true;
                }
                catch (Exception exception)
                {
                    // 清理或日志助手的异常不得逃到 PlayerLoop，导致生命周期观察器反复运行。
                    try { Log.Error(LogTag.UI, "未激活页面作用域收尾失败：{0}", exception); }
                    catch
                    {
                        try { UnityEngine.Debug.LogException(exception); }
                        catch { }
                    }
                    return Stop();
                }
            }

            /// <summary>移除循环项前切断托管引用，旧观察器不能再结束后续新开的作用域。</summary>
            private bool Stop()
            {
                m_Owner = null;
                m_Scope = null;
                return false;
            }
        }
    }
}
