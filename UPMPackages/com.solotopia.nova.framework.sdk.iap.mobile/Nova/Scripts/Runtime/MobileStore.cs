/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStore.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   Google Play + iOS App Store 官方内购 store 入口（Unity IAP 5.x）
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using NovaFramework.SDK.IAP.Runtime;
using UnityEngine.Purchasing;
using ProductInfo = NovaFramework.SDK.IAP.Runtime.ProductInfo;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// Google Play 与 iOS App Store 官方移动内购 store（Unity IAP 5.x）。
    /// 职责收窄为对外接口实现；StoreController 事件由 StoreService 统一路由给各内部服务；
    /// 核心业务逻辑分布在 8 个内部服务中（Extended/Store/Init/Purchase/Validation/Product/Restore/Subscription）。
    /// </summary>
    [IAPStore]
    public sealed partial class MobileStore : IAPStoreBase, IIAPMobileQueryCapable, IIAPMobileSubscriptionCapable
    {
        /// <summary>
        /// 当前 store 的渠道类型，固定为 Mobile。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.Mobile;

        /// <summary>
        /// 判断当前 store 是否能处理指定请求，仅接受 IAPMobileRequest 类型。
        /// </summary>
        /// <param name="request">待判断的支付请求。</param>
        /// <returns>请求为 IAPMobileRequest 时返回 true，否则返回 false。</returns>
        public override bool CanHandle(IAPRequest request) => request is IAPMobileRequest;

        /// <summary>
        /// 异步初始化 store：创建内部服务，通过 Unity IAP 5.x StoreController.Connect() 连接平台商店，触发补单扫描。
        /// </summary>
        /// <param name="table">所有 store 共用的商品表接口。</param>
        /// <param name="config">store 专属配置，应为 MobileStoreConfig 实例；为 null 时以空配置降级运行。</param>
        /// <param name="ctx">运行时上下文，包含跨模块依赖引用。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        public override async UniTask InitializeAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct)
        {
            await base.InitializeAsync(table, config, ctx, ct);

            var mobileConfig = config as MobileStoreConfig ?? new MobileStoreConfig();
            m_Hub = new MobileServiceHub(ctx, mobileConfig, table, this);

            // 网络验单/查单协议层
            m_Hub.PayService = new MobileIapNetService();

            // ExtendedService 和 StoreService 必须先建，InitService.InitializeAsync 中会通过 hub 引用它们
            // StoreController 唯一调用收口
            m_Hub.ExtendedService = new MobileExtendedService(m_Hub);
            // On* 平台回调统一路由入口
            m_Hub.StoreService = new MobileStoreService(m_Hub);
            // IAP 初始化生命周期
            m_Hub.InitService = new MobileInitService(m_Hub);
            // 商品缓存与票据解析
            m_Hub.ProductService = new MobileProductService(m_Hub);
            // 订阅到期持久化与倒计时
            m_Hub.SubscriptionService = new MobileSubscriptionService(m_Hub);
            // 订单状态机与验单队列
            m_Hub.ValidationService = new MobileValidationService(m_Hub);
            // Restore 流程协调
            m_Hub.RestoreService = new MobileRestoreService(m_Hub);
            // 购买发起与平台回调处理
            m_Hub.PurchaseService = new MobilePurchaseService(m_Hub);

            // 存档读写须等 SetUserId 拿到 m_GameUID 之后才能进行；初始化阶段先用空容器占位。
            m_PersistData = (MobileStorePersistData)CreateEmptyPersistData();

            bool ok = await m_Hub.InitService.InitializeAsync(table, ct);
            if (!ok)
            {
                LogWarning("Unity IAP 初始化失败，支付功能不可用。");
                return;
            }
        }

        /// <summary>
        /// 异步发起支付流程，路由给 MobilePurchaseService。
        /// EnableAlwaysPaySucceed 开启时跳过真实平台调用直接返回成功。
        /// </summary>
        /// <param name="request">支付请求。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>包含支付结果的 IAPResult。</returns>
        public override UniTask<IAPResult> PayAsync(IAPRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            m_LoadingGuard.HasUserInteracted = true;
            if (Context.EnableAlwaysPaySucceed)
            {
                TrackBuyInternal(request.TableId, null, request.CustomData);
                return UniTask.FromResult(new IAPResult(request.TableId, "MOCK_ORDER_MOBILE", false, true, request.CustomData));
            }

            return PayGuardAsync(request, ct, () => m_Hub.PurchaseService.PayAsync(request as IAPMobileRequest, ct));
        }

        /// <summary>
        /// 异步恢复历史已购订单，路由给 MobileRestoreService。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>恢复到的历史订单结果列表。</returns>
        public override UniTask<IReadOnlyList<IAPResult>> RestorePurchasesAsync(CancellationToken ct) => m_Hub.RestoreService.RestoreAsync(ct);

        /// <summary>
        /// 释放 store 资源，依次释放各服务。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        public override async UniTask DisposeAsync(CancellationToken ct)
        {
            // 终止进行中支付，释放 TCS
            m_Hub?.PurchaseService?.Dispose();
            // 强制结束进行中 Restore
            m_Hub?.RestoreService?.Dispose();
            // 清空验单队列与 TCS
            m_Hub?.ValidationService?.Dispose();
            // 清空票据与权益缓存
            m_Hub?.ProductService?.Dispose();
            // 取消订阅倒计时
            m_Hub?.SubscriptionService?.Dispose();
            // 通知 ExtendedService 清空 Controller
            m_Hub?.InitService?.Dispose();
            // 兜底清空 Controller 引用（Dispose 幂等）
            m_Hub?.ExtendedService?.Dispose();
            // 清空 receipt 解析缓存
            MobileReceiptParser.ClearCache();
            // 清空当前运行期埋点去重缓存
            ClearTrackRuntimeCacheInternal();
            // 断开存档引用
            m_PersistData = null;
            await base.DisposeAsync(ct);
        }

        /// <summary>
        /// 切换用户 UID，同步通知 ValidationService 重新加载存档；
        /// 同 UID 重复调用时跳过下游存档重读，保持 SetUserId 幂等语义。
        /// </summary>
        /// <param name="uid">已登录用户的唯一 ID。</param>
        public override void SetUserId(string uid)
        {
            LogDebug($"SetUserId: {uid}");
            string oldUid = m_GameUID;
            base.SetUserId(uid);
            if (m_GameUID == oldUid)
            {
                return;
            }

            LoadPersistDataInternal();
        }

        /// <summary>
        /// 扫描本地未完成订单并触发补单验单流程，路由给 MobileValidationService。
        /// 须在用户登录成功、SetUserId 调用后手动触发。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>补单扫描完成的异步任务。</returns>
        public override async UniTask CheckLocalOrdersAsync(CancellationToken ct)
        {
            await m_Hub.ValidationService.CheckLocalOrdersAsync(ct);
        }

        /// <summary>
        /// 设置 Android 订阅升降级的 ProrationMode。
        /// </summary>
        /// <param name="replaceMode">Google Play ProrationMode 枚举整数值。</param>
        public void SetSubscriptionReplaceMode(int replaceMode) => m_Hub?.PurchaseService?.SetSubscriptionReplaceMode(replaceMode);

        /// <summary>
        /// 批量查询平台商品信息（本地化价格、标题等）。
        /// </summary>
        /// <param name="productIds">商品 ID 列表。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>查询到的商品信息列表。</returns>
        public UniTask<IReadOnlyList<ProductInfo>> QueryProductsAsync(IReadOnlyList<string> productIds, CancellationToken ct) => m_Hub.ProductService.QueryProductsAsync(productIds, ct);

        /// <summary>
        /// 获取订阅到期时间戳（毫秒）。
        /// </summary>
        /// <param name="tableId">订阅商品配置表行 ID。</param>
        /// <returns>到期 Unix 毫秒时间戳；未订阅时返回 0。</returns>
        public long GetSubscriptionExpireTime(long tableId) => m_Hub?.SubscriptionService?.GetExpireTimeMs(tableId) ?? 0L;

        /// <summary>
        /// 判断指定商品是否存在非消耗品持有标记。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>持有非消耗品时返回 true，否则返回 false。</returns>
        public bool HasNonConsumeProduct(long tableId)
        {
            if (m_PersistData?.NonConsumeOwnership == null)
            {
                return false;
            }

            return m_PersistData.NonConsumeOwnership.TryGetValue(tableId, out bool owned) && owned;
        }

        /// <summary>
        /// 根据 tableId 同步获取平台商品信息：先经 PurchasesTable 解析 ProductID，再向 Unity IAP 5.x StoreController 查询元数据。
        /// InitService 未就绪、tableId 未配置或平台未注册该商品任一不满足均返回 null，调用方自行 null 检查。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>对应的 ProductInfo；未命中时返回 null。</returns>
        public ProductInfo GetProductInfo(long tableId)
        {
            if (m_Hub?.InitService?.IsReady != true || m_Hub.ExtendedService?.IsAttached != true)
            {
                return null;
            }

            IAPProductEntry entry = m_Hub.Table?.FindByTableId(tableId);
            if (entry == null)
            {
                return null;
            }

            Product p = m_Hub.ExtendedService.GetProductById(entry.ProductID);
            if (p == null)
            {
                return null;
            }

            return new ProductInfo(p.definition.id, p.metadata.localizedPriceString, p.metadata.isoCurrencyCode, p.metadata.localizedTitle);
        }
    }
}
