/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableSettings.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   Table Luban Project 与运行时 Binding 设置
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Table 设置；Editor 保存 Luban 导出预设，Player 保存生成代码提供的运行时 Binding。
    /// </summary>
    [Serializable]
    public sealed class TableSettings
    {
#if UNITY_EDITOR
        public TableProjectSettings Project = TableProjectSettings.CreateDefault();
#endif
        public TableRuntimeSettings Runtime = new TableRuntimeSettings();
    }

    /// <summary>
    /// Player 运行时 Table 配置，可同时加载任意数量的 Luban 生成 Binding。
    /// </summary>
    [Serializable]
    public sealed class TableRuntimeSettings
    {
        public List<TableRuntimeBindingSetting> Bindings = new List<TableRuntimeBindingSetting>();
    }

    /// <summary>
    /// 一组 Luban 生成 Tables 的运行时 Binding 与数据资源前缀。
    /// </summary>
    [Serializable]
    public sealed class TableRuntimeBindingSetting
    {
        public string BindingTypeName = string.Empty;
        public string DataAssetLocationPrefix = string.Empty;
    }

#if UNITY_EDITOR
    /// <summary>
    /// 官方 Luban Project 入口及可组合使用的客户端导出 Profile。
    /// </summary>
    [Serializable]
    public sealed class TableProjectSettings
    {
        public string ConfigPath = "Assets/Samples/MainDemo/Excels/Tables/luban.conf";
        public string Target = "table";
        public List<TableExportProfileSetting> Profiles = new List<TableExportProfileSetting>();

        /// <summary>
        /// 创建包含全部客户端格式且默认选择 JSON 导出的项目设置。
        /// </summary>
        /// <returns>可直接在 Inspector 中编辑的默认设置。</returns>
        public static TableProjectSettings CreateDefault()
        {
            return new TableProjectSettings
            {
                Profiles = TableExportProfileSetting.CreateBuiltIn(),
            };
        }
    }

    /// <summary>
    /// 可序列化的 Table 导出 Profile，所有 target 与筛选参数均按 Luban 原生语义透传。
    /// </summary>
    [Serializable]
    public sealed class TableExportProfileSetting
    {
        public string Id = string.Empty;
        public bool Enabled;
        public List<string> CodeTargets = new List<string>();
        public List<string> DataTargets = new List<string>();
        public string CodeOutputPath = string.Empty;
        public string DataOutputPath = string.Empty;
        public List<string> IncludeTags = new List<string>();
        public List<string> ExcludeTags = new List<string>();
        public List<string> Variants = new List<string>();
        public List<TableLubanExtraArgument> ExtraArguments = new List<TableLubanExtraArgument>();
        public List<string> CustomTemplateDirs = new List<string>();

        /// <summary>
        /// 创建 JSON、Binary、Protobuf Binary/JSON 与 MsgPack 五个内置客户端 Profile。
        /// </summary>
        /// <returns>完整且无需额外安装 Codec 的 Profile 列表。</returns>
        public static List<TableExportProfileSetting> CreateBuiltIn()
        {
            return CreateBuiltIn("Assets/Samples/MainDemo");
        }

        /// <summary>
        /// 为指定 Demo 根目录创建五种完整客户端 Profile；默认只选择 JSON，其他预设可任意组合启用。
        /// </summary>
        /// <param name="demoRoot">Demo 的 Assets 相对根目录。</param>
        /// <returns>绑定到该 Demo 代码与数据目录的内置 Profile。</returns>
        public static List<TableExportProfileSetting> CreateBuiltIn(string demoRoot)
        {
            string normalizedRoot = (demoRoot ?? string.Empty).TrimEnd('/', '\\');
            string codePath = normalizedRoot + "/Scripts/Runtime/DataTypes/Tables";
            string dataPath = normalizedRoot + "/Jsons/Tables";
            return new List<TableExportProfileSetting>
            {
                Create("json", true, codePath, dataPath, "cs-newtonsoft-json", "json"),
                Create("binary", false, codePath, dataPath, "cs-bin", "bin"),
                Create("protobuf-binary", false, codePath, dataPath,
                    "protobuf3,cs-newtonsoft-json", "protobuf3-bin"),
                Create("protobuf-json", false, codePath, dataPath,
                    "protobuf3,cs-newtonsoft-json", "protobuf3-json"),
                Create("msgpack", false, codePath, dataPath,
                    "cs-newtonsoft-json", "msgpack"),
            };
        }

        /// <summary>
        /// 创建单个内置 Profile，并把逗号分隔代码目标展开为可重复的 -c 参数。
        /// </summary>
        /// <param name="id">Profile 唯一标识。</param>
        /// <param name="enabled">是否纳入无参数批量导出。</param>
        /// <param name="codePath">代码发布目录。</param>
        /// <param name="dataPath">数据发布目录。</param>
        /// <param name="codeTargets">逗号分隔的代码生成目标。</param>
        /// <param name="dataTarget">数据生成目标。</param>
        /// <returns>初始化完成的 Profile。</returns>
        private static TableExportProfileSetting Create(
            string id,
            bool enabled,
            string codePath,
            string dataPath,
            string codeTargets,
            string dataTarget)
        {
            return new TableExportProfileSetting
            {
                Id = id,
                Enabled = enabled,
                CodeTargets = new List<string>(codeTargets.Split(',')),
                DataTargets = new List<string> { dataTarget },
                CodeOutputPath = codePath,
                DataOutputPath = dataPath,
                CustomTemplateDirs = new List<string>
                {
                    "Assets/Framework/Templates/Luban/default",
                    "Assets/Framework/Templates/Luban/table",
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
