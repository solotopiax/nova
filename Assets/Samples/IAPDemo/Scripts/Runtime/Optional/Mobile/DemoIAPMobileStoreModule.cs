/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPMobileStoreModule.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   Mobile package 存在时启用的移动支付 Demo 适配模块
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine;
using UnityEngine.Scripting;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 将 Mobile 强类型调用隔离在受 package 版本宏控制的独立程序集内。
    /// </summary>
    [Preserve]
    internal sealed class DemoIAPMobileStoreModule : IDemoIAPStoreModule
    {
        /// <summary>
        /// 自定义 ReceiptParam 测试支付固定使用的商品表行 ID。
        /// </summary>
        private const long c_CustomReceiptParamTableId = 1L;

        /// <summary>
        /// 已拉取的平台商品信息缓存。
        /// </summary>
        private readonly Dictionary<long, ProductInfo> m_ProductInfos = new Dictionary<long, ProductInfo>();

        /// <summary>
        /// 不依赖 Mobile package 的 Core Bridge。
        /// </summary>
        private DemoIAPBridge m_Bridge;

        /// <summary>
        /// Prefab 中序列化的移动支付 Panel 壳。
        /// </summary>
        private DemoIAPMobilePanelView m_Panel;

        /// <summary>
        /// 获取移动官方支付商店类型。
        /// </summary>
        public DemoIAPStoreKind Kind => DemoIAPStoreKind.Mobile;

        /// <summary>
        /// 注入 Core Bridge 与移动 Panel 壳，并绑定移动业务回调。
        /// </summary>
        /// <param name="context">商店模块初始化上下文。</param>
        public void Initialize(DemoIAPStoreContext context)
        {
            m_Bridge = context.Bridge;
            m_Panel = context.Panel as DemoIAPMobilePanelView;
            if (m_Panel == null)
            {
                throw new InvalidOperationException("Mobile 模块未取得 DemoIAPMobilePanelView。");
            }

            m_Panel.Configure(BuildProductTitle, tableId => PayAsync(tableId).Forget(),
                receiptParam => PayAsync(c_CustomReceiptParamTableId, receiptParam).Forget(),
                () => RestorePurchasesAsync().Forget());
        }

        /// <summary>
        /// 创建移动商店演示商品卡。
        /// </summary>
        public void BuildProducts()
        {
            m_Panel?.BuildProducts();
        }

        /// <summary>
        /// 刷新移动商店能力和平台商品价格；使用启动时的 Bridge 与取消令牌，失效页面不回写缓存或控件。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async UniTask RefreshAsync()
        {
            DemoIAPBridge bridge = m_Bridge;
            if (!IsCurrentBridge(bridge)) return;
            bool available = TryGetQueryCapability(out IIAPMobileQueryCapable capability);
            m_Panel?.SetStatus(GetRuntimeStoreName(), available, available ? "同步中" : "能力不可用");
            if (!available)
            {
                m_Bridge?.AppendFeedback("当前 IAP Store 未暴露移动端商品查询能力。", FeedbackLevel.Warn);
                return;
            }
            var cancellationToken = bridge.CancellationToken;

            var productIds = new List<string>();
            var productIdToTableId = new Dictionary<string, long>();
            for (int i = 0; i < DemoIAPProductCatalog.AllProductIds.Length; i++)
            {
                long tableId = DemoIAPProductCatalog.AllProductIds[i];
                IAPProductEntry entry = bridge.FindProductEntry(tableId);
                if (entry == null || string.IsNullOrEmpty(entry.ProductID)
                    || productIdToTableId.ContainsKey(entry.ProductID))
                {
                    continue;
                }

                productIds.Add(entry.ProductID);
                productIdToTableId.Add(entry.ProductID, tableId);
            }

            if (productIds.Count == 0)
            {
                m_Panel?.SetStatus(GetRuntimeStoreName(), true, "商品表未配置 ProductID");
                return;
            }

            try
            {
                IReadOnlyList<ProductInfo> infos = await capability.QueryProductsAsync(productIds, cancellationToken);
                if (!IsCurrentBridge(bridge)) return;
                if (infos != null)
                {
                    for (int i = 0; i < infos.Count; i++)
                    {
                        ProductInfo info = infos[i];
                        if (info != null && !string.IsNullOrEmpty(info.ProductId)
                            && productIdToTableId.TryGetValue(info.ProductId, out long tableId))
                        {
                            m_ProductInfos[tableId] = info;
                            m_Panel?.UpdateProductText(tableId, BuildProductTitle(tableId));
                        }
                    }
                }

                int count = infos != null ? infos.Count : 0;
                m_Panel?.SetStatus(GetRuntimeStoreName(), true, "已同步 " + count + " 个");
                bridge.AppendFeedback("移动端商品信息刷新完成：" + count, FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                if (IsCurrentBridge(bridge)) bridge.AppendFeedback("移动端商品信息刷新已取消。", FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                if (IsCurrentBridge(bridge))
                {
                    m_Panel?.SetStatus(GetRuntimeStoreName(), true, "同步失败");
                    bridge.AppendFeedback("移动端商品信息刷新失败：" + exception.Message, FeedbackLevel.Error);
                }
                else Debug.LogException(exception);
            }
        }

        /// <summary>
        /// 设置移动支付 Panel 的业务按钮交互状态。
        /// </summary>
        /// <param name="interactable">是否允许交互。</param>
        public void SetInteractable(bool interactable)
        {
            m_Panel?.SetInteractable(interactable);
        }

        /// <summary>
        /// 将移动支付 Panel 复位到顶部。
        /// </summary>
        public void ResetScrollPosition()
        {
            m_Panel?.ResetScrollPosition();
        }

        /// <summary>
        /// 清理移动商品缓存、商品卡和回调引用。
        /// </summary>
        public void ClearRuntimeContent()
        {
            m_ProductInfos.Clear();
            m_Panel?.ClearRuntimeContent();
            m_Panel = null;
            m_Bridge = null;
        }

        /// <summary>
        /// 发起移动官方商店支付。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>异步任务。</returns>
        private UniTask PayAsync(long tableId)
        {
            return PayAsync(tableId, tableId.ToString());
        }

        /// <summary>
        /// 使用指定 ReceiptParam 发起移动官方商店支付；页面关闭停止本次页面回写，交易处理仍由核心负责。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <param name="receiptParam">票据透传参数。</param>
        /// <returns>异步任务。</returns>
        private async UniTask PayAsync(long tableId, string receiptParam)
        {
            DemoIAPBridge bridge = m_Bridge;
            if (!IsCurrentBridge(bridge) || !bridge.TryInitialize())
            {
                return;
            }

            var cancellationToken = bridge.CancellationToken;
            IAPPlugin iap = bridge.IAP;
            bridge.SetPayInteractable(false);
            try
            {
                var request = new IAPMobileRequest
                {
                    TableId = tableId,
                    ReceiptParam = receiptParam,
                    CustomData = DemoIAPBridge.BuildCustomData(tableId),
                };
                IAPResult result = await iap.PayAsync<IAPResult>(request, cancellationToken);
                if (IsCurrentBridge(bridge))
                    bridge.AppendFeedback("移动支付结果：" + DemoIAPBridge.FormatResult(result),
                        result != null && result.IsSuccess ? FeedbackLevel.Success : FeedbackLevel.Error);
            }
            catch (OperationCanceledException)
            {
                if (IsCurrentBridge(bridge)) bridge.AppendFeedback("移动支付已取消。", FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                if (IsCurrentBridge(bridge)) bridge.AppendFeedback("移动支付调用失败：" + exception.Message, FeedbackLevel.Error);
                else Debug.LogException(exception);
            }
            finally
            {
                if (IsCurrentBridge(bridge)) bridge.SetPayInteractable(true);
            }
        }

        /// <summary>
        /// 恢复移动商店历史订阅和非消耗品购买；持有本次会话令牌，关闭后的结果不再写页面。
        /// </summary>
        /// <returns>异步任务。</returns>
        private async UniTask RestorePurchasesAsync()
        {
            DemoIAPBridge bridge = m_Bridge;
            if (!IsCurrentBridge(bridge) || !bridge.TryInitialize())
            {
                return;
            }

            try
            {
                var cancellationToken = bridge.CancellationToken;
                IReadOnlyList<IAPResult> results = await bridge.IAP.RestorePurchasesAsync<IAPResult>(cancellationToken);
                if (!IsCurrentBridge(bridge)) return;
                int count = results != null ? results.Count : 0;
                bridge.AppendFeedback(count > 0 ? "恢复购买完成：" + count : "恢复购买完成，未找到可恢复订单。",
                    count > 0 ? FeedbackLevel.Success : FeedbackLevel.Info);
            }
            catch (OperationCanceledException)
            {
                if (IsCurrentBridge(bridge)) bridge.AppendFeedback("恢复购买已取消。", FeedbackLevel.Warn);
            }
            catch (Exception exception)
            {
                if (IsCurrentBridge(bridge)) bridge.AppendFeedback("恢复购买失败：" + exception.Message, FeedbackLevel.Error);
                else Debug.LogException(exception);
            }
        }

        /// <summary>
        /// 只允许当前未释放模块会话回写商品缓存、按钮与反馈，旧 Bridge 的交易结果仍由核心处理。
        /// </summary>
        /// <param name="bridge">操作开始时保存的 Bridge。</param>
        /// <returns>模块仍持有同一未释放 Bridge 时返回 true。</returns>
        private bool IsCurrentBridge(DemoIAPBridge bridge)
        {
            return bridge != null && !bridge.IsDisposed && ReferenceEquals(m_Bridge, bridge);
        }

        /// <summary>
        /// 尝试取得 Mobile 商品查询能力。
        /// </summary>
        /// <param name="capability">移动商品查询能力。</param>
        /// <returns>能力可用时返回 true。</returns>
        private bool TryGetQueryCapability(out IIAPMobileQueryCapable capability)
        {
            capability = null;
            return m_Bridge != null && m_Bridge.TryInitialize()
                   && m_Bridge.IAP.TryGetCapability(out capability);
        }

        /// <summary>
        /// 构建移动商品标题，优先使用平台本地化价格。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>商品卡标题。</returns>
        private string BuildProductTitle(long tableId)
        {
            string group = DemoIAPProductCatalog.GetGroupLabel(tableId);
            return m_ProductInfos.TryGetValue(tableId, out ProductInfo info) && info != null
                ? "ID" + tableId + DemoIAPBridge.FormatGroupLabel(group) + "  " + info.LocalizedPrice
                : m_Bridge?.BuildProductButtonText(tableId, group)
                  ?? "ID" + tableId + DemoIAPBridge.FormatGroupLabel(group);
        }

        /// <summary>
        /// 获取当前运行平台对应的官方商店名称。
        /// </summary>
        /// <returns>Google Play、Apple App Store 或 Editor / Unsupported。</returns>
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
    }
}
