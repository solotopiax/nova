/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherStore.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   代金券/金币虚拟货币 store 实现
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// 代金券与金币虚拟货币 store。
    /// 继承 IAPStoreBase，实现代金券/金币余额查询与扣费计算能力（IIAPVoucherCapable）。
    /// InitializeAsync 阶段加载持久化存档并触发补单；
    /// SetUserId 触发余额刷新；
    /// PayAsync 通过 SubmitDeductAsync 完成服务端抵扣全流程。
    /// </summary>
    [IAPStore]
    public sealed partial class VoucherStore : IAPStoreBase, IIAPVoucherCapable
    {
        /// <summary>
        /// 当前 store 的渠道类型，固定为 Voucher。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.Voucher;

        /// <summary>
        /// 异步初始化 store：创建余额快照、加载持久化存档、触发遗留补单。
        /// Voucher 通过 coinId 路由，不依赖商品表，table 参数忽略。
        /// </summary>
        /// <param name="table">所有 store 共用的商品表（IAP 接口）；Voucher store 忽略此参数，直接用 coinId 路由。</param>
        /// <param name="config">store 专属配置（Voucher 无独立 config，传 null 即可）。</param>
        /// <param name="ctx">运行时上下文，包含跨模块依赖引用。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        public override async UniTask InitializeAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct)
        {
            await base.InitializeAsync(table, config, ctx, ct);
            m_StoreConfig = config as VoucherStoreConfig;
            m_IapNetService = new VoucherIapNetService();

            m_Snapshot = new VoucherBalanceSnapshot();
            m_IsBalanceReady = false;
            m_PersistData = (VoucherStorePersistData)CreateEmptyPersistData();
        }

        /// <summary>
        /// 切换当前登录用户 UID，按账号加载存档、触发遗留补单，并刷新余额。
        /// 同账号重复调用时幂等返回。
        /// </summary>
        /// <param name="uid">已登录用户的唯一 ID。</param>
        public override void SetUserId(string uid)
        {
            string oldUid = m_GameUID;
            base.SetUserId(uid);
            if (m_GameUID == oldUid || string.IsNullOrEmpty(m_GameUID)) return;
            LoadPersistState();
            ReplayPendingDeductsAsync(CancellationToken.None).Forget();
            FetchBalanceInternalAsync(m_GameUID, CancellationToken.None).Forget();
        }

        /// <summary>
        /// 切换账号后将本地遗留的进行中抵扣逐一重新提交（detail 为 null 表示补单路径）。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>补单完成的异步任务。</returns>
        private async UniTaskVoid ReplayPendingDeductsAsync(CancellationToken ct)
        {
            if (m_PersistData?.PendingDeductStates == null || m_PersistData.PendingDeductStates.Count == 0) return;
            var pending = new List<VoucherPendingDeduct>(m_PersistData.PendingDeductStates.Values);
            foreach (var deduct in pending)
            {
                ct.ThrowIfCancellationRequested();
                await SubmitDeductAsync(deduct.TableId, deduct.Uid, null, deduct.CustomString, ct);
            }
        }

        /// <summary>
        /// 判断当前 store 是否能处理指定请求，仅接受 IAPVoucherRequest 类型。
        /// </summary>
        /// <param name="request">待判断的支付请求。</param>
        /// <returns>请求为 IAPVoucherRequest 时返回 true，否则返回 false。</returns>
        public override bool CanHandle(IAPRequest request)
        {
            return request is IAPVoucherRequest;
        }

        /// <summary>
        /// 异步发起代金券/金币支付流程。
        /// EnableAlwaysPaySucceed 开启时跳过真实扣费直接返回成功；
        /// 防止并发重复支付同一商品；
        /// 通过 SubmitDeductAsync 完成服务端抵扣与验单。
        /// </summary>
        /// <param name="request">支付请求，已通过 CanHandle 确认为 IAPVoucherRequest。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>包含支付结果和订单信息的 IAPResult。</returns>
        public override UniTask<IAPResult> PayAsync(IAPRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (Context.EnableAlwaysPaySucceed)
                return UniTask.FromResult(new IAPResult(request.TableId, "MOCK_ORDER_VOUCHER", false, true, request.CustomData));

            return PayGuardAsync(request, ct, async () =>
            {
                var voucherRequest = (IAPVoucherRequest)request;
                m_InPayTableId = request.TableId;

                string uid = m_GameUID ?? string.Empty;

                var detail = BuildDeductDetail(voucherRequest, m_Snapshot);
                string customString = voucherRequest.CustomData ?? string.Empty;
                AddPendingDeduct(request.TableId, uid, string.Empty, customString);

                try
                {
                    return await SubmitDeductAsync(request.TableId, uid, detail, customString, ct);
                }
                finally
                {
                    m_InPayTableId = 0;
                }
            });
        }

        /// <summary>
        /// 获取指定金币类型 ID 对应的当前持有数量。
        /// </summary>
        /// <param name="coinId">金币类型 ID（对应配置表主键）。</param>
        /// <returns>当前持有数量；余额未拉取或未找到时返回 0。</returns>
        public int GetCoinQuantity(int coinId)
        {
            return m_IsBalanceReady ? m_Snapshot.GetCoinQuantity(coinId) : 0;
        }

        /// <summary>
        /// 获取指定代金券档位 ID 对应的当前持有数量。
        /// </summary>
        /// <param name="voucherTierId">代金券档位 ID（对应配置表主键）。</param>
        /// <returns>当前持有数量；余额未拉取或未找到时返回 0。</returns>
        public int GetVoucherQuantity(int voucherTierId)
        {
            return m_IsBalanceReady ? m_Snapshot.GetVoucherQuantity(voucherTierId) : 0;
        }

        /// <summary>
        /// 根据商品配置表 ID 与价格计算当前最优扣费方案。
        /// 需要先通过 FetchBalanceAsync 成功拉取余额，否则返回 Cash 模式。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <param name="priceMilliCents">商品价格（毫分，price_usd × 1000），避免浮点误差。</param>
        /// <returns>推荐的扣费方案，含扣费模式及各资产用量明细。</returns>
        public DeductPlan CalcDeductPlan(long tableId, long priceMilliCents)
        {
            if (!m_IsBalanceReady || Context == null || m_Snapshot == null)
                return new DeductPlan(VoucherDeductMode.Cash, 0, 0, priceMilliCents);
            return m_Snapshot.CalcDeductPlan(priceMilliCents);
        }

        /// <summary>
        /// 异步拉取余额，成功后 GetCoinQuantity / GetVoucherQuantity / CalcDeductPlan 生效。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>是否拉取成功。</returns>
        public async UniTask<bool> FetchBalanceAsync(CancellationToken ct)
        {
            return await FetchBalanceInternalAsync(m_GameUID ?? string.Empty, ct);
        }

        /// <summary>
        /// 释放 store 资源。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        public override async UniTask DisposeAsync(CancellationToken ct)
        {
            m_Snapshot?.Clear();
            m_Snapshot = null;
            m_IsBalanceReady = false;
            m_PersistData = null;
            m_StoreConfig = null;
            m_IapNetService = null;
            await base.DisposeAsync(ct);
        }

        /// <summary>
        /// 覆写基类工厂，提供 VoucherStore 专属空存档容器。
        /// </summary>
        /// <returns>已 EnsureInitialized 的 VoucherStorePersistData 实例。</returns>
        protected override IIAPStorePersistData CreateEmptyPersistData()
        {
            var data = new VoucherStorePersistData();
            data.EnsureInitialized();
            return data;
        }

        /// <summary>
        /// 将 IAPVoucherRequest 的 VoucherCodes 与 CoinUsages 转换为服务端抵扣明细。
        /// VoucherCodes 为激活码列表，通过快照反查所属档位以填充 DeductVoucherItem。
        /// </summary>
        /// <param name="request">代金券支付请求。</param>
        /// <param name="snapshot">当前余额快照，用于将激活码反查到档位信息；为 null 时跳过 VoucherCodes。</param>
        /// <returns>抵扣明细；无可用券和金币时返回空明细。</returns>
        private static DeductDetail BuildDeductDetail(IAPVoucherRequest request, VoucherBalanceSnapshot snapshot)
        {
            var detail = new DeductDetail
            {
                VoucherUsed = new List<DeductVoucherItem>(),
                CoinUsed = new List<DeductCoinItem>(),
            };

            if (snapshot != null && request.VoucherCodes != null && request.VoucherCodes.Count > 0)
            {
                // 激活码按档位聚合：找出每个激活码所属的 voucherTierId，再按档位汇总数量
                var tierCount = new Dictionary<int, int>();
                foreach (string code in request.VoucherCodes)
                {
                    if (string.IsNullOrEmpty(code)) continue;
                    int tierId = snapshot.FindTierIdByVoucherCode(code);
                    if (tierId <= 0) continue;
                    tierCount.TryGetValue(tierId, out int cur);
                    tierCount[tierId] = cur + 1;
                }
                foreach (var kv in tierCount)
                {
                    string faceValue = snapshot.GetVoucherFaceValue(kv.Key);
                    detail.VoucherUsed.Add(new DeductVoucherItem { VoucherTierId = kv.Key, FaceValue = faceValue, Quantity = kv.Value });
                }
            }

            if (request.CoinUsages != null)
            {
                foreach (var usage in request.CoinUsages)
                {
                    if (usage != null && usage.Quantity > 0)
                        detail.CoinUsed.Add(new DeductCoinItem { CoinId = usage.CoinId, Quantity = usage.Quantity });
                }
            }

            return detail;
        }
    }
}
