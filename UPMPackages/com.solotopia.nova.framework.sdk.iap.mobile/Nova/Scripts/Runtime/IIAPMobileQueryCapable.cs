/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPMobileQueryCapable.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   具备移动端商品查询能力的 store 扩展接口
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 具备平台商品查询能力的移动端 store 扩展接口。
    /// 实现此接口的 store 支持从 Google Play / iOS App Store 获取商品价格、标题等元数据。
    /// 通过 IAPPlugin.TryGetCapability<IIAPMobileQueryCapable> 取用。
    /// </summary>
    public interface IIAPMobileQueryCapable : IIAPCapable
    {
        /// <summary>
        /// 异步批量查询指定商品 ID 列表的平台商品信息。
        /// </summary>
        /// <param name="productIds">要查询的平台商品 ID 列表，不得为 null。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>查询到的商品信息列表；未找到的商品不计入结果。</returns>
        UniTask<IReadOnlyList<ProductInfo>> QueryProductsAsync(IReadOnlyList<string> productIds, CancellationToken ct);

        /// <summary>
        /// 同步获取指定 tableId 商品的平台元数据。
        /// 内部 m_StoreController.products.WithID 本就是同步访问，故采用同步签名；
        /// 平台 SDK 未初始化、tableId 不在表中或商品平台不可购时返回 null，调用方自行做 null 检查。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>对应的 ProductInfo；未命中或不可购时返回 null。</returns>
        ProductInfo GetProductInfo(long tableId);
    }
}
