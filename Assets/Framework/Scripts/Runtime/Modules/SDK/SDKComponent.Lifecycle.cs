/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKComponent.Lifecycle.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   SDK 组件 —— Unity 应用生命周期代理（Pause / Focus / Quit）
 ***************************************************************/

namespace NovaFramework.Runtime
{
    public sealed partial class SDKComponent : FrameworkComponent
    {
        /// <summary>
        /// 应用暂停/恢复事件代理：转发至 Manager.BroadcastPause，由 Manager 分发给所有 ISDKPauseListener 插件。
        /// Manager 未就绪（Awake 未完成或已销毁）时静默跳过。
        /// </summary>
        /// <param name="isPaused">true 表示进入后台，false 表示回到前台。</param>
        private void OnApplicationPause(bool isPaused)
        {
            if (m_SDKManager == null)
            {
                return;
            }

            m_SDKManager.BroadcastPause(isPaused);
        }

        /// <summary>
        /// 应用焦点事件代理：转发至 Manager.BroadcastFocus，由 Manager 分发给所有 ISDKFocusListener 插件。
        /// Manager 未就绪时静默跳过。
        /// </summary>
        /// <param name="hasFocus">true 表示获得焦点，false 表示失去焦点。</param>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (m_SDKManager == null)
            {
                return;
            }

            m_SDKManager.BroadcastFocus(hasFocus);
        }

        /// <summary>
        /// 应用退出事件代理：转发至 Manager.BroadcastQuit，由 Manager 分发给所有 ISDKQuitListener 插件。
        /// Manager 未就绪时静默跳过。
        /// </summary>
        private void OnApplicationQuit()
        {
            if (m_SDKManager == null)
            {
                return;
            }

            m_SDKManager.BroadcastQuit();
        }
    }
}
