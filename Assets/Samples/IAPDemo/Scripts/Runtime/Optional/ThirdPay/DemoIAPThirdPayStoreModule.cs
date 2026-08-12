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
using UnityEngine;
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
        /// 当前成功发现的可选支付商店列表。
        /// </summary>
        private IReadOnlyList<DemoIAPStoreKind> m_AvailableStores;

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
            m_AvailableStores = context.AvailableStores;
            if (m_Panel == null)
            {
                throw new InvalidOperationException("ThirdPay 模块未取得 DemoIAPThirdPayPanelView。");
            }

            m_Panel.Configure(BuildProductTitle, tableId => PayAsync(tableId).Forget(),
                () => RefreshAsync().Forget());
        }

        /// <summary>
        /// 创建第三方支付演示商品卡。
        /// </summary>
        public void BuildProducts()
        {
            m_Panel?.BuildProducts();
        }

        /// <summary>
        /// 第三方支付 Tab 被选中时重新请求商品。
        /// </summary>
        /// <returns>刷新结束的异步任务。</returns>
        public UniTask OnSelectedAsync()
        {
            return RefreshAsync();
        }

        /// <summary>
        /// 刷新服务端第三方支付商品和当前账号资格。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async UniTask RefreshAsync()
        {
            var status = new ThirdPayStatus
            {
                RuntimeStore = GetRuntimeStoreName(),
                OpenStores = BuildOpenStoreText(),
                WhiteList = "未公开/待接入",
                BlackList = "未公开/待接入",
                GooglePolicy = Application.platform == RuntimePlatform.Android
                    ? "不跳过，执行政策校验"
                    : "跳过（非 Android）",
            };

            if (m_Bridge == null || !m_Bridge.TryInitialize()
                || !m_Bridge.IAP.TryGetCapability(out IIAPThirdPayCapable capability))
            {
                m_Panel?.SetStatusText(status.ToDisplayText());
                m_Bridge?.AppendFeedback("当前 IAP 插件未暴露 ThirdPay 能力。", FeedbackLevel.Warn);
                return;
            }

            try
            {
                bool productReady = await capability.FetchProductListAsync(m_Bridge.CancellationToken);
                status.Eligible = productReady;
                if (productReady)
                {
                    RefreshProductPrices(capability);
                    m_Panel?.RefreshProductTitles();
                    m_Bridge.AppendFeedback("第三方支付资格刷新完成：" + (status.Eligible ? "具备资格" : "暂无资格"),
                        status.Eligible ? FeedbackLevel.Success : FeedbackLevel.Warn);
                }
                else
                {
                    m_Bridge.AppendFeedback("第三方支付商品刷新失败，已保留上次价格。", FeedbackLevel.Error);
                }
            }
            catch (OperationCanceledException)
            {
                m_Bridge.AppendFeedback("第三方支付资格刷新已取消。", FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                m_Bridge.AppendFeedback("第三方支付资格刷新失败：" + exception.Message, FeedbackLevel.Error);
            }

            m_Panel?.SetStatusText(status.ToDisplayText());
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
            m_AvailableStores = null;
            m_Panel = null;
            m_Bridge = null;
        }

        /// <summary>
        /// 创建 ThirdPay 请求并进入框架第三方支付 URL 流程。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>异步任务。</returns>
        private async UniTask PayAsync(long tableId)
        {
            if (m_Bridge == null || !m_Bridge.TryInitialize()
                || !m_Bridge.IAP.TryGetCapability(out IIAPThirdPayCapable _))
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
        private void RefreshProductPrices(IIAPThirdPayCapable capability)
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
        /// 将当前成功加载的可选商店模块格式化为开放商店文本。
        /// </summary>
        /// <returns>开放商店列表。</returns>
        private string BuildOpenStoreText()
        {
            if (m_AvailableStores == null || m_AvailableStores.Count == 0)
            {
                return "无";
            }

            var names = new string[m_AvailableStores.Count];
            for (int i = 0; i < m_AvailableStores.Count; i++)
            {
                names[i] = m_AvailableStores[i].ToString();
            }
            return string.Join(" / ", names);
        }

        /// <summary>
        /// 获取当前运行平台对应的官方商店名称。
        /// </summary>
        /// <returns>面向演示界面的商店名称。</returns>
        private static string GetRuntimeStoreName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    return "Google Play";
                case RuntimePlatform.IPhonePlayer:
                    return "Apple App Store";
                default:
                    return "Editor / Unsupported";
            }
        }

        /// <summary>
        /// 第三方支付 Panel 使用的内部诊断快照。
        /// </summary>
        private sealed class ThirdPayStatus
        {
            /// <summary>
            /// 当前运行平台商店名。
            /// </summary>
            internal string RuntimeStore = string.Empty;

            /// <summary>
            /// 当前已加载的支付商店模块。
            /// </summary>
            internal string OpenStores = string.Empty;

            /// <summary>
            /// 白名单公开状态。
            /// </summary>
            internal string WhiteList = string.Empty;

            /// <summary>
            /// 黑名单公开状态。
            /// </summary>
            internal string BlackList = string.Empty;

            /// <summary>
            /// Google 外部内容链政策处理状态。
            /// </summary>
            internal string GooglePolicy = string.Empty;

            /// <summary>
            /// 当前账号是否具备第三方支付资格。
            /// </summary>
            internal bool Eligible;

            /// <summary>
            /// 将诊断快照格式化为 Panel 多行文本。
            /// </summary>
            /// <returns>多行诊断文本。</returns>
            internal string ToDisplayText()
            {
                return "当前商店：" + RuntimeStore
                       + "\n开放 Store：" + OpenStores
                       + "\n白名单：" + WhiteList
                       + "\n黑名单：" + BlackList
                       + "\nGoogle 外部内容链：" + GooglePolicy
                       + "\n第三方支付资格：" + (Eligible ? "具备" : "不具备");
            }
        }
    }
}
