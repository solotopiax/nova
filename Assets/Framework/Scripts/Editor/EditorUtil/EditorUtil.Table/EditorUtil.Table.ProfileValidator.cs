/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Table.ProfileValidator.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   Table Luban 导出 Profile 校验
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
            /// 保存 Profile 集合校验结果及全部可操作错误，不在首个错误处提前返回。
            /// </summary>
            public sealed class ProfileValidationResult
            {
                /// <summary>
                /// 创建校验结果并冻结错误集合。
                /// </summary>
                /// <param name="errors">本次校验发现的全部错误。</param>
                internal ProfileValidationResult(IEnumerable<string> errors)
                {
                    Errors = new ReadOnlyCollection<string>(new List<string>(errors));
                }

                public bool IsValid => Errors.Count == 0;
                public IReadOnlyList<string> Errors { get; }
            }

            /// <summary>
            /// 校验 Table 导出 Profile 标识；目标与输出目录按实际导出范围校验。
            /// </summary>
            public static class ProfileValidator
            {
                /// <summary>
                /// 校验 Profile 集合中的非空项和唯一 ID。
                /// </summary>
                /// <param name="profiles">待校验的全部导出 Profile。</param>
                /// <returns>包含所有错误的校验结果。</returns>
                public static ProfileValidationResult Validate(IEnumerable<TableExportProfileSetting> profiles)
                {
                    var errors = new List<string>();
                    var ids = new HashSet<string>(StringComparer.Ordinal);
                    foreach (TableExportProfileSetting profile in profiles ?? Array.Empty<TableExportProfileSetting>())
                    {
                        if (profile == null)
                        {
                            errors.Add("导出 Profile 不能为空。");
                            continue;
                        }

                        if (string.IsNullOrWhiteSpace(profile.Id))
                        {
                            errors.Add("导出 Profile ID 不能为空。");
                        }
                        else if (!ids.Add(profile.Id))
                        {
                            errors.Add($"检测到重复的导出 Profile ID：{profile.Id}。");
                        }

                    }

                    return new ProfileValidationResult(errors);
                }
            }
        }
    }
}
