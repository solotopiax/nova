/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdChannelConfigListDrawer.cs
 * author:    yingzheng
 * created:   2026/5/14
 * descrip:   AdChannelConfigList 的自定义 PropertyDrawer，
 *            继承 SerializeReferenceListDrawer 泛型基类，
 *            仅实现全局字段绘制与 4 个抽象 hook。
 ***************************************************************/

using System;
using UnityEditor;
using UnityEngine;
using NovaFramework.Editor;
using NovaFramework.SDK.AdPlugin.Runtime;

namespace NovaFramework.SDK.AdPlugin.Editor
{
    /// <summary>
    /// AdChannelConfigList 的自定义 PropertyDrawer。
    /// 继承 SerializeReferenceListDrawer 泛型基类，仅重写全局字段绘制（5 个全局配置字段）
    /// 以及 HeaderTitle / GetEntryLabel / FilterAddableType / EmptyAddMenuLabel 4 个抽象 hook。
    /// </summary>
    [CustomPropertyDrawer(typeof(AdChannelConfigList))]
    public sealed class AdChannelConfigListDrawer : SerializeReferenceListDrawer<IAdChannelConfig, AdChannelConfigList>
    {
        /// <summary>
        /// 全局字段区与渠道列表之间的间距。
        /// </summary>
        private const float c_GlobalSectionSpacing = 6f;

        /// <summary>
        /// BannerIlrdInterval 字段 IntSlider 控件固定宽度。
        /// 注意：仅用于 IntSlider 控件本身宽度，不用于 HelpBox 宽度计算。
        /// </summary>
        private const float c_BannerSliderWidth = 300f;

        /// <summary>
        /// 全局字段绘制左侧缩进量。
        /// 与基类 SerializeReferenceListDrawer 的 c_FieldIndent = 12f 约定对齐。
        /// </summary>
        private const float c_FieldIndent = 12f;

        /// <inheritdoc/>
        protected override string HeaderTitle => "渠道列表";

        /// <inheritdoc/>
        protected override string EmptyAddMenuLabel => "无可用渠道配置实现";

        /// <inheritdoc/>
        protected override string GetEntryLabel(IAdChannelConfig item, int index) => item.Channel.ToString();

        /// <inheritdoc/>
        protected override bool FilterAddableType(Type type)
        {
            return !type.IsAbstract && !type.IsInterface;
        }

        /// <inheritdoc/>
        protected override float DrawGlobalFields(Rect position, float startY, SerializedProperty property, AdChannelConfigList wrapper)
        {
            float lineH = EditorGUIUtility.singleLineHeight;
            float lineStep = lineH + 2f;
            float y = startY;

            y = DrawFieldWithHelp(position, y, lineH, lineStep, property, "m_EnableBidding", "竞价模式");
            y = DrawIntSliderWithHelp(position, y, lineH, lineStep, property, "m_BannerIlrdInterval", "Banner上报ILRD间隔");
            y = DrawFieldWithHelp(position, y, lineH, lineStep, property, "m_MuteAd", "广告静音");
            y = DrawFieldWithHelp(position, y, lineH, lineStep, property, "m_RetryLoadAdMaxNum", "加载重试次数");
            y = DrawFieldWithHelp(position, y, lineH, lineStep, property, "m_RetryLoadAdInterv", "重试加载间隔(秒)");

            y += c_GlobalSectionSpacing;
            return y - startY;
        }

        /// <inheritdoc/>
        protected override float GetGlobalFieldsHeight(SerializedProperty property, AdChannelConfigList wrapper)
        {
            float lineStep = EditorGUIUtility.singleLineHeight + 2f;
            float h = lineStep * 5f + c_GlobalSectionSpacing;
            // 使用 EditorGUIUtility.currentViewWidth - c_FieldIndent 近似可用宽度，
            // 与基类 GetEntryHeight 同款策略对齐，避免 HelpBox 高度估算偏差。
            float availableWidth = EditorGUIUtility.currentViewWidth - c_FieldIndent;
            h += CalcGlobalTooltipHeight("m_EnableBidding", availableWidth);
            h += CalcGlobalTooltipHeight("m_BannerIlrdInterval", availableWidth);
            h += CalcGlobalTooltipHeight("m_MuteAd", availableWidth);
            h += CalcGlobalTooltipHeight("m_RetryLoadAdMaxNum", availableWidth);
            h += CalcGlobalTooltipHeight("m_RetryLoadAdInterv", availableWidth);
            return h;
        }

        /// <summary>
        /// 绘制单个普通全局字段（PropertyField）及其 Tooltip HelpBox。
        /// </summary>
        /// <param name="position">Inspector 完整矩形。</param>
        /// <param name="y">当前绘制 y 坐标。</param>
        /// <param name="lineH">单行高度。</param>
        /// <param name="lineStep">单行步进（含底部间距）。</param>
        /// <param name="property">父级 SerializedProperty。</param>
        /// <param name="fieldName">字段名（含 m_ 前缀）。</param>
        /// <param name="label">显示标签文本。</param>
        /// <returns>绘制完成后的新 y 值。</returns>
        private static float DrawFieldWithHelp(Rect position, float y, float lineH, float lineStep, SerializedProperty property, string fieldName, string label)
        {
            SerializedProperty prop = property.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return y;
            }
            EditorUtil.Draw.PropertyField(new Rect(position.x, y, position.width, lineH), prop, label);
            y += lineStep;
            string tooltip = EditorUtil.Reflect.GetFieldTooltip(typeof(AdChannelConfigList), fieldName);
            if (!string.IsNullOrEmpty(tooltip))
            {
                // HelpBox 宽度铺满可用宽度（起点 position.x + c_FieldIndent，宽度 = position.width - c_FieldIndent）
                float helpBoxWidth = position.width - c_FieldIndent;
                float helpH = EditorUtil.Draw.CalcHelpBoxHeight(MessageType.Info, tooltip, helpBoxWidth);
                EditorUtil.Draw.HelpBox(new Rect(position.x + c_FieldIndent, y, helpBoxWidth, helpH), MessageType.Info, tooltip);
                y += helpH + 4f;
            }
            return y;
        }

        /// <summary>
        /// 绘制 BannerIlrdInterval 整数滑动条字段及其 Tooltip HelpBox。
        /// </summary>
        /// <param name="position">Inspector 完整矩形。</param>
        /// <param name="y">当前绘制 y 坐标。</param>
        /// <param name="lineH">单行高度。</param>
        /// <param name="lineStep">单行步进（含底部间距）。</param>
        /// <param name="property">父级 SerializedProperty。</param>
        /// <param name="fieldName">字段名（含 m_ 前缀）。</param>
        /// <param name="label">显示标签文本。</param>
        /// <returns>绘制完成后的新 y 值。</returns>
        private static float DrawIntSliderWithHelp(Rect position, float y, float lineH, float lineStep, SerializedProperty property, string fieldName, string label)
        {
            SerializedProperty prop = property.FindPropertyRelative(fieldName);
            if (prop == null)
            {
                return y;
            }
            // IntSlider 控件本身宽度保留 c_BannerSliderWidth（300f），与 HelpBox 宽度无关
            EditorUtil.Draw.IntSlider(new Rect(position.x, y, c_BannerSliderWidth, lineH), label, prop, 0, 10);
            y += lineStep;
            string tooltip = EditorUtil.Reflect.GetFieldTooltip(typeof(AdChannelConfigList), fieldName);
            if (!string.IsNullOrEmpty(tooltip))
            {
                // HelpBox 宽度铺满可用宽度（起点 position.x + c_FieldIndent，宽度 = position.width - c_FieldIndent）
                float helpBoxWidth = position.width - c_FieldIndent;
                float helpH = EditorUtil.Draw.CalcHelpBoxHeight(MessageType.Info, tooltip, helpBoxWidth);
                EditorUtil.Draw.HelpBox(new Rect(position.x + c_FieldIndent, y, helpBoxWidth, helpH), MessageType.Info, tooltip);
                y += helpH + 4f;
            }
            return y;
        }

        /// <summary>
        /// 计算单个全局字段的 Tooltip HelpBox 高度（含底部 4f 间距）。
        /// 字段无 TooltipAttribute 时返回 0。
        /// </summary>
        /// <param name="fieldName">字段名（含 m_ 前缀）。</param>
        /// <param name="availableWidth">可用宽度（像素）。</param>
        /// <returns>HelpBox 高度（含 4f 间距），或 0。</returns>
        private static float CalcGlobalTooltipHeight(string fieldName, float availableWidth)
        {
            string tooltip = EditorUtil.Reflect.GetFieldTooltip(typeof(AdChannelConfigList), fieldName);
            if (string.IsNullOrEmpty(tooltip))
            {
                return 0f;
            }
            return EditorUtil.Draw.CalcHelpBoxHeight(MessageType.Info, tooltip, availableWidth) + 4f;
        }
    }
}
