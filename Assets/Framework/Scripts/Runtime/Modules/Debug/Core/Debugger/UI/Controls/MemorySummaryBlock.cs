/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemorySummaryBlock.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    public class MemorySummaryBlock : DebugMonoBehaviourEx
    {
        /// <summary>
        /// 块标题文本。
        /// </summary>
        [RequiredField] public Text Title;

        /// <summary>
        /// 驱动内存行列表的虚拟垂直布局组，通过 AddItem/ClearItems 填充数据。
        /// </summary>
        [RequiredField] public VirtualVerticalLayoutGroup VirtualLayoutGroup;

        /// <summary>
        /// 触发内存采样的按钮。
        /// </summary>
        [RequiredField] public Button SampleButton;

        /// <summary>
        /// 采样模式切换 ToggleGroup。
        /// </summary>
        [RequiredField] public ToggleGroup TypeToggleGroup;

        /// <summary>
        /// 长度必须为 11，顺序：Summary/All/Texture/Mesh/Material/Shader/AnimationClip/AudioClip/Font/TextAsset/ScriptableObject
        /// </summary>
        [RequiredField] public Toggle[] TypeToggles;

        /// <summary>
        /// 采样元信息单行文本：Sampled at {t} | Mode {M} | {N} objects ({S})
        /// </summary>
        [RequiredField] public Text DetailLabel;

        /// <summary>
        /// 数据列表表头文本（Type/Count/Size 或 Name/Type/Size）。
        /// </summary>
        [RequiredField] public Text DescLabel;

        /// <summary>
        /// Toggle 区域 RectTransform，由 ContentSizeFitter 驱动高度，用于检测是否折行。
        /// </summary>
        [RequiredField] public RectTransform TypeToggleArea;

        /// <summary>
        /// 内存采样进度行容器，TypeToggleArea 折 2 行时向上偏移以补偿额外高度。
        /// </summary>
        [RequiredField] public RectTransform RunTaskGameObject;

        /// <summary>
        /// 内存列表 ScrollRect，TypeToggleArea 折 2 行时向上偏移以补偿额外高度。
        /// </summary>
        [RequiredField] public RectTransform DebugConsoleScrollRect;

        /// <summary>
        /// GridLayoutGroup 单行高度（cellSize.y=28）+ 行间距（spacing.y=4）= 32。
        /// 两行比一行多出此高度，用于驱动下方节点的 anchoredPosition 偏移。
        /// </summary>
        private const float c_ExtraRowHeight = 32f;

        /// <summary>
        /// 超过此阈值判定为 2 行（单行 preferredHeight=28，阈值取 30 留余量）。
        /// </summary>
        private const float c_SingleRowThreshold = 60;

        private void Start()
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(TypeToggleArea);
            float h = TypeToggleArea.rect.height;
            if (h <= c_SingleRowThreshold)
            {
                RunTaskGameObject.anchoredPosition += new Vector2(0f, c_ExtraRowHeight);
                DebugConsoleScrollRect.anchoredPosition += new Vector2(0f, c_ExtraRowHeight);
            }
        }
    }
}
