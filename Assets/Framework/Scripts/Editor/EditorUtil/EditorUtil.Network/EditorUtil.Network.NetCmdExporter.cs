/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Network.NetCmdExporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   NetCmds 公共导出入口，把数据/类型请求交给 NetworkExporter 专用编排
 * input:     NetCmdSettings（源目录、单元路径与正式输出位置）
 * output:    保持原表结构生成的 NetCmds JSON/C# 产物
 * boundary:  仅保持稳定公共 API；Excel 搬运、Luban 暂存和发布均由内部编排负责
 * failure:   内部任一阶段失败时返回 false，正式产物不被部分更新
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Network
        {
            /// <summary>
            /// NetCmds 稳定公共导出入口。表格结构不做模式筛选，生成结果通过暂存事务发布。
            /// </summary>
            public static class NetCmdExporter
            {
                public static bool ExportNetCmdAll(NetCmdSettings settings)
                {
                    return NetworkExporter.ExportNetCmds(settings, NetworkExporter.ExportMode.All);
                }

                public static bool ExportNetCmdCode(NetCmdSettings settings)
                {
                    return NetworkExporter.ExportNetCmds(settings, NetworkExporter.ExportMode.Code);
                }

                public static bool ExportNetCmdData(NetCmdSettings settings)
                {
                    return NetworkExporter.ExportNetCmds(settings, NetworkExporter.ExportMode.Data);
                }
            }
        }
    }
}
