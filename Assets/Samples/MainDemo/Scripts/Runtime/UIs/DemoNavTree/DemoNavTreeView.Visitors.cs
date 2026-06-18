/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoNavTreeView.Visitors.cs
 * author:    taoye
 * created:   2026/05/22
 * descrip:   演示树导航主 View — 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Samples.Runtime
{
    /// <summary>
    /// 演示树导航主 View，继承框架 UIView。
    /// </summary>
    public sealed partial class DemoNavTreeView : UIView
    {
        /// <summary>
        /// 滚动控件，绑定 DemoNavTreeView.prefab 上的 ScrollRect 组件。
        /// </summary>

        [SerializeField] private ScrollRect m_ScrollRect;

        /// <summary>
        /// ScrollRect.content 节点（VerticalLayoutGroup 宿主），节点项实例化到此节点下。
        /// </summary>

        [SerializeField] private RectTransform m_Content;

        /// <summary>
        /// 单节点预制体引用，用于 Instantiate 各节点行。
        /// </summary>

        [SerializeField] private DemoNavTreeNodeView m_NodeItemPrefab;

        /// <summary>
        /// 应用 SafeArea 的根 RectTransform，运行时由 ApplySafeArea 修改 offset。
        /// </summary>

        [SerializeField] private RectTransform m_RootSafeArea;

        /// <summary>
        /// ScrollView 的背景 Image，运行时按 HSV 色环匀速循环修改其颜色。
        /// </summary>

        [SerializeField] private Image m_BackgroundImage;

        /// <summary>
        /// 背景 Hue 循环一周所用秒数；越大越缓，Inspector 可调，默认 8 秒。
        /// </summary>

        [SerializeField] private float m_BackgroundHueCycleSeconds = 8f;

        /// <summary>
        /// 背景颜色饱和度（HSV 色环"小箭头"水平位置），固定不随时间变化，Inspector 可调。
        /// </summary>

        [SerializeField, Range(0f, 1f)] private float m_BackgroundSaturation = 0.5f;

        /// <summary>
        /// 背景颜色明度（HSV 色环"小箭头"垂直位置），固定不随时间变化，Inspector 可调。
        /// </summary>

        [SerializeField, Range(0f, 1f)] private float m_BackgroundValue = 0.25f;

        /// <summary>
        /// 背景颜色透明度，与原始 prefab 0.95 对齐，Inspector 可调。
        /// </summary>

        [SerializeField, Range(0f, 1f)] private float m_BackgroundAlpha = 0.95f;

        /// <summary>
        /// 已实例化的节点视图缓存列表，用于 RefreshVisibility 全量刷新。
        /// </summary>
        private readonly List<DemoNavTreeNodeView> m_NodeViews = new List<DemoNavTreeNodeView>();

        /// <summary>
        /// 背景 Hue 当前累计相位（0~1 循环），由 OnUpdate 累加。
        /// </summary>
        private float m_BackgroundHuePhase;
    }
}
