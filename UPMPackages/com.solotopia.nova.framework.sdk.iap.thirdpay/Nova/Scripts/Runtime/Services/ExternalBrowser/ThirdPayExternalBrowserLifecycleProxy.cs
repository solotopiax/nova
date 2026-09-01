/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayExternalBrowserLifecycleProxy.cs
 * author:    yingzheng
 * created:   2026/8/27
 * descrip:   ThirdPay 外部浏览器支付 Unity 生命周期代理
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// ThirdPay 外部浏览器支付专用 Unity 生命周期代理。
    /// </summary>
    internal sealed class ThirdPayExternalBrowserLifecycleProxy : MonoBehaviour
    {
        /// <summary>
        /// 隐藏 GameObject 名称。
        /// </summary>
        private const string c_GameObjectName = "[Nova ThirdPay ExternalBrowser Lifecycle]";

        /// <summary>
        /// 当前全局生命周期代理实例。
        /// </summary>
        private static ThirdPayExternalBrowserLifecycleProxy s_Instance;

        /// <summary>
        /// 暂停/恢复事件订阅。
        /// </summary>
        private event Action<bool> PauseReceived;

        /// <summary>
        /// 焦点变化事件订阅。
        /// </summary>
        private event Action<bool> FocusReceived;

        /// <summary>
        /// 注册 ThirdPay 外部浏览器支付生命周期回调。
        /// </summary>
        /// <param name="onPause">应用暂停/恢复回调。</param>
        /// <param name="onFocus">应用焦点变化回调。</param>
        public static void Register(Action<bool> onPause, Action<bool> onFocus)
        {
            ThirdPayExternalBrowserLifecycleProxy proxy = EnsureInstance();
            if (onPause != null)
            {
                proxy.PauseReceived -= onPause;
                proxy.PauseReceived += onPause;
            }

            if (onFocus != null)
            {
                proxy.FocusReceived -= onFocus;
                proxy.FocusReceived += onFocus;
            }
        }

        /// <summary>
        /// 注销 ThirdPay 外部浏览器支付生命周期回调。
        /// </summary>
        /// <param name="onPause">应用暂停/恢复回调。</param>
        /// <param name="onFocus">应用焦点变化回调。</param>
        public static void Unregister(Action<bool> onPause, Action<bool> onFocus)
        {
            ThirdPayExternalBrowserLifecycleProxy proxy = s_Instance;
            if (proxy == null)
            {
                return;
            }

            if (onPause != null)
            {
                proxy.PauseReceived -= onPause;
            }

            if (onFocus != null)
            {
                proxy.FocusReceived -= onFocus;
            }
        }

        /// <summary>
        /// 获取或创建隐藏生命周期代理。
        /// </summary>
        /// <returns>生命周期代理实例。</returns>
        private static ThirdPayExternalBrowserLifecycleProxy EnsureInstance()
        {
            if (s_Instance != null)
            {
                return s_Instance;
            }

            var go = new GameObject(c_GameObjectName);
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<ThirdPayExternalBrowserLifecycleProxy>();
            return s_Instance;
        }

        /// <summary>
        /// Unity 应用暂停/恢复回调。
        /// </summary>
        /// <param name="isPaused">true 表示进入后台或暂停，false 表示恢复前台。</param>
        private void OnApplicationPause(bool isPaused)
        {
            PauseReceived?.Invoke(isPaused);
        }

        /// <summary>
        /// Unity 应用焦点变化回调。
        /// </summary>
        /// <param name="hasFocus">true 表示获得焦点，false 表示失去焦点。</param>
        private void OnApplicationFocus(bool hasFocus)
        {
            FocusReceived?.Invoke(hasFocus);
        }

        /// <summary>
        /// Unity 对象销毁时清理静态实例引用。
        /// </summary>
        private void OnDestroy()
        {
            if (ReferenceEquals(s_Instance, this))
            {
                s_Instance = null;
            }

            PauseReceived = null;
            FocusReceived = null;
        }
    }
}
