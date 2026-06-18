/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateSettings.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动设置
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动设置。持有 Emphasis 和 Custom 两个独立区域的字段，不直接实现 IDataTableSettings。
    /// 通过 GetEmphasisAsSettings / GetCustomAsSettings 按需提供各区域的适配视图。
    /// </summary>
    [Serializable]
    public class VibrateSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// Emphasis 数据源目录路径（仅编辑器使用）。
        /// </summary>
        public string EmphasisSourceDirPath;

        /// <summary>
        /// Custom 数据源目录路径（仅编辑器使用）。
        /// </summary>
        public string CustomSourceDirPath;
#endif

        /// <summary>
        /// Emphasis 振动单元设置列表（每个单元独立指定导出路径和运行时 Asset 地址）。
        /// </summary>
        public List<VibrateUnitSetting> EmphasisUnitsSettings = new List<VibrateUnitSetting>();

        /// <summary>
        /// Custom 振动单元设置列表（每个单元独立指定导出路径和运行时 Asset 地址）。
        /// </summary>
        public List<VibrateUnitSetting> CustomUnitsSettings = new List<VibrateUnitSetting>();

        /// <summary>
        /// 获取 Emphasis 区域作为独立 IDataTableSettings 的适配视图。
        /// </summary>
        /// <returns>Emphasis 区域的 IDataTableSettings 适配实例。</returns>
        public IDataTableSettings GetEmphasisAsSettings()
        {
            return new VibrateAreaSettingsAdapter(
#if UNITY_EDITOR
                EmphasisSourceDirPath,
#endif
                EmphasisUnitsSettings);
        }

        /// <summary>
        /// 获取 Custom 区域作为独立 IDataTableSettings 的适配视图。
        /// </summary>
        /// <returns>Custom 区域的 IDataTableSettings 适配实例。</returns>
        public IDataTableSettings GetCustomAsSettings()
        {
            return new VibrateAreaSettingsAdapter(
#if UNITY_EDITOR
                CustomSourceDirPath,
#endif
                CustomUnitsSettings);
        }
    }

    /// <summary>
    /// 振动区域设置适配器，将 VibrateSettings 中单个区域的字段包装为 IDataTableSettings。
    /// 仅作传参适配使用，不参与序列化。
    /// </summary>
    internal sealed class VibrateAreaSettingsAdapter : IDataTableSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// 数据源目录路径（仅编辑器使用）。
        /// </summary>
        private readonly string m_SourceDirPath;
        string IDataTableSettings.SourceDirPath => m_SourceDirPath;
#endif

        /// <summary>
        /// 振动单元设置列表。
        /// </summary>
        private readonly List<VibrateUnitSetting> m_Units;
        IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units => m_Units;

        /// <summary>
        /// 初始化振动区域设置适配器的新实例。
        /// </summary>
        /// <param name="units">振动单元设置列表。</param>
#if UNITY_EDITOR
        /// <param name="sourceDirPath">数据源目录路径（仅编辑器使用）。</param>
        public VibrateAreaSettingsAdapter(string sourceDirPath, List<VibrateUnitSetting> units)
        {
            m_SourceDirPath = sourceDirPath ?? string.Empty;
#else
        public VibrateAreaSettingsAdapter(List<VibrateUnitSetting> units)
        {
#endif
            m_Units = units;
        }
    }
}
