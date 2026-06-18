/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkSettings.cs
 * author:    taoye
 * created:   2026/3/11
 * descrip:   网络设置
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 网络设置，包含域名表（HostKey）和指令表（NetCmd）两套独立 Luban 构建单元。
    /// </summary>
    [Serializable]
    public class NetworkSettings
    {
        /// <summary>
        /// 域名表设置。
        /// </summary>
        public HostKeySettings HostKeySettings;

        /// <summary>
        /// 指令表设置。
        /// </summary>
        public NetCmdSettings NetCmdSettings;
    }

    /// <summary>
    /// 域名表设置，实现 IDataTableSettings 接口以接入 Luban 导出流水线。
    /// 管理 HostKey Excel 数据源的编辑器工作路径、运行时命名空间和单元设置列表。
    /// </summary>
    [Serializable]
    public class HostKeySettings : IDataTableSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// 数据源目录路径。
        /// </summary>
        public string SourceDirPath;
        /// <inheritdoc />
        string IDataTableSettings.SourceDirPath => SourceDirPath ?? "";

        /// <summary>
        /// 导出数据位置。
        /// </summary>
        public string DatasExportPath;

        /// <summary>
        /// 导出类型定义位置。
        /// </summary>
        public string ClassesExportPath;
#endif

        /// <summary>
        /// 域名表单元设置列表。
        /// </summary>
        public List<HostKeyUnitSetting> HostKeyUnits = new List<HostKeyUnitSetting>();
        /// <inheritdoc />
        IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units => HostKeyUnits ?? new List<HostKeyUnitSetting>();
    }

    /// <summary>
    /// 单个域名表数据源的单元设置，实现 IDataTableUnitSetting 接口以接入 Luban 导出流水线。
    /// 使用 Map 模式，以 "Name" 为索引字段。
    /// </summary>
    [Serializable]
    public class HostKeyUnitSetting : IDataTableUnitSetting
    {
#if UNITY_EDITOR
        /// <summary>
        /// 相对域名表目录的数据源文件相对路径，用于唯一标识该文件。
        /// </summary>
        public string SourcePath;
        /// <inheritdoc />
        string IDataTableUnitSetting.SourcePath => SourcePath;

        /// <summary>
        /// 该数据源的导出数据位置。
        /// </summary>
        public string DatasExportPath;
        /// <inheritdoc />
        string IDataTableUnitSetting.DatasExportPath => DatasExportPath;

        /// <summary>
        /// 该数据源的导出类型定义位置。
        /// </summary>
        public string ClassesExportPath;
        /// <inheritdoc />
        string IDataTableUnitSetting.ClassesExportPath => ClassesExportPath;

        /// <inheritdoc />
        string IDataTableUnitSetting.LubanInputPath => "_temp/" + System.IO.Path.GetFileNameWithoutExtension(SourcePath);
#endif

        /// <summary>
        /// 域名表资源的地址。
        /// </summary>
        public string AssetLocation;
        /// <inheritdoc />
        string IDataTableUnitSetting.AssetLocation => AssetLocation;

        /// <inheritdoc />
        DataTableMode IDataTableUnitSetting.Mode => DataTableMode.Map;

        /// <inheritdoc />
        string IDataTableUnitSetting.IndexField => "Name";

        /// <summary>
        /// 域名表数据类型短名称列表（不含命名空间），一个 JSON 可包含多个类型。
        /// </summary>
        public List<string> DataTypeNames = new List<string>();
        /// <inheritdoc />
        IReadOnlyList<string> IDataTableUnitSetting.DataTypeNames => DataTypeNames;
    }

    /// <summary>
    /// 指令表设置，实现 IDataTableSettings 接口以接入 Luban 导出流水线。
    /// 管理 NetCmd Excel 数据源的编辑器工作路径、运行时命名空间和单元设置列表。
    /// </summary>
    [Serializable]
    public class NetCmdSettings : IDataTableSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// 数据源目录路径。
        /// </summary>
        public string SourceDirPath;
        /// <inheritdoc />
        string IDataTableSettings.SourceDirPath => SourceDirPath ?? "";

        /// <summary>
        /// 导出数据位置。
        /// </summary>
        public string DatasExportPath;

        /// <summary>
        /// 导出类型定义位置。
        /// </summary>
        public string ClassesExportPath;
#endif

        /// <summary>
        /// 指令表单元设置列表。
        /// </summary>
        public List<NetCmdUnitSetting> NetCmdUnits = new List<NetCmdUnitSetting>();
        /// <inheritdoc />
        IReadOnlyList<IDataTableUnitSetting> IDataTableSettings.Units => NetCmdUnits ?? new List<NetCmdUnitSetting>();
    }

    /// <summary>
    /// 单个指令表数据源的单元设置，实现 IDataTableUnitSetting 接口以接入 Luban 导出流水线。
    /// 使用 Map 模式，以 "Name" 为索引字段。
    /// </summary>
    [Serializable]
    public class NetCmdUnitSetting : IDataTableUnitSetting
    {
#if UNITY_EDITOR
        /// <summary>
        /// 相对指令表目录的数据源文件相对路径，用于唯一标识该文件。
        /// </summary>
        public string SourcePath;
        /// <inheritdoc />
        string IDataTableUnitSetting.SourcePath => SourcePath;

        /// <summary>
        /// 该数据源的导出数据位置。
        /// </summary>
        public string DatasExportPath;
        /// <inheritdoc />
        string IDataTableUnitSetting.DatasExportPath => DatasExportPath;

        /// <summary>
        /// 该数据源的导出类型定义位置。
        /// </summary>
        public string ClassesExportPath;
        /// <inheritdoc />
        string IDataTableUnitSetting.ClassesExportPath => ClassesExportPath;

        /// <inheritdoc />
        string IDataTableUnitSetting.LubanInputPath => "_temp/" + System.IO.Path.GetFileNameWithoutExtension(SourcePath);
#endif

        /// <summary>
        /// 指令表资源的地址。
        /// </summary>
        public string AssetLocation;
        /// <inheritdoc />
        string IDataTableUnitSetting.AssetLocation => AssetLocation;

        /// <inheritdoc />
        DataTableMode IDataTableUnitSetting.Mode => DataTableMode.Map;

        /// <inheritdoc />
        string IDataTableUnitSetting.IndexField => "Name";

        /// <summary>
        /// 指令表数据类型短名称列表（不含命名空间），一个 JSON 可包含多个类型。
        /// </summary>
        public List<string> DataTypeNames = new List<string>();
        /// <inheritdoc />
        IReadOnlyList<string> IDataTableUnitSetting.DataTypeNames => DataTypeNames;
    }

}
