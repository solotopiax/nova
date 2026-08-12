/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPView.Methods.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   IAP Demo 登录、可选商店发现与动态 Tab 编排方法
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Kit.Network.GameLogin.Runtime;
using NovaFramework.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 统一 IAP 演示 View 的通用方法。
    /// </summary>
    public sealed partial class DemoIAPView
    {
        /// <summary>
        /// 确保当前 View 持有不依赖具体支付包的 Core IAP 桥接层。
        /// </summary>
        private void EnsureIAPBridge()
        {
            if (m_IapBridge == null)
            {
                m_IapBridge = new DemoIAPBridge(AppendFeedback, SetPaymentInteractable);
            }
        }

        /// <summary>
        /// 从已加载程序集发现可选商店模块，并绑定到 Prefab 中的 Core Panel 壳。
        /// </summary>
        private void InitializeStoreModules()
        {
            if (m_StoreModulesInitialized)
            {
                return;
            }

            EnsureIAPBridge();
            List<IDemoIAPStoreModule> discovered = DemoIAPStoreModuleDiscovery.CreateAvailableModules();
            var availableKinds = new List<DemoIAPStoreKind>();
            for (int i = 0; i < discovered.Count; i++)
            {
                if (!availableKinds.Contains(discovered[i].Kind) && GetPanel(discovered[i].Kind) != null)
                {
                    availableKinds.Add(discovered[i].Kind);
                }
            }

            for (int i = 0; i < discovered.Count; i++)
            {
                IDemoIAPStoreModule module = discovered[i];
                if (FindStoreModule(module.Kind) != null)
                {
                    continue;
                }

                MonoBehaviour panel = GetPanel(module.Kind);
                if (panel == null)
                {
                    continue;
                }

                module.Initialize(new DemoIAPStoreContext(m_IapBridge, panel, availableKinds, AppendFeedback));
                m_StoreModules.Add(module);
            }

            m_StoreModulesInitialized = true;
            ApplyStoreModuleVisibility();
            ShowFirstAvailableTab();
        }

        /// <summary>
        /// 绑定登录按钮和三个通用 Tab；缺少 package 的 Tab 随后会被隐藏。
        /// </summary>
        private void BindStaticButtons()
        {
            InitializeStoreModules();
            BindButton(m_LoginButton, "登录", "Nova.Network.Kit<Login>().Async", OnLoginClick);
            BindTabButton(m_MobileTabButton, "移动支付",
                () => SelectTab(DemoIAPStoreKind.Mobile, "移动支付"));
            BindTabButton(m_ThirdPayTabButton, "第三方支付",
                () => SelectTab(DemoIAPStoreKind.ThirdPay, "第三方支付"));
            BindTabButton(m_VoucherTabButton, "金券支付",
                () => SelectTab(DemoIAPStoreKind.Voucher, "金券支付"));
        }

        /// <summary>
        /// 绑定无 API Hint 的纯导航 Tab 按钮。
        /// </summary>
        /// <param name="button">目标 Tab 按钮。</param>
        /// <param name="label">Tab 文案。</param>
        /// <param name="callback">点击回调。</param>
        private static void BindTabButton(Button button, string label, UnityAction callback)
        {
            if (button == null)
            {
                return;
            }

            SetButtonLabel(button, label);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }

        /// <summary>
        /// 登录按钮点击入口。
        /// </summary>
        private void OnLoginClick()
        {
            LoginAsync().Forget();
        }

        /// <summary>
        /// 登录账号；成功后隐藏登录按钮并显示第一个已安装商店 Tab。
        /// </summary>
        /// <returns>异步任务。</returns>
        private async UniTaskVoid LoginAsync()
        {
            if (m_LoggedIn || m_LoginInProgress)
            {
                return;
            }

            m_LoginInProgress = true;
            if (m_LoginButton != null)
            {
                m_LoginButton.interactable = false;
                SetButtonLabel(m_LoginButton, "登录中…");
            }

            try
            {
                NetResponse<PbNetLoginResp> response = await Nova.Network.Kit<Login>().Async(string.Empty, string.Empty, false);
                if (!response.IsSuccess)
                {
                    AppendFeedback("登录失败：" + response.ErrorCode + "，" + response.ErrorMessage, FeedbackLevel.Error);
                    return;
                }

                m_LoggedIn = true;
                SetAuthenticatedContentActive(true);
                ShowFirstAvailableTab();
                BuildAllStoreProducts();
                AppendFeedback("登录成功，已展示当前工程安装的支付商店。", FeedbackLevel.Success);
                m_IapBridge?.CheckLocalOrdersAsync().Forget();
                RefreshAllPaymentStateAsync().Forget();
            }
            finally
            {
                m_LoginInProgress = false;
                if (!m_LoggedIn && m_LoginButton != null)
                {
                    m_LoginButton.interactable = true;
                    SetButtonLabel(m_LoginButton, "登录");
                }
            }
        }

        /// <summary>
        /// 为全部已发现商店创建各自商品卡。
        /// </summary>
        private void BuildAllStoreProducts()
        {
            for (int i = 0; i < m_StoreModules.Count; i++)
            {
                m_StoreModules[i].BuildProducts();
            }
        }

        /// <summary>
        /// 并行刷新全部已发现商店状态；未安装的商店不会进入调用链。
        /// </summary>
        /// <returns>异步任务。</returns>
        private async UniTaskVoid RefreshAllPaymentStateAsync()
        {
            if (m_StoreModules.Count == 0)
            {
                return;
            }

            var tasks = new UniTask[m_StoreModules.Count];
            for (int i = 0; i < m_StoreModules.Count; i++)
            {
                tasks[i] = m_StoreModules[i].RefreshAsync();
            }
            await UniTask.WhenAll(tasks);
        }

        /// <summary>
        /// 根据已发现模块切换目标 Panel 和 Tab 视觉；缺少模块时不执行切换。
        /// </summary>
        /// <param name="kind">目标商店类型。</param>
        /// <returns>目标商店可用并完成切换时返回 true。</returns>
        private bool ShowTab(DemoIAPStoreKind kind)
        {
            if (FindStoreModule(kind) == null)
            {
                return false;
            }

            SetPanelActive(m_MobilePanel, kind == DemoIAPStoreKind.Mobile);
            SetPanelActive(m_ThirdPayPanel, kind == DemoIAPStoreKind.ThirdPay);
            SetPanelActive(m_VoucherPanel, kind == DemoIAPStoreKind.Voucher);
            SetTabVisual(m_MobileTabButton, m_MobileTabIndicator, kind == DemoIAPStoreKind.Mobile);
            SetTabVisual(m_ThirdPayTabButton, m_ThirdPayTabIndicator, kind == DemoIAPStoreKind.ThirdPay);
            SetTabVisual(m_VoucherTabButton, m_VoucherTabIndicator, kind == DemoIAPStoreKind.Voucher);
            return true;
        }

        /// <summary>
        /// 选择首个已发现商店；全部可选包缺失时只显示占位提示。
        /// </summary>
        private void ShowFirstAvailableTab()
        {
            if (m_StoreModules.Count > 0)
            {
                ShowTab(m_StoreModules[0].Kind);
                return;
            }

            SetPanelActive(m_MobilePanel, false);
            SetPanelActive(m_ThirdPayPanel, false);
            SetPanelActive(m_VoucherPanel, false);
        }

        /// <summary>
        /// 响应用户 Tab 点击，切换并复位目标商店 Panel。
        /// </summary>
        /// <param name="kind">目标商店类型。</param>
        /// <param name="label">反馈区商店名称。</param>
        private void SelectTab(DemoIAPStoreKind kind, string label)
        {
            IDemoIAPStoreModule module = FindStoreModule(kind);
            if (module == null || !ShowTab(kind))
            {
                return;
            }

            module.ResetScrollPosition();
            if (module is IDemoIAPStoreSelectionHandler selectionHandler)
            {
                selectionHandler.OnSelectedAsync().Forget();
            }
            AppendFeedback("已切换到" + label + " Panel。", FeedbackLevel.Info);
        }

        /// <summary>
        /// 根据商店模块存在性显示 Tab，并让 HorizontalLayoutGroup 自动均分剩余按钮。
        /// </summary>
        private void ApplyStoreModuleVisibility()
        {
            SetGameObjectActive(m_MobileTabButton, FindStoreModule(DemoIAPStoreKind.Mobile) != null);
            SetGameObjectActive(m_ThirdPayTabButton, FindStoreModule(DemoIAPStoreKind.ThirdPay) != null);
            SetGameObjectActive(m_VoucherTabButton, FindStoreModule(DemoIAPStoreKind.Voucher) != null);
            if (m_NoStorePackageText != null)
            {
                m_NoStorePackageText.gameObject.SetActive(m_StoreModules.Count == 0);
                m_NoStorePackageText.text = "未安装 IAP Store Package";
            }
        }

        /// <summary>
        /// 按商店类型查找已发现模块。
        /// </summary>
        /// <param name="kind">商店类型。</param>
        /// <returns>匹配模块；不存在时返回空。</returns>
        private IDemoIAPStoreModule FindStoreModule(DemoIAPStoreKind kind)
        {
            for (int i = 0; i < m_StoreModules.Count; i++)
            {
                if (m_StoreModules[i].Kind == kind)
                {
                    return m_StoreModules[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 按商店类型返回 Prefab 中序列化的 Core Panel 壳。
        /// </summary>
        /// <param name="kind">商店类型。</param>
        /// <returns>对应 Panel 壳。</returns>
        private MonoBehaviour GetPanel(DemoIAPStoreKind kind)
        {
            switch (kind)
            {
                case DemoIAPStoreKind.Mobile:
                    return m_MobilePanel;
                case DemoIAPStoreKind.ThirdPay:
                    return m_ThirdPayPanel;
                case DemoIAPStoreKind.Voucher:
                    return m_VoucherPanel;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 清理全部模块创建的运行时内容并丢弃发现结果。
        /// </summary>
        private void ClearStoreModules()
        {
            for (int i = 0; i < m_StoreModules.Count; i++)
            {
                m_StoreModules[i].ClearRuntimeContent();
            }
            m_StoreModules.Clear();
            m_StoreModulesInitialized = false;
        }

        /// <summary>
        /// 切换 Panel 组件所在节点的激活状态。
        /// </summary>
        /// <param name="panel">目标 Panel。</param>
        /// <param name="active">是否显示。</param>
        private static void SetPanelActive(MonoBehaviour panel, bool active)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// 切换按钮节点激活状态。
        /// </summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="active">是否显示。</param>
        private static void SetGameObjectActive(Button button, bool active)
        {
            if (button != null)
            {
                button.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// 更新 Tab 背景、文字颜色与底部选中条。
        /// </summary>
        /// <param name="button">Tab 按钮。</param>
        /// <param name="indicator">选中指示条。</param>
        /// <param name="selected">是否选中。</param>
        private static void SetTabVisual(Button button, GameObject indicator, bool selected)
        {
            if (indicator != null)
            {
                indicator.SetActive(selected);
            }
            TMP_Text text = button != null ? button.transform.Find("Text")?.GetComponent<TMP_Text>() : null;
            if (text != null)
            {
                text.color = selected ? Color.white : s_InactiveTabTextColor;
            }
            Image background = button != null ? button.GetComponent<Image>() : null;
            if (background != null)
            {
                background.color = selected ? s_ActiveTabBackgroundColor : s_InactiveTabBackgroundColor;
            }
        }

        /// <summary>
        /// 根据登录状态互斥显示登录按钮和认证内容。
        /// </summary>
        /// <param name="active">是否显示认证内容。</param>
        private void SetAuthenticatedContentActive(bool active)
        {
            if (m_LoginButton != null)
            {
                m_LoginButton.gameObject.SetActive(!active);
            }
            if (m_AuthenticatedContent != null)
            {
                m_AuthenticatedContent.SetActive(active);
            }
        }

        /// <summary>
        /// 同步全部已发现商店模块的业务按钮交互状态。
        /// </summary>
        /// <param name="interactable">是否允许交互。</param>
        private void SetPaymentInteractable(bool interactable)
        {
            for (int i = 0; i < m_StoreModules.Count; i++)
            {
                m_StoreModules[i].SetInteractable(interactable);
            }
        }

        /// <summary>
        /// 统一配置业务按钮主文字、API Hint 与点击回调。
        /// </summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="label">主文案。</param>
        /// <param name="apiHint">API 提示。</param>
        /// <param name="callback">点击回调。</param>
        internal static void BindButton(Button button, string label, string apiHint, UnityAction callback)
        {
            if (button == null)
            {
                return;
            }
            SetButtonLabel(button, label);
            SetActionButtonApiHint(button, apiHint);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(callback);
        }

        /// <summary>
        /// 设置按钮主文本，不影响同级 API Hint。
        /// </summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="text">主文案。</param>
        internal static void SetButtonLabel(Button button, string text)
        {
            TMP_Text label = button != null ? button.transform.Find("Text")?.GetComponent<TMP_Text>() : null;
            if (label != null)
            {
                label.text = text;
            }
        }

        /// <summary>
        /// 设置按钮底部 API Hint。
        /// </summary>
        /// <param name="button">目标按钮。</param>
        /// <param name="text">API 提示。</param>
        internal static void SetActionButtonApiHint(Button button, string text)
        {
            TMP_Text hint = button != null ? button.transform.Find("ApiHintText")?.GetComponent<TMP_Text>() : null;
            if (hint != null)
            {
                hint.text = text;
            }
        }
    }
}
