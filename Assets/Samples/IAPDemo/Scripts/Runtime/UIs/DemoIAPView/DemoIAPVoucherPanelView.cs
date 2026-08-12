/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPVoucherPanelView.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   不依赖 Voucher package 的金券钱包与支付 Panel 展示壳
 ***************************************************************/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 只负责金券 UI 和输入校验；具体 Voucher SDK 调用由可选程序集注入。
    /// </summary>
    public sealed class DemoIAPVoucherPanelView : MonoBehaviour
    {
        /// <summary>
        /// 金券支付 Panel 自己的滚动区域。
        /// </summary>
        [SerializeField] private ScrollRect m_ScrollRect;

        /// <summary>
        /// 钱包就绪状态与服务端版本文本。
        /// </summary>
        [SerializeField] private TMP_Text m_StatusText;

        /// <summary>
        /// 钱包刷新入口。
        /// </summary>
        [SerializeField] private Button m_RefreshButton;

        /// <summary>
        /// 兑换券余额动态列表根节点。
        /// </summary>
        [SerializeField] private RectTransform m_VoucherBalanceContent;

        /// <summary>
        /// 兑换券余额行隐藏模板。
        /// </summary>
        [SerializeField] private TMP_Text m_VoucherBalanceRowTemplate;

        /// <summary>
        /// 代币余额动态列表根节点。
        /// </summary>
        [SerializeField] private RectTransform m_CoinBalanceContent;

        /// <summary>
        /// 代币余额行隐藏模板。
        /// </summary>
        [SerializeField] private TMP_Text m_CoinBalanceRowTemplate;

        /// <summary>
        /// 测试发放资产类型切换入口。
        /// </summary>
        [SerializeField] private Button m_GrantTypeButton;

        /// <summary>
        /// 测试发放资产类型 ID 输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_GrantIdInput;

        /// <summary>
        /// 测试发放数量输入框。
        /// </summary>
        [SerializeField] private TMP_InputField m_GrantQuantityInput;

        /// <summary>
        /// 测试发放请求入口。
        /// </summary>
        [SerializeField] private Button m_TestGrantButton;

        /// <summary>
        /// 金券商品卡动态列表根节点。
        /// </summary>
        [SerializeField] private RectTransform m_ProductList;

        /// <summary>
        /// 金券商品卡隐藏模板。
        /// </summary>
        [SerializeField] private RectTransform m_ProductCardTemplate;

        /// <summary>
        /// 运行时创建的金券商品卡。
        /// </summary>
        private readonly List<GameObject> m_RuntimeCards = new List<GameObject>();

        /// <summary>
        /// 运行时金券支付按钮。
        /// </summary>
        private readonly List<Button> m_PayButtons = new List<Button>();

        /// <summary>
        /// 运行时兑换券余额文本行。
        /// </summary>
        private readonly List<TMP_Text> m_RuntimeVoucherRows = new List<TMP_Text>();

        /// <summary>
        /// 运行时代币余额文本行。
        /// </summary>
        private readonly List<TMP_Text> m_RuntimeCoinRows = new List<TMP_Text>();

        /// <summary>
        /// Voucher 可选模块注入的商品标题构建回调。
        /// </summary>
        private Func<long, string> m_ProductTitleBuilder;

        /// <summary>
        /// Voucher 可选模块注入的支付回调。
        /// </summary>
        private Action<long> m_PayRequested;

        /// <summary>
        /// Voucher 可选模块注入的钱包刷新回调。
        /// </summary>
        private Action m_RefreshRequested;

        /// <summary>
        /// Voucher 可选模块注入的测试发放回调。
        /// </summary>
        private Action<bool, int, int> m_TestGrantRequested;

        /// <summary>
        /// BaseDemoView 底部反馈区回调。
        /// </summary>
        private Action<string, FeedbackLevel> m_Feedback;

        /// <summary>
        /// 当前测试发放类型；为 true 时发放兑换券，否则发放代币。
        /// </summary>
        private bool m_GrantVoucher = true;

        /// <summary>
        /// 接收 Voucher 可选模块提供的业务回调和商品标题构建器。
        /// </summary>
        /// <param name="productTitleBuilder">商品标题构建回调。</param>
        /// <param name="payRequested">金券支付回调。</param>
        /// <param name="refreshRequested">钱包刷新回调。</param>
        /// <param name="testGrantRequested">测试发放回调。</param>
        /// <param name="feedback">底部反馈区回调。</param>
        internal void Configure(Func<long, string> productTitleBuilder, Action<long> payRequested,
            Action refreshRequested, Action<bool, int, int> testGrantRequested,
            Action<string, FeedbackLevel> feedback)
        {
            m_ProductTitleBuilder = productTitleBuilder;
            m_PayRequested = payRequested;
            m_RefreshRequested = refreshRequested;
            m_TestGrantRequested = testGrantRequested;
            m_Feedback = feedback;
            DemoIAPView.BindButton(m_RefreshButton, "刷新钱包", "RefreshWalletAsync",
                () => m_RefreshRequested?.Invoke());
            DemoIAPView.BindButton(m_GrantTypeButton, m_GrantVoucher ? "发放：兑换券" : "发放：代币",
                "VoucherTestGrantRequest", ToggleGrantType);
            DemoIAPView.BindButton(m_TestGrantButton, "模拟发放", "TestGrantAsync", OnTestGrantClick);
            SetTemplateActive(m_VoucherBalanceRowTemplate, false);
            SetTemplateActive(m_CoinBalanceRowTemplate, false);
            if (m_ProductCardTemplate != null)
            {
                m_ProductCardTemplate.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 创建金券商店商品卡；重复调用不会重复创建。
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
        /// 使用可选模块转换后的基础文本渲染钱包状态和全部余额。
        /// </summary>
        /// <param name="ready">钱包是否就绪。</param>
        /// <param name="version">钱包服务端版本。</param>
        /// <param name="voucherRows">兑换券余额文案。</param>
        /// <param name="coinRows">代币余额文案。</param>
        internal void RenderWallet(bool ready, long version, IReadOnlyList<string> voucherRows,
            IReadOnlyList<string> coinRows)
        {
            DestroyTexts(m_RuntimeVoucherRows);
            DestroyTexts(m_RuntimeCoinRows);
            if (m_StatusText != null)
            {
                m_StatusText.text = ready ? "钱包状态：已就绪 · v" + version : "钱包状态：未就绪";
            }

            AddBalanceRows(m_VoucherBalanceRowTemplate, m_VoucherBalanceContent, voucherRows, m_RuntimeVoucherRows);
            AddBalanceRows(m_CoinBalanceRowTemplate, m_CoinBalanceContent, coinRows, m_RuntimeCoinRows);
        }

        /// <summary>
        /// 设置钱包、模拟发放和金券支付按钮的交互状态。
        /// </summary>
        /// <param name="interactable">是否允许交互。</param>
        internal void SetInteractable(bool interactable)
        {
            if (m_RefreshButton != null) m_RefreshButton.interactable = interactable;
            if (m_GrantTypeButton != null) m_GrantTypeButton.interactable = interactable;
            if (m_TestGrantButton != null) m_TestGrantButton.interactable = interactable;
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
        /// 清理运行时商品卡、余额行和可选模块回调引用。
        /// </summary>
        internal void ClearRuntimeContent()
        {
            DestroyObjects(m_RuntimeCards);
            DestroyTexts(m_RuntimeVoucherRows);
            DestroyTexts(m_RuntimeCoinRows);
            m_PayButtons.Clear();
            m_ProductTitleBuilder = null;
            m_PayRequested = null;
            m_RefreshRequested = null;
            m_TestGrantRequested = null;
            m_Feedback = null;
        }

        /// <summary>
        /// 在兑换券和代币测试发放类型间切换。
        /// </summary>
        private void ToggleGrantType()
        {
            m_GrantVoucher = !m_GrantVoucher;
            DemoIAPView.SetButtonLabel(m_GrantTypeButton, m_GrantVoucher ? "发放：兑换券" : "发放：代币");
            m_Feedback?.Invoke("模拟发放类型已切换为" + (m_GrantVoucher ? "兑换券。" : "代币。"), FeedbackLevel.Info);
        }

        /// <summary>
        /// 校验模拟发放输入，并把基础值传给 Voucher 可选模块。
        /// </summary>
        private void OnTestGrantClick()
        {
            if (!int.TryParse(m_GrantIdInput != null ? m_GrantIdInput.text : string.Empty, out int assetId)
                || !int.TryParse(m_GrantQuantityInput != null ? m_GrantQuantityInput.text : string.Empty, out int quantity)
                || assetId <= 0 || quantity <= 0)
            {
                m_Feedback?.Invoke("模拟发放需要输入正数 ID 和数量。", FeedbackLevel.Warn);
                return;
            }

            m_TestGrantRequested?.Invoke(m_GrantVoucher, assetId, quantity);
        }

        /// <summary>
        /// 克隆并绑定单张金券商品卡。
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
                title.text = m_ProductTitleBuilder?.Invoke(tableId)
                             ?? "ID" + tableId + DemoIAPBridge.FormatGroupLabel(DemoIAPProductCatalog.GetGroupLabel(tableId));
            }
            TMP_Text meta = card.Find("ProductMeta")?.GetComponent<TMP_Text>();
            if (meta != null)
            {
                meta.text = "先计算抵扣方案，再幂等确认扣减";
            }
            Button payButton = card.Find("PayButton")?.GetComponent<Button>();
            DemoIAPView.BindButton(payButton, "金券支付", "Quote → IAPVoucherRequest",
                () => m_PayRequested?.Invoke(tableId));
            if (payButton != null)
            {
                m_PayButtons.Add(payButton);
            }
        }

        /// <summary>
        /// 批量克隆余额文本行。
        /// </summary>
        /// <param name="template">余额行模板。</param>
        /// <param name="parent">目标容器。</param>
        /// <param name="values">余额文案列表。</param>
        /// <param name="rows">运行时行登记列表。</param>
        private static void AddBalanceRows(TMP_Text template, RectTransform parent, IReadOnlyList<string> values,
            List<TMP_Text> rows)
        {
            if (template == null || parent == null || values == null)
            {
                return;
            }

            for (int i = 0; i < values.Count; i++)
            {
                TMP_Text row = Instantiate(template, parent);
                row.gameObject.SetActive(true);
                row.text = values[i];
                rows.Add(row);
            }
        }

        /// <summary>
        /// 设置余额模板节点激活状态。
        /// </summary>
        /// <param name="template">目标模板。</param>
        /// <param name="active">是否激活。</param>
        private static void SetTemplateActive(TMP_Text template, bool active)
        {
            if (template != null)
            {
                template.gameObject.SetActive(active);
            }
        }

        /// <summary>
        /// 销毁运行时对象列表。
        /// </summary>
        /// <param name="objects">待销毁对象。</param>
        private static void DestroyObjects(List<GameObject> objects)
        {
            for (int i = 0; i < objects.Count; i++)
            {
                if (objects[i] != null)
                {
                    Destroy(objects[i]);
                }
            }
            objects.Clear();
        }

        /// <summary>
        /// 销毁运行时余额文本行。
        /// </summary>
        /// <param name="rows">待销毁文本行。</param>
        private static void DestroyTexts(List<TMP_Text> rows)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                {
                    Destroy(rows[i].gameObject);
                }
            }
            rows.Clear();
        }
    }
}
