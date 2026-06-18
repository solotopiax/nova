/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIView.Methods.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 视图基类 - 非公开实现方法
 ***************************************************************/

using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 视图基类。
    /// </summary>
    public abstract partial class UIView : MonoBehaviour, IUIView
    {
        /// <summary>
        /// 视图初始化钩子。仅在实例首次创建时触发。
        /// 基类实现负责挂载 Canvas，将 RectTransform 全屏拉伸，并将全局主字体应用到所有子 Text 组件。
        /// 注意：子类重写时应调用 base.OnInit(userData) 以保留基础初始化逻辑。
        /// </summary>
        /// <param name="userData">用户自定义数据。</param>
        protected virtual void OnInit(object userData)
        {
            if (m_CachedTransform == null)
            {
                m_CachedTransform = transform;
            }

            m_OriginalLayer = gameObject.layer;

            m_CachedCanvas = gameObject.GetOrAddComponent<Canvas>();
            m_CachedCanvas.overrideSorting = true;
            OriginalDepth = m_CachedCanvas.sortingOrder;

            RectTransform rectTransform = GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = Vector2.zero;

            gameObject.GetOrAddComponent<GraphicRaycaster>();

            // by taoye: 这里需要添加本地化支持，暂时先注释掉。
            // Text[] texts = GetComponentsInChildren<Text>(true);
            // for (int i = 0; i < texts.Length; i++)
            // {
            //     texts[i].font = s_MainFont;
            //     if (!string.IsNullOrEmpty(texts[i].text))
            //     {
            //         texts[i].text = GameEntry.Localization.GetString(texts[i].text);
            //     }
            // }
        }

        /// <summary>
        /// 设置视图的可见性。
        /// 默认通过切换 GameObject 的激活状态实现，子类可重写以实现自定义显隐逻辑。
        /// </summary>
        /// <param name="visible">目标可见性。</param>
        protected virtual void InternalSetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        /// <summary>
        /// 递归设置 GameObject 及其所有子节点的 Layer。
        /// </summary>
        /// <param name="go">目标 GameObject。</param>
        /// <param name="layer">目标 Layer。</param>
        private static void InternalSetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
            {
                InternalSetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
            }
        }

    }
}
