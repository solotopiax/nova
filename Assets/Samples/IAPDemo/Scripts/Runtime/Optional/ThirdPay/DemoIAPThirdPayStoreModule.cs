/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPThirdPayStoreModule.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   ThirdPay package 存在时启用的第三方支付 Demo 适配模块
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.SDK.IAP.ThirdPay.Runtime;
using UnityEngine.Scripting;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 将 ThirdPay 强类型调用隔离在受 package 版本宏控制的独立程序集内。
    /// </summary>
    [Preserve]
    internal sealed class DemoIAPThirdPayStoreModule : IDemoIAPStoreModule, IDemoIAPStoreSelectionHandler
    {
        /// <summary>
        /// 不依赖 ThirdPay package 的 Core Bridge。
        /// </summary>
        private DemoIAPBridge m_Bridge;

        /// <summary>
        /// Prefab 中序列化的第三方支付 Panel 壳。
        /// </summary>
        private DemoIAPThirdPayPanelView m_Panel;

        /// <summary>
        /// 最近一次成功刷新得到的服务端本地价格，失败时继续用于商品卡展示。
        /// </summary>
        private readonly Dictionary<long, string> m_ProductPrices = new Dictionary<long, string>();

        /// <summary>
        /// 获取第三方支付商店类型。
        /// </summary>
        public DemoIAPStoreKind Kind => DemoIAPStoreKind.ThirdPay;

        /// <summary>
        /// 注入 Core Bridge 与第三方支付 Panel 壳，并绑定业务回调。
        /// </summary>
        /// <param name="context">商店模块初始化上下文。</param>
        public void Initialize(DemoIAPStoreContext context)
        {
            m_Bridge = context.Bridge;
            m_Panel = context.Panel as DemoIAPThirdPayPanelView;
            if (m_Panel == null)
            {
                throw new InvalidOperationException("ThirdPay 模块未取得 DemoIAPThirdPayPanelView。");
            }

            m_Panel.Configure(BuildProductTitle, tableId => PayAsync(tableId).Forget(), HandleDebugCountryChanged, HandleSkipPaymentInformationChanged);
        }

        /// <summary>
        /// 创建第三方支付演示商品卡。
        /// </summary>
        public void BuildProducts()
        {
            m_Panel?.BuildProducts();
        }

        /// <summary>
        /// 第三方支付 Tab 被选中时展示自动请求取得的统一支付配置。
        /// </summary>
        /// <returns>刷新结束的异步任务。</returns>
        public UniTask OnSelectedAsync()
        {
            return RefreshAsync();
        }

        /// <summary>
        /// 展示自动请求取得的第三方支付开关、关闭原因和商品 SKU 数量。
        /// </summary>
        /// <returns>异步任务。</returns>
        public UniTask RefreshAsync()
        {
            var status = new ThirdPayStatus();

            if (!TryGetThirdPayCapability(out IIAPThirdPayConfigCapable configCapability) || !TryGetThirdPayCapability(out IIAPThirdPayProductCapable productCapability) || !TryGetThirdPayCapability(out IIAPThirdPayCheckoutCapable checkoutCapability))
            {
                m_Panel?.SetStatusText(status.ToDisplayText());
                m_Bridge?.AppendFeedback("当前 IAP 插件未暴露 ThirdPay 能力。", FeedbackLevel.Warn);
                return UniTask.CompletedTask;
            }

            try
            {
                m_Panel?.SetSkipPaymentInformation(checkoutCapability.IsPaymentInformationScreenSkipped);
                status.ConfigReady = configCapability.IsPaymentConfigReady;
                status.PaymentEnabled = status.ConfigReady && configCapability.IsPaymentAvailable;
                status.DisabledReason = status.ConfigReady ? configCapability.PaymentDisabledReason : 0;
                status.ProductSkuCount = productCapability.GetProductList().Count;
                if (status.ProductSkuCount > 0)
                {
                    RefreshProductPrices(productCapability);
                    m_Panel?.RefreshProductTitles();
                }

                if (!status.ConfigReady)
                {
                    m_Bridge.AppendFeedback("第三方支付配置尚未就绪，等待登录或国家切换后的内部预取。", FeedbackLevel.Warn);
                }
                else if (!status.PaymentEnabled)
                {
                    m_Bridge.AppendFeedback("第三方支付未开启，失败原因码：" + status.DisabledReason, FeedbackLevel.Warn);
                }
                else
                {
                    m_Bridge.AppendFeedback("第三方支付配置响应已展示，商品 SKU 数量：" + status.ProductSkuCount, FeedbackLevel.Success);
                }
            }
            catch (Exception exception)
            {
                m_Bridge.AppendFeedback("第三方支付配置响应展示失败：" + exception.Message, FeedbackLevel.Error);
            }

            m_Panel?.SetStatusText(status.ToDisplayText());
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 设置第三方支付 Panel 的业务按钮交互状态。
        /// </summary>
        /// <param name="interactable">是否允许交互。</param>
        public void SetInteractable(bool interactable)
        {
            m_Panel?.SetInteractable(interactable);
        }

        /// <summary>
        /// 将第三方支付 Panel 复位到顶部。
        /// </summary>
        public void ResetScrollPosition()
        {
            m_Panel?.ResetScrollPosition();
        }

        /// <summary>
        /// 清理第三方支付商品卡和模块引用。
        /// </summary>
        public void ClearRuntimeContent()
        {
            m_Panel?.ClearRuntimeContent();
            m_ProductPrices.Clear();
            m_Panel = null;
            m_Bridge = null;
        }

        /// <summary>
        /// 应用 Demo 调试国家选择；商品列表由底层 SetDebugCountryCode 自动刷新。
        /// </summary>
        /// <param name="countryCode">ISO 3166-1 alpha-2 国家代码；空值表示恢复自动识别。</param>
        private void HandleDebugCountryChanged(string countryCode)
        {
            if (!TryGetThirdPayCapability(out IIAPThirdPayConfigCapable capability))
            {
                m_Bridge?.AppendFeedback("ThirdPay 能力不可用，无法设置调试国家。", FeedbackLevel.Warn);
                return;
            }

            capability.SetDebugCountryCode(countryCode);
            m_Bridge.AppendFeedback(
                "ThirdPay 调试国家已设置为：" + (string.IsNullOrEmpty(countryCode) ? "Auto" : countryCode) + "，商品列表将由 Store 内部重新预取。",
                FeedbackLevel.Info);
        }

        /// <summary>
        /// 应用 Demo 调试开关，覆盖 ThirdPayStoreConfig 的默认信息页配置。
        /// </summary>
        /// <param name="skip">是否跳过信息页。</param>
        private void HandleSkipPaymentInformationChanged(bool skip)
        {
            if (!TryGetThirdPayCapability(out IIAPThirdPayCheckoutCapable capability))
            {
                m_Bridge?.AppendFeedback("ThirdPay 能力不可用，无法设置信息页跳过开关。", FeedbackLevel.Warn);
                return;
            }

            capability.SetSkipPaymentInformationScreen(skip);
            m_Bridge.AppendFeedback(
                skip ? "ThirdPay 已设置为跳过信息页。" : "ThirdPay 已恢复信息页流程。",
                FeedbackLevel.Info);
        }

        /// <summary>
        /// 获取当前 ThirdPay 能力，集中处理 Demo 调试控件的运行时依赖检查。
        /// </summary>
        /// <typeparam name="T">需要获取的 ThirdPay 细分能力类型。</typeparam>
        /// <param name="capability">当前 ThirdPay 细分能力。</param>
        /// <returns>成功取得能力时返回 true。</returns>
        private bool TryGetThirdPayCapability<T>(out T capability) where T : class, IIAPCapable
        {
            capability = null;
            return m_Bridge != null && m_Bridge.TryInitialize()
                   && m_Bridge.IAP.TryGetCapability(out capability);
        }

        /// <summary>
        /// 创建 ThirdPay 请求并进入框架第三方支付 URL 流程。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>异步任务。</returns>
        private async UniTask PayAsync(long tableId)
        {
            if (!TryGetThirdPayCapability(out IIAPThirdPayConfigCapable _))
            {
                m_Bridge?.AppendFeedback("ThirdPay 能力不可用。", FeedbackLevel.Warn);
                return;
            }

            m_Bridge.SetPayInteractable(false);
            try
            {
                var request = new IAPThirdPayRequest
                {
                    TableId = tableId,
                    CustomData = DemoIAPBridge.BuildCustomData(tableId),
                    ReceiptParam = tableId.ToString(),
                };
                IAPResult result = await m_Bridge.IAP.PayAsync<IAPResult>(request, m_Bridge.CancellationToken);
                m_Bridge.AppendFeedback("第三方支付 URL 流程结束：" + DemoIAPBridge.FormatResult(result),
                    result != null && result.IsSuccess ? FeedbackLevel.Success : FeedbackLevel.Info);
            }
            catch (OperationCanceledException)
            {
                m_Bridge.AppendFeedback("第三方支付已取消。", FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                m_Bridge.AppendFeedback("第三方支付失败：" + exception.Message, FeedbackLevel.Error);
            }
            finally
            {
                m_Bridge.SetPayInteractable(!m_Bridge.IsDisposed);
            }
        }

        /// <summary>
        /// 使用基础商品表价格构建第三方商品标题。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>商品卡标题。</returns>
        private string BuildProductTitle(long tableId)
        {
            string group = DemoIAPProductCatalog.GetGroupLabel(tableId);
            if (m_ProductPrices.TryGetValue(tableId, out string price))
            {
                return "ID" + tableId + DemoIAPBridge.FormatGroupLabel(group) + "  " + price;
            }

            return m_Bridge?.BuildProductButtonText(tableId, group)
                   ?? "ID" + tableId + DemoIAPBridge.FormatGroupLabel(group);
        }

        /// <summary>
        /// 使用本次成功响应替换服务端价格快照，缺失或不完整的商品回退到基础商品表价格。
        /// </summary>
        /// <param name="capability">ThirdPay 商品查询能力。</param>
        private void RefreshProductPrices(IIAPThirdPayProductCapable capability)
        {
            var refreshedPrices = new Dictionary<long, string>();
            for (int i = 0; i < DemoIAPProductCatalog.AllProductIds.Length; i++)
            {
                long tableId = DemoIAPProductCatalog.AllProductIds[i];
                PbNetThirdProductInfo product = capability.GetProductInfo(tableId);
                if (product == null || string.IsNullOrEmpty(product.LocalPrice)
                    || string.IsNullOrEmpty(product.LocalCurrency))
                {
                    continue;
                }

                refreshedPrices[tableId] = product.LocalPrice + " " + product.LocalCurrency;
            }

            m_ProductPrices.Clear();
            foreach (KeyValuePair<long, string> pair in refreshedPrices)
            {
                m_ProductPrices.Add(pair.Key, pair.Value);
            }
        }

        /// <summary>
        /// 第三方支付 Panel 使用的精简配置状态。
        /// </summary>
        private sealed class ThirdPayStatus
        {
            /// <summary>
            /// 是否已经取得有效的统一支付配置。
            /// </summary>
            internal bool ConfigReady;

            /// <summary>
            /// 服务端是否允许当前用户发起第三方支付。
            /// </summary>
            internal bool PaymentEnabled;

            /// <summary>
            /// 服务端返回的第三方支付关闭原因码。
            /// </summary>
            internal int DisabledReason;

            /// <summary>
            /// 当前统一配置中返回的商品 SKU 数量。
            /// </summary>
            internal int ProductSkuCount;

            /// <summary>
            /// 将配置状态格式化为仅包含开关、失败原因和 SKU 数量的多行文本。
            /// </summary>
            /// <returns>第三方支付状态文本。</returns>
            internal string ToDisplayText()
            {
                string text = "第三方支付：" + (ConfigReady && PaymentEnabled ? "已开启" : "未开启");
                if (!ConfigReady)
                {
                    text += "\n失败原因：未获取到有效支付配置";
                }
                else if (!PaymentEnabled)
                {
                    text += "\n失败原因码：" + DisabledReason;
                }

                return text + "\n商品 SKU 数量：" + ProductSkuCount;
            }
        }
    }
}
