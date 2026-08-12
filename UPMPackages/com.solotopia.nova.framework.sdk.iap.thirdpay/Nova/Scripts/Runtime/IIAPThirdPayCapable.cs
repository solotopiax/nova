/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPThirdPayCapable.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   第三方支付业务能力接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付业务侧能力入口。
    /// </summary>
    public interface IIAPThirdPayCapable : IIAPCapable
    {
        /// <summary>
        /// 覆盖当前支付国家或地区代码。
        /// </summary>
        /// <param name="countryCode">ISO 3166-1 alpha-2 国家或地区代码。</param>
        void SetCountryCode(string countryCode);

        /// <summary>
        /// 手动设置当前账号需要透传的渠道参数。
        /// </summary>
        /// <param name="channelParams">CID 等渠道参数。</param>
        void SetChannelParams(string channelParams);

        /// <summary>
        /// 拉取当前国家或地区可用的第三方支付商品。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>成功取得有效商品列表时返回 true。</returns>
        UniTask<bool> FetchProductListAsync(CancellationToken ct);

        /// <summary>
        /// 按支付表行 ID 获取已拉取的第三方商品信息。
        /// </summary>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <returns>匹配的第三方商品；未命中时返回 null。</returns>
        PbNetThirdProductInfo GetProductInfo(long tableId);

    }
}
