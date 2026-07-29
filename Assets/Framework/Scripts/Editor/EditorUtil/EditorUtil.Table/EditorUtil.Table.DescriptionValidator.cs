/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Table.DescriptionValidator.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   Table Luban 导出描述校验
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Table
        {
            /// <summary>
            /// 保存导出描述集合校验结果及全部可操作错误，不在首个错误处提前返回。
            /// </summary>
            public sealed class DescriptionValidationResult
            {
                /// <summary>
                /// 创建校验结果并冻结错误集合。
                /// </summary>
                /// <param name="errors">本次校验发现的全部错误。</param>
                internal DescriptionValidationResult(IEnumerable<string> errors)
                {
                    Errors = new ReadOnlyCollection<string>(new List<string>(errors));
                }

                public bool IsValid => Errors.Count == 0;
                public IReadOnlyList<string> Errors { get; }
            }

            /// <summary>
            /// 校验 Table 导出描述标识；目标与输出目录按实际导出范围校验。
            /// </summary>
            public static class DescriptionValidator
            {
                /// <summary>
                /// 校验导出描述集合中的非空项和唯一 ID。
                /// </summary>
                /// <param name="descriptions">待校验的全部导出描述。</param>
                /// <returns>包含所有错误的校验结果。</returns>
                public static DescriptionValidationResult Validate(IEnumerable<TableExportDescriptionSetting> descriptions)
                {
                    var errors = new List<string>();
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (TableExportDescriptionSetting description in descriptions ?? Array.Empty<TableExportDescriptionSetting>())
                    {
                        if (description == null)
                        {
                            errors.Add("导出描述不能为空。");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(description.Id))
                        {
                            errors.Add("导出描述 ID 不能为空。");
                        }
                        else if (!ids.Add(description.Id))
                        {
                            errors.Add($"检测到重复的导出描述 ID：{description.Id}。");
                        }

                        if (description.IncludeTags != null && description.IncludeTags.Count > 0 &&
                            description.ExcludeTags != null && description.ExcludeTags.Count > 0)
                        {
                            errors.Add($"导出描述 {description.Id} 的包含 Tags 与排除 Tags 不能同时配置。");
                        }

                        if (description.OutputScope == TableOutputScope.SelectedTables &&
                            (description.OutputTables == null || description.OutputTables.Count == 0))
                        {
                            errors.Add($"导出描述 {description.Id} 选择了指定表格，但表格清单为空。");
                        }

                    }

                    return new DescriptionValidationResult(errors);
                }
            }
        }
    }
}
