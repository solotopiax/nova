/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Network.ProtoExporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Proto 协议导出工具，薄桥接：从 ProtoSettings 读配置，调 EditorUtil.Proto.CliRunner
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Network
        {
            /// <summary>
            /// Proto 协议导出工具，从 ProtoSettings 读取源目录与各文件的输出目录，
            /// 按单元逐文件调用 EditorUtil.Proto.CliRunner.CompileSingle 完成 C# 代码生成。
            /// 等价于 Inspector "导出所有协议类型" 按钮。
            /// </summary>
            public static class ProtoExporter
            {
                /// <summary>
                /// 编译 ProtoSettings 中所有已配置的 .proto 文件为 C# 代码，
                /// 每个 ProtoUnitSetting 的 CSharpExportPath 独立指定输出目录。
                /// 与 EditorUtil.Proto.CliRunner.CompileAll 的区别：
                ///   CompileAll 将所有文件输出到同一目录；
                ///   ExportAllProtos 按每个 Unit 的 CSharpExportPath 分别输出，与 Inspector 行为完全一致。
                /// </summary>
                /// <param name="settings">Protobuf 编辑器设置。</param>
                /// <returns>全部单元编译成功返回 true，任意单元失败返回 false。</returns>
                public static bool ExportAllProtos(ProtoSettings settings)
                {
                    if (settings == null)
                    {
                        Log.Error(LogTag.Editor, "ProtoSettings 为 null，无法导出 Proto。");
                        return false;
                    }

                    string protoDir = settings.ProtoSourceDirPath;
                    if (string.IsNullOrEmpty(protoDir) || !Util.SysIO.Directory.Exists(protoDir))
                    {
                        Log.Error(LogTag.Editor, "Proto 源目录不存在：{0}", protoDir);
                        return false;
                    }

                    var units = settings.ProtoUnits;
                    if (units == null || units.Count == 0)
                    {
                        Log.Warning(LogTag.Editor, "ProtoSettings.ProtoUnits 为空，跳过导出。");
                        return true;
                    }

                    string rootNorm = protoDir.TrimEnd('/', '\\');
                    bool allSuccess = true;

                    for (int i = 0; i < units.Count; i++)
                    {
                        ProtoUnitSetting unit = units[i];
                        if (string.IsNullOrEmpty(unit.SourcePath) || string.IsNullOrEmpty(unit.CSharpExportPath))
                        {
                            continue;
                        }

                        string fullProtoPath = Util.SysIO.Path.Combine(rootNorm, unit.SourcePath);
                        if (!Util.SysIO.File.Exists(fullProtoPath))
                        {
                            continue;
                        }

                        if (!EditorUtil.Proto.CliRunner.CompileSingle(fullProtoPath, rootNorm, unit.CSharpExportPath))
                        {
                            allSuccess = false;
                        }
                    }

                    if (allSuccess)
                    {
                        AssetDatabase.Refresh();
                    }

                    return allSuccess;
                }
            }
        }
    }
}
