/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPMobilePanelView.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   不依赖 Mobile package 的移动支付 Panel 展示壳
 ***************************************************************/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 只负责移动商店 UI 渲染；具体 Mobile SDK 调用由可选程序集注入。
    /// </summary>
    public sealed class DemoIAPMobilePanelView : MonoBehaviour
    {
        /// <summary>
        /// 自定义 ReceiptParam 测试支付固定使用的商品表行 ID。
        /// </summary>
        private const long c_CustomReceiptParamTableId = 1L;

        /// <summary>
        /// 自定义 ReceiptParam 输入框的默认文本。
        /// </summary>
        private const string c_CustomReceiptParamDefaultValue = "1";

        /// <summary>
        /// Mobile 平台透传 ReceiptParam 的最大字符数。
        /// </summary>
        private const int c_CustomReceiptParamMaxLength = 16;

        /// <summary>
        /// 自定义 ReceiptParam 测试支付卡的运行时节点名。
        /// </summary>
        private const string c_CustomReceiptParamCardName = "ProductCard_CustomReceiptParam_1";

        /// <summary>
        /// 自定义 ReceiptParam 输入框的运行时节点名。
        /// </summary>
        private const string c_CustomReceiptParamInputName = "ReceiptParamInput";

        /// <summary>
        /// 自定义 ReceiptParam 测试支付卡的布局高度。
        /// </summary>
        private const float c_CustomReceiptParamCardHeight = 292f;

        /// <summary>
        /// 自定义 ReceiptParam 输入框的布局高度。
        /// </summary>
        private const float c_CustomReceiptParamInputHeight = 52f;

        /// <summary>
        /// 自定义 ReceiptParam 输入框背景色。
        /// </summary>
        private static readonly Color s_CustomReceiptParamInputBackgroundColor = new Color32(0xF8, 0xFA, 0xFC, 0xFF);

        /// <summary>
        /// 自定义 ReceiptParam 输入框正文颜色。
        /// </summary>
        private static readonly Color s_CustomReceiptParamInputTextColor = new Color32(0x1E, 0x29, 0x3B, 0xFF);

        /// <summary>
        /// 自定义 ReceiptParam 输入框占位文本颜色。
        /// </summary>
        private static readonly Color s_CustomReceiptParamPlaceholderColor = new Color32(0x94, 0xA3, 0xB8, 0xFF);

        /// <summary>
        /// 移动商店 Panel 自己的滚动区域。
        /// </summary>
        [SerializeField] private ScrollRect m_ScrollRect;

        /// <summary>
        /// 当前运行平台商店名称文本。
        /// </summary>
        [SerializeField] private TMP_Text m_StoreText;

        /// <summary>
        /// Mobile 能力可用状态文本。
        /// </summary>
        [SerializeField] private TMP_Text m_CapabilityText;

        /// <summary>
        /// 平台商品同步状态文本。
        /// </summary>
        [SerializeField] private TMP_Text m_ProductStatusText;

        /// <summary>
        /// 恢复移动订阅与非消耗品入口。
        /// </summary>
        [SerializeField] private Button m_RestorePurchasesButton;

        /// <summary>
        /// 移动商品卡动态列表根节点。
        /// </summary>
        [SerializeField] private RectTransform m_ProductList;

        /// <summary>
        /// 移动商品卡隐藏模板。
        /// </summary>
        [SerializeField] private RectTransform m_ProductCardTemplate;

        /// <summary>
        /// 运行时创建的移动商品卡。
        /// </summary>
        private readonly List<GameObject> m_RuntimeCards = new List<GameObject>();

        /// <summary>
        /// 运行时移动支付按钮。
        /// </summary>
        private readonly List<Button> m_PayButtons = new List<Button>();

        /// <summary>
        /// 运行时创建的自定义 ReceiptParam 输入框。
        /// </summary>
        private TMP_InputField m_CustomReceiptParamInput;

        /// <summary>
        /// Mobile 可选模块注入的商品标题构建回调。
        /// </summary>
        private Func<long, string> m_ProductTitleBuilder;

        /// <summary>
        /// Mobile 可选模块注入的支付回调。
        /// </summary>
        private Action<long> m_PayRequested;

        /// <summary>
        /// Mobile 可选模块注入的自定义 ReceiptParam 支付回调。
        /// </summary>
        private Action<string> m_CustomReceiptParamPayRequested;

        /// <summary>
        /// Mobile 可选模块注入的恢复购买回调。
        /// </summary>
        private Action m_RestoreRequested;

        /// <summary>
        /// 接收 Mobile 可选模块提供的展示文本和业务回调。
        /// </summary>
        /// <param name="productTitleBuilder">商品标题构建回调。</param>
        /// <param name="payRequested">移动支付回调。</param>
        /// <param name="customReceiptParamPayRequested">自定义 ReceiptParam 支付回调。</param>
        /// <param name="restoreRequested">恢复订阅回调。</param>
        internal void Configure(Func<long, string> productTitleBuilder, Action<long> payRequested,
            Action<string> customReceiptParamPayRequested, Action restoreRequested)
        {
            m_ProductTitleBuilder = productTitleBuilder;
            m_PayRequested = payRequested;
            m_CustomReceiptParamPayRequested = customReceiptParamPayRequested;
            m_RestoreRequested = restoreRequested;
            DemoIAPView.BindButton(m_RestorePurchasesButton, "恢复订阅", "RestorePurchasesAsync",
                () => m_RestoreRequested?.Invoke());
            if (m_ProductCardTemplate != null)
            {
                m_ProductCardTemplate.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 创建移动商店商品卡；重复调用不会重复创建。
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
            BuildCustomReceiptParamProductCard();
        }

        /// <summary>
        /// 渲染移动商店名称、能力和商品同步状态。
        /// </summary>
        /// <param name="storeName">当前运行平台商店名。</param>
        /// <param name="capabilityAvailable">移动支付能力是否可用。</param>
        /// <param name="productStatus">商品同步状态文案。</param>
        internal void SetStatus(string storeName, bool capabilityAvailable, string productStatus)
        {
            if (m_StoreText != null)
            {
                m_StoreText.text = "当前商店：" + storeName;
            }
            if (m_CapabilityText != null)
            {
                m_CapabilityText.text = "官方支付能力：" + (capabilityAvailable ? "可用" : "不可用");
            }
            if (m_ProductStatusText != null)
            {
                m_ProductStatusText.text = "商品状态：" + productStatus;
            }
        }

        /// <summary>
        /// 更新指定移动商品卡标题。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <param name="text">最新标题。</param>
        internal void UpdateProductText(long tableId, string text)
        {
            TMP_Text title = FindProductTitle(tableId);
            if (title != null)
            {
                title.text = text;
            }
        }

        /// <summary>
        /// 设置恢复订阅与移动支付按钮的交互状态。
        /// </summary>
        /// <param name="interactable">是否允许交互。</param>
        internal void SetInteractable(bool interactable)
        {
            if (m_RestorePurchasesButton != null)
            {
                m_RestorePurchasesButton.interactable = interactable;
            }
            for (int i = 0; i < m_PayButtons.Count; i++)
            {
                if (m_PayButtons[i] != null)
                {
                    m_PayButtons[i].interactable = interactable;
                }
            }
            if (m_CustomReceiptParamInput != null)
            {
                m_CustomReceiptParamInput.interactable = interactable;
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
            for (int i = 0; i < m_RuntimeCards.Count; i++)
            {
                if (m_RuntimeCards[i] != null)
                {
                    Destroy(m_RuntimeCards[i]);
                }
            }
            m_RuntimeCards.Clear();
            m_PayButtons.Clear();
            m_CustomReceiptParamInput = null;
            m_ProductTitleBuilder = null;
            m_PayRequested = null;
            m_CustomReceiptParamPayRequested = null;
            m_RestoreRequested = null;
        }

        /// <summary>
        /// 克隆并绑定单张移动商品卡。
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
                meta.text = "移动官方商店 SKU";
            }
            Button payButton = card.Find("PayButton")?.GetComponent<Button>();
            DemoIAPView.BindButton(payButton, "移动支付", "PayAsync<IAPResult>",
                () => m_PayRequested?.Invoke(tableId));
            if (payButton != null)
            {
                m_PayButtons.Add(payButton);
            }
        }

        /// <summary>
        /// 创建 tableId=1 且 ReceiptParam 可输入的移动支付测试卡。
        /// </summary>
        private void BuildCustomReceiptParamProductCard()
        {
            RectTransform card = Instantiate(m_ProductCardTemplate, m_ProductList);
            card.name = c_CustomReceiptParamCardName;
            card.gameObject.SetActive(true);
            m_RuntimeCards.Add(card.gameObject);
            ApplyCustomReceiptParamCardLayout(card);

            TMP_Text title = card.Find("ProductTitle")?.GetComponent<TMP_Text>();
            if (title != null)
            {
                title.text = "ID" + c_CustomReceiptParamTableId + "  [自定义 ReceiptParam]";
            }

            TMP_Text meta = card.Find("ProductMeta")?.GetComponent<TMP_Text>();
            if (meta != null)
            {
                meta.text = "固定 tableId=1，输入 ReceiptParam 后发起同 SKU 不同业务参数支付";
            }

            Button payButton = card.Find("PayButton")?.GetComponent<Button>();
            m_CustomReceiptParamInput = CreateCustomReceiptParamInput(card, title ?? meta, payButton);
            DemoIAPView.BindButton(payButton, "自定义参数支付", "tableId=1 + ReceiptParam",
                () => m_CustomReceiptParamPayRequested?.Invoke(GetCustomReceiptParam()));
            if (payButton != null)
            {
                m_PayButtons.Add(payButton);
            }
        }

        /// <summary>
        /// 调整自定义 ReceiptParam 测试卡高度，避免新增输入框压缩按钮。
        /// </summary>
        /// <param name="card">自定义 ReceiptParam 测试卡。</param>
        private static void ApplyCustomReceiptParamCardLayout(RectTransform card)
        {
            if (card == null)
            {
                return;
            }

            LayoutElement element = card.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = card.gameObject.AddComponent<LayoutElement>();
            }
            element.preferredHeight = c_CustomReceiptParamCardHeight;
        }

        /// <summary>
        /// 创建自定义 ReceiptParam 输入框，并插入到支付按钮之前。
        /// </summary>
        /// <param name="card">自定义 ReceiptParam 测试卡。</param>
        /// <param name="referenceText">用于复用字体资产的参考文本。</param>
        /// <param name="payButton">自定义 ReceiptParam 测试卡支付按钮。</param>
        /// <returns>创建完成的 TMP 输入框。</returns>
        private static TMP_InputField CreateCustomReceiptParamInput(RectTransform card, TMP_Text referenceText,
            Button payButton)
        {
            var inputObject = new GameObject(c_CustomReceiptParamInputName, typeof(RectTransform), typeof(Image),
                typeof(LayoutElement), typeof(TMP_InputField));
            var inputRect = inputObject.GetComponent<RectTransform>();
            inputRect.SetParent(card, false);
            inputRect.sizeDelta = new Vector2(0f, c_CustomReceiptParamInputHeight);
            if (payButton != null)
            {
                inputRect.SetSiblingIndex(payButton.transform.GetSiblingIndex());
            }

            Image background = inputObject.GetComponent<Image>();
            background.color = s_CustomReceiptParamInputBackgroundColor;

            LayoutElement layout = inputObject.GetComponent<LayoutElement>();
            layout.minHeight = c_CustomReceiptParamInputHeight;
            layout.preferredHeight = c_CustomReceiptParamInputHeight;

            TMP_Text text = CreateCustomReceiptParamInputText(inputRect, "Text", referenceText,
                s_CustomReceiptParamInputTextColor, string.Empty);
            TMP_Text placeholder = CreateCustomReceiptParamInputText(inputRect, "Placeholder", referenceText,
                s_CustomReceiptParamPlaceholderColor, "输入十六进制参数（1-16 位，不能 0 开头）");

            TMP_InputField input = inputObject.GetComponent<TMP_InputField>();
            input.targetGraphic = background;
            input.textViewport = inputRect;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.characterLimit = c_CustomReceiptParamMaxLength;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.text = c_CustomReceiptParamDefaultValue;
            return input;
        }

        /// <summary>
        /// 创建自定义 ReceiptParam 输入框内部文本节点。
        /// </summary>
        /// <param name="parent">输入框根节点。</param>
        /// <param name="name">文本节点名称。</param>
        /// <param name="referenceText">用于复用字体资产的参考文本。</param>
        /// <param name="color">文本颜色。</param>
        /// <param name="text">初始文本。</param>
        /// <returns>创建完成的 TMP 文本。</returns>
        private static TMP_Text CreateCustomReceiptParamInputText(RectTransform parent, string name,
            TMP_Text referenceText, Color color, string text)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(parent, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(14f, 6f);
            textRect.offsetMax = new Vector2(-14f, -6f);

            TMP_Text tmpText = textObject.GetComponent<TMP_Text>();
            if (referenceText != null)
            {
                tmpText.font = referenceText.font;
                tmpText.fontSharedMaterial = referenceText.fontSharedMaterial;
                tmpText.fontSize = referenceText.fontSize;
            }
            else
            {
                tmpText.fontSize = 24f;
            }
            tmpText.text = text;
            tmpText.color = color;
            tmpText.alignment = TextAlignmentOptions.MidlineLeft;
            tmpText.enableWordWrapping = false;
            tmpText.raycastTarget = false;
            return tmpText;
        }

        /// <summary>
        /// 读取当前自定义 ReceiptParam 输入框文本。
        /// </summary>
        /// <returns>输入框文本；输入框尚未创建时返回默认值。</returns>
        private string GetCustomReceiptParam()
        {
            return m_CustomReceiptParamInput != null ? m_CustomReceiptParamInput.text : c_CustomReceiptParamDefaultValue;
        }

        /// <summary>
        /// 查找指定商品卡标题。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>标题组件；商品卡不存在时返回空。</returns>
        private TMP_Text FindProductTitle(long tableId)
        {
            Transform card = m_ProductList != null ? m_ProductList.Find("ProductCard_" + tableId) : null;
            return card != null ? card.Find("ProductTitle")?.GetComponent<TMP_Text>() : null;
        }
    }
}
