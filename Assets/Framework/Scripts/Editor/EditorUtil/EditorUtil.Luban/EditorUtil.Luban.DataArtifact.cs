/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.DataArtifact.cs
 * author:    taoye
 * created:   2026/7/31
 * descrip:   Luban 数据产物格式互斥登记
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// 管理同名 JSON 与 Binary 正式数据产物的互斥关系。
            /// </summary>
            internal static class DataArtifact
            {
                /// <summary>
                /// 在输出事务中登记另一种格式的数据文件及其 Unity 元文件删除操作。
                /// </summary>
                /// <param name="outputApplier">当前输出事务。</param>
                /// <param name="selectedDataPath">本次选中格式的正式数据路径。</param>
                /// <param name="selectedFormat">本次导出的 Luban 数据格式。</param>
                internal static void RegisterCounterpartDeletion(
                    FileSystem.OutputApplier outputApplier,
                    string selectedDataPath,
                    LubanDataFormat selectedFormat)
                {
                    if (outputApplier == null)
                    {
                        throw new ArgumentNullException(nameof(outputApplier));
                    }
                    if (string.IsNullOrWhiteSpace(selectedDataPath))
                    {
                        throw new ArgumentException("Luban data path cannot be empty.", nameof(selectedDataPath));
                    }

                    string counterpartExtension = selectedFormat switch
                    {
                        LubanDataFormat.Json => ".bytes",
                        LubanDataFormat.Binary => ".json",
                        _ => throw new ArgumentOutOfRangeException(nameof(selectedFormat), selectedFormat, null),
                    };
                    string counterpartPath = IOPath.ChangeExtension(selectedDataPath, counterpartExtension);
                    outputApplier.AddDeletion(counterpartPath);
                    outputApplier.AddDeletion(counterpartPath + ".meta");
                }
            }
        }
    }
}
