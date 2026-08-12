/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProtobufVoucherGateway.Methods.cs
 * author:    yingzheng
 * created:   2026/8/3
 * descrip:   ProtobufVoucherGateway 非公开协议映射方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// ProtobufVoucherGateway 非公开协议映射方法。
    /// </summary>
    internal sealed partial class ProtobufVoucherGateway
    {
        /// <summary>
        /// 从不可变交易命令构建 protobuf 扣减请求。
        /// </summary>
        /// <param name="command">已经持久化的不可变交易命令。</param>
        /// <param name="header">当前网络会话的公共请求头。</param>
        /// <returns>完整的 Voucher 扣减请求。</returns>
        /// <exception cref="ArgumentNullException">交易命令或公共请求头为空时抛出。</exception>
        internal static PbNetGiftVoucherDeductReq BuildDeductRequest(VoucherSpendCommand command, PbNetReqHeader header)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            var request = new PbNetGiftVoucherDeductReq
            {
                Head = header ?? throw new ArgumentNullException(nameof(header)),
                GameOrderId = command.GameOrderId,
                TableId = command.TableId,
                Country = command.Country,
            };
            request.VoucherCodes.Add(command.VoucherCodes);
            foreach (CoinUsageData usage in command.CoinUsages)
            {
                request.CoinUsages.Add(new PbNetCoinUsage
                {
                    CoinId = usage.CoinId,
                    Quantity = usage.Quantity,
                });
            }

            return request;
        }

        /// <summary>
        /// 从公开领域请求构建 protobuf 测试发放请求。
        /// </summary>
        /// <param name="request">测试发放领域请求。</param>
        /// <param name="header">当前网络会话的公共请求头。</param>
        /// <returns>完整的 Voucher 测试发放请求。</returns>
        /// <exception cref="ArgumentNullException">领域请求或公共请求头为空时抛出。</exception>
        private static PbNetGiftVoucherTestGrantReq BuildTestGrantRequest(VoucherTestGrantRequest request, PbNetReqHeader header)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            var protocolRequest = new PbNetGiftVoucherTestGrantReq
            {
                Head = header ?? throw new ArgumentNullException(nameof(header)),
            };
            foreach (VoucherGrantLine grant in request.VoucherGrants)
            {
                protocolRequest.VoucherGrants.Add(new PbNetVoucherGrant
                {
                    VoucherTierId = grant.VoucherTierId,
                    Quantity = grant.Quantity,
                });
            }

            foreach (CoinGrantLine grant in request.CoinGrants)
            {
                protocolRequest.CoinGrants.Add(new PbNetCoinGrant
                {
                    CoinId = grant.CoinId,
                    Quantity = grant.Quantity,
                });
            }

            return protocolRequest;
        }

        /// <summary>
        /// 将测试发放网络响应转换为钱包结果，不执行自动重试。
        /// </summary>
        /// <param name="response">测试发放网络响应。</param>
        /// <returns>成功后的钱包或有限错误分类。</returns>
        private static VoucherGatewayWalletResult ClassifyTestGrant(NetResponse<PbNetGiftVoucherTestGrantResp> response)
        {
            if (response == null)
            {
                return new VoucherGatewayWalletResult(false, IAPVoucherErrorCode.NetworkError, "Voucher 测试发放响应为空，服务端结果未知。", null, null);
            }

            if (response.Data != null)
            {
                if (!response.Data.Status)
                {
                    string message = string.IsNullOrEmpty(response.Data.Message) ? response.ErrorMessage : response.Data.Message;
                    return new VoucherGatewayWalletResult(false, IAPVoucherErrorCode.ServerRejected, message, null, null);
                }

                if (!TryMapWallet(response.Data.VoucherGroups, response.Data.CoinBalances, out List<VoucherAssetData> vouchers, out List<CoinAssetData> coins))
                {
                    return new VoucherGatewayWalletResult(false, IAPVoucherErrorCode.ProtocolError, "Voucher 测试发放成功响应包含无效资产面值。", null, null);
                }

                return new VoucherGatewayWalletResult(true, IAPVoucherErrorCode.None, response.Data.Message, vouchers, coins);
            }

            if (response.ErrorCode == NetErrorCode.URL_NOT_FOUND)
            {
                return new VoucherGatewayWalletResult(false, IAPVoucherErrorCode.TestGrantUnavailable, response.ErrorMessage, null, null);
            }

            IAPVoucherErrorCode errorCode = response.IsSuccess ? IAPVoucherErrorCode.ProtocolError : IAPVoucherErrorCode.NetworkError;
            string errorMessage = response.IsSuccess ? "Voucher 测试发放成功响应缺少业务结果。" : response.ErrorMessage;
            return new VoucherGatewayWalletResult(false, errorCode, errorMessage, null, null);
        }

        /// <summary>
        /// 将网络、公共错误和业务状态压缩为交易协调器可处理的有限结果。
        /// </summary>
        /// <param name="response">网络层返回的 Voucher 扣减响应。</param>
        /// <returns>成功、拒绝、可重试或未知结果。</returns>
        internal static VoucherGatewayDeductResult Classify(NetResponse<PbNetGiftVoucherDeductResp> response)
        {
            if (response == null)
            {
                return new VoucherGatewayDeductResult(VoucherGatewayDisposition.Unknown, 0, "Voucher 抵扣响应为空。");
            }

            // 业务错误响应仍可能携带原订单已处理终态，必须优先依据业务体恢复。
            if (response.Data != null)
            {
                PbNetGiftVoucherDeductStatus status = response.Data.Status;
                if (status == PbNetGiftVoucherDeductStatus.Success || status == PbNetGiftVoucherDeductStatus.Shipped)
                {
                    if (!TryMapWallet(response.Data.VoucherGroups, response.Data.CoinBalances, out List<VoucherAssetData> vouchers, out List<CoinAssetData> coins))
                    {
                        return new VoucherGatewayDeductResult(VoucherGatewayDisposition.Unknown, NetErrorCode.PROTO_PARSE_FAILED, "Voucher 抵扣成功响应包含无效资产面值。");
                    }

                    return new VoucherGatewayDeductResult(VoucherGatewayDisposition.Succeeded, response.ErrorCode, response.Data.Message, vouchers, coins);
                }

                if (status == PbNetGiftVoucherDeductStatus.Failed)
                {
                    string message = string.IsNullOrEmpty(response.Data.Message) ? response.ErrorMessage : response.Data.Message;
                    return new VoucherGatewayDeductResult(VoucherGatewayDisposition.Rejected, response.ErrorCode, message);
                }
            }

            if (response.IsSuccess)
            {
                return new VoucherGatewayDeductResult(VoucherGatewayDisposition.Unknown, response.ErrorCode, "Voucher 抵扣成功响应缺少明确终态。");
            }

            if (response.ErrorCode == NetErrorCode.PROTO_PARSE_FAILED)
            {
                return new VoucherGatewayDeductResult(VoucherGatewayDisposition.Unknown, response.ErrorCode, response.ErrorMessage);
            }

            if (response.ErrorCode <= 0 || response.ErrorCode == NetErrorCode.SERVER_ERROR || response.ErrorCode == NetErrorCode.DATABASE_ERROR)
            {
                return new VoucherGatewayDeductResult(VoucherGatewayDisposition.Retryable, response.ErrorCode, response.ErrorMessage);
            }

            return new VoucherGatewayDeductResult(VoucherGatewayDisposition.Rejected, response.ErrorCode, response.ErrorMessage);
        }

        /// <summary>
        /// 将协议资产列表转换为内部精确钱包模型。
        /// </summary>
        /// <param name="voucherGroups">服务端返回的礼券分组。</param>
        /// <param name="coinBalances">服务端返回的赠币余额。</param>
        /// <param name="vouchers">转换后的礼券资产。</param>
        /// <param name="coins">转换后的赠币资产。</param>
        /// <returns>全部正数量资产均能精确转换时返回 true，否则返回 false。</returns>
        private static bool TryMapWallet(IEnumerable<PbNetGiftVoucherGroup> voucherGroups, IEnumerable<PbNetCoinBalance> coinBalances, out List<VoucherAssetData> vouchers, out List<CoinAssetData> coins)
        {
            vouchers = new List<VoucherAssetData>();
            coins = new List<CoinAssetData>();
            if (voucherGroups != null)
            {
                foreach (PbNetGiftVoucherGroup group in voucherGroups)
                {
                    if (group == null || group.Quantity <= 0)
                    {
                        continue;
                    }

                    long faceValueMills = VoucherMoney.ParseMills(group.FaceValue);
                    if (faceValueMills <= 0 || group.VoucherCodes.Count != group.Quantity)
                    {
                        return false;
                    }

                    vouchers.Add(new VoucherAssetData(group.VoucherTierId, group.FaceValue, faceValueMills, group.VoucherCodes));
                }
            }

            if (coinBalances != null)
            {
                foreach (PbNetCoinBalance balance in coinBalances)
                {
                    if (balance == null || balance.Quantity <= 0)
                    {
                        continue;
                    }

                    long faceValueMills = VoucherMoney.ParseMills(balance.FaceValue);
                    if (faceValueMills <= 0)
                    {
                        return false;
                    }

                    coins.Add(new CoinAssetData(balance.CoinId, balance.FaceValue, faceValueMills, balance.Quantity));
                }
            }

            return true;
        }
    }
}
