/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayStore.Methods.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   ThirdPayStore 私有/受保护方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.SDK.IAP.Runtime;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    public partial class ThirdPayStore
    {
        /// <summary>
        /// 从持久化层加载当前账号的存档；不存在或损坏时落回 IAPStoreBase 提供的空容器。
        /// </summary>
        private void LoadPersistDataForAccount()
        {
            m_PersistData = LoadPersistData<ThirdPayPersistData>();
        }

        /// <summary>
        /// 将进行中订单写入存档字典并持久化。
        /// 同一 tableId 重复写入时覆盖（确保只记录最新订单）。
        /// </summary>
        /// <param name="order">订单上下文。</param>
        private void AddOrderToStore(ThirdOrderData order)
        {
            if (m_PersistData == null) return;
            m_PersistData.OrderingStates[order.TableId] = order;
            SavePersistData(m_PersistData);
        }

        /// <summary>
        /// 从存档字典移除指定订单并持久化。
        /// tableId 不存在时静默跳过。
        /// </summary>
        /// <param name="tableId">配置表行 ID。</param>
        private void RemoveOrderFromStore(long tableId)
        {
            if (m_PersistData?.OrderingStates == null) return;
            if (!m_PersistData.OrderingStates.ContainsKey(tableId)) return;
            m_PersistData.OrderingStates.Remove(tableId);
            SavePersistData(m_PersistData);
        }

        /// <summary>
        /// 异步拉取第三方商品列表，失败时按固定次数循环重试。
        /// </summary>
        /// <param name="uid">用户 UID（string 形式）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>是否成功拉取商品列表。</returns>
        private async UniTask<bool> FetchProductListInternalAsync(string uid, CancellationToken ct)
        {
            if (m_IapNetService == null)
            {
                Log.Warning(LogTag.IAPThirdPay, "FetchProductList：IapNetService 未初始化。");
                return false;
            }

            var req = new PbNetThirdProductListReq
            {
                Head = NetBuilder.BuildHeader(),
                Country = m_CountryCode ?? string.Empty,
            };

            int maxRetry = 3;
            for (int i = 0; i <= maxRetry; i++)
            {
                ct.ThrowIfCancellationRequested();
                var resp = await m_IapNetService.GetProductListAsync(m_Config?.GetProductListCmdName, req);
                if (resp.IsSuccess && resp.Data != null)
                {
                    m_ProductListInfo = resp.Data;
                    return true;
                }
                Log.Warning(LogTag.IAPThirdPay, $"FetchProductList 失败 attempt={i}，uid={uid}");
            }
            return false;
        }

        /// <summary>
        /// 异步发起第三方支付完整流程：补单短路 → 客户端造单号 → 取 Google token → AES URL → Open → 验单 → 合规上报。
        /// </summary>
        /// <param name="request">第三方支付请求。</param>
        /// <param name="mode">当前平台对应的打开模式。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>支付结果。</returns>
        private async UniTask<IAPResult> ExecutePayFlowAsync(IAPThirdPayRequest request, IAPThirdOpenMode mode, CancellationToken ct)
        {
            long tableId = request.TableId;

            // ① 已存在未完成订单 → 直接走验证链
            if (m_PersistData?.OrderingStates != null
                && m_PersistData.OrderingStates.TryGetValue(tableId, out var pending))
            {
                return await ValidateOrderAsync(pending, isRecovered: true, ct);
            }

            // ② 客户端造单号 + 取 Google token（仅 InAppAuto）
            string uid = m_GameUID ?? string.Empty;
            string clientOrderId = GenerateOrderId();
            string googleToken = null;
            if (mode == IAPThirdOpenMode.InAppAuto && m_GoogleExpand != null)
                googleToken = await m_GoogleExpand.ConsumeTokenAsync();

            // ③ 构造存档项 + 加密 URL
            var orderData = new ThirdOrderData
            {
                TableId = tableId,
                Uid = uid,
                ClientOrderId = clientOrderId,
                CustomString = request.CustomData?.Extra,
            };
            string url = BuildAesUrl(orderData, request, mode, googleToken);
            if (string.IsNullOrEmpty(url))
                return new IAPResult(tableId, (int)IAPThirdPayErrorCode.StoreInitFailed, "构造支付 URL 失败。", request.CustomData);

            // ④ 落盘后再 Open（避免崩溃丢单）
            AddOrderToStore(orderData);

            // ⑤ 打开 Browser / WebView
            var openResult = await OpenAsync(url, mode, request.AdaptRectTransform, ct);

            // ⑥ 三态结果
            switch (openResult)
            {
                case ThirdPayOpenResult.Cancel:
                    PbNetProductInfo productForTrack = GetProductInfoByTableId(tableId);
                    string productIdForTrack = productForTrack?.ProductId ?? string.Empty;
                    float priceForTrack = ParseLocalPrice(productForTrack?.LocalPrice);
                    bool debugForTrack = Context?.EnableAlwaysPaySucceed ?? false;
                    TrackThirdPayCloseOrder(tableId, productIdForTrack, debugForTrack, priceForTrack, clientOrderId, request.PayTypeId, request.CustomData);
                    RemoveOrderFromStore(tableId);
                    return new IAPResult(tableId, (int)IAPThirdPayErrorCode.UserCancelled, "用户取消支付。", request.CustomData);

                case ThirdPayOpenResult.Failed:
                    return new IAPResult(tableId, (int)IAPThirdPayErrorCode.NetworkError, "打开支付页失败。", request.CustomData);

                case ThirdPayOpenResult.Success:
                default:
                    var result = await ValidateOrderAsync(orderData, isRecovered: false, ct);
                    if (result.IsSuccess && !string.IsNullOrEmpty(googleToken) && m_GoogleExpand != null)
                        await m_GoogleExpand.ReportAsync(clientOrderId, googleToken);
                    return result;
            }
        }

        /// <summary>
        /// 单订单验单封装：包成单元素列表后走批量验单入口，再按 TableId 取出对应结果。
        /// </summary>
        /// <param name="order">订单上下文（含 TableId / Uid / ClientOrderId）。</param>
        /// <param name="isRecovered">是否为补单（跨会话恢复）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>支付结果。</returns>
        private async UniTask<IAPResult> ValidateOrderAsync(ThirdOrderData order, bool isRecovered, CancellationToken ct)
        {
            var results = await ValidateOrdersAsync(new List<ThirdOrderData> { order }, isRecovered, ct);
            foreach (var r in results)
            {
                if (r.TableId == order.TableId) return r;
            }
            return new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, "验单失败超出重试上限。", null);
        }

        /// <summary>
        /// 异步批量验单：将多笔订单的 client_order_id 一次性塞进 PbNetThirdCheckOrderReq.ClientOrderIds 发起一次请求。
        /// 失败时按 s_ValidateRetryIntervals 间隔迭代重试；补单（isRecovered=true）只重试 1 次。
        /// </summary>
        /// <param name="orders">订单上下文列表。</param>
        /// <param name="isRecovered">是否为补单（跨会话恢复）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>每笔订单对应的支付结果列表（顺序与入参一致）。</returns>
        private async UniTask<List<IAPResult>> ValidateOrdersAsync(List<ThirdOrderData> orders, bool isRecovered, CancellationToken ct)
        {
            if (orders == null || orders.Count == 0)
                return new List<IAPResult>();

            if (m_IapNetService == null)
            {
                Log.Warning(LogTag.IAPThirdPay, "ValidateOrders：IapNetService 未初始化。");
                return BuildAllFailedResults(orders, IAPThirdPayErrorCode.ServerValidationFailed, "验单 NetService 未初始化。");
            }

            var req = new PbNetThirdCheckOrderReq
            {
                Head = NetBuilder.BuildHeader(),
            };
            foreach (var o in orders)
                req.ClientOrderIds.Add(o.ClientOrderId);

            int maxRetry = isRecovered ? 1 : s_ValidateRetryIntervals.Length;

            for (int retryIndex = 0; retryIndex < maxRetry; retryIndex++)
            {
                if (retryIndex > 0)
                    await UniTask.Delay(TimeSpan.FromSeconds(s_ValidateRetryIntervals[retryIndex]), cancellationToken: ct);

                ct.ThrowIfCancellationRequested();
                var resp = await m_IapNetService.CheckOrderAsync(m_Config?.CheckOrderCmdName, req);

                if (!resp.IsSuccess || resp.Data == null)
                {
                    Log.Warning(LogTag.IAPThirdPay, $"批量验单失败，retryIndex={retryIndex}，count={orders.Count}");
                    continue;
                }

                return ProcessValidateResponses(orders, resp.Data, isRecovered);
            }

            return BuildAllFailedResults(orders, IAPThirdPayErrorCode.ServerValidationFailed, "验单失败超出重试上限。");
        }

        /// <summary>
        /// 构造一组「整体失败」的结果，长度与订单列表一致。
        /// </summary>
        /// <param name="orders">订单列表。</param>
        /// <param name="code">错误码。</param>
        /// <param name="msg">错误信息。</param>
        /// <returns>每笔订单对应一个失败结果。</returns>
        private static List<IAPResult> BuildAllFailedResults(List<ThirdOrderData> orders, IAPThirdPayErrorCode code, string msg)
        {
            var results = new List<IAPResult>(orders.Count);
            foreach (var o in orders)
                results.Add(new IAPResult(o.TableId, (int)code, msg, null));
            return results;
        }

        /// <summary>
        /// 解析批量验单响应并按入参订单顺序返回每笔订单的支付结果。
        /// Status 3/5（PaySuccess/ReceivedAward）→ 成功；4/1（PayFailed/NotPay）→ 失败；其他 → 处理中。
        /// 成功时写 m_PersistData.LastPayMethod 并通过 RemoveOrderFromStore 单点存档。
        /// </summary>
        /// <param name="orders">入参订单列表。</param>
        /// <param name="checkResp">服务端验单响应。</param>
        /// <param name="isRecovered">是否为补单。</param>
        /// <returns>每笔订单对应的支付结果列表（顺序与入参一致）。</returns>
        private List<IAPResult> ProcessValidateResponses(List<ThirdOrderData> orders, PbNetThirdCheckOrderResp checkResp, bool isRecovered)
        {
            var results = new List<IAPResult>(orders.Count);
            if (checkResp?.List == null || checkResp.List.Count == 0)
            {
                foreach (var o in orders)
                    results.Add(new IAPResult(o.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, "验单响应无订单信息。", null));
                return results;
            }

            foreach (var order in orders)
            {
                PbNetThirdCheckOrderInfo matched = null;
                foreach (var item in checkResp.List)
                {
                    if (item.ClientOrderId == order.ClientOrderId)
                    {
                        matched = item;
                        break;
                    }
                }

                if (matched == null)
                {
                    results.Add(new IAPResult(order.TableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, "验单响应未包含该订单。", null));
                    continue;
                }

                long itemTableId = matched.TableId != 0 ? matched.TableId : order.TableId;
                string payMethod = ParseStringField(matched.CustomParam, "pay_method") ?? string.Empty;

                switch (matched.Status)
                {
                    case c_StatusPaySuccess:
                    case c_StatusReceivedAward:
                        if (m_PersistData != null)
                        {
                            m_PersistData.LastPayMethod = payMethod;
                            // RemoveOrderFromStore 内部已 SavePersistData，单点存档
                        }
                        RemoveOrderFromStore(itemTableId);
                        results.Add(new IAPResult(itemTableId, matched.OrderId, isRecovered, true, null));
                        break;

                    case c_StatusPayFailed:
                    case c_StatusNotPay:
                        RemoveOrderFromStore(itemTableId);
                        results.Add(new IAPResult(itemTableId, (int)IAPThirdPayErrorCode.ServerValidationFailed, $"第三方验单失败，状态={matched.Status}。", null));
                        break;

                    default:
                        results.Add(new IAPResult(itemTableId, (int)IAPThirdPayErrorCode.StoreNotAvailable, "订单处理中，请稍后重试。", null));
                        break;
                }
            }
            return results;
        }

        /// <summary>
        /// 客户端生成订单 ID：本地时间戳 yyyyMMddHHmmss + 一段 GUID 截取。
        /// </summary>
        /// <param name="length">GUID 截取长度。</param>
        /// <returns>客户端生成的订单 ID。</returns>
        private static string GenerateOrderId(int length = 8)
        {
           return DateTime.Now.ToString("yyyyMMddHHmmss") + System.Guid.NewGuid().ToString("N").Substring(0,length);
        }

        /// <summary>
        /// 构造 AES 加密后的支付页 URL。
        /// query 字段：uid / order_id / table_id / custom / country / app_id / pay_type_id / pay_method / cid（+ google_token，仅 InAppAuto）。
        /// AES Key/IV 取自 ConfigManager.Common.AppAesKey/AppAesIV；PayUrlBase 取自 m_Config.PayUrlBase。
        /// </summary>
        /// <param name="order">订单上下文。</param>
        /// <param name="request">支付请求。</param>
        /// <param name="mode">当前打开模式（决定是否注入 google_token）。</param>
        /// <param name="googleToken">Google ExternalOfferToken；非 InAppAuto 模式或为空时跳过。</param>
        /// <returns>完整的支付 URL；AesKey 缺失或异常时返回 null。</returns>
        private string BuildAesUrl(ThirdOrderData order, IAPThirdPayRequest request, IAPThirdOpenMode mode, string googleToken)
        {
            var configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            string aesKey = configManager?.Common?.AppAesKey;
            string aesIV = configManager?.Common?.AppAesIV;
            if (string.IsNullOrEmpty(aesKey) || string.IsNullOrEmpty(aesIV))
            {
                Log.Error(LogTag.IAPThirdPay, "ConfigManager.Common.AppAesKey/AppAesIV 为空，无法构造支付 URL。");
                return null;
            }
            if (string.IsNullOrEmpty(m_Config?.PayUrlBase))
            {
                Log.Error(LogTag.IAPThirdPay, "ThirdPayStoreConfig.PayUrlBase 未配置。");
                return null;
            }

            var query = new Dictionary<string, string>
            {
                ["uid"] = order.Uid ?? string.Empty,
                ["order_id"] = order.ClientOrderId,
                ["table_id"] = order.TableId.ToString(),
                ["custom"] = order.CustomString ?? string.Empty,
                ["country"] = m_CountryCode ?? string.Empty,
                ["app_id"] = ParseAppId().ToString(),
                ["pay_type_id"] = request.PayTypeId.ToString(),
                ["pay_method"] = request.PayMethod ?? string.Empty,
                ["cid"] = m_PersistData?.ChannelParams ?? string.Empty,
            };
            if (mode == IAPThirdOpenMode.InAppAuto && !string.IsNullOrEmpty(googleToken))
                query["google_token"] = googleToken;

            string rawQuery = BuildQueryString(query);

            string encrypted;
            try
            {
                encrypted = Util.Encrypt.AES.EncryptString(rawQuery, aesKey, aesIV);
            }
            catch (Exception ex)
            {
                Log.Error(LogTag.IAPThirdPay, $"AES 加密失败：{ex.Message}");
                return null;
            }
            if (string.IsNullOrEmpty(encrypted))
                return null;

            return $"{m_Config.PayUrlBase}?d={UnityWebRequestEscape(encrypted)}";
        }

        /// <summary>
        /// 将 key=value 字典拼成 URL query 字符串（不含前导 ?）；值经 URL 编码。
        /// </summary>
        /// <param name="kv">键值对。</param>
        /// <returns>编码后的 query 字符串。</returns>
        private static string BuildQueryString(Dictionary<string, string> kv)
        {
            var sb = new StringBuilder(256);
            bool first = true;
            foreach (var p in kv)
            {
                if (!first) sb.Append('&');
                sb.Append(p.Key).Append('=').Append(UnityWebRequestEscape(p.Value));
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// 对字符串做 URL 转义（包装 UnityWebRequest.EscapeURL，避免 null）。
        /// </summary>
        /// <param name="s">原字符串。</param>
        /// <returns>转义后字符串；null/空时返回空字符串。</returns>
        private static string UnityWebRequestEscape(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : UnityEngine.Networking.UnityWebRequest.EscapeURL(s);
        }

        /// <summary>
        /// 打开第三方支付页面（Browser 直跳浏览器 / InAppAuto 内嵌 WebView）。
        /// 框架内置默认 Browser 实现；InAppAuto 由业务层覆写。
        /// </summary>
        /// <param name="url">已构造好的支付 URL。</param>
        /// <param name="mode">当前打开模式。</param>
        /// <param name="adaptRect">WebView 适配锚点（仅 InAppAuto 使用）。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>三态打开结果。</returns>
        protected virtual UniTask<ThirdPayOpenResult> OpenAsync(string url, IAPThirdOpenMode mode, RectTransform adaptRect, CancellationToken ct)
        {
            if (mode == IAPThirdOpenMode.Browser)
                return OpenBrowserAsync(url, ct);
            throw new NotImplementedException("InAppAuto 模式必须由业务层覆写 ThirdPayStore.OpenAsync。");
        }

        /// <summary>
        /// 异步对本地存档中的所有未完成订单做一次批量验单（跨会话补单）。
        /// 利用 PbNetThirdCheckOrderReq.ClientOrderIds 一次性传入全部 client_order_id，避免逐单串行请求。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>补单完成的异步任务。</returns>
        private async UniTask RestorePendingOrdersAsync(CancellationToken ct)
        {
            if (m_PersistData?.OrderingStates == null || m_PersistData.OrderingStates.Count == 0)
                return;

            var pending = new List<ThirdOrderData>(m_PersistData.OrderingStates.Values);
            ct.ThrowIfCancellationRequested();
            await ValidateOrdersAsync(pending, isRecovered: true, ct);
        }

        /// <summary>
        /// 异步拉取渠道参数（CID）写入 m_PersistData.ChannelParams。
        /// 当前协议未定义 CID 接口；先以空实现占位，待协议补齐后再填入网络请求。
        /// </summary>
        /// <param name="ct">取消令牌。</param>
        /// <returns>拉取完成的异步任务。</returns>
        private UniTask FetchChannelParamsAsync(CancellationToken ct)
        {
            // 占位：当前协议未提供 CID 拉取接口；保留方法签名以稳定 SetAccountID 调用链。
            // 业务侧后续接入 CID 协议时，在此方法体内：
            //   1) 调用对应 NetCmd
            //   2) 将返回值写入 m_PersistData.ChannelParams
            //   3) 调用 SavePersistData(m_PersistData)
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 根据配置表行 ID 从已拉取的商品列表中查找商品信息。
        /// </summary>
        /// <param name="tableId">配置表行 ID。</param>
        /// <returns>对应的商品信息；未找到时返回 null。</returns>
        private PbNetProductInfo GetProductInfoByTableId(long tableId)
        {
            if (m_ProductListInfo?.ProductList == null) return null;
            foreach (var p in m_ProductListInfo.ProductList)
            {
                if (p.Id == tableId)
                    return p;
            }
            return null;
        }

        /// <summary>
        /// 从 JSON 字符串中提取指定 key 的字符串值。
        /// 仅处理简单扁平 JSON（"key":"value" 格式），供 custom_param 解析使用。
        /// </summary>
        /// <param name="json">JSON 字符串。</param>
        /// <param name="key">要提取的字段名。</param>
        /// <returns>字段值字符串；未找到或格式错误时返回 null。</returns>
        private static string ParseStringField(string json, string key)
        {
            if (string.IsNullOrEmpty(json)) return null;
            string searchKey = $"\"{key}\":\"";
            int start = json.IndexOf(searchKey, StringComparison.Ordinal);
            if (start < 0) return null;
            start += searchKey.Length;
            int end = json.IndexOf('"', start);
            if (end < 0) return null;
            return json.Substring(start, end - start);
        }

        /// <summary>
        /// 从 store 配置读取 AppId；Config 为 null 时返回 0。
        /// </summary>
        /// <returns>AppId 整数。</returns>
        private int ParseAppId()
        {
            return m_Config?.AppId ?? 0;
        }

        /// <summary>
        /// 将 PbNetProductInfo.LocalPrice 字符串解析为 float；空串/非数字返回 0。
        /// 打点字段 nova_price 使用，仅作弱一致诊断数值，不参与金额计算。
        /// </summary>
        /// <param name="localPrice">LocalPrice 字符串。</param>
        /// <returns>解析得到的 float 数值；失败返回 0。</returns>
        private static float ParseLocalPrice(string localPrice)
        {
            if (string.IsNullOrEmpty(localPrice)) return 0f;
            return float.TryParse(localPrice, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

    }
}
