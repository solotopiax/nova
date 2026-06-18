/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherStore.Methods.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   VoucherStore 私有/受保护方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    public sealed partial class VoucherStore
    {
        /// <summary>
        /// 从持久化层加载当前账号的存档容器；不存在或损坏时落回 IAPStoreBase 提供的空容器。
        /// </summary>
        private void LoadPersistState()
        {
            m_PersistData = LoadPersistData<VoucherStorePersistData>();
        }

        /// <summary>
        /// 将进行中抵扣写入存档并持久化。
        /// </summary>
        /// <param name="tableId">配置表行 ID。</param>
        /// <param name="uid">用户 UID（string 形式）。</param>
        /// <param name="orderId">服务端抵扣订单 ID。</param>
        /// <param name="customString">透传业务自定义数据。</param>
        private void AddPendingDeduct(long tableId, string uid, string orderId, string customString)
        {
            if (m_PersistData == null) return;
            m_PersistData.PendingDeductStates[tableId] = new VoucherPendingDeduct
            {
                TableId = tableId,
                Uid = uid,
                OrderId = orderId,
                CustomString = customString,
            };
            SavePersistData(m_PersistData);
        }

        /// <summary>
        /// 从存档字典移除指定抵扣条目并持久化。
        /// tableId 不存在时静默跳过。
        /// </summary>
        /// <param name="tableId">配置表行 ID。</param>
        private void RemovePendingDeduct(long tableId)
        {
            if (m_PersistData?.PendingDeductStates == null) return;
            if (!m_PersistData.PendingDeductStates.ContainsKey(tableId)) return;
            m_PersistData.PendingDeductStates.Remove(tableId);
            SavePersistData(m_PersistData);
        }

        /// <summary>
        /// 异步从服务端拉取礼券档位与赠币余额（待协议接入）。
        /// 当前为占位实现，直接返回 false 并打日志，不发出网络请求。
        /// proto 端 user_id 仍为 int32，本方法在调用前对 string uid 做一次 int.TryParse 边界转换；
        /// 待 pb_net_gift_voucher.proto 把 user_id 改 string 后此处转换可移除。
        /// </summary>
        /// <param name="uid">用户 UID（string 形式）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>固定 false，待协议接入后改为真实拉取结果。</returns>
        private async UniTask<bool> FetchBalanceInternalAsync(string uid, CancellationToken ct)
        {
            string getVoucherListCmd = m_StoreConfig?.GetVoucherListCmdName;
            if (string.IsNullOrEmpty(getVoucherListCmd))
            {
                Log.Warning(LogTag.IAPVoucher, $"FetchBalance：GetVoucherListCmdName 未配置，uid={uid}");
                return false;
            }

            int.TryParse(uid, out int userId);
            var req = new PbNetGiftVoucherListReq { UserId = userId };
            var resp = await m_IapNetService.GetVoucherListAsync(getVoucherListCmd, req);
            if (!resp.IsSuccess || resp.Data == null)
            {
                Log.Warning(LogTag.IAPVoucher, $"FetchBalance 失败，uid={uid}");
                return false;
            }

            var voucherGroups = new List<GiftVoucherGroup>();
            foreach (var pb in resp.Data.VoucherGroups)
            {
                voucherGroups.Add(new GiftVoucherGroup
                {
                    VoucherTierId = pb.VoucherTierId,
                    FaceValue = pb.FaceValue,
                    FaceValueMilliCents = VoucherBalanceSnapshot.ParseFaceValueToMilliCents(pb.FaceValue),
                    Quantity = pb.Quantity,
                    VoucherCodes = new List<string>(pb.VoucherCodes),
                });
            }
            var coinBalances = new List<CoinBalance>();
            foreach (var pb in resp.Data.CoinBalances)
            {
                coinBalances.Add(new CoinBalance
                {
                    CoinId = pb.CoinId,
                    FaceValue = pb.FaceValue,
                    FaceValueMilliCents = VoucherBalanceSnapshot.ParseFaceValueToMilliCents(pb.FaceValue),
                    Quantity = pb.Quantity,
                });
            }
            m_Snapshot.Clear();
            m_Snapshot.SetVoucherGroups(voucherGroups);
            m_Snapshot.SetCoinBalances(coinBalances);
            m_IsBalanceReady = true;
            return true;
        }

        /// <summary>
        /// 异步向服务端提交代金券/金币抵扣请求（待协议接入）。
        /// 当前为占位实现，直接清理本地存档并返回 NetworkError，不发出网络请求。
        /// proto 端 user_id 仍为 int32，本方法在调用前对 string uid 做一次 int.TryParse 边界转换；
        /// 待 pb_net_gift_voucher.proto 把 user_id 改 string 后此处转换可移除。
        /// </summary>
        /// <param name="tableId">配置表行 ID。</param>
        /// <param name="uid">用户 UID（string 形式）。</param>
        /// <param name="detail">本次抵扣明细；补单路径可为 null。</param>
        /// <param name="customString">透传业务自定义数据。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>固定 NetworkError 的支付结果，待协议接入后改为真实抵扣流程。</returns>
        private async UniTask<IAPResult> SubmitDeductAsync(long tableId, string uid, DeductDetail detail, string customString, CancellationToken ct)
        {
            string deductVoucherCmd = m_StoreConfig?.DeductVoucherCmdName;
            if (string.IsNullOrEmpty(deductVoucherCmd))
            {
                Log.Warning(LogTag.IAPVoucher, $"SubmitDeduct：DeductVoucherCmdName 未配置，tableId={tableId}");
                RemovePendingDeduct(tableId);
                return new IAPResult(tableId, (int)IAPVoucherErrorCode.NetworkError, "DeductVoucherCmdName 未配置。", null);
            }

            int.TryParse(uid, out int userId);
            var req = new PbNetGiftVoucherDeductReq
            {
                UserId = userId,
                TableId = tableId,
            };

            if (detail != null)
            {
                if (detail.VoucherUsed != null)
                {
                    foreach (var item in detail.VoucherUsed)
                    {
                        var codes = m_Snapshot.GetVoucherCodes(item.VoucherTierId);
                        int take = Math.Min(item.Quantity, codes.Count);
                        for (int i = 0; i < take; i++)
                            req.VoucherCodes.Add(codes[i]);
                    }
                }
                if (detail.CoinUsed != null)
                {
                    foreach (var item in detail.CoinUsed)
                        req.CoinUsages.Add(new PbNetCoinUsage { CoinId = item.CoinId, Quantity = item.Quantity });
                }
            }

            var resp = await m_IapNetService.DeductVoucherAsync(deductVoucherCmd, req);
            RemovePendingDeduct(tableId);

            if (!resp.IsSuccess || resp.Data == null)
            {
                Log.Warning(LogTag.IAPVoucher, $"SubmitDeduct 失败，tableId={tableId}");
                return new IAPResult(tableId, (int)IAPVoucherErrorCode.NetworkError, "代金券扣减失败。", null);
            }

            if (resp.Data.Status != 0)
            {
                Log.Warning(LogTag.IAPVoucher, $"SubmitDeduct 服务端拒绝，status={resp.Data.Status}，tableId={tableId}");
                return new IAPResult(tableId, (int)IAPVoucherErrorCode.ServerValidationFailed, resp.Data.Message, null);
            }

            return new IAPResult(tableId, string.Empty, false, true, null);
        }
    }
}
