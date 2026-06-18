/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPBridge.cs
 * author:    nova-create-sample
 * created:   2026/06/05
 * descrip:   DemoIAPView IAP 调度桥接层 - 公开接口
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// DemoIAPView 的 IAP 调度桥接层。
    /// 收口 SDKComponent/IAPPlugin 查询、事件订阅、移动端支付与 Restore 等示例调用。
    /// </summary>
    internal sealed partial class DemoIAPBridge : IDisposable
    {
        /// <summary>
        /// 构造 IAP Demo 桥接层。
        /// </summary>
        /// <param name="feedback">反馈区追加回调。</param>
        /// <param name="productTextChanged">商品按钮文本刷新回调。</param>
        /// <param name="payInteractableChanged">支付按钮可交互状态刷新回调。</param>
        public DemoIAPBridge(Action<string, FeedbackLevel> feedback, Action<long, string> productTextChanged, Action<bool> payInteractableChanged)
        {
            m_Feedback = feedback;
            m_ProductTextChanged = productTextChanged;
            m_PayInteractableChanged = payInteractableChanged;
        }

        /// <summary>
        /// 尝试解析并缓存 IAPPlugin，同时订阅 IAP 事件。
        /// </summary>
        /// <returns>成功解析 IAPPlugin 时返回 true，否则返回 false。</returns>
        public bool TryInitialize()
        {
            if (m_Disposed)
            {
                return false;
            }

            if (m_IAP != null)
            {
                return true;
            }

            SDKComponent sdk = FrameworkComponentsGroup.GetComponent<SDKComponent>();
            if (sdk != null && sdk.TryGet<IAPPlugin>(out IAPPlugin iap))
            {
                m_IAP = iap;
                SubscribeEvents();
                SetPayInteractable(true);
                AppendFeedback("IAP 插件已连接。", FeedbackLevel.Success);
                return true;
            }

            SetPayInteractable(false);
            AppendFeedback("未找到 IAP 插件，支付功能暂不可用。", FeedbackLevel.Warn);
            return false;
        }


        /// <summary>
        /// 触发本地补单扫描。
        /// </summary>
        /// <returns>异步任务。</returns>
        public async UniTask CheckLocalOrdersAsync()
        {
            if (!TryInitialize())
            {
                return;
            }

            try
            {
                await m_IAP.CheckLocalOrdersAsync(m_Cancellation.Token);
                AppendFeedback("本地补单扫描已完成。", FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("本地补单扫描已取消。", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                AppendFeedback("本地补单扫描失败：" + ex.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 查询指定商品的移动端 SKU（商品表中的 ProductID 字段）。
        /// </summary>
        /// <param name="tableId">商品表行 id。</param>
        /// <returns>SKU 字符串；商品不存在或未配置 ProductID 时返回空串。</returns>
        public string GetProductSku(long tableId)
        {
            IAPProductEntry entry = FindProductEntry(tableId);
            return entry != null && !string.IsNullOrEmpty(entry.ProductID) ? entry.ProductID : string.Empty;
        }

        /// <summary>
        /// 构建支付按钮显示文本，优先使用移动端平台商品信息，其次使用本地商品表配置。
        /// </summary>
        /// <param name="tableId">商品表行 id。</param>
        /// <param name="groupLabel">分组文案。</param>
        /// <returns>按钮文本。</returns>
        public string BuildProductButtonText(long tableId, string groupLabel)
        {
            ProductInfo info;
            if (m_MobileProductInfos.TryGetValue(tableId, out info) && info != null)
            {
              
                return "ID" + tableId + FormatGroupLabel(groupLabel) + "  " + info.LocalizedPrice;
            }

            string price = "价格未知";
            IAPProductEntry entry = FindProductEntry(tableId);
            if (entry != null && !string.IsNullOrEmpty(entry.Price))
            {
                price = entry.Price + " " + entry.Currency;
            }

            return "ID" + tableId + FormatGroupLabel(groupLabel) + "  " + price;
        }

        /// <summary>
        /// 刷新移动端平台商品信息，并通知 View 更新按钮文本。
        /// </summary>
        /// <param name="tableIds">待刷新商品表行 id 列表。</param>
        /// <returns>异步任务。</returns>
        public async UniTask RefreshMobileProductInfoAsync(IReadOnlyList<long> tableIds)
        {
            if (tableIds == null || tableIds.Count == 0 || !TryInitialize())
            {
                return;
            }

            if (!m_IAP.TryGetCapability<IIAPMobileQueryCapable>(out IIAPMobileQueryCapable capability))
            {
                AppendFeedback("当前 IAP store 未暴露移动端商品查询能力。", FeedbackLevel.Warn);
                RefreshProductTexts(tableIds);
                return;
            }

            var productIds = new List<string>();
            var productIdToTableId = new Dictionary<string, long>();
            for (int i = 0; i < tableIds.Count; i++)
            {
                IAPProductEntry entry = FindProductEntry(tableIds[i]);
                if (entry == null || string.IsNullOrEmpty(entry.ProductID))
                {
                    continue;
                }

                if (!productIdToTableId.ContainsKey(entry.ProductID))
                {
                    productIds.Add(entry.ProductID);
                    productIdToTableId.Add(entry.ProductID, tableIds[i]);
                }
            }

            if (productIds.Count == 0)
            {
                AppendFeedback("商品表未配置移动端 ProductID，跳过平台价格刷新。", FeedbackLevel.Warn);
                RefreshProductTexts(tableIds);
                return;
            }

            try
            {
                IReadOnlyList<ProductInfo> infos = await capability.QueryProductsAsync(productIds, m_Cancellation.Token);
                if (infos != null)
                {
                    for (int i = 0; i < infos.Count; i++)
                    {
                        ProductInfo info = infos[i];
                        if (info == null || string.IsNullOrEmpty(info.ProductId))
                        {
                            continue;
                        }

                        long tableId;
                        if (productIdToTableId.TryGetValue(info.ProductId, out tableId))
                        {
                            m_MobileProductInfos[tableId] = info;
                        }
                    }
                }

                RefreshProductTexts(tableIds);
                AppendFeedback("移动端商品信息刷新完成：" + (infos != null ? infos.Count : 0), FeedbackLevel.Success);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("移动端商品信息刷新已取消。", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                RefreshProductTexts(tableIds);
                AppendFeedback("移动端商品信息刷新失败：" + ex.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 发起移动端支付。
        /// </summary>
        /// <param name="tableId">商品表行 id。</param>
        /// <returns>支付结果；插件不可用或调用异常时返回 null。</returns>
        public async UniTask<IAPResult> PayMobileAsync(long tableId)
        {
            if (!TryInitialize())
            {
                return null;
            }

            SetPayInteractable(false);
            try
            {
                IAPRequest request = CreateMobileRequest(tableId);
                IAPResult result = await m_IAP.PayAsync<IAPResult>(request, m_Cancellation.Token);
                AppendFeedback("UI支付结果回调: " + FormatResult(result), result != null && result.IsSuccess ? FeedbackLevel.Success : FeedbackLevel.Error);
                return result;
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("支付已取消：tableId=" + tableId, FeedbackLevel.Warn);
                return null;
            }
            catch (Exception ex)
            {
                AppendFeedback("支付调用失败：tableId=" + tableId + "，" + ex.Message, FeedbackLevel.Error);
                return null;
            }
            finally
            {
                SetPayInteractable(!m_Disposed);
            }
        }

        /// <summary>
        /// 恢复历史购买。
        /// </summary>
        /// <returns>恢复结果列表；插件不可用或调用异常时返回空列表。</returns>
        public async UniTask<IReadOnlyList<IAPResult>> RestorePurchasesAsync()
        {
            if (!TryInitialize())
            {
                return Array.Empty<IAPResult>();
            }

            try
            {
                IReadOnlyList<IAPResult> results = await m_IAP.RestorePurchasesAsync<IAPResult>(m_Cancellation.Token);
                int count = results != null ? results.Count : 0;
                AppendFeedback(count > 0 ? "恢复购买完成：" + count : "恢复购买完成，未恢复到订阅或非消耗品订单。", count > 0 ? FeedbackLevel.Success : FeedbackLevel.Info);
                return results ?? Array.Empty<IAPResult>();
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("恢复购买已取消。", FeedbackLevel.Warn);
                return Array.Empty<IAPResult>();
            }
            catch (Exception ex)
            {
                AppendFeedback("恢复购买失败：" + ex.Message, FeedbackLevel.Error);
                return Array.Empty<IAPResult>();
            }
        }

        /// <summary>
        /// 设置移动端 store 启用状态。
        /// </summary>
        /// <param name="enabled">是否启用。</param>
        /// <returns>异步任务。</returns>
        public async UniTask SetMobileStoreEnabledAsync(bool enabled)
        {
            if (!TryInitialize())
            {
                return;
            }

            try
            {
                await m_IAP.SetStoreEnabled(IAPStoreType.Mobile, enabled, m_Cancellation.Token);
                SetPayInteractable(enabled);
                AppendFeedback("移动端 IAP Store 已" + (enabled ? "启用。" : "禁用。"), enabled ? FeedbackLevel.Success : FeedbackLevel.Warn);
            }
            catch (OperationCanceledException)
            {
                AppendFeedback("移动端 IAP Store 状态切换已取消。", FeedbackLevel.Warn);
            }
            catch (Exception ex)
            {
                AppendFeedback("移动端 IAP Store 状态切换失败：" + ex.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 判断订阅是否仍在有效期内。
        /// </summary>
        /// <param name="tableId">订阅商品表行 id。</param>
        /// <returns>有效期内返回 true，否则返回 false。</returns>
        public bool IsSubscriptionActive(long tableId)
        {
            return TryGetMobileSubscriptionCapability(out IIAPMobileSubscriptionCapable capability)
                   && capability.InSubscriptionPeriod(tableId);
        }

        /// <summary>
        /// 获取订阅到期时间戳。
        /// </summary>
        /// <param name="tableId">订阅商品表行 id。</param>
        /// <returns>Unix 毫秒时间戳；无订阅时返回 0。</returns>
        public long GetSubscriptionExpireTime(long tableId)
        {
            return TryGetMobileSubscriptionCapability(out IIAPMobileSubscriptionCapable capability)
                ? capability.GetSubscriptionExpireTime(tableId)
                : 0L;
        }

        /// <summary>
        /// 判断非消耗品是否存在持有标记。
        /// </summary>
        /// <param name="tableId">非消耗品商品表行 id。</param>
        /// <returns>已持有返回 true，否则返回 false。</returns>
        public bool HasNonConsumeProduct(long tableId)
        {
            return TryGetMobileSubscriptionCapability(out IIAPMobileSubscriptionCapable capability)
                   && capability.HasNonConsumeProduct(tableId);
        }

        /// <summary>
        /// 释放订阅与异步取消令牌。
        /// </summary>
        public void Dispose()
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            m_Cancellation.Cancel();
            UnsubscribeEvents();
            m_Cancellation.Dispose();
            m_IAP = null;
        }
    }
}
