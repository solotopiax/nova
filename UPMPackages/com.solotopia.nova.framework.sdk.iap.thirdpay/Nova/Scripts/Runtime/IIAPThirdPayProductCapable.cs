/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPThirdPayProductCapable.cs
 * author:    yingzheng
 * created:   2026/9/17
 * descrip:   第三方支付商品查询能力接口
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付商品快照查询能力。
    /// </summary>
    public interface IIAPThirdPayProductCapable : IIAPCapable
    {
        /// <summary>
        /// 按支付表行 ID 获取已拉取的第三方商品信息。
        /// </summary>
        /// <param name="tableId">支付商品表行 ID。</param>
        /// <returns>匹配的第三方商品；未命中时返回 null。</returns>
        PbNetThirdProductInfo GetProductInfo(long tableId);

        /// <summary>
        /// 获取最近一次成功拉取的全部第三方支付商品。
        /// </summary>
        /// <returns>商品列表；尚未拉取成功或列表为空时返回空列表。</returns>
        IReadOnlyList<PbNetThirdProductInfo> GetProductList();

        /// <summary>
        /// 判断最近一次成功拉取的第三方支付商品列表是否有商品。
        /// </summary>
        /// <returns>有商品时返回 true。</returns>
        bool HasProducts();
    }
}
