/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IIAPVoucherTestCapable.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 测试环境资产发放能力接口
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// Voucher Store 独立暴露的测试环境资产发放能力。
    /// </summary>
    public interface IIAPVoucherTestCapable : IIAPCapable
    {
        /// <summary>
        /// 向当前账号测试发放礼券和赠币，并在成功后更新钱包快照。
        /// </summary>
        /// <param name="request">测试发放请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>测试发放结果以及调用完成后的当前钱包。</returns>
        UniTask<VoucherTestGrantResult> TestGrantAsync(VoucherTestGrantRequest request, CancellationToken ct = default);
    }
}
