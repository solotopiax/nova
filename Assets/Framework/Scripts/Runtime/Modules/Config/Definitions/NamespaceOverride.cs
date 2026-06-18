/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NamespaceOverride.cs
 * author:    taoye
 * created:   2026/6/2
 * descrip:   Namespace 字段的维度 Override 单项容器
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Namespace 字段的维度 Override 单项。
    /// 当 ConfigMasterSO.NamespaceMask 至少勾选一个维度轴时，
    /// 列表中与当前 Platform / Channel / DevelopMode 匹配的首个条目覆盖顶层 Namespace 字段；
    /// 未勾选的轴填写 None（Platform / Channel）或任意值（DevelopMode 无 None 哨兵时默认 Debug）。
    /// 列表为空或无命中时，使用顶层 ConfigMasterSO.Namespace 作为全局默认值。
    /// </summary>
    [Serializable]
    public sealed class NamespaceOverride
    {
        /// <summary>
        /// 平台类型筛选轴；仅当 NamespaceMask.ByPlatform == true 时参与匹配，
        /// 不参与匹配时设置为 PlatformType.None 哨兵。
        /// </summary>
        public PlatformType Platform;

        /// <summary>
        /// 渠道类型筛选轴；仅当 NamespaceMask.ByChannel == true 时参与匹配，
        /// 不参与匹配时设置为 ChannelType.None 哨兵。
        /// </summary>
        public ChannelType Channel;

        /// <summary>
        /// 开发模式筛选轴；仅当 NamespaceMask.ByDevelopMode == true 时参与匹配；
        /// DevelopMode 枚举无 None 哨兵，不参与匹配时维持默认值 DevelopMode.Debug。
        /// </summary>
        public DevelopMode DevelopMode;

        /// <summary>
        /// 当前维度组合下的 Namespace Override 值；覆盖顶层 ConfigMasterSO.Namespace 字段。
        /// </summary>
        public string Value;
    }
}
