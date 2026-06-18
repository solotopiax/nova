/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TagCategoryRowItem.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// 左侧分类行组件，封装选中指示条、名称文字、数量文字与点击回调。
    /// </summary>
    public class TagCategoryRowItem : DebugMonoBehaviourEx
    {
        /// <summary>
        /// 行背景 Image，用于 Button 颜色过渡目标。
        /// </summary>
        [RequiredField] public Image Background;

        /// <summary>
        /// 左侧选中指示条，选中时显示蓝色，未选中时隐藏。
        /// </summary>
        [RequiredField] public Image SelectionIndicator;

        /// <summary>
        /// 分类名称文字。
        /// </summary>
        [RequiredField] public Text NameText;

        /// <summary>
        /// 该分类下的标签总数文字。
        /// </summary>
        [RequiredField] public Text CountText;

        /// <summary>
        /// 选中状态指示条颜色。
        /// </summary>
        private static readonly Color s_IndicatorColor = new Color(0.14f, 0.44f, 0.84f, 1f);

        /// <summary>
        /// 选中时背景色。
        /// </summary>
        private static readonly Color s_SelectedBg = new Color(0.20f, 0.20f, 0.22f, 1f);

        /// <summary>
        /// 未选中时背景色（透明）。
        /// </summary>
        private static readonly Color s_NormalBg = new Color(0f, 0f, 0f, 0f);

        /// <summary>
        /// 按分类名称、标签数量、选中状态和点击回调初始化本行。
        /// </summary>
        /// <param name="categoryName">分类显示名称。</param>
        /// <param name="count">该分类下的标签总数。</param>
        /// <param name="selected">初始是否选中。</param>
        /// <param name="onClicked">行被点击时的回调。</param>
        public void Setup(string categoryName, int count, bool selected, Action onClicked)
        {
            NameText.text = categoryName;
            CountText.text = count.ToString();
            SetSelected(selected);
            var btn = GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => onClicked?.Invoke());
        }

        /// <summary>
        /// 切换选中状态：更新背景色、指示条颜色、名称文字颜色。
        /// </summary>
        /// <param name="selected">是否选中。</param>
        public void SetSelected(bool selected)
        {
            Background.color = selected ? s_SelectedBg : s_NormalBg;
            SelectionIndicator.color = selected ? s_IndicatorColor : new Color(0f, 0f, 0f, 0f);
            NameText.color = selected ? Color.white : new Color(0.60f, 0.60f, 0.62f, 1f);
        }
    }
}
