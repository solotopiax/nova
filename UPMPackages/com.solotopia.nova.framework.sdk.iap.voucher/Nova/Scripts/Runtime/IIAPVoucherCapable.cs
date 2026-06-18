/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPVoucherCapable.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   具备代金券/金币查询与扣费计算能力的 store 扩展接口
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 具备代金券与金币余额查询及扣费计算能力的 store 扩展接口。
    /// 实现此接口的 store 负责维护金币与代金券余额，并能计算最优扣费方案。
    /// 通过 IAPPlugin.TryGetCapability<IIAPVoucherCapable> 取用。
    /// </summary>
    public interface IIAPVoucherCapable : IIAPCapable
    {
        /// <summary>
        /// 获取指定金币类型 ID 对应的当前持有数量。
        /// </summary>
        /// <param name="coinId">金币类型 ID（对应配置表主键）。</param>
        /// <returns>当前持有数量；未找到时返回 0。</returns>
        int GetCoinQuantity(int coinId);

        /// <summary>
        /// 获取指定代金券档位 ID 对应的当前持有数量。
        /// </summary>
        /// <param name="voucherTierId">代金券档位 ID（对应配置表主键）。</param>
        /// <returns>当前持有数量；未找到时返回 0。</returns>
        int GetVoucherQuantity(int voucherTierId);

        /// <summary>
        /// 根据商品配置表 ID 与价格计算当前最优扣费方案。
        /// 需要先通过 FetchBalanceAsync 成功拉取余额，否则返回 Cash 模式。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="priceMilliCents">商品价格（毫分，price_usd × 1000），避免浮点误差。</param>
        /// <returns>推荐的扣费方案，含扣费模式及各资产用量明细。</returns>
        DeductPlan CalcDeductPlan(long tableId, long priceMilliCents);
    }
}
