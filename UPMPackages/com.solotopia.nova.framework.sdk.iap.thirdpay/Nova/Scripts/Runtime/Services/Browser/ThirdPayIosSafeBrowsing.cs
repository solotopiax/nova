/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayIosSafeBrowsing.cs
 * author:    yingzheng
 * created:   2026/11/16
 * descrip:   iOS 系统浏览器托管桥与原生回调生命周期
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
#if UNITY_IOS && !UNITY_EDITOR
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
#endif

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 只桥接系统页面，不接收或解释支付 Deep Link。
    /// </summary>
    internal sealed class ThirdPayIosSafeBrowsing : IThirdPaySafeBrowsing
    {
        public event Action<long> UserDismissed;
        private long m_SessionId;
        private bool m_Disposed;
        private UniTaskCompletionSource<bool> m_Shown;

        public bool IsSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return !m_Disposed;
#else
                return false;
#endif
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeCallback(long sessionId, int eventCode);
        private static readonly NativeCallback s_Callback = OnNativeEvent;
        private static readonly Dictionary<long, ThirdPayIosSafeBrowsing> s_Sessions = new Dictionary<long, ThirdPayIosSafeBrowsing>();

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NovaThirdPaySafeBrowsingShow(string url, long sessionId, NativeCallback callback);

        [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
        private static extern void NovaThirdPaySafeBrowsingDismiss(long sessionId);

        /// <summary>
        /// AOT 静态入口由长寿命委托持有，原生仅发送会话 ID 和页面事件。
        /// </summary>
        [MonoPInvokeCallback(typeof(NativeCallback))]
        private static void OnNativeEvent(long sessionId, int eventCode)
        {
            DispatchNativeEventAsync(sessionId, eventCode).Forget();
        }

        /// <summary>
        /// 将所有原生回调串行化到主线程，释放后的旧会话直接丢弃。
        /// </summary>
        private static async UniTask DispatchNativeEventAsync(long sessionId, int eventCode)
        {
            await UniTask.SwitchToMainThread();
            if (!s_Sessions.TryGetValue(sessionId, out ThirdPayIosSafeBrowsing browser) || browser.m_Disposed) return;
            if (eventCode == 1) browser.m_Shown.TrySetResult(true);
            else if (eventCode == 2) browser.m_Shown.TrySetResult(false);
            else if (eventCode == 3)
            {
                browser.UserDismissed?.Invoke(sessionId);
                browser.m_Shown.TrySetResult(false);
            }
        }
#endif

        /// <summary>
        /// 只接受 HTTP(S)，先登记回调再请求原生展示，取消无需等待动画结束。
        /// </summary>
        public async UniTask<bool> ShowAsync(string url, long sessionId, CancellationToken ct)
        {
            await UniTask.SwitchToMainThread();
            if (!IsSupported || sessionId <= 0 || !Uri.TryCreate(url, UriKind.Absolute, out Uri uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return false;
            if (m_SessionId != 0) throw new InvalidOperationException("系统浏览器实例只能持有一个页面。");
            ct.ThrowIfCancellationRequested();
#if UNITY_IOS && !UNITY_EDITOR
            m_SessionId = sessionId;
            m_Shown = new UniTaskCompletionSource<bool>();
            s_Sessions.Add(sessionId, this);
            using (ct.Register(() => CancelOnMainThreadAsync(sessionId).Forget()))
            {
                if (ct.IsCancellationRequested) { Dismiss(sessionId); return false; }
                try
                {
                    NovaThirdPaySafeBrowsingShow(url, sessionId, s_Callback);
                    return await m_Shown.Task;
                }
                catch
                {
                    Dismiss(sessionId);
                    throw;
                }
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// 取消可能来自后台线程，只在主线程关闭对应原生页面。
        /// </summary>
        private async UniTask CancelOnMainThreadAsync(long sessionId)
        {
            await UniTask.SwitchToMainThread();
            Dismiss(sessionId);
        }

        /// <summary>
        /// 移除映射后再关闭原生页，主动关闭不会变成用户取消；重复关闭无副作用。
        /// </summary>
        public void Dismiss(long sessionId)
        {
            if (!PlayerLoopHelper.IsMainThread) { CancelOnMainThreadAsync(sessionId).Forget(); return; }
            if (m_SessionId != sessionId || sessionId == 0) return;
            m_SessionId = 0;
#if UNITY_IOS && !UNITY_EDITOR
            s_Sessions.Remove(sessionId);
            NovaThirdPaySafeBrowsingDismiss(sessionId);
#endif
            m_Shown?.TrySetResult(false);
        }

        /// <summary>
        /// 在主线程释放委托订阅和原生页面，显示等待同步结束。
        /// </summary>
        public void Dispose()
        {
            if (!PlayerLoopHelper.IsMainThread) { DisposeOnMainThreadAsync().Forget(); return; }
            if (m_Disposed) return;
            m_Disposed = true;
            UserDismissed = null;
            Dismiss(m_SessionId);
        }

        /// <summary>
        /// 后台释放操作必须等待主线程再访问原生 UIKit 对象。
        /// </summary>
        private async UniTask DisposeOnMainThreadAsync()
        {
            await UniTask.SwitchToMainThread();
            Dispose();
        }
    }
}
