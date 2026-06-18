/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProtoSettings.cs
 * author:    taoye
 * created:   2026/4/17
 * descrip:   Protobuf 编辑器设置
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Protobuf 编辑器设置，存储 .proto 文件目录与各文件的 C# 导出路径。
    /// </summary>
    [Serializable]
    public class ProtoSettings
    {
#if UNITY_EDITOR
        /// <summary>
        /// .proto 文件根目录路径。
        /// </summary>
        public string ProtoSourceDirPath;
#endif

        /// <summary>
        /// Proto 单元设置列表。
        /// </summary>
        public List<ProtoUnitSetting> ProtoUnits = new List<ProtoUnitSetting>();
    }

    /// <summary>
    /// 单个 .proto 文件的导出设置。
    /// </summary>
    [Serializable]
    public class ProtoUnitSetting
    {
#if UNITY_EDITOR
        /// <summary>
        /// 相对 Proto 根目录的 .proto 文件路径。
        /// </summary>
        public string SourcePath;

        /// <summary>
        /// protoc 生成 C# 代码的输出目录。
        /// </summary>
        public string CSharpExportPath;
#endif
    }

}
