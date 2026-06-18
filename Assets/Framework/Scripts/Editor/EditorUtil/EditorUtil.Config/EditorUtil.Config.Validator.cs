/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.Validator.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   CommonConfig / PluginConfig 必填字段校验；返回问题列表供 ConfigWindow 弹窗展示
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// CommonConfig / PluginConfig 必填字段校验；返回问题列表供 ConfigWindow 弹窗展示。
            /// </summary>
            public static class Validator
            {
                /// <summary>
                /// 校验问题的严重度级别。
                /// <para>Warning 表示建议修正但不阻断流程；Error 表示必须修正否则配置不可用。</para>
                /// </summary>
                public enum Severity
                {
                    /// <summary>
                    /// 建议修正；不影响当前流程继续执行。
                    /// </summary>
                    Warning,

                    /// <summary>
                    /// 必须修正；配置存在致命缺陷。
                    /// </summary>
                    Error,
                }

                /// <summary>
                /// 一条校验问题；包含字段路径、人读消息与严重度。
                /// <para>使用 readonly struct 保证不可变性，可安全放入列表传递。</para>
                /// </summary>
                public readonly struct ValidationIssue
                {
                    /// <summary>
                    /// 问题所在字段的路径，格式如 "Common.AppID" 或 "SDKConfigs[0]"。
                    /// </summary>
                    public readonly string Path;

                    /// <summary>
                    /// 面向用户的问题描述文本。
                    /// </summary>
                    public readonly string Message;

                    /// <summary>
                    /// 问题严重度；决定 ConfigWindow 以何种样式呈现该条目。
                    /// </summary>
                    public readonly Severity Level;

                    /// <summary>
                    /// 构造一条校验问题。
                    /// </summary>
                    /// <param name="path">问题所在字段路径。</param>
                    /// <param name="message">面向用户的描述文本。</param>
                    /// <param name="level">问题严重度。</param>
                    public ValidationIssue(string path, string message, Severity level)
                    {
                        Path = path;
                        Message = message;
                        Level = level;
                    }
                }

                /// <summary>
                /// 对指定 Platform×Channel×DevelopMode 组合执行全量校验，返回所有发现的问题列表。
                /// <para>校验范围：ConfigMasterSO 空值检查、CommonConfig 必填字段、目标矩阵行存在性及 SDKConfigs / KitConfigs 空引用。</para>
                /// </summary>
                /// <param name="master">待校验的 ConfigMasterSO 实例；传入 null 时直接返回含根 Error 的列表。</param>
                /// <param name="platform">目标平台。</param>
                /// <param name="channel">目标渠道。</param>
                /// <param name="mode">目标开发模式（Debug / Release）。</param>
                /// <returns>发现的问题列表；无问题时返回空列表。</returns>
                public static IReadOnlyList<ValidationIssue> Validate(ConfigMasterSO master, PlatformType platform, ChannelType channel, DevelopMode mode)
                {
                    List<ValidationIssue> issues = new();

                    if (master == null)
                    {
                        issues.Add(new ValidationIssue("<root>", "ConfigMaster 为空。", Severity.Error));
                        return issues;
                    }

                    // 顶层维度化校验路径：经 DimensionalResolver 取当前坐标生效值，避免全不勾/勾选两态校验错位
                    RequireNotEmpty(issues, "Namespace", DimensionalResolver.ResolveNamespace(master, platform, channel, mode));
                    ValidateCommon(master.GetCommon(platform, channel, mode), issues);

                    if (master.TryGetEntry(platform, channel, out var entry))
                    {
                        List<ISDKPluginConfig> sdkConfigs = entry.GetSDKConfigs(mode);
                        for (int i = 0; i < sdkConfigs.Count; i++)
                        {
                            if (sdkConfigs[i] == null)
                            {
                                issues.Add(new ValidationIssue(
                                    $"SDKConfigs[{i}]", "SDK 配置项为 null；请在弹框中选择 保留/清理。", Severity.Error));
                            }
                        }

                        List<IKitConfig> kitConfigs = entry.GetKitConfigs(mode);
                        for (int i = 0; i < kitConfigs.Count; i++)
                        {
                            if (kitConfigs[i] == null)
                            {
                                issues.Add(new ValidationIssue(
                                    $"KitConfigs[{i}]", "Kit 配置项为 null；请在弹框中选择 保留/清理。", Severity.Error));
                            }
                        }
                    }
                    else
                    {
                        issues.Add(new ValidationIssue(
                            $"Entries[{platform}/{channel}]", "未找到对应 Platform×Channel 行；请在结构巡检中补齐。", Severity.Error));
                    }

                    return issues;
                }

                /// <summary>
                /// 校验 CommonConfig 全部必填字段；问题追加至 issues 列表。
                /// </summary>
                /// <param name="common">待校验的 CommonConfig 实例；为 null 时追加 Error 后直接返回。</param>
                /// <param name="issues">问题收集列表；校验发现的所有条目追加至此。</param>
                private static void ValidateCommon(CommonConfig common, List<ValidationIssue> issues)
                {
                    if (common == null)
                    {
                        issues.Add(new ValidationIssue("Common", "CommonConfig 为 null。", Severity.Error));
                        return;
                    }

                    RequireNotEmpty(issues, "Common.AppID", common.AppID);
                    RequireNotEmpty(issues, "Common.AppAesKey", common.AppAesKey);
                    RequireNotEmpty(issues, "Common.AppAesIV", common.AppAesIV);
                }

                /// <summary>
                /// 若 value 为 null 或空字符串，向 issues 追加一条路径为 path 的必填 Error。
                /// </summary>
                /// <param name="issues">问题收集列表。</param>
                /// <param name="path">字段路径，用于 ValidationIssue.Path。</param>
                /// <param name="value">待检测的字段值。</param>
                private static void RequireNotEmpty(List<ValidationIssue> issues, string path, string value)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        issues.Add(new ValidationIssue(path, "必填字段为空。", Severity.Error));
                    }
                }
            }
        }
    }
}
