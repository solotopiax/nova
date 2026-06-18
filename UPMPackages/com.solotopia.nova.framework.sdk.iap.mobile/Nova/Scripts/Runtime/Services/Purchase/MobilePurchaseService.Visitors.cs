/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobilePurchaseService.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobilePurchaseService 字段与属性
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobilePurchaseService
    {
        /// <summary>
        /// 当前正在进行中的支付对应的配置表行 ID；0 = 空闲，同时起防重入作用。
        /// 读写委托给基类字段（通过 MobileStore.InPayTableId）。
        /// </summary>
        internal long InPayTableId { get => m_Hub.Store.InPayTableId; private set => m_Hub.Store.InPayTableId = value; }

        /// <summary>
        /// 当前活跃支付的完成信号，桥接平台购买回调到 PayAsync 的 await 点。
        /// </summary>
        private UniTaskCompletionSource<IAPResult> m_PayTcs;

        /// <summary>
        /// 当前活跃支付请求的透传字符串数据，供购买失败回调回传给业务层。
        /// </summary>
        private string m_CurrentCustomData;

        /// <summary>
        /// 订阅升降级时 Android ProrationMode，默认 0（ImmediateWithTimeProration）。
        /// </summary>
        private int m_SubscriptionReplaceMode;
    }
}
