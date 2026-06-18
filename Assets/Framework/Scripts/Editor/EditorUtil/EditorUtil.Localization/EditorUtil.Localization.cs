/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Localization.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   本地化导出工具入口（Editor-only，partial 桥接）
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 本地化模块导出工具入口（Editor-only）。
        /// 对外暴露文本和字体两条导出轨道；Inspector 薄封装调用本类 API。
        /// </summary>
        public static partial class Localization
        {
            /// <summary>
            /// 数据表设置适配器，将指定类型的单元设置列表包装为 IDataTableSettings 供 Luban Pipeline 消费。
            /// </summary>
            /// <typeparam name="TUnit">单元设置类型。</typeparam>
            private sealed class DataTableSettingsAdapter<TUnit> : IDataTableSettings where TUnit : IDataTableUnitSetting
            {
#if UNITY_EDITOR
                /// <inheritdoc />
                public string SourceDirPath { get; }
#endif

                /// <inheritdoc />
                public IReadOnlyList<IDataTableUnitSetting> Units { get; }

                /// <summary>
                /// 构造数据表设置适配器。
                /// </summary>
                /// <param name="sourceDirPath">数据源目录路径。</param>
                /// <param name="units">单元设置列表。</param>
                public DataTableSettingsAdapter(string sourceDirPath, List<TUnit> units)
                {
#if UNITY_EDITOR
                    SourceDirPath = sourceDirPath;
#endif
                    Units = units?.Cast<IDataTableUnitSetting>().ToList() ?? new List<IDataTableUnitSetting>();
                }
            }
        }
    }
}
