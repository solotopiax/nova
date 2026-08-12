/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPViewPrefabBuilder.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   构建可按已安装支付 package 动态显示 Tab 的 DemoIAPView Variant
 ***************************************************************/

using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Sdk.IAP.Samples.Editor
{
    /// <summary>
    /// 保留 BaseDemoView 三段式骨架，并构建通用 Tab、Panel 壳和无商店占位状态。
    /// </summary>
    public static class DemoIAPViewPrefabBuilder
    {
        private const string c_TargetPrefabPath = "Assets/Samples/IAPDemo/Prefabs/UIs/DemoIAPView/DemoIAPView.prefab";
        private const string c_ViewTypeName = "NovaFramework.Sdk.IAP.Samples.Runtime.DemoIAPView";
        private const string c_MobilePanelTypeName = "NovaFramework.Sdk.IAP.Samples.Runtime.DemoIAPMobilePanelView";
        private const string c_ThirdPayPanelTypeName = "NovaFramework.Sdk.IAP.Samples.Runtime.DemoIAPThirdPayPanelView";
        private const string c_VoucherPanelTypeName = "NovaFramework.Sdk.IAP.Samples.Runtime.DemoIAPVoucherPanelView";

        private static readonly Color s_PageColor = new Color32(0xEE, 0xF2, 0xF7, 0xFF);
        private static readonly Color s_PanelColor = new Color32(0xF8, 0xFA, 0xFC, 0xFF);
        private static readonly Color s_SubPanelColor = Color.white;
        private static readonly Color s_PrimaryColor = new Color32(0x1E, 0x40, 0xAF, 0xFF);
        private static readonly Color s_SecondaryColor = new Color32(0xDB, 0xEA, 0xFE, 0xFF);
        private static readonly Color s_InactiveTabColor = new Color32(0xD7, 0xE0, 0xEA, 0xFF);
        private static readonly Color s_InactiveTextColor = new Color32(0x33, 0x41, 0x55, 0xFF);
        private static readonly Color s_ApiHintColor = new Color32(0x1E, 0x40, 0xAF, 0xFF);
        private static readonly Color s_MutedColor = new Color32(0x64, 0x74, 0x8B, 0xFF);

        /// <summary>
        /// 从 Unity 菜单重建三商店 Tab 演示 Prefab。
        /// </summary>
        [MenuItem("Nova/Samples/IAP/Rebuild DemoIAPView Prefab")]
        public static void Build()
        {
            Type viewType = RequireType(c_ViewTypeName);
            Type mobilePanelType = RequireType(c_MobilePanelTypeName);
            Type thirdPayPanelType = RequireType(c_ThirdPayPanelTypeName);
            Type voucherPanelType = RequireType(c_VoucherPanelTypeName);

            GameObject root = PrefabUtility.LoadPrefabContents(c_TargetPrefabPath);
            try
            {
                Component view = root.GetComponent(viewType);
                if (view == null)
                {
                    throw new InvalidOperationException("目标 Prefab 根节点缺少 DemoIAPView 组件。");
                }

                var serializedView = new SerializedObject(view);
                serializedView.Update();
                TMP_Text fontSource = serializedView.FindProperty("m_TitleText")?.objectReferenceValue as TMP_Text;
                RectTransform content = root.transform.Find("InteractionArea/Viewport/Content") as RectTransform;
                if (content == null)
                {
                    throw new InvalidOperationException("目标 Prefab 缺少 InteractionArea/Viewport/Content。");
                }

                ClearChildren(content);
                ConfigureRootContent(content);
                BuildHierarchy(content, fontSource, serializedView, mobilePanelType, thirdPayPanelType, voucherPanelType);
                serializedView.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, c_TargetPrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(c_TargetPrefabPath, ImportAssetOptions.ForceUpdate);
                Debug.Log("DemoIAPView 三商店 Tab Prefab 构建完成：" + c_TargetPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>
        /// 构建登录入口、Tab 栏和三个独立商店 Panel。
        /// </summary>
        /// <param name="content">交互区 Content。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="view">DemoIAPView 序列化对象。</param>
        /// <param name="mobilePanelType">移动 Panel 组件类型。</param>
        /// <param name="thirdPayPanelType">第三方 Panel 组件类型。</param>
        /// <param name="voucherPanelType">金券 Panel 组件类型。</param>
        private static void BuildHierarchy(RectTransform content, TMP_Text fontSource, SerializedObject view,
            Type mobilePanelType, Type thirdPayPanelType, Type voucherPanelType)
        {
            Button loginButton = CreateActionButton(content, fontSource, "LoginButton", "登录",
                "Nova.Network.Kit<Login>().Async", 72f, ActionButtonStyle.Primary);

            RectTransform authenticated = CreateRect(content, "AuthenticatedContent");
            authenticated.gameObject.AddComponent<Image>().color = s_PageColor;
            var authenticatedLayout = authenticated.gameObject.AddComponent<LayoutElement>();
            authenticatedLayout.minHeight = 640f;
            authenticatedLayout.flexibleHeight = 1f;
            RectTransform tabs = CreateHorizontalContainer(authenticated, "StoreTabs", 92f, 8f);
            tabs.anchorMin = new Vector2(0f, 1f);
            tabs.anchorMax = Vector2.one;
            tabs.pivot = new Vector2(0.5f, 1f);
            tabs.offsetMin = new Vector2(0f, -92f);
            tabs.offsetMax = Vector2.zero;
            TabParts mobileTab = CreateTabButton(tabs, fontSource, "MobileTabButton", "移动支付", true);
            TabParts thirdPayTab = CreateTabButton(tabs, fontSource, "ThirdPayTabButton", "第三方支付", false);
            TabParts voucherTab = CreateTabButton(tabs, fontSource, "VoucherTabButton", "金券支付", false);

            TMP_Text noStorePackage = CreateText(authenticated, fontSource, "NoStorePackageText",
                "未安装 IAP Store Package", 30f, s_MutedColor, 320f);
            RectTransform noStoreRect = noStorePackage.rectTransform;
            noStoreRect.anchorMin = Vector2.zero;
            noStoreRect.anchorMax = Vector2.one;
            noStoreRect.offsetMin = new Vector2(30f, 30f);
            noStoreRect.offsetMax = new Vector2(-30f, -112f);
            noStorePackage.alignment = TextAlignmentOptions.Center;
            noStorePackage.gameObject.SetActive(false);

            PanelParts mobile = BuildMobilePanel(authenticated, fontSource, mobilePanelType);
            PanelParts thirdPay = BuildThirdPayPanel(authenticated, fontSource, thirdPayPanelType);
            VoucherPanelParts voucher = BuildVoucherPanel(authenticated, fontSource, voucherPanelType);

            thirdPay.Root.gameObject.SetActive(false);
            voucher.Root.gameObject.SetActive(false);

            Assign(view, "m_LoginButton", loginButton);
            Assign(view, "m_AuthenticatedContent", authenticated.gameObject);
            Assign(view, "m_MobileTabButton", mobileTab.Button);
            Assign(view, "m_ThirdPayTabButton", thirdPayTab.Button);
            Assign(view, "m_VoucherTabButton", voucherTab.Button);
            Assign(view, "m_MobileTabIndicator", mobileTab.Indicator);
            Assign(view, "m_ThirdPayTabIndicator", thirdPayTab.Indicator);
            Assign(view, "m_VoucherTabIndicator", voucherTab.Indicator);
            Assign(view, "m_MobilePanel", mobile.Component);
            Assign(view, "m_ThirdPayPanel", thirdPay.Component);
            Assign(view, "m_VoucherPanel", voucher.Component);
            Assign(view, "m_NoStorePackageText", noStorePackage);

            authenticated.gameObject.SetActive(false);
        }

        /// <summary>
        /// 构建移动商店状态卡、恢复订阅按钮和独立商品列表。
        /// </summary>
        /// <param name="parent">认证内容根节点。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="panelType">移动 Panel 组件类型。</param>
        /// <returns>移动 Panel 构建结果。</returns>
        private static PanelParts BuildMobilePanel(Transform parent, TMP_Text fontSource, Type panelType)
        {
            ScrollPanelParts panel = CreatePanelRoot(parent, "MobilePanel", 640f);
            Component component = panel.Root.gameObject.AddComponent(panelType);
            RectTransform status = CreateSection(panel.Content, "StoreStatusSection", 340f, s_PanelColor);
            CreateSectionHeader(status, fontSource, "移动商店", "DemoIAPMobilePanelView.cs");
            TMP_Text store = CreateText(status, fontSource, "StoreText", "当前商店：等待登录", 24f, Color.black, 44f);
            TMP_Text capability = CreateText(status, fontSource, "CapabilityText", "官方支付能力：等待检测", 24f, Color.black, 44f);
            TMP_Text productStatus = CreateText(status, fontSource, "ProductStatusText", "商品状态：等待同步", 24f, Color.black, 44f);
            Button restore = CreateActionButton(status, fontSource, "RestorePurchasesButton", "恢复订阅",
                "RestorePurchasesAsync", 108f, ActionButtonStyle.Secondary);
            CreateText(panel.Content, fontSource, "ProductsTitle", "移动商品", 28f, Color.black, 48f);
            ProductListParts products = CreateProductList(panel.Content, fontSource, "移动支付", "PayAsync<IAPResult>");

            var serialized = new SerializedObject(component);
            Assign(serialized, "m_ScrollRect", panel.ScrollRect);
            Assign(serialized, "m_StoreText", store);
            Assign(serialized, "m_CapabilityText", capability);
            Assign(serialized, "m_ProductStatusText", productStatus);
            Assign(serialized, "m_RestorePurchasesButton", restore);
            Assign(serialized, "m_ProductList", products.List);
            Assign(serialized, "m_ProductCardTemplate", products.Template);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return new PanelParts(panel.Root, component);
        }

        /// <summary>
        /// 构建第三方支付诊断卡、刷新按钮和独立商品列表。
        /// </summary>
        /// <param name="parent">认证内容根节点。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="panelType">第三方 Panel 组件类型。</param>
        /// <returns>第三方 Panel 构建结果。</returns>
        private static PanelParts BuildThirdPayPanel(Transform parent, TMP_Text fontSource, Type panelType)
        {
            ScrollPanelParts panel = CreatePanelRoot(parent, "ThirdPayPanel", 820f);
            Component component = panel.Root.gameObject.AddComponent(panelType);
            RectTransform status = CreateSection(panel.Content, "StoreStatusSection", 520f, s_PanelColor);
            CreateSectionHeader(status, fontSource, "第三方支付商店", "DemoIAPThirdPayPanelView.cs");
            TMP_Text summary = CreateText(status, fontSource, "StatusText",
                "当前商店：等待登录\n开放 Store：等待检测\n支付方式：等待服务端\n白名单：未公开/待接入\n黑名单：未公开/待接入\nGoogle 外部内容链：等待检测\n第三方支付资格：等待检测",
                23f, Color.black, 300f);
            Button refresh = CreateActionButton(status, fontSource, "RefreshButton", "刷新资格",
                "RefreshThirdPayAsync", 108f, ActionButtonStyle.Secondary);
            CreateText(panel.Content, fontSource, "ProductsTitle", "第三方商品", 28f, Color.black, 48f);
            ProductListParts products = CreateProductList(panel.Content, fontSource, "第三方支付", "IAPThirdPayRequest → OpenURL");

            var serialized = new SerializedObject(component);
            Assign(serialized, "m_ScrollRect", panel.ScrollRect);
            Assign(serialized, "m_StatusText", summary);
            Assign(serialized, "m_RefreshButton", refresh);
            Assign(serialized, "m_ProductList", products.List);
            Assign(serialized, "m_ProductCardTemplate", products.Template);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return new PanelParts(panel.Root, component);
        }

        /// <summary>
        /// 构建金券钱包、当前拥有、模拟发放和独立商品列表。
        /// </summary>
        /// <param name="parent">认证内容根节点。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="panelType">金券 Panel 组件类型。</param>
        /// <returns>金券 Panel 构建结果。</returns>
        private static VoucherPanelParts BuildVoucherPanel(Transform parent, TMP_Text fontSource, Type panelType)
        {
            ScrollPanelParts panel = CreatePanelRoot(parent, "VoucherPanel", 1120f);
            Component component = panel.Root.gameObject.AddComponent(panelType);

            RectTransform wallet = CreateSection(panel.Content, "WalletSection", 240f, s_PanelColor);
            CreateSectionHeader(wallet, fontSource, "金券钱包", "DemoIAPVoucherPanelView.cs");
            TMP_Text status = CreateText(wallet, fontSource, "StatusText", "钱包状态：未就绪", 24f, Color.black, 44f);
            Button refresh = CreateActionButton(wallet, fontSource, "RefreshButton", "刷新钱包",
                "RefreshWalletAsync", 108f, ActionButtonStyle.Secondary);

            RectTransform balances = CreateSection(panel.Content, "BalancesSection", 300f, s_SubPanelColor);
            CreateText(balances, fontSource, "Title", "当前拥有", 28f, Color.black, 46f);
            RectTransform voucherBalances = CreateDynamicList(balances, "VoucherBalances", 58f, 6f);
            TMP_Text voucherRow = CreateText(voucherBalances, fontSource, "BalanceRowTemplate", "兑换券 #1001 · 面值 6.00    × 0", 22f, Color.black, 52f);
            voucherRow.gameObject.SetActive(false);
            RectTransform coinBalances = CreateDynamicList(balances, "CoinBalances", 58f, 6f);
            TMP_Text coinRow = CreateText(coinBalances, fontSource, "BalanceRowTemplate", "代币 #2001 · 面值 0.01    × 0", 22f, Color.black, 52f);
            coinRow.gameObject.SetActive(false);

            RectTransform grant = CreateSection(panel.Content, "TestGrantSection", 390f, s_SubPanelColor);
            CreateText(grant, fontSource, "Title", "模拟添加兑换券 / 代币", 28f, Color.black, 48f);
            Button grantType = CreateActionButton(grant, fontSource, "GrantTypeButton", "发放：兑换券",
                "VoucherTestGrantRequest", 92f, ActionButtonStyle.Secondary);
            RectTransform inputs = CreateHorizontalContainer(grant, "GrantInputs", 78f, 12f);
            TMP_InputField grantId = CreateInput(inputs, fontSource, "GrantIdInput", "资产 ID");
            TMP_InputField quantity = CreateInput(inputs, fontSource, "GrantQuantityInput", "数量");
            Button testGrant = CreateActionButton(grant, fontSource, "TestGrantButton", "模拟发放",
                "TestGrantAsync", 108f, ActionButtonStyle.Primary);

            CreateText(panel.Content, fontSource, "ProductsTitle", "金券商品", 28f, Color.black, 48f);
            ProductListParts products = CreateProductList(panel.Content, fontSource, "金券支付", "Quote → IAPVoucherRequest");

            var serialized = new SerializedObject(component);
            Assign(serialized, "m_ScrollRect", panel.ScrollRect);
            Assign(serialized, "m_StatusText", status);
            Assign(serialized, "m_RefreshButton", refresh);
            Assign(serialized, "m_VoucherBalanceContent", voucherBalances);
            Assign(serialized, "m_VoucherBalanceRowTemplate", voucherRow);
            Assign(serialized, "m_CoinBalanceContent", coinBalances);
            Assign(serialized, "m_CoinBalanceRowTemplate", coinRow);
            Assign(serialized, "m_GrantTypeButton", grantType);
            Assign(serialized, "m_GrantIdInput", grantId);
            Assign(serialized, "m_GrantQuantityInput", quantity);
            Assign(serialized, "m_TestGrantButton", testGrant);
            Assign(serialized, "m_ProductList", products.List);
            Assign(serialized, "m_ProductCardTemplate", products.Template);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return new VoucherPanelParts(panel.Root, component);
        }

        /// <summary>
        /// 创建 Panel 顶部标题与右侧脚本标签。
        /// </summary>
        /// <param name="parent">状态卡根节点。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="title">标题。</param>
        /// <param name="scriptName">Panel 脚本名。</param>
        private static void CreateSectionHeader(Transform parent, TMP_Text fontSource, string title, string scriptName)
        {
            RectTransform header = CreateHorizontalContainer(parent, "Header", 52f, 10f);
            TMP_Text titleText = CreateText(header, fontSource, "Title", title, 28f, Color.black, 52f);
            titleText.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_Text tag = CreateText(header, fontSource, "ScriptTag", scriptName, 16f, s_ApiHintColor, 52f);
            tag.alignment = TextAlignmentOptions.MidlineRight;
            LayoutElement tagLayout = tag.gameObject.GetComponent<LayoutElement>();
            tagLayout.preferredWidth = 285f;
            tagLayout.flexibleWidth = 0f;
        }

        /// <summary>
        /// 创建商店独立商品列表和隐藏商品卡模板。
        /// </summary>
        /// <param name="parent">Panel 根节点。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="buttonLabel">支付按钮文案。</param>
        /// <param name="apiHint">支付 API Hint。</param>
        /// <returns>商品列表与模板。</returns>
        private static ProductListParts CreateProductList(Transform parent, TMP_Text fontSource, string buttonLabel, string apiHint)
        {
            RectTransform list = CreateDynamicList(parent, "ProductList", 80f, 12f);
            RectTransform template = CreateSection(list, "ProductCardTemplate", 230f, Color.white);
            CreateText(template, fontSource, "ProductTitle", "ID1  [普通]  价格未知", 26f, Color.black, 48f);
            CreateText(template, fontSource, "ProductMeta", "商品信息等待刷新", 18f, s_MutedColor, 34f);
            CreateActionButton(template, fontSource, "PayButton", buttonLabel, apiHint, 108f, ActionButtonStyle.Primary);
            template.gameObject.SetActive(false);
            return new ProductListParts(list, template);
        }

        /// <summary>
        /// 创建深蓝激活态、灰蓝未激活态的固定 Tab 按钮。
        /// </summary>
        /// <param name="parent">Tab 栏。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="name">节点名称。</param>
        /// <param name="label">Tab 文案。</param>
        /// <param name="selected">是否默认选中。</param>
        /// <returns>Tab 按钮与指示条。</returns>
        private static TabParts CreateTabButton(Transform parent, TMP_Text fontSource, string name, string label, bool selected)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, 92f);
            rect.gameObject.AddComponent<Image>().color = selected ? s_PrimaryColor : s_InactiveTabColor;
            Button button = rect.gameObject.AddComponent<Button>();
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.preferredHeight = 92f;
            layout.flexibleWidth = 1f;

            TMP_Text text = CreateText(rect, fontSource, "Text", label, 24f,
                selected ? Color.white : s_InactiveTextColor, 0f);
            Stretch((RectTransform)text.transform);
            text.alignment = TextAlignmentOptions.Center;

            RectTransform indicator = CreateRect(rect, "ActiveIndicator");
            indicator.anchorMin = new Vector2(0f, 0f);
            indicator.anchorMax = new Vector2(1f, 0f);
            indicator.pivot = new Vector2(0.5f, 0f);
            indicator.offsetMin = new Vector2(18f, 5f);
            indicator.offsetMax = new Vector2(-18f, 11f);
            indicator.gameObject.AddComponent<Image>().color = selected ? Color.white : s_PrimaryColor;
            indicator.gameObject.SetActive(selected);
            return new TabParts(button, indicator.gameObject);
        }

        /// <summary>
        /// 创建填充 Tab 下方区域、拥有独立 Viewport 与 Content 的滚动 Panel。
        /// </summary>
        /// <param name="parent">认证内容根节点。</param>
        /// <param name="name">节点名称。</param>
        /// <param name="minimumHeight">最小高度。</param>
        /// <returns>Panel 根节点、滚动组件与内容节点。</returns>
        private static ScrollPanelParts CreatePanelRoot(Transform parent, string name, float minimumHeight)
        {
            RectTransform root = CreateRect(parent, name);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = new Vector2(0f, -106f);
            root.gameObject.AddComponent<Image>().color = s_PageColor;
            ScrollRect scrollRect = root.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.inertia = true;
            scrollRect.scrollSensitivity = 36f;

            RectTransform viewport = CreateRect(root, "Viewport");
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = s_PageColor;
            viewport.gameObject.AddComponent<RectMask2D>();

            RectTransform content = CreateDynamicList(viewport, "Content", minimumHeight, 14f);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, minimumHeight);
            VerticalLayoutGroup contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(4, 4, 4, 20);

            scrollRect.viewport = viewport;
            scrollRect.content = content;
            return new ScrollPanelParts(root, content, scrollRect);
        }

        /// <summary>
        /// 创建带垂直布局的纯色内容卡。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="name">节点名称。</param>
        /// <param name="height">设计高度。</param>
        /// <param name="color">背景色。</param>
        /// <returns>内容卡 RectTransform。</returns>
        private static RectTransform CreateSection(Transform parent, string name, float height, Color color)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            rect.gameObject.AddComponent<Image>().color = color;
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return rect;
        }

        /// <summary>
        /// 创建由内容高度驱动的垂直动态列表。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="name">节点名称。</param>
        /// <param name="minimumHeight">最小高度。</param>
        /// <param name="spacing">子项间距。</param>
        /// <returns>动态列表 RectTransform。</returns>
        private static RectTransform CreateDynamicList(Transform parent, string name, float minimumHeight, float spacing)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, minimumHeight);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rect.gameObject.AddComponent<LayoutElement>().minHeight = minimumHeight;
            return rect;
        }

        /// <summary>
        /// 创建横向布局容器。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="name">节点名称。</param>
        /// <param name="height">设计高度。</param>
        /// <param name="spacing">子项间距。</param>
        /// <returns>横向容器。</returns>
        private static RectTransform CreateHorizontalContainer(Transform parent, string name, float height, float spacing)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            return rect;
        }

        /// <summary>
        /// 创建 A 配色中的深蓝主按钮或浅蓝次级按钮。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="name">节点名称。</param>
        /// <param name="label">按钮文案。</param>
        /// <param name="apiHint">API Hint。</param>
        /// <param name="height">按钮高度。</param>
        /// <param name="style">按钮视觉层级。</param>
        /// <returns>按钮组件。</returns>
        private static Button CreateActionButton(Transform parent, TMP_Text fontSource, string name, string label,
            string apiHint, float height, ActionButtonStyle style)
        {
            RectTransform rect = CreateRect(parent, name);
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            bool primary = style == ActionButtonStyle.Primary;
            rect.gameObject.AddComponent<Image>().color = primary ? s_PrimaryColor : s_SecondaryColor;
            Button button = rect.gameObject.AddComponent<Button>();
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = 1f;

            TMP_Text text = CreateText(rect, fontSource, "Text", label, 28f,
                primary ? Color.white : s_PrimaryColor, 0f);
            RectTransform textRect = (RectTransform)text.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(6f, 18f);
            textRect.offsetMax = new Vector2(-6f, -4f);
            text.alignment = TextAlignmentOptions.Center;

            TMP_Text hint = CreateText(rect, fontSource, "ApiHintText", apiHint, 18f,
                primary ? s_SecondaryColor : s_PrimaryColor, 0f);
            RectTransform hintRect = (RectTransform)hint.transform;
            hintRect.anchorMin = Vector2.zero;
            hintRect.anchorMax = new Vector2(1f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 6f);
            hintRect.sizeDelta = new Vector2(0f, 24f);
            hint.alignment = TextAlignmentOptions.Center;
            return button;
        }

        /// <summary>
        /// 创建仅接受整数的 TMP 输入框。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="name">节点名称。</param>
        /// <param name="placeholderText">占位文案。</param>
        /// <returns>TMP 输入框。</returns>
        private static TMP_InputField CreateInput(Transform parent, TMP_Text fontSource, string name, string placeholderText)
        {
            RectTransform root = CreateRect(parent, name);
            root.gameObject.AddComponent<Image>().color = Color.white;
            root.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
            input.contentType = TMP_InputField.ContentType.IntegerNumber;

            RectTransform viewport = CreateRect(root, "TextArea");
            Stretch(viewport);
            viewport.offsetMin = new Vector2(12f, 6f);
            viewport.offsetMax = new Vector2(-12f, -6f);
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text placeholder = CreateText(viewport, fontSource, "Placeholder", placeholderText, 24f, new Color(0f, 0f, 0f, 0.45f), 0f);
            TMP_Text value = CreateText(viewport, fontSource, "Text", string.Empty, 24f, Color.black, 0f);
            Stretch((RectTransform)placeholder.transform);
            Stretch((RectTransform)value.transform);
            placeholder.fontStyle = FontStyles.Italic;
            input.textViewport = viewport;
            input.placeholder = placeholder;
            input.textComponent = value;
            return input;
        }

        /// <summary>
        /// 创建 TMP 文本并复用当前 Demo 字体。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="fontSource">字体来源文本。</param>
        /// <param name="name">节点名称。</param>
        /// <param name="value">显示文案。</param>
        /// <param name="fontSize">字号。</param>
        /// <param name="color">颜色。</param>
        /// <param name="height">设计高度；零表示不加入布局高度。</param>
        /// <returns>TMP 文本。</returns>
        private static TMP_Text CreateText(Transform parent, TMP_Text fontSource, string name, string value, float fontSize, Color color, float height)
        {
            RectTransform rect = CreateRect(parent, name);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            if (fontSource != null && fontSource.font != null)
            {
                text.font = fontSource.font;
            }
            if (height > 0f)
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
                rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            }
            return text;
        }

        /// <summary>
        /// 创建基础 RectTransform 节点。
        /// </summary>
        /// <param name="parent">父节点。</param>
        /// <param name="name">节点名称。</param>
        /// <returns>新节点 RectTransform。</returns>
        private static RectTransform CreateRect(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>
        /// 将 RectTransform 拉伸填满父节点。
        /// </summary>
        /// <param name="rect">目标 RectTransform。</param>
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// 配置根 Content 填满交互区，并关闭外层滚动以固定认证后的 Tab 栏。
        /// </summary>
        /// <param name="content">交互区 Content。</param>
        private static void ConfigureRootContent(RectTransform content)
        {
            Stretch(content);
            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            }
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.spacing = 14f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null)
            {
                UnityEngine.Object.DestroyImmediate(fitter);
            }

            ScrollRect outerScrollRect = content.GetComponentInParent<ScrollRect>();
            if (outerScrollRect != null)
            {
                outerScrollRect.horizontal = false;
                outerScrollRect.vertical = false;
                outerScrollRect.StopMovement();
            }
        }

        /// <summary>
        /// 删除 Variant 交互区旧内容，避免保留历史共用商品结构。
        /// </summary>
        /// <param name="parent">交互区 Content。</param>
        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// 为序列化字段赋 Unity 对象引用，字段缺失时立即失败。
        /// </summary>
        /// <param name="serializedObject">目标序列化对象。</param>
        /// <param name="propertyName">字段名称。</param>
        /// <param name="value">Unity 对象引用。</param>
        private static void Assign(SerializedObject serializedObject, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new MissingFieldException(serializedObject.targetObject.GetType().FullName, propertyName);
            }
            property.objectReferenceValue = value;
        }

        /// <summary>
        /// 从已加载程序集解析必需类型。
        /// </summary>
        /// <param name="fullName">类型全名。</param>
        /// <returns>已解析类型。</returns>
        private static Type RequireType(string fullName)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            throw new InvalidOperationException("未找到类型，请先完成脚本编译：" + fullName);
        }

        /// <summary>
        /// 业务按钮的视觉层级。
        /// </summary>
        private enum ActionButtonStyle
        {
            /// <summary>
            /// 深蓝底白字的主操作。
            /// </summary>
            Primary,

            /// <summary>
            /// 浅蓝底深蓝字的辅助操作。
            /// </summary>
            Secondary,
        }

        /// <summary>
        /// 独立滚动 Panel 的构建结果。
        /// </summary>
        private readonly struct ScrollPanelParts
        {
            internal readonly RectTransform Root;
            internal readonly RectTransform Content;
            internal readonly ScrollRect ScrollRect;

            /// <summary>
            /// 创建独立滚动 Panel 构建结果。
            /// </summary>
            /// <param name="root">Panel 根节点。</param>
            /// <param name="content">Panel 内容节点。</param>
            /// <param name="scrollRect">Panel 滚动组件。</param>
            internal ScrollPanelParts(RectTransform root, RectTransform content, ScrollRect scrollRect)
            {
                Root = root;
                Content = content;
                ScrollRect = scrollRect;
            }
        }

        /// <summary>
        /// Tab 按钮和选中指示条构建结果。
        /// </summary>
        private readonly struct TabParts
        {
            internal readonly Button Button;
            internal readonly GameObject Indicator;

            /// <summary>
            /// 创建 Tab 构建结果。
            /// </summary>
            /// <param name="button">Tab 按钮。</param>
            /// <param name="indicator">选中指示条。</param>
            internal TabParts(Button button, GameObject indicator)
            {
                Button = button;
                Indicator = indicator;
            }
        }

        /// <summary>
        /// 通用 Panel 构建结果。
        /// </summary>
        private class PanelParts
        {
            internal readonly RectTransform Root;
            internal readonly Component Component;

            /// <summary>
            /// 创建 Panel 构建结果。
            /// </summary>
            /// <param name="root">Panel 根节点。</param>
            /// <param name="component">Panel 组件。</param>
            internal PanelParts(RectTransform root, Component component)
            {
                Root = root;
                Component = component;
            }
        }

        /// <summary>
        /// 金券 Panel 构建结果。
        /// </summary>
        private sealed class VoucherPanelParts : PanelParts
        {
            /// <summary>
            /// 创建金券 Panel 构建结果。
            /// </summary>
            /// <param name="root">Panel 根节点。</param>
            /// <param name="component">Panel 组件。</param>
            internal VoucherPanelParts(RectTransform root, Component component) : base(root, component)
            {
            }
        }

        /// <summary>
        /// 商品列表和商品卡模板构建结果。
        /// </summary>
        private readonly struct ProductListParts
        {
            internal readonly RectTransform List;
            internal readonly RectTransform Template;

            /// <summary>
            /// 创建商品列表构建结果。
            /// </summary>
            /// <param name="list">商品列表。</param>
            /// <param name="template">商品卡模板。</param>
            internal ProductListParts(RectTransform list, RectTransform template)
            {
                List = list;
                Template = template;
            }
        }
    }
}
