/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxAdGroupConfigValidator.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   MAX 广告组配置与跨格式广告 ID 归属校验
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using NovaFramework.Runtime;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.MaxAdPlugin.Runtime
{
    /// <summary>
    /// 校验 MAX 完整渠道的组语法及广告 ID 唯一性；错误只使涉及的格式计划无效。
    /// </summary>
    public static class MaxAdGroupConfigValidator
    {
        /// <summary>
        /// 固定配置顺序，用于稳定报告重复 ID 的格式归属。
        /// </summary>
        private static readonly AdFormat[] s_Formats = { AdFormat.Rewarded, AdFormat.Interstitial, AdFormat.AppOpen, AdFormat.Banner };

        /// <summary>
        /// 解析三个全屏原始组列表及 Banner 真实 ID，并对跨格式重复同时标错双方。
        /// 先完成所有解析再检测冲突，避免先前错误掩盖其他格式的回调归属歧义。
        /// </summary>
        /// <param name="config">MAX 渠道配置；null 返回每个格式均无效的诊断计划。</param>
        /// <returns>格式到独立不可变计划的只读字典；合法空配置保持有效但没有可注册组。</returns>
        public static IReadOnlyDictionary<AdFormat, AdGroupPlanValidationResult> Validate(MaxAdChannelConfig config)
        {
            var plans = new Dictionary<AdFormat, AdGroupPlanValidationResult>();
            var additionalErrors = new Dictionary<AdFormat, List<string>>();
            foreach (AdFormat format in s_Formats)
            {
                additionalErrors.Add(format, new List<string>());
                if (config == null)
                {
                    plans.Add(format, new AdGroupPlanValidationResult(null, null, new[] { "MAX 渠道配置为空，无法注册广告格式。" }, null));
                    continue;
                }
                plans.Add(format, AdGroupConfigParser.Parse(format, GetExpressions(config, format)));
            }

            var owners = new Dictionary<string, List<AdFormat>>(StringComparer.Ordinal);
            foreach (AdFormat format in s_Formats)
            {
                // 无效格式中仍可能存在合法可识别 ID，不能因已有错误漏掉跨格式冲突。
                var seenInFormat = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in plans[format].EffectivePlacementIds)
                {
                    if (!seenInFormat.Add(id)) continue;
                    if (!owners.TryGetValue(id, out List<AdFormat> previousFormats))
                    {
                        owners.Add(id, new List<AdFormat> { format });
                        continue;
                    }
                    foreach (AdFormat previousFormat in previousFormats)
                    {
                        string message = $"MAX 广告 ID '{id}' 同时配置于 {previousFormat} 和 {format}，原厂回调归属存在歧义；两个格式均不注册。";
                        additionalErrors[previousFormat].Add(message);
                        additionalErrors[format].Add(message);
                    }
                    previousFormats.Add(format);
                }
            }

            foreach (AdFormat format in s_Formats)
            {
                if (additionalErrors[format].Count == 0) continue;
                AdGroupPlanValidationResult original = plans[format];
                var errors = new List<string>(original.Errors);
                errors.AddRange(additionalErrors[format]);
                plans[format] = new AdGroupPlanValidationResult(original.Groups, original.EffectivePlacementIds, errors, original.Warnings);
            }
            return new ReadOnlyDictionary<AdFormat, AdGroupPlanValidationResult>(plans);
        }

        /// <summary>
        /// 按格式读取原始组表达式；Banner 保留原始单 ID 列表供严格语法检查。
        /// </summary>
        private static IReadOnlyList<string> GetExpressions(MaxAdChannelConfig config, AdFormat format)
        {
            switch (format)
            {
                case AdFormat.Rewarded: return config.RVPlacementGroups;
                case AdFormat.Interstitial: return config.InterPlacementGroups;
                case AdFormat.AppOpen: return config.AppOpenPlacementGroups;
                case AdFormat.Banner: return config.BannerPlacementIds;
                default: throw new ArgumentOutOfRangeException(nameof(format), format, "MAX 不支持此配置格式。");
            }
        }
    }
}
