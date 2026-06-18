/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DemoIAPBridge.Mobile.cs
 * author:    nova-create-sample
 * created:   2026/06/05
 * descrip:   DemoIAPView IAP 调度桥接层 - 移动端能力
 ***************************************************************/

using NovaFramework.SDK.IAP.Runtime;
using UnityEngine;

namespace NovaFramework.Sdk.IAP.Samples.Runtime
{
    internal sealed partial class DemoIAPBridge
    {
        /// <summary>
        /// 构建移动端支付请求，附带场景与商品透传数据。
        /// </summary>
        /// <param name="tableId">商品表行 id。</param>
        /// <returns>移动端支付请求。</returns>
        private IAPRequest CreateMobileRequest(long tableId)
        {
            IAPMobileRequest request = new IAPMobileRequest
            {
                TableId = tableId,
                CustomData = JsonUtility.ToJson(new PayPayload
                {
                    TableId = tableId,
                    Scene = c_SceneName
                })
            };
            return request;
        }

        /// <summary>
        /// 尝试获取移动端订阅能力接口。
        /// </summary>
        /// <param name="capability">输出参数，移动端订阅能力接口；获取失败时为 null。</param>
        /// <returns>成功获取返回 true，否则返回 false。</returns>
        private bool TryGetMobileSubscriptionCapability(out IIAPMobileSubscriptionCapable capability)
        {
            capability = null;
            return TryInitialize()
                   && m_IAP.TryGetCapability<IIAPMobileSubscriptionCapable>(out capability);
        }
    }
}
