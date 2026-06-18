/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIGroupHelperBase.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 视图分组辅助器基类
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 视图分组辅助器基类。
    /// 每个视图分组对应一个辅助器实例。
    /// </summary>
    public abstract class UIGroupHelperBase : MonoBehaviour, IUIGroupHelper
    {
        /// <summary>
        /// 视图分组深度换算系数，由 UIManager 在创建视图分组时透传写入。
        /// </summary>
        private int m_DepthFactor = 0;

        /// <summary>
        /// 当前视图分组深度。
        /// </summary>
        private int m_Depth = 0;

        /// <summary>
        /// 缓存的 Canvas 组件引用。
        /// </summary>
        private Canvas m_CachedCanvas = null;

        /// <summary>
        /// 唤醒。
        /// 获取或挂载 Canvas，确保该视图分组节点具备 UI 渲染能力。
        /// </summary>
        private void Awake()
        {
            m_CachedCanvas = gameObject.GetOrAddComponent<Canvas>();
        }

        /// <summary>
        /// 开始。
        /// 将 RectTransform 拉伸铺满父节点。
        /// sortingOrder 已在 SetDepth 中统一设置，此处不再冗余赋值。
        /// </summary>
        private void Start()
        {
            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;
        }

        /// <summary>
        /// 设置视图分组深度换算系数。
        /// 由 UIManager 在创建视图分组时透传写入，乘以视图分组深度后得到 Canvas.sortingOrder。
        /// </summary>
        /// <param name="value">视图分组深度换算系数。</param>
        public virtual void SetDepthFactor(int value)
        {
            m_DepthFactor = value;
        }

        /// <summary>
        /// 设置视图分组深度。
        /// 计算 sortingOrder = m_DepthFactor * depth 并应用到 Canvas。
        /// 包含 Canvas 懒初始化（防止 Awake 之前被调用）和 sortingOrder 溢出校验。
        /// 越界时记录 Error 并 Clamp 到 short 范围，避免 Unity 静默截断导致层级紊乱。
        /// </summary>
        /// <param name="depth">视图分组深度。</param>
        public virtual void SetDepth(int depth)
        {
            m_Depth = depth;

            if (m_CachedCanvas == null)
            {
                m_CachedCanvas = gameObject.GetOrAddComponent<Canvas>();
            }

            int sortingOrder = m_DepthFactor * depth;
            if (sortingOrder < short.MinValue || sortingOrder > short.MaxValue)
            {
                Log.Error(LogTag.UI, Txt.Format("视图分组深度 {0} 计算的 sortingOrder {1} 超出有效范围（{2} ~ {3}），已 Clamp 到边界。请检查 UIGroup 配置或调整 UIComponent 上的视图分组深度换算系数。", depth, sortingOrder, short.MinValue, short.MaxValue));
                sortingOrder = Mathf.Clamp(sortingOrder, short.MinValue, short.MaxValue);
            }

            m_CachedCanvas.overrideSorting = true;
            m_CachedCanvas.sortingOrder = sortingOrder;
        }

    }
}
