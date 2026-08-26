/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPThirdPayPanelView.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   不依赖 ThirdPay package 的第三方支付 Panel 展示壳
 ***************************************************************/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 只负责第三方商店 UI 渲染；具体 ThirdPay SDK 调用由可选程序集注入。
    /// </summary>
    public sealed class DemoIAPThirdPayPanelView : MonoBehaviour
    {
        /// <summary>
        /// 第三方支付 Panel 自己的滚动区域。
        /// </summary>
        [SerializeField] private ScrollRect m_ScrollRect;

        /// <summary>
        /// 第三方商店、政策与资格汇总文本。
        /// </summary>
        [SerializeField] private TMP_Text m_StatusText;

        /// <summary>
        /// 第三方支付资格刷新入口。
        /// </summary>
        [SerializeField] private Button m_RefreshButton;

        /// <summary>
        /// 第三方商品卡动态列表根节点。
        /// </summary>
        [SerializeField] private RectTransform m_ProductList;

        /// <summary>
        /// 第三方商品卡隐藏模板。
        /// </summary>
        [SerializeField] private RectTransform m_ProductCardTemplate;

        /// <summary>
        /// ThirdPay 调试国家选择下拉框；Auto 表示使用 Store 默认或运行时识别的国家。
        /// </summary>
        [SerializeField] private TMP_Dropdown m_DebugCountryDropdown;

        /// <summary>
        /// 是否跳过 Google 第三方支付信息页的调试开关。
        /// </summary>
        [SerializeField] private Toggle m_SkipPaymentInformationToggle;

        /// <summary>
        /// 运行时创建的第三方商品卡。
        /// </summary>
        private readonly List<GameObject> m_RuntimeCards = new List<GameObject>();

        /// <summary>
        /// 运行时第三方支付按钮。
        /// </summary>
        private readonly List<Button> m_PayButtons = new List<Button>();

        /// <summary>
        /// 按支付表行 ID 保存已创建商品卡的标题组件，供服务端价格返回后原位刷新。
        /// </summary>
        private readonly Dictionary<long, TMP_Text> m_ProductTitles = new Dictionary<long, TMP_Text>();

        /// <summary>
        /// ThirdPay 可选模块注入的商品标题构建回调。
        /// </summary>
        private Func<long, string> m_ProductTitleBuilder;

        /// <summary>
        /// ThirdPay 可选模块注入的支付回调。
        /// </summary>
        private Action<long> m_PayRequested;

        /// <summary>
        /// ThirdPay 可选模块注入的资格刷新回调。
        /// </summary>
        private Action m_RefreshRequested;

        /// <summary>
        /// 调试国家变更回调。
        /// </summary>
        private Action<string> m_DebugCountryChanged;

        /// <summary>
        /// 跳过第三方支付信息页变更回调。
        /// </summary>
        private Action<bool> m_SkipPaymentInformationChanged;

        private static readonly string[] s_DebugCountryCodes =
        {
            string.Empty,
            "US",
            "CN",
            "JP",
            "KR",
            "GB",
            "DE",
            "FR",
            "AU",
            "RU",
        };

        private static readonly string[] s_DebugCountryLabels =
        {
            "自动（运行时）",
            "美国",
            "中国",
            "日本",
            "韩国",
            "英国",
            "德国",
            "法国",
            "澳大利亚",
            "俄罗斯",
        };

        /// <summary>
        /// 接收 ThirdPay 可选模块提供的展示文本和业务回调。
        /// </summary>
        /// <param name="productTitleBuilder">商品标题构建回调。</param>
        /// <param name="payRequested">第三方支付回调。</param>
        /// <param name="refreshRequested">资格刷新回调。</param>
        internal void Configure(Func<long, string> productTitleBuilder, Action<long> payRequested,
            Action refreshRequested, Action<string> debugCountryChanged,
            Action<bool> skipPaymentInformationChanged)
        {
            m_ProductTitleBuilder = productTitleBuilder;
            m_PayRequested = payRequested;
            m_RefreshRequested = refreshRequested;
            m_DebugCountryChanged = debugCountryChanged;
            m_SkipPaymentInformationChanged = skipPaymentInformationChanged;
            DemoIAPView.BindButton(m_RefreshButton, "刷新资格", "RefreshThirdPayAsync",
                () => m_RefreshRequested?.Invoke());
            ConfigureDebugControls();
            if (m_ProductCardTemplate != null)
            {
                m_ProductCardTemplate.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 初始化调试国家和 Google 信息页开关，并将控件事件绑定到 ThirdPay 模块回调。
        /// </summary>
        private void ConfigureDebugControls()
        {
            if (m_DebugCountryDropdown != null)
            {
                m_DebugCountryDropdown.onValueChanged.RemoveListener(OnDebugCountryChanged);
                m_DebugCountryDropdown.ClearOptions();
                m_DebugCountryDropdown.AddOptions(new List<string>(s_DebugCountryLabels));
                m_DebugCountryDropdown.SetValueWithoutNotify(0);
                m_DebugCountryDropdown.onValueChanged.AddListener(OnDebugCountryChanged);
            }

            if (m_SkipPaymentInformationToggle != null)
            {
                m_SkipPaymentInformationToggle.onValueChanged.RemoveListener(OnSkipPaymentInformationChanged);
                m_SkipPaymentInformationToggle.onValueChanged.AddListener(OnSkipPaymentInformationChanged);
            }
        }

        /// <summary>
        /// 响应调试国家选择并转换为 ThirdPay 使用的 ISO 国家代码。
        /// </summary>
        /// <param name="index">调试国家选项索引。</param>
        private void OnDebugCountryChanged(int index)
        {
            string countryCode = index >= 0 && index < s_DebugCountryCodes.Length
                ? s_DebugCountryCodes[index]
                : string.Empty;
            m_DebugCountryChanged?.Invoke(countryCode);
        }

        /// <summary>
        /// 响应跳过第三方支付信息页的调试开关。
        /// </summary>
        /// <param name="skip">是否跳过信息页。</param>
        private void OnSkipPaymentInformationChanged(bool skip)
        {
            m_SkipPaymentInformationChanged?.Invoke(skip);
        }

        /// <summary>
        /// 创建第三方商店商品卡；重复调用不会重复创建。
        /// </summary>
        internal void BuildProducts()
        {
            if (m_ProductList == null || m_ProductCardTemplate == null || m_RuntimeCards.Count > 0)
            {
                return;
            }

            for (int i = 0; i < DemoIAPProductCatalog.AllProductIds.Length; i++)
            {
                BuildProductCard(DemoIAPProductCatalog.AllProductIds[i]);
            }
        }

        /// <summary>
        /// 渲染第三方商店、支付方式、名单、政策和资格诊断文本。
        /// </summary>
        /// <param name="text">完整多行状态文本。</param>
        internal void SetStatusText(string text)
        {
            if (m_StatusText != null)
            {
                m_StatusText.text = text;
            }
        }

        /// <summary>
        /// 将跳过第三方支付信息页的调试开关同步为 Store 当前值。
        /// </summary>
        /// <param name="skip">Store 当前是否跳过信息页。</param>
        internal void SetSkipPaymentInformation(bool skip)
        {
            if (m_SkipPaymentInformationToggle != null)
            {
                m_SkipPaymentInformationToggle.SetIsOnWithoutNotify(skip);
            }
        }

        /// <summary>
        /// 使用当前标题构建回调刷新全部已有第三方商品卡。
        /// </summary>
        internal void RefreshProductTitles()
        {
            foreach (KeyValuePair<long, TMP_Text> pair in m_ProductTitles)
            {
                if (pair.Value != null)
                {
                    pair.Value.text = m_ProductTitleBuilder?.Invoke(pair.Key)
                                      ?? "ID" + pair.Key + DemoIAPBridge.FormatGroupLabel(
                                          DemoIAPProductCatalog.GetGroupLabel(pair.Key));
                }
            }
        }

        /// <summary>
        /// 设置刷新资格和第三方支付按钮的交互状态。
        /// </summary>
        /// <param name="interactable">是否允许交互。</param>
        internal void SetInteractable(bool interactable)
        {
            if (m_RefreshButton != null)
            {
                m_RefreshButton.interactable = interactable;
            }
            if (m_DebugCountryDropdown != null)
            {
                m_DebugCountryDropdown.interactable = interactable;
            }
            if (m_SkipPaymentInformationToggle != null)
            {
                m_SkipPaymentInformationToggle.interactable = interactable;
            }
            for (int i = 0; i < m_PayButtons.Count; i++)
            {
                if (m_PayButtons[i] != null)
                {
                    m_PayButtons[i].interactable = interactable;
                }
            }
        }

        /// <summary>
        /// 停止滚动惯性并复位到顶部。
        /// </summary>
        internal void ResetScrollPosition()
        {
            if (m_ScrollRect == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            m_ScrollRect.StopMovement();
            m_ScrollRect.verticalNormalizedPosition = 1f;
        }

        /// <summary>
        /// 清理运行时商品卡和可选模块回调引用。
        /// </summary>
        internal void ClearRuntimeContent()
        {
            m_DebugCountryDropdown?.onValueChanged.RemoveListener(OnDebugCountryChanged);
            m_SkipPaymentInformationToggle?.onValueChanged.RemoveListener(OnSkipPaymentInformationChanged);
            for (int i = 0; i < m_RuntimeCards.Count; i++)
            {
                if (m_RuntimeCards[i] != null)
                {
                    Destroy(m_RuntimeCards[i]);
                }
            }
            m_RuntimeCards.Clear();
            m_PayButtons.Clear();
            m_ProductTitles.Clear();
            m_ProductTitleBuilder = null;
            m_PayRequested = null;
            m_RefreshRequested = null;
            m_DebugCountryChanged = null;
            m_SkipPaymentInformationChanged = null;
        }

        /// <summary>
        /// 克隆并绑定单张第三方商品卡。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        private void BuildProductCard(long tableId)
        {
            RectTransform card = Instantiate(m_ProductCardTemplate, m_ProductList);
            card.name = "ProductCard_" + tableId;
            card.gameObject.SetActive(true);
            m_RuntimeCards.Add(card.gameObject);

            TMP_Text title = card.Find("ProductTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                m_ProductTitles[tableId] = title;
                title.text = m_ProductTitleBuilder?.Invoke(tableId)
                             ?? "ID" + tableId + DemoIAPBridge.FormatGroupLabel(DemoIAPProductCatalog.GetGroupLabel(tableId));
            }
            TMP_Text meta = card.Find("ProductMeta")?.GetComponent<TMP_Text>();
            if (meta != null)
            {
                meta.text = "点击后直接跳转当前支付 URL";
            }
            Button payButton = card.Find("PayButton")?.GetComponent<Button>();
            DemoIAPView.BindButton(payButton, "第三方支付", "IAPThirdPayRequest → OpenURL",
                () => m_PayRequested?.Invoke(tableId));
            if (payButton != null)
            {
                m_PayButtons.Add(payButton);
            }
        }
    }
}
