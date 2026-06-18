/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPLoadingPanelPresenter.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   IAP 支付 Loading 面板默认呈现器，按 Resources 路径懒加载并显隐预制体
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAP 支付 Loading 面板默认呈现器。
    /// 按 Resources 相对路径懒加载并实例化配置好的 Loading 预制体，通过 Show/Hide 切换激活状态；
    /// 实例常驻 DontDestroyOnLoad 复用，避免反复 Instantiate。
    /// 作为 IAPLoadingGuard 的默认 UI 回调实现，由 IAPStoreBase 在初始化阶段绑定。
    /// </summary>
    public sealed class IAPLoadingPanelPresenter
    {
        /// <summary>
        /// Loading 预制体相对 Resources 的加载路径。
        /// </summary>
        private readonly string m_PrefabPath;

        /// <summary>
        /// 已实例化的 Loading 面板对象；首次 Show 时懒加载创建，之后复用。
        /// </summary>
        private GameObject m_Instance;

        /// <summary>
        /// 构造呈现器，仅记录路径，不触发任何资源加载。
        /// </summary>
        /// <param name="prefabPath">Loading 预制体相对 Resources 的路径。</param>
        public IAPLoadingPanelPresenter(string prefabPath) => m_PrefabPath = prefabPath;

        /// <summary>
        /// 显示 Loading 面板：首次调用时懒加载并实例化预制体，后续仅激活复用实例。
        /// 预制体加载失败时记录 Warning 并跳过，不抛异常。
        /// </summary>
        public void Show()
        {
            if (m_Instance == null)
            {
                GameObject prefab = Resources.Load<GameObject>(m_PrefabPath);
                if (prefab == null)
                {
                    Log.Warning(LogTag.IAPPlugin, $"IAP Loading 预制体加载失败，路径：{m_PrefabPath}");
                    return;
                }

                m_Instance = Object.Instantiate(prefab);
                m_Instance.name = m_PrefabPath;
                Object.DontDestroyOnLoad(m_Instance);
            }

            m_Instance.SetActive(true);
        }

        /// <summary>
        /// 隐藏 Loading 面板：仅停用实例以便复用，不销毁。
        /// </summary>
        public void Hide()
        {
            if (m_Instance != null)
            {
                m_Instance.SetActive(false);
            }
        }

        /// <summary>
        /// 销毁已实例化的 Loading 面板，释放呈现器持有的 GameObject。
        /// </summary>
        public void Dispose()
        {
            if (m_Instance != null)
            {
                Object.Destroy(m_Instance);
                m_Instance = null;
            }
        }
    }
}
