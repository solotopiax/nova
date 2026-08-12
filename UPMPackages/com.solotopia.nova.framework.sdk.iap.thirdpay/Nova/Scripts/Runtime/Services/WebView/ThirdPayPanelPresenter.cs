/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayPanelPresenter.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   第三方支付默认面板加载与释放
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 解析支付页适配区域，并管理框架创建的临时默认面板。
    /// </summary>
    internal sealed class ThirdPayPanelPresenter : IDisposable
    {
        private GameObject m_PanelInstance;

        /// <summary>
        /// 优先使用调用方传入区域；为空时从 Resources 加载并实例化默认面板。
        /// </summary>
        /// <param name="requestedRect">调用方指定的支付页区域。</param>
        /// <param name="defaultPanelPath">默认面板 Resources 路径。</param>
        /// <returns>UniWebView 使用的适配区域。</returns>
        public RectTransform Resolve(RectTransform requestedRect, string defaultPanelPath)
        {
            if (requestedRect != null)
            {
                return requestedRect;
            }

            if (string.IsNullOrWhiteSpace(defaultPanelPath))
            {
                throw new InvalidOperationException("IAPPluginConfig.LoadingPanelPrefab 未配置，无法作为第三方支付默认面板。");
            }

            GameObject prefab = Resources.Load<GameObject>(defaultPanelPath);
            if (prefab == null)
            {
                throw new InvalidOperationException($"无法从 Resources 加载第三方支付默认面板：{defaultPanelPath}。");
            }

            m_PanelInstance = UnityEngine.Object.Instantiate(prefab);
            RectTransform rectTransform = m_PanelInstance.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                return rectTransform;
            }

            UnityEngine.Object.Destroy(m_PanelInstance);
            m_PanelInstance = null;
            throw new InvalidOperationException($"第三方支付默认面板根节点不是 RectTransform：{defaultPanelPath}。");
        }

        /// <summary>
        /// 销毁本次支付创建的临时默认面板；调用方传入的区域不由此处释放。
        /// </summary>
        public void Dispose()
        {
            if (m_PanelInstance == null)
            {
                return;
            }

            UnityEngine.Object.Destroy(m_PanelInstance);
            m_PanelInstance = null;
        }
    }
}
