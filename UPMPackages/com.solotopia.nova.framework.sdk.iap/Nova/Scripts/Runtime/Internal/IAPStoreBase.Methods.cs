/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPStoreBase.Methods.cs
 * author:    yingzheng
 * created:   2026/6/5
 * descrip:   IAPStoreBase 私有/保护辅助方法
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// IAPStoreBase 私有/保护辅助方法。
    /// </summary>
    public abstract partial class IAPStoreBase
    {
        /// <summary>
        /// 若 Context.LoadingPanelPrefab 配置了路径且尚未绑定呈现器，则创建默认 Loading 呈现器，
        /// 并将其 Show/Hide 绑定为 m_LoadingGuard 的显隐回调。
        /// 路径为空时保持未绑定状态，Loading 行为为空操作；业务层可在初始化后再次调用
        /// BindLoadingCallbacks 覆盖为自定义 UI。
        /// </summary>
        private void TryBindDefaultLoadingPanel()
        {
            if (m_LoadingPresenter != null)
            {
                return;
            }

            string path = Context?.LoadingPanelPrefab;
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            m_LoadingPresenter = new IAPLoadingPanelPresenter(path);
            BindLoadingCallbacks(m_LoadingPresenter.Show, m_LoadingPresenter.Hide);
        }
    }
}
