/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableSettings.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   Table Luban Project、导出描述与运行时加载描述
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Table 设置；Editor 保存多个 Luban Project，Player 只消费加载描述。
    /// </summary>
    [Serializable]
    public sealed class TableSettings
    {
#if UNITY_EDITOR
        public List<TableLubanProjectSetting> Projects = new List<TableLubanProjectSetting>();
#endif
        public TableRuntimeSettings Runtime = new TableRuntimeSettings();
    }

    /// <summary>
    /// Player 运行时设置，可同时加载多个 Luban Tables 数据集。
    /// </summary>
    [Serializable]
    public sealed class TableRuntimeSettings
    {
        public List<TableLoadDescriptionSetting> LoadDescriptions = new List<TableLoadDescriptionSetting>();
    }

    /// <summary>
    /// 一套 Luban 生成结果的运行时加载描述。
    /// </summary>
    [Serializable]
    public sealed class TableLoadDescriptionSetting
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string ProjectId = string.Empty;
        public string ExportDescriptionId = string.Empty;
        public string RuntimeDataTarget = string.Empty;
        public string ResolvedBindingTypeName = string.Empty;
        public List<TableAssetAddressSetting> Assets = new List<TableAssetAddressSetting>();
    }

    /// <summary>
    /// 把 Luban 逻辑数据文件映射到 YooAsset 可寻址地址。
    /// </summary>
    [Serializable]
    public sealed class TableAssetAddressSetting
    {
        public string DataFile = string.Empty;
        public string AssetPath = string.Empty;
        public string AssetAddress = string.Empty;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Nova 管理的一套正式 Luban 工程入口及其导出描述。
    /// </summary>
    [Serializable]
    public sealed class TableLubanProjectSetting
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public string ConfigPath = string.Empty;
        public List<TableExportDescriptionSetting> ExportDescriptions = new List<TableExportDescriptionSetting>();

        /// <summary>
        /// 创建 MainDemo 使用的默认 Project。
        /// </summary>
        /// <returns>包含五种客户端格式预设的 Project。</returns>
        public static TableLubanProjectSetting CreateDefault()
        {
            return new TableLubanProjectSetting
            {
                Id = "main",
                Name = "Main",
                ConfigPath = "Assets/Samples/MainDemo/Excels/Tables/luban.conf",
                ExportDescriptions = TableExportDescriptionSetting.CreateBuiltIn(),
            };
        }
    }

    /// <summary>
    /// 导出描述创建时使用的五种客户端友好预设。
    /// </summary>
    public enum TableExportFormat
    {
        Json,
        Binary,
        ProtobufBinary,
        ProtobufJson,
        MsgPack,
    }

    /// <summary>
    /// 一次 Luban 调用的表格输出范围。
    /// </summary>
    public enum TableOutputScope
    {
        AllTables,
        SelectedTables,
    }

    /// <summary>
    /// Nova 对一次 Luban CLI 调用的持久化描述。
    /// </summary>
    [Serializable]
    public sealed class TableExportDescriptionSetting
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public bool Enabled;
        public string Target = "table";
        public TableExportFormat Format;
        public List<string> CodeTargets = new List<string>();
        public List<string> DataTargets = new List<string>();
        public TableOutputScope OutputScope;
        public List<string> OutputTables = new List<string>();
        public string CodeOutputPath = string.Empty;
        public string DataOutputPath = string.Empty;
        public List<string> IncludeTags = new List<string>();
        public List<string> ExcludeTags = new List<string>();
        public List<string> FieldVariants = new List<string>();
        public List<string> CustomTemplateDirs = new List<string>();
        public List<TableLubanExtraArgument> AdvancedArguments = new List<TableLubanExtraArgument>();

        /// <summary>
        /// 创建 JSON、Binary、Protobuf Binary/JSON 与 MsgPack 五种客户端预设。
        /// </summary>
        /// <returns>默认启用 JSON 的完整描述列表。</returns>
        public static List<TableExportDescriptionSetting> CreateBuiltIn()
        {
            return CreateBuiltIn("Assets/Samples/MainDemo");
        }

        /// <summary>
        /// 为指定 Demo 根目录创建五种客户端预设。
        /// </summary>
        /// <param name="demoRoot">Demo 的 Assets 相对根目录。</param>
        /// <returns>绑定到该目录的描述列表。</returns>
        public static List<TableExportDescriptionSetting> CreateBuiltIn(string demoRoot)
        {
            string root = (demoRoot ?? string.Empty).TrimEnd('/', '\\');
            string codePath = root + "/Scripts/Runtime/DataTypes/Tables";
            string dataPath = root + "/Jsons/Tables";
            return new List<TableExportDescriptionSetting>
            {
                Create("json", "JSON", true, TableExportFormat.Json, codePath, dataPath,
                    "cs-newtonsoft-json", "json"),
                Create("binary", "Binary", false, TableExportFormat.Binary, codePath, dataPath,
                    "cs-bin", "bin"),
                Create("protobuf-binary", "Protobuf Binary", false, TableExportFormat.ProtobufBinary,
                    codePath, dataPath, "protobuf3,cs-newtonsoft-json", "protobuf3-bin"),
                Create("protobuf-json", "Protobuf JSON", false, TableExportFormat.ProtobufJson,
                    codePath, dataPath, "protobuf3,cs-newtonsoft-json", "protobuf3-json"),
                Create("msgpack", "MsgPack", false, TableExportFormat.MsgPack, codePath, dataPath,
                    "cs-newtonsoft-json", "msgpack"),
            };
        }

        /// <summary>
        /// 创建单个导出描述并展开逗号分隔的代码 Target。
        /// </summary>
        private static TableExportDescriptionSetting Create(string id, string name, bool enabled,
            TableExportFormat format, string codePath, string dataPath, string codeTargets, string dataTarget)
        {
            return new TableExportDescriptionSetting
            {
                Id = id,
                Name = name,
                Enabled = enabled,
                Format = format,
                CodeTargets = new List<string>(codeTargets.Split(',')),
                DataTargets = new List<string> { dataTarget },
                CodeOutputPath = codePath,
                DataOutputPath = dataPath,
                CustomTemplateDirs = new List<string>
                {
                    "Packages/com.solotopia.nova.framework/Templates/Luban/default",
                    "Packages/com.solotopia.nova.framework/Templates/Luban/table",
                },
            };
        }
    }

    /// <summary>
    /// 表示一个原样传递给 Luban -x 的名称和值。
    /// </summary>
    [Serializable]
    public sealed class TableLubanExtraArgument
    {
        public string Name = string.Empty;
        public string Value = string.Empty;
    }
#endif
}
