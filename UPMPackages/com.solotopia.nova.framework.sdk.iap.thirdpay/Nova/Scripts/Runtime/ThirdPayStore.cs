/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   第三方支付 store 实现（Browser / InAppAuto，客户端造单 + AES URL 直跳）
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.SDK.IAP.ThirdPay.Runtime.Internal;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 第三方支付 store。
    /// 客户端造订单号 + AES 加密 query 拼接 PayUrlBase，
    /// Browser 模式跳系统浏览器（默认实现）/ InAppAuto 模式由业务层覆写 OpenAsync 接入 WebView。
    /// 跨会话补单在 SetUserId 时机执行，存档统一容器为 ThirdPayPersistData，按 {uid} 隔离。
    /// </summary>
    [IAPStore]
    public partial class ThirdPayStore : IAPStoreBase
    {
        /// <summary>
        /// 当前 store 的渠道类型，固定为 ThirdPay。
        /// </summary>
        public override IAPStoreType StoreType => IAPStoreType.ThirdPay;

        /// <summary>
        /// 异步初始化 store：保存配置、解析 NetCmd 注入业务网络 Service、构造空存档容器、初始化 Google 合规扩展。
        /// 不读盘——读盘动作迁移到 SetUserId 时机，因为 InitializeAsync 阶段 m_GameUID 通常为空。
        /// </summary>
        /// <param name="table">所有 store 共用的商品表（IAP 接口）。</param>
        /// <param name="config">store 专属配置，应为 ThirdPayStoreConfig 类型。</param>
        /// <param name="ctx">运行时上下文，包含跨模块依赖引用。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>初始化完成的异步任务。</returns>
        public override async UniTask InitializeAsync(IIAPProductTable table, IIAPStoreConfig config, IIAPStoreContext ctx, CancellationToken ct)
        {
            await base.InitializeAsync(table, config, ctx, ct);
            m_Config = config as ThirdPayStoreConfig;

            m_IapNetService = new ThirdIapNetService();

            m_PersistData = (ThirdPayPersistData)CreateEmptyPersistData();
            m_GoogleExpand = new ThirdPayGoogleExpand();
        }

        /// <summary>
        /// 判断当前 store 是否能处理指定请求，仅接受 IAPThirdPayRequest 类型。
        /// </summary>
        /// <param name="request">待判断的支付请求。</param>
        /// <returns>请求为 IAPThirdPayRequest 时返回 true，否则返回 false。</returns>
        public override bool CanHandle(IAPRequest request)
        {
            return request is IAPThirdPayRequest;
        }

        /// <summary>
        /// 异步发起第三方支付流程。
        /// EnableAlwaysPaySucceed 开启时跳过真实平台调用直接返回成功；
        /// 通过 PayGuardAsync 守卫（已内置 Enabled / IsInPay 检查）；
        /// ExecutePayFlowAsync 完成主链路。
        /// </summary>
        /// <param name="request">支付请求，已通过 CanHandle 确认为 IAPThirdPayRequest。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>包含支付结果和订单信息的 IAPResult。</returns>
        public override UniTask<IAPResult> PayAsync(IAPRequest request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (Context.EnableAlwaysPaySucceed)
                return UniTask.FromResult(new IAPResult(request.TableId, "MOCK_ORDER_THIRDPAY", false, true, request.CustomData));

            return PayGuardAsync(request, ct, async () =>
            {
                var thirdRequest = (IAPThirdPayRequest)request;
                m_InPayTableId = request.TableId;
                try
                {
                    IAPThirdOpenMode mode = GetCurrentOpenMode();
                    return await ExecutePayFlowAsync(thirdRequest, mode, ct);
                }
                finally
                {
                    m_InPayTableId = 0;
                }
            });
        }

        /// <summary>
        /// 设置当前账号 UID 并触发 SetUserId 三件事：
        /// 加载存档 → 跨会话补单 → CID 拉取 → Google token 预取。
        /// 同账号重复调用时幂等返回。
        /// </summary>
        /// <param name="uid">用户 UID（字符串形式）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>设置完成的异步任务。</returns>
        public async UniTask SetUserIdAsync(string uid, CancellationToken ct)
        {
            string oldUid = m_GameUID;
            base.SetUserId(uid);
            if (m_GameUID == oldUid) return;
            LoadPersistDataForAccount();
            await RestorePendingOrdersAsync(ct);
            await FetchChannelParamsAsync(ct);
            m_GoogleExpand?.PrefetchAsync().Forget();
        }

        /// <summary>
        /// 异步拉取服务端第三方商品列表，成功后可通过 GetProductInfo / GetPayTypeList 访问。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>是否拉取成功。</returns>
        public async UniTask<bool> FetchProductListAsync(CancellationToken ct)
        {
            return await FetchProductListInternalAsync(m_GameUID ?? string.Empty, ct);
        }

        /// <summary>
        /// 根据配置表行 ID 获取第三方商品信息。
        /// 需在 FetchProductListAsync 成功后调用，否则返回 null。
        /// </summary>
        /// <param name="tableId">商品配置表行 ID。</param>
        /// <returns>商品信息；未找到时返回 null。</returns>
        public PbNetProductInfo GetProductInfo(long tableId)
        {
            return GetProductInfoByTableId(tableId);
        }

        /// <summary>
        /// 获取当前可用的第三方支付渠道列表。
        /// 上次支付成功的渠道（m_PersistData.LastPayMethod）将排在列表首位。
        /// 需在 FetchProductListAsync 成功后调用，否则返回空列表。
        /// </summary>
        /// <returns>支付渠道列表，首项为上次成功支付的渠道（若存在）。</returns>
        public List<PbNetPayTypeInfo> GetPayTypeList()
        {
            if (m_ProductListInfo?.PayTypeList == null)
                return new List<PbNetPayTypeInfo>();

            var list = new List<PbNetPayTypeInfo>(m_ProductListInfo.PayTypeList);
            string lastPayMethod = m_PersistData?.LastPayMethod;
            if (!string.IsNullOrEmpty(lastPayMethod))
            {
                int idx = list.FindIndex(t => t.PayMethod == lastPayMethod);
                if (idx > 0)
                {
                    var first = list[idx];
                    list.RemoveAt(idx);
                    list.Insert(0, first);
                }
            }
            return list;
        }

        /// <summary>
        /// 设置当前国家/地区代码，写入后会用于后续的商品列表拉取和支付 URL 构造。
        /// </summary>
        /// <param name="code">ISO 3166-1 alpha-2 国家代码（如 "US"、"CN"）。</param>
        public void SetCountryCode(string code)
        {
            m_CountryCode = code ?? string.Empty;
        }

        /// <summary>
        /// 当前平台对应的打开模式：Android → AndroidOpenMode；iOS → IosOpenMode；其他平台兜底 Browser。
        /// </summary>
        /// <returns>当前平台的打开模式。</returns>
        private IAPThirdOpenMode GetCurrentOpenMode()
        {
#if UNITY_ANDROID
            return m_Config?.AndroidOpenMode ?? IAPThirdOpenMode.Browser;
#elif UNITY_IOS
            return m_Config?.IosOpenMode ?? IAPThirdOpenMode.Browser;
#else
            return IAPThirdOpenMode.Browser;
#endif
        }

        /// <summary>
        /// 释放 store 资源，清空运行时状态。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>释放完成的异步任务。</returns>
        public override async UniTask DisposeAsync(CancellationToken ct)
        {
            m_InPayTableId = 0;
            m_ProductListInfo = null;
            m_PersistData = null;
            m_GoogleExpand = null;
            m_BrowserOpenWaiter = null;
            m_IapNetService = null;
            m_Config = null;
            await base.DisposeAsync(ct);
        }

        /// <summary>
        /// 覆写基类工厂，提供 ThirdPayStore 专属空存档容器。
        /// </summary>
        /// <returns>已 EnsureInitialized 的 ThirdPayPersistData 实例。</returns>
        protected override IIAPStorePersistData CreateEmptyPersistData()
        {
            var data = new ThirdPayPersistData();
            data.EnsureInitialized();
            return data;
        }
    }
}
