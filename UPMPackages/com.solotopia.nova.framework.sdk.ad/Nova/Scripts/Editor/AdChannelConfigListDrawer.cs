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
    /// 渠道配置列表的自定义绘制，按动态高度显示全局开关、组重试序列、国家码超时与渠道项。
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
        /// AdPluginConfig 中渠道配置字段名。
        /// </summary>
        private const string c_ChannelConfigsFieldName = "m_ChannelConfigs";

        /// <summary>
        /// AdPluginConfig 中国家码等待超时字段名。
        /// 该字段序列化在 AdPluginConfig 上，但为保持面板阅读顺序，在渠道全局配置区绘制。
        /// </summary>
        private const string c_CountryCodeWaitTimeoutFieldName = "m_CountryCodeWaitTimeoutSeconds";

        /// <inheritdoc/>
        protected override string HeaderTitle => "渠道列表";

        /// <inheritdoc/>
        protected override string EmptyAddMenuLabel => "无可用渠道配置实现";

        /// <summary>
        /// 以广告渠道名称标识列表项，保持与现有渠道类型一致。
        /// </summary>
        protected override string GetEntryLabel(IAdChannelConfig item, int index) => item.Channel.ToString();

        /// <summary>
        /// 只允许向列表添加可实例化的渠道配置类型。
        /// </summary>
        protected override bool FilterAddableType(Type type)
        {
            return !type.IsAbstract && !type.IsInterface;
        }

        /// <summary>
        /// 按声明顺序绘制全局开关、组重试等待序列及国家码等待超时。
        /// </summary>
        protected override float DrawGlobalFields(Rect position, float startY, SerializedProperty property, AdChannelConfigList wrapper)
        {
            float lineH = EditorGUIUtility.singleLineHeight;
            float lineStep = lineH + 2f;
            float y = startY;

            y = DrawFieldWithHelp(position, y, lineH, lineStep, property, "m_EnableBidding", "竞价模式");
            y = DrawIntSliderWithHelp(position, y, lineH, lineStep, property, "m_BannerIlrdInterval", "Banner上报ILRD间隔");
            y = DrawFieldWithHelp(position, y, lineH, lineStep, property, "m_MuteAd", "广告静音");
            y = DrawFieldWithHelp(position, y, lineH, lineStep, property, "m_GroupRetryIntervalsSeconds", "广告组重试间隔（秒）");
            SerializedProperty countryCodeWaitTimeout = FindCountryCodeWaitTimeoutProperty(property);
            y = DrawFieldWithHelp(
                position,
                y,
                lineH,
                lineStep,
                countryCodeWaitTimeout,
                typeof(AdPluginConfig),
                c_CountryCodeWaitTimeoutFieldName,
                "国家码等待超时(秒)");

            y += c_GlobalSectionSpacing;
            return y - startY;
        }

        /// <summary>
        /// 根据数组展开状态计算全局字段与说明高度，保证渠道列表和国家码字段不重叠。
        /// </summary>
        protected override float GetGlobalFieldsHeight(SerializedProperty property, AdChannelConfigList wrapper)
        {
            float lineStep = EditorGUIUtility.singleLineHeight + 2f;
            bool hasCountryCodeWaitTimeout = FindCountryCodeWaitTimeoutProperty(property) != null;
            SerializedProperty retryIntervals = property.FindPropertyRelative("m_GroupRetryIntervalsSeconds");
            float h = lineStep * (hasCountryCodeWaitTimeout ? 4f : 3f) + c_GlobalSectionSpacing;
            if (retryIntervals != null)
            {
                h += EditorGUI.GetPropertyHeight(retryIntervals, true) + 2f;
            }
            // 使用 EditorGUIUtility.currentViewWidth 近似可用宽度，
            // 与基类 GetEntryHeight 同款策略对齐，避免 HelpBox 高度估算偏差。
            float availableWidth = EditorGUIUtility.currentViewWidth;
            h += CalcGlobalTooltipHeight("m_EnableBidding", availableWidth);
            h += CalcGlobalTooltipHeight("m_BannerIlrdInterval", availableWidth);
            h += CalcGlobalTooltipHeight("m_MuteAd", availableWidth);
            h += CalcGlobalTooltipHeight("m_GroupRetryIntervalsSeconds", availableWidth);
            if (hasCountryCodeWaitTimeout)
            {
                h += CalcTooltipHeight(typeof(AdPluginConfig), c_CountryCodeWaitTimeoutFieldName, availableWidth);
            }
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
            return DrawFieldWithHelp(position, y, lineH, lineStep, prop, typeof(AdChannelConfigList), fieldName, label);
        }

        /// <summary>
        /// 按含子项的动态高度绘制指定属性及说明，支持重试间隔数组展开和编辑。
        /// </summary>
        /// <param name="position">Inspector 完整矩形。</param>
        /// <param name="y">当前绘制 y 坐标。</param>
        /// <param name="lineH">单行高度。</param>
        /// <param name="lineStep">单行步进（含底部间距）。</param>
        /// <param name="prop">待绘制字段属性。</param>
        /// <param name="tooltipOwnerType">Tooltip 所在类型。</param>
        /// <param name="fieldName">字段名（含 m_ 前缀）。</param>
        /// <param name="label">显示标签文本。</param>
        /// <returns>绘制完成后的新 y 值。</returns>
        private static float DrawFieldWithHelp(Rect position, float y, float lineH, float lineStep, SerializedProperty prop, Type tooltipOwnerType, string fieldName, string label)
        {
            if (prop == null)
            {
                return y;
            }
            float fieldHeight = EditorGUI.GetPropertyHeight(prop, true);
            EditorUtil.Draw.PropertyField(new Rect(position.x, y, position.width, fieldHeight), prop, label, true);
            y += fieldHeight + lineStep - lineH;
            string tooltip = EditorUtil.Reflect.GetFieldTooltip(tooltipOwnerType, fieldName);
            if (!string.IsNullOrEmpty(tooltip))
            {
                // HelpBox 与对应配置项共用相同的左边缘和可用宽度。
                float helpBoxWidth = position.width;
                float helpH = EditorUtil.Draw.CalcHelpBoxHeight(MessageType.Info, tooltip, helpBoxWidth);
                EditorUtil.Draw.HelpBox(new Rect(position.x, y, helpBoxWidth, helpH), MessageType.Info, tooltip);
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
                // HelpBox 与对应配置项共用相同的左边缘和可用宽度。
                float helpBoxWidth = position.width;
                float helpH = EditorUtil.Draw.CalcHelpBoxHeight(MessageType.Info, tooltip, helpBoxWidth);
                EditorUtil.Draw.HelpBox(new Rect(position.x, y, helpBoxWidth, helpH), MessageType.Info, tooltip);
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
            return CalcTooltipHeight(typeof(AdChannelConfigList), fieldName, availableWidth);
        }

        /// <summary>
        /// 计算指定类型字段的 Tooltip HelpBox 高度（含底部 4f 间距）。
        /// 字段没有 TooltipAttribute 时返回 0。
        /// </summary>
        /// <param name="tooltipOwnerType">Tooltip 所在类型。</param>
        /// <param name="fieldName">字段名（含 m_ 前缀）。</param>
        /// <param name="availableWidth">可用宽度（像素）。</param>
        /// <returns>HelpBox 高度（含 4f 间距），或 0。</returns>
        private static float CalcTooltipHeight(Type tooltipOwnerType, string fieldName, float availableWidth)
        {
            string tooltip = EditorUtil.Reflect.GetFieldTooltip(tooltipOwnerType, fieldName);
            if (string.IsNullOrEmpty(tooltip))
            {
                return 0f;
            }
            return EditorUtil.Draw.CalcHelpBoxHeight(MessageType.Info, tooltip, availableWidth) + 4f;
        }

        /// <summary>
        /// 从 AdPluginConfig.m_ChannelConfigs 定位同级国家码等待超时字段。
        /// </summary>
        /// <param name="channelConfigsProperty">当前渠道配置列表属性。</param>
        /// <returns>国家码等待超时属性；路径不匹配时返回 null。</returns>
        private static SerializedProperty FindCountryCodeWaitTimeoutProperty(SerializedProperty channelConfigsProperty)
        {
            if (channelConfigsProperty == null)
            {
                return null;
            }

            string path = channelConfigsProperty.propertyPath;
            if (!path.EndsWith(c_ChannelConfigsFieldName, StringComparison.Ordinal))
            {
                return null;
            }

            string siblingPath = path.Substring(0, path.Length - c_ChannelConfigsFieldName.Length)
                                 + c_CountryCodeWaitTimeoutFieldName;
            return channelConfigsProperty.serializedObject.FindProperty(siblingPath);
        }
    }
}
