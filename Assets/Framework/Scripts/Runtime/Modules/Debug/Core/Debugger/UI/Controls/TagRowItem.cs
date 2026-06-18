/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TagRowItem.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.UI;

    /// <summary>
    /// TagLeafRow 行控制脚本，封装 Toggle 初始化、标签名、描述文字、圆点与数量显示。
    /// </summary>
    public class TagRowItem : DebugMonoBehaviourEx
    {
        /// <summary>
        /// 行 Toggle 组件，外部通过此引用保存到 TagNode.Toggle。
        /// </summary>
        [RequiredField] public Toggle Toggle;

        /// <summary>
        /// 标签名文字组件，显示完整标签字符串（如 "[SDK][AF]"）。
        /// </summary>
        [RequiredField] public Text TagLabel;

        /// <summary>
        /// 描述文字组件，弹性区域，灰色显示。
        /// </summary>
        [RequiredField] public Text DescLabel;

        /// <summary>
        /// 蓝色实心圆点图片组件。
        /// </summary>
        [RequiredField] public Image DotImage;

        /// <summary>
        /// 数量底图组件，深色圆角背景。
        /// </summary>
        [RequiredField] public Image CountBackground;

        /// <summary>
        /// 数量文字组件，叠加在 CountBackground 上方。
        /// </summary>
        [RequiredField] public Text CountLabel;

        /// <summary>
        /// 初始化本行 UI，绑定标签名、描述、数量、Toggle 状态及回调。
        /// </summary>
        /// <param name="tagLabel">完整标签字符串（如 "[SDK][AF]"）。</param>
        /// <param name="description">描述文字，可为空字符串。</param>
        /// <param name="count">日志数量，0 表示未知。</param>
        /// <param name="isOn">Toggle 初始勾选状态。</param>
        /// <param name="onToggleChanged">Toggle 值变更时的回调。</param>
        public void Setup(string tagLabel, string description, int count, bool isOn, UnityAction<bool> onToggleChanged)
        {
            TagLabel.text = tagLabel;
            DescLabel.text = description;
            Toggle.isOn = isOn;
            Toggle.onValueChanged.AddListener(onToggleChanged);
            UpdateCount(count);
        }

        /// <summary>
        /// 更新数量文字显示，count 小于等于 0 时清空，大于 0 时显示数字。
        /// </summary>
        /// <param name="count">最新日志数量。</param>
        public void UpdateCount(int count)
        {
            CountLabel.text = count >= 0 ? count.ToString() : "0";
        }

        /// <summary>
        /// 行点击事件，翻转 Toggle 状态。
        /// </summary>
        public void OnRowClick()
        {
            Toggle.isOn = !Toggle.isOn;
        }
    }
}
