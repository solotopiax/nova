/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.ExportProfile.cs
 * author:    taoye
 * created:   2026/7/16
 * descrip:   Luban 导出目标固定配置
 ***************************************************************/

using System;
using System.Collections.Generic;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// 单个 Luban 导出目标的固定配置。仅供 Nova Editor 导出链内部使用。
            /// </summary>
            internal sealed class LubanExportProfile
            {
                internal string Id { get; }
                internal string TargetName { get; }
                internal string ManagerName { get; }
                internal string TemplateKey { get; }
                internal int MinHeaderRowCount { get; }

                internal LubanExportProfile(string id, string targetName, string managerName, string templateKey, int minHeaderRowCount = 5)
                {
                    Id = Require(id, nameof(id));
                    TargetName = Require(targetName, nameof(targetName));
                    ManagerName = Require(managerName, nameof(managerName));
                    TemplateKey = Require(templateKey, nameof(templateKey));
                    MinHeaderRowCount = minHeaderRowCount > 0
                        ? minHeaderRowCount
                        : throw new ArgumentOutOfRangeException(nameof(minHeaderRowCount));
                }

                private static string Require(string value, string parameterName)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        throw new ArgumentException("Luban export profile values cannot be empty.", parameterName);
                    }

                    return value;
                }
            }

            /// <summary>
            /// Nova 内置 Luban 导出目标目录。target、manager 与模板键只在此处定义。
            /// </summary>
            internal static class LubanExportProfiles
            {
                internal static readonly LubanExportProfile Sound = Create("sound", "SoundTables");
                internal static readonly LubanExportProfile UI = Create("ui", "UITables");
                internal static readonly LubanExportProfile NetworkCmd = Create("network-cmd", "NetworkTables");
                internal static readonly LubanExportProfile NetworkHostKey = Create("network-hostkey", "HostKeyTables");
                internal static readonly LubanExportProfile LocalizationText = Create("localization-text", "LocalizationTextTables");
                internal static readonly LubanExportProfile LocalizationFont = Create("localization-font", "LocalizationFontTables");
                internal static readonly LubanExportProfile VibrateEmphasis = Create("vibrate-emphasis", "VibrateEmphasisTables");
                internal static readonly LubanExportProfile VibrateCustom = Create("vibrate-custom", "VibrateCustomTables");

                internal static IReadOnlyList<LubanExportProfile> All { get; } = new[]
                {
                    Sound,
                    UI,
                    NetworkCmd,
                    NetworkHostKey,
                    LocalizationText,
                    LocalizationFont,
                    VibrateEmphasis,
                    VibrateCustom,
                };

                private static LubanExportProfile Create(string targetName, string managerName, int minHeaderRowCount = 5)
                {
                    return new LubanExportProfile(targetName, targetName, managerName, targetName, minHeaderRowCount);
                }
            }
        }
    }
}
