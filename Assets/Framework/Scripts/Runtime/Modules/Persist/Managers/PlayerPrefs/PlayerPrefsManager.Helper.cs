/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerPrefsManager.Helper.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   PlayerPrefs 持久化管理器 —— 跨平台底层适配
 *            支持：抖音小游戏(NOVA_DYSDK) / 微信小游戏(NOVA_WXSDK) /
 *                  支付宝小游戏(NOVA_ALIPAYSDK) / Unity 默认
 ***************************************************************/

namespace NovaFramework.Runtime
{
    internal sealed partial class PlayerPrefsManager : PersistManagerBase<PlayerPrefsManagerConfig>, IPlayerPrefsManager
    {
        /// <summary>
        /// 将 PlayerPrefs 落盘到持久化存储（平台差异收敛于此）。
        /// </summary>
        private void _Save()
        {
#if NOVA_DYSDK
            StarkSDK.API.PlayerPrefs.Save();
#elif NOVA_WXSDK
            // 微信小游戏写操作已自动同步，无需手动落盘
#elif NOVA_ALIPAYSDK
            // 支付宝小游戏写操作已自动同步，无需手动落盘
#else
            UnityEngine.PlayerPrefs.Save();
#endif
        }

        /// <summary>
        /// 判断指定键是否存在于 PlayerPrefs。
        /// </summary>
        /// <param name="key">PlayerPrefs 键。</param>
        /// <returns>存在返回 true。</returns>
        private bool _HasKey(string key)
        {
#if NOVA_DYSDK
            return StarkSDK.API.PlayerPrefs.HasKey(key);
#elif NOVA_WXSDK
            return WX.GetStorageSync(key) != null;
#elif NOVA_ALIPAYSDK
            return AlipaySDK.API.GetStorageSync(key) != null;
#else
            return UnityEngine.PlayerPrefs.HasKey(key);
#endif
        }

        /// <summary>
        /// 删除指定键。
        /// </summary>
        /// <param name="key">PlayerPrefs 键。</param>
        private void _DeleteKey(string key)
        {
#if NOVA_DYSDK
            StarkSDK.API.PlayerPrefs.DeleteKey(key);
#elif NOVA_WXSDK
            WX.RemoveStorageSync(key);
#elif NOVA_ALIPAYSDK
            AlipaySDK.API.RemoveStorageSync(key);
#else
            UnityEngine.PlayerPrefs.DeleteKey(key);
#endif
        }

        /// <summary>
        /// 写入字符串到 PlayerPrefs（内存，不落盘）。
        /// </summary>
        /// <param name="key">PlayerPrefs 键。</param>
        /// <param name="value">要写入的字符串值。</param>
        private void _SetString(string key, string value)
        {
#if NOVA_DYSDK
            StarkSDK.API.PlayerPrefs.SetString(key, value);
#elif NOVA_WXSDK
            WX.SetStorageSync(key, value);
#elif NOVA_ALIPAYSDK
            AlipaySDK.API.SetStorageSync(key, value);
#else
            UnityEngine.PlayerPrefs.SetString(key, value);
#endif
        }

        /// <summary>
        /// 从 PlayerPrefs 读取字符串。
        /// </summary>
        /// <param name="key">PlayerPrefs 键。</param>
        /// <param name="defaultValue">键不存在时返回的默认值。</param>
        /// <returns>读取到的字符串，不存在时返回默认值。</returns>
        private string _GetString(string key, string defaultValue = "")
        {
#if NOVA_DYSDK
            return StarkSDK.API.PlayerPrefs.GetString(key, defaultValue);
#elif NOVA_WXSDK
            return WX.GetStorageSync(key) ?? defaultValue;
#elif NOVA_ALIPAYSDK
            return AlipaySDK.API.GetStorageSync(key) ?? defaultValue;
#else
            return UnityEngine.PlayerPrefs.GetString(key, defaultValue);
#endif
        }
    }
}
