/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TagDetailRowItem.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine.Events;
    using UnityEngine.UI;

    /// <summary>
    /// 右侧标签详情行组件，封装 Toggle、Tag 名称、描述文字、颜色点与数量显示。
    /// </summary>
    public class TagDetailRowItem : DebugMonoBehaviourEx
    {
        /// <summary>
        /// 行 Toggle，由 TagFilterPanelControl 保存到 TagNode.Toggle。
        /// </summary>
        [RequiredField] public Toggle Toggle;

        /// <summary>
        /// Tag 标识文字，如 "[Localization]"。
        /// </summary>
        [RequiredField] public Text TagLabel;

        /// <summary>
        /// 描述文字。
        /// </summary>
        [RequiredField] public Text DescLabel;

        /// <summary>
        /// 颜色点底圆背景 Image。
        /// </summary>
        [RequiredField] public Image DotBackground;

        /// <summary>
        /// 颜色点前景 Image，颜色由外部按 Tag 分类设置。
        /// </summary>
        [RequiredField] public Image DotForeground;

        /// <summary>
        /// 日志条数文字。
        /// </summary>
        [RequiredField] public Text CountLabel;

        /// <summary>
        /// 按标签名称、描述、初始 Toggle 状态和回调初始化本行。
        /// </summary>
        /// <param name="tagName">Tag 标识字符串（含方括号）。</param>
        /// <param name="description">Tag 描述文字。</param>
        /// <param name="isOn">Toggle 初始状态。</param>
        /// <param name="onToggleChanged">Toggle 值变更时的回调。</param>
        public void Setup(string tagName, string description, bool isOn, UnityAction<bool> onToggleChanged)
        {
            TagLabel.text = tagName;
            DescLabel.text = description;
            Toggle.isOn = isOn;
            Toggle.onValueChanged.AddListener(onToggleChanged);
        }

        /// <summary>
        /// 点击行背景时触发，切换 Toggle 状态。
        /// </summary>
        public void OnRowClick()
        {
            Toggle.isOn = !Toggle.isOn;
        }
    }
}
