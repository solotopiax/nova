/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPBridge.cs
 * author:    yingzheng
 * created:   2026/8/4
 * descrip:   不依赖可选支付包的 IAP Demo Core 调度桥接层
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

using FeedbackLevel = NovaFramework.Sdk.IAP.Samples.Runtime.BaseDemoView.FeedbackLevel;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    /// <summary>
    /// 收口基础 IAPPlugin 查询、全局事件和生命周期；具体商店能力由可选模块实现。
    /// </summary>
    internal sealed partial class DemoIAPBridge : IDisposable
    {
        /// <summary>
        /// 构造不依赖具体商店 package 的 Core IAP 桥接层。
        /// </summary>
        /// <param name="feedback">反馈区追加回调。</param>
        /// <param name="payInteractableChanged">全部商店业务按钮交互状态回调。</param>
        internal DemoIAPBridge(Action<string, FeedbackLevel> feedback, Action<bool> payInteractableChanged)
        {
            m_Feedback = feedback;
            m_PayInteractableChanged = payInteractableChanged;
        }

        /// <summary>
        /// 尝试解析并缓存 IAPPlugin，同时订阅基础 IAP 全局事件。
        /// </summary>
        /// <returns>成功解析 IAPPlugin 时返回 true。</returns>
        internal bool TryInitialize()
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
        /// 触发基础 IAP 本地补单扫描。
        /// </summary>
        /// <returns>异步任务。</returns>
        internal async UniTask CheckLocalOrdersAsync()
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
            catch (Exception exception)
            {
                AppendFeedback("本地补单扫描失败：" + exception.Message, FeedbackLevel.Error);
            }
        }

        /// <summary>
        /// 查询指定商品表行对应的移动 SKU；未配置时返回空串。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <returns>商品 SKU。</returns>
        internal string GetProductSku(long tableId)
        {
            IAPProductEntry entry = FindProductEntry(tableId);
            return entry != null && !string.IsNullOrEmpty(entry.ProductID) ? entry.ProductID : string.Empty;
        }

        /// <summary>
        /// 使用基础商品表价格构建商品卡标题，不读取任何可选商店类型。
        /// </summary>
        /// <param name="tableId">商品表行 ID。</param>
        /// <param name="groupLabel">商品演示分组。</param>
        /// <returns>商品卡标题。</returns>
        internal string BuildProductButtonText(long tableId, string groupLabel)
        {
            string price = "价格未知";
            IAPProductEntry entry = FindProductEntry(tableId);
            if (entry != null && !string.IsNullOrEmpty(entry.Price))
            {
                price = entry.Price + " " + entry.Currency;
            }

            return "ID" + tableId + FormatGroupLabel(groupLabel) + "  " + price;
        }

        /// <summary>
        /// 释放基础事件订阅和 View 生命周期取消令牌。
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
