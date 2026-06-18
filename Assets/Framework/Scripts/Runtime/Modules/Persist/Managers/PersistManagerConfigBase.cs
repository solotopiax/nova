/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistManagerConfigBase.cs
 * author:    taoye
 * created:   2026/4/27
 * descrip:   持久化管理器配置基类，提取三套 Config 的公共属性
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 持久化管理器配置基类。
    /// 提取 FileFragmentManagerConfig / PlayerPrefsManagerConfig / SQLiteManagerConfig 的公共属性。
    /// </summary>
    public class PersistManagerConfigBase
    {
        /// <summary>
        /// 是否启用 AES 加密存储值。
        /// </summary>
        public bool UseAESEncrypt { get; set; }

        /// <summary>
        /// 自动保存间隔（秒）。0 或负数表示禁用自动保存。
        /// </summary>
        public float AutoSaveInterval { get; set; }
    }
}
