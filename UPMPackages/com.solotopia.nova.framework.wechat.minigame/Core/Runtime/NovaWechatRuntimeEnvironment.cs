/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaWechatRuntimeEnvironment.cs
 * author:    taoye
 * created:   2026/9/18
 * descrip:   微信小游戏运行环境识别
 ***************************************************************/

using System.Runtime.InteropServices;

namespace WeChatWASM
{
    /// <summary>
    /// 区分普通浏览器 WebGL 与真实微信小游戏运行环境。
    /// </summary>
    internal static class NovaWechatRuntimeEnvironment
    {
#if UNITY_WEBGL && !UNITY_EDITOR && !WEIXINMINIGAME && !UNITY_WECHATMINIGAME && !UNITY_WEIXINMINIGAME
        [DllImport("__Internal")]
        private static extern int NovaWechatIsMiniGame();
#endif

        /// <summary>
        /// 获取当前 Player 是否实际运行在微信小游戏容器中。
        /// </summary>
        internal static bool IsMiniGame
        {
            get
            {
#if WEIXINMINIGAME || UNITY_WECHATMINIGAME || UNITY_WEIXINMINIGAME
                return true;
#elif UNITY_WEBGL && !UNITY_EDITOR
                return NovaWechatIsMiniGame() != 0;
#else
                return false;
#endif
            }
        }
    }
}
