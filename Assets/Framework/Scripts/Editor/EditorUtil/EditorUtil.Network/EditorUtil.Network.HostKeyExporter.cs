/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Network.HostKeyExporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   HostKeys 公共导出入口，把数据/类型请求交给 NetworkExporter 专用编排
 * input:     HostKeySettings（源目录、单元路径与正式输出位置）
 * output:    当前 ConfigRuntime DevelopMode 对应的 HostKeys JSON/C# 产物
 * boundary:  仅保持稳定公共 API；Sheet 校验、Luban 暂存和发布均由内部编排负责
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
            /// HostKeys 稳定公共导出入口。导出前会校验所有 Debug/Release Sheet 配对，
            /// 再按当前 ConfigRuntime.DevelopMode 选择数据并通过暂存事务发布。
            /// </summary>
            public static class HostKeyExporter
            {
                public static bool ExportHostKeyAll(HostKeySettings settings)
                {
                    return NetworkExporter.ExportHostKeys(settings, NetworkExporter.ExportMode.All);
                }

                public static bool ExportHostKeyCode(HostKeySettings settings)
                {
                    return NetworkExporter.ExportHostKeys(settings, NetworkExporter.ExportMode.Code);
                }

                public static bool ExportHostKeyData(HostKeySettings settings)
                {
                    return NetworkExporter.ExportHostKeys(settings, NetworkExporter.ExportMode.Data);
                }
            }
        }
    }
}
