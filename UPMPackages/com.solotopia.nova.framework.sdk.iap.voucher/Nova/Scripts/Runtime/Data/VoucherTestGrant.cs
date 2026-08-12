/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherTestGrant.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   Voucher 测试发放请求与结果只读模型
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 单个礼券档位的测试发放明细。
    /// </summary>
    public sealed class VoucherGrantLine
    {
        /// <summary>
        /// 礼券档位 ID。
        /// </summary>
        public int VoucherTierId { get; }

        /// <summary>
        /// 发放数量。
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// 创建礼券测试发放明细。
        /// </summary>
        /// <param name="voucherTierId">礼券档位 ID，必须为正数。</param>
        /// <param name="quantity">发放数量，必须为正数。</param>
        /// <exception cref="ArgumentOutOfRangeException">ID 或数量不是正数时抛出。</exception>
        public VoucherGrantLine(int voucherTierId, int quantity)
        {
            if (voucherTierId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(voucherTierId), "礼券档位 ID 必须为正数。");
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "礼券发放数量必须为正数。");
            }

            VoucherTierId = voucherTierId;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// 单种赠币的测试发放明细。
    /// </summary>
    public sealed class CoinGrantLine
    {
        /// <summary>
        /// 赠币类型 ID。
        /// </summary>
        public int CoinId { get; }

        /// <summary>
        /// 发放数量。
        /// </summary>
        public int Quantity { get; }

        /// <summary>
        /// 创建赠币测试发放明细。
        /// </summary>
        /// <param name="coinId">赠币类型 ID，必须为正数。</param>
        /// <param name="quantity">发放数量，必须为正数。</param>
        /// <exception cref="ArgumentOutOfRangeException">ID 或数量不是正数时抛出。</exception>
        public CoinGrantLine(int coinId, int quantity)
        {
            if (coinId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(coinId), "赠币类型 ID 必须为正数。");
            }

            if (quantity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(quantity), "赠币发放数量必须为正数。");
            }

            CoinId = coinId;
            Quantity = quantity;
        }
    }

    /// <summary>
    /// 当前账号的 Voucher 测试发放请求。
    /// </summary>
    public sealed class VoucherTestGrantRequest
    {
        /// <summary>
        /// 待发放的礼券明细。
        /// </summary>
        public IReadOnlyList<VoucherGrantLine> VoucherGrants { get; }

        /// <summary>
        /// 待发放的赠币明细。
        /// </summary>
        public IReadOnlyList<CoinGrantLine> CoinGrants { get; }

        /// <summary>
        /// 创建测试发放请求并防御性复制明细集合。
        /// </summary>
        /// <param name="voucherGrants">礼券发放明细；为空表示不发放礼券。</param>
        /// <param name="coinGrants">赠币发放明细；为空表示不发放赠币。</param>
        /// <exception cref="ArgumentException">两个集合都没有有效明细时抛出。</exception>
        /// <exception cref="ArgumentNullException">任一集合包含空明细时抛出。</exception>
        public VoucherTestGrantRequest(IEnumerable<VoucherGrantLine> voucherGrants, IEnumerable<CoinGrantLine> coinGrants)
        {
            var voucherList = voucherGrants == null ? new List<VoucherGrantLine>() : new List<VoucherGrantLine>(voucherGrants);
            var coinList = coinGrants == null ? new List<CoinGrantLine>() : new List<CoinGrantLine>(coinGrants);

            if (voucherList.Exists(item => item == null))
            {
                throw new ArgumentNullException(nameof(voucherGrants), "礼券发放集合不能包含空明细。");
            }

            if (coinList.Exists(item => item == null))
            {
                throw new ArgumentNullException(nameof(coinGrants), "赠币发放集合不能包含空明细。");
            }

            if (voucherList.Count == 0 && coinList.Count == 0)
            {
                throw new ArgumentException("测试发放请求至少需要一条礼券或赠币明细。");
            }

            VoucherGrants = new ReadOnlyCollection<VoucherGrantLine>(voucherList.ToArray());
            CoinGrants = new ReadOnlyCollection<CoinGrantLine>(coinList.ToArray());
        }
    }

    /// <summary>
    /// Voucher 测试发放结果。
    /// </summary>
    public sealed class VoucherTestGrantResult
    {
        /// <summary>
        /// 测试发放是否成功并发布了新钱包。
        /// </summary>
        public bool IsSuccess { get; }

        /// <summary>
        /// 失败错误码；成功时为 None。
        /// </summary>
        public IAPVoucherErrorCode ErrorCode { get; }

        /// <summary>
        /// 服务端消息或本地错误描述。
        /// </summary>
        public string ErrorMessage { get; }

        /// <summary>
        /// 调用完成后的当前可见钱包快照。
        /// </summary>
        public VoucherWalletSnapshot Wallet { get; }

        /// <summary>
        /// 创建 Voucher 测试发放结果。
        /// </summary>
        /// <param name="isSuccess">是否成功。</param>
        /// <param name="errorCode">Voucher 错误码。</param>
        /// <param name="errorMessage">服务端消息或本地错误描述。</param>
        /// <param name="wallet">当前可见钱包快照。</param>
        internal VoucherTestGrantResult(bool isSuccess, IAPVoucherErrorCode errorCode, string errorMessage, VoucherWalletSnapshot wallet)
        {
            IsSuccess = isSuccess;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage ?? string.Empty;
            Wallet = wallet ?? VoucherWalletSnapshot.CreateNotReady();
        }
    }
}
