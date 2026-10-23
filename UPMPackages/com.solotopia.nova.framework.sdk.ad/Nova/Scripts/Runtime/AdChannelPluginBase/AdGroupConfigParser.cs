/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdGroupConfigParser.cs
 * author:    Codex
 * created:   2026/10/13
 * descrip:   广告组表达式的共享纯解析与不可变有效计划
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告组解析结果；拓扑、真实 ID 和诊断均为独立深只读快照。
    /// 无效结果保留已识别拓扑以便报告冲突，消费者必须先检查 IsValid。
    /// </summary>
    public sealed class AdGroupPlanValidationResult
    {
        /// <summary>
        /// 按配置顺序保存的广告组；组内按尝试顺序保存真实 ID。
        /// </summary>
        public IReadOnlyList<IReadOnlyList<string>> Groups { get; }

        /// <summary>
        /// 按组及组内顺序展开的真实 ID；不会隐式去重。
        /// </summary>
        public IReadOnlyList<string> EffectivePlacementIds { get; }

        /// <summary>
        /// 会阻止当前格式注册的配置错误。
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// 不阻止当前格式注册的配置诊断。
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// 没有配置错误时为 true；未配置格式也属于合法空计划。
        /// </summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>
        /// 从解析或渠道校验结果创建深只读快照，避免配置编辑或调用方写入改变运行计划。
        /// </summary>
        /// <param name="groups">有序真实 ID 组。</param>
        /// <param name="effectivePlacementIds">有序真实 ID 展开结果。</param>
        /// <param name="errors">阻止当前格式注册的错误。</param>
        /// <param name="warnings">不阻止注册的警告。</param>
        public AdGroupPlanValidationResult(IReadOnlyList<IReadOnlyList<string>> groups, IReadOnlyList<string> effectivePlacementIds, IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            var groupSnapshot = new List<IReadOnlyList<string>>();
            if (groups != null)
            {
                for (int i = 0; i < groups.Count; i++)
                {
                    groupSnapshot.Add(CopyStrings(groups[i]));
                }
            }
            Groups = groupSnapshot.AsReadOnly();
            EffectivePlacementIds = CopyStrings(effectivePlacementIds);
            Errors = CopyStrings(errors);
            Warnings = CopyStrings(warnings);
        }

        /// <summary>
        /// 复制字符串列表并包装为不可写视图；空输入返回同样不可写的空列表。
        /// </summary>
        private static IReadOnlyList<string> CopyStrings(IReadOnlyList<string> source)
        {
            var snapshot = new List<string>(source == null ? 0 : source.Count);
            if (source != null)
            {
                for (int i = 0; i < source.Count; i++) snapshot.Add(source[i]);
            }
            return snapshot.AsReadOnly();
        }
    }

    /// <summary>
    /// 共享广告组纯解析器；列表项是组边界，逗号与竖线是同义组内分隔符。
    /// 不依赖 SDK，也不缓存可编辑源配置。
    /// </summary>
    public static class AdGroupConfigParser
    {
        /// <summary>
        /// 组内唯一允许的两个英文分隔符。
        /// </summary>
        private static readonly char[] s_GroupSeparators = { ',', '|' };

        /// <summary>
        /// 解析有序广告组表达式，Trim 并过滤空项；重复和不支持语法报错，不猜测或去重。
        /// Banner 仅接受每项一个真实 ID，不允许任何组内分隔符。
        /// </summary>
        /// <param name="format">需要注册的广告格式；Banner 使用单 ID 校验。</param>
        /// <param name="groupExpressions">每项一组的源列表；null 或空列表表示格式未配置。</param>
        /// <returns>与源列表隔离的深只读结果；存在错误时消费者不得注册其 ID。</returns>
        public static AdGroupPlanValidationResult Parse(AdFormat format, IReadOnlyList<string> groupExpressions)
        {
            var groups = new List<IReadOnlyList<string>>();
            var ids = new List<string>();
            var errors = new List<string>();
            var warnings = new List<string>();
            var firstOccurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            if (groupExpressions == null)
            {
                return new AdGroupPlanValidationResult(groups, ids, errors, warnings);
            }

            for (int sourceIndex = 0; sourceIndex < groupExpressions.Count; sourceIndex++)
            {
                string expression = groupExpressions[sourceIndex]?.Trim();
                if (string.IsNullOrEmpty(expression)) continue;
                if (HasUnsupportedSyntax(expression))
                {
                    errors.Add($"{format} 第 {sourceIndex + 1} 项使用不支持的语法；仅支持英文逗号或竖线分隔组内 ID，不支持分号、中文逗号或 JSON。");
                    continue;
                }
                if (format == AdFormat.Banner && expression.IndexOfAny(s_GroupSeparators) >= 0)
                {
                    errors.Add($"Banner 第 {sourceIndex + 1} 项必须填写单个真实广告 ID，不能使用广告组表达式。");
                    continue;
                }

                var group = new List<string>();
                string[] tokens = expression.Split(s_GroupSeparators, StringSplitOptions.None);
                for (int itemIndex = 0; itemIndex < tokens.Length; itemIndex++)
                {
                    string id = tokens[itemIndex].Trim();
                    if (id.Length == 0) continue;
                    if (firstOccurrences.TryGetValue(id, out int firstSourceIndex))
                    {
                        errors.Add($"{format} 第 {sourceIndex + 1} 项的广告 ID '{id}' 重复；首次出现在第 {firstSourceIndex + 1} 项，回调归属存在歧义。");
                    }
                    else
                    {
                        firstOccurrences.Add(id, sourceIndex);
                    }
                    group.Add(id);
                    ids.Add(id);
                }
                if (group.Count > 0) groups.Add(group);
            }
            return new AdGroupPlanValidationResult(groups, ids, errors, warnings);
        }

        /// <summary>
        /// 拒绝不支持的分号、中文分隔符及 JSON 结构/字符串标记，避免把表达式误作原厂 ID。
        /// </summary>
        private static bool HasUnsupportedSyntax(string expression)
        {
            for (int i = 0; i < expression.Length; i++)
            {
                char character = expression[i];
                if (character == ';' || character == '；' || character == '，' || character == '、' ||
                    character == '[' || character == ']' || character == '{' || character == '}' || character == '"')
                {
                    return true;
                }
            }
            return false;
        }
    }
}
