/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigManagerConfig.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   ConfigManager 初始化入参，承载 AB 加载路径
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// ConfigManager 初始化入参；携带 Asset 地址，
    /// 由 ConfigComponent 在 Start 阶段从 Inspector 字段构造后交给 ConfigManager.Initialize。
    /// </summary>
    public sealed class ConfigManagerConfig
    {
        /// <summary>
        /// ConfigRuntimeSO 的 Asset 地址。
        /// </summary>
        public string AssetLocation;
    }
}
