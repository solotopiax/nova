/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ThirdPayProductCatalogState.cs
 * author:    yingzheng
 * created:   2026/9/16
 * descrip:   ThirdPay 商品快照运行时状态
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.SDK.IAP.ThirdPay.Runtime
{
    /// <summary>
    /// 维护 ThirdPay 商品列表快照和在途请求上下文，避免 Store 直接散落请求版本字段。
    /// </summary>
    internal sealed class ThirdPayProductCatalogState
    {
        /// <summary>
        /// 最近一次成功拉取的第三方商品列表。
        /// </summary>
        private PbNetThirdProductListResp m_ProductList;

        /// <summary>
        /// 商品列表请求版本号，用于忽略旧国家或旧账号返回的过期响应。
        /// </summary>
        private int m_RequestVersion;

        /// <summary>
        /// 当前商品列表在途请求的版本号。
        /// </summary>
        private int m_FetchVersion;

        /// <summary>
        /// 当前商品列表在途请求对应的 GameUID。
        /// </summary>
        private string m_FetchUid = string.Empty;

        /// <summary>
        /// 当前商品列表在途请求对应的协议命令名。
        /// </summary>
        private string m_FetchCmdName = string.Empty;

        /// <summary>
        /// 当前商品列表在途请求对应的有效国家或地区代码。
        /// </summary>
        private string m_FetchCountryCode = string.Empty;

        /// <summary>
        /// 当前商品列表在途请求的共享完成源。
        /// </summary>
        private UniTaskCompletionSource<bool> m_FetchCompletion;

        /// <summary>
        /// 最近一次成功拉取的全部第三方支付商品。
        /// </summary>
        public IReadOnlyList<PbNetThirdProductInfo> ProductList =>
            m_ProductList?.ProductList != null ? m_ProductList.ProductList : Array.Empty<PbNetThirdProductInfo>();

        /// <summary>
        /// 当前是否已有非空商品快照。
        /// </summary>
        public bool HasProductList => m_ProductList?.ProductList != null && m_ProductList.ProductList.Count > 0;

        /// <summary>
        /// 当前在途商品请求的完成源。
        /// </summary>
        public UniTaskCompletionSource<bool> FetchCompletion => m_FetchCompletion;

        /// <summary>
        /// 清空商品快照和在途请求，并重置版本号。
        /// </summary>
        public void Reset()
        {
            m_ProductList = null;
            CompleteFetchAsInvalidated();
            m_RequestVersion = 0;
        }

        /// <summary>
        /// 使当前商品快照和在途请求失效。
        /// </summary>
        public void Invalidate()
        {
            m_RequestVersion++;
            m_ProductList = null;
            CompleteFetchAsInvalidated();
        }

        /// <summary>
        /// 判断当前是否已有相同上下文的商品列表在途请求可供复用。
        /// </summary>
        /// <param name="requestUid">请求发起时的 GameUID。</param>
        /// <param name="requestCmdName">商品列表协议命令名。</param>
        /// <param name="requestCountryCode">请求使用的有效国家或地区代码。</param>
        /// <returns>同一请求仍在途且未被版本号失效时返回 true。</returns>
        public bool IsSameFetchInFlight(string requestUid, string requestCmdName, string requestCountryCode)
        {
            return m_FetchCompletion != null
                && m_FetchVersion == m_RequestVersion
                && string.Equals(m_FetchUid, requestUid, StringComparison.Ordinal)
                && string.Equals(m_FetchCmdName, requestCmdName, StringComparison.Ordinal)
                && string.Equals(m_FetchCountryCode, requestCountryCode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 记录一次新的商品列表在途请求。
        /// </summary>
        /// <param name="requestUid">请求发起时的 GameUID。</param>
        /// <param name="requestCmdName">商品列表协议命令名。</param>
        /// <param name="requestCountryCode">请求使用的有效国家或地区代码。</param>
        /// <param name="completion">新建的共享完成源。</param>
        /// <returns>本次请求使用的版本号。</returns>
        public int BeginFetch(string requestUid, string requestCmdName, string requestCountryCode, out UniTaskCompletionSource<bool> completion)
        {
            int requestVersion = ++m_RequestVersion;
            completion = new UniTaskCompletionSource<bool>();
            m_FetchVersion = requestVersion;
            m_FetchUid = requestUid;
            m_FetchCmdName = requestCmdName;
            m_FetchCountryCode = requestCountryCode;
            m_FetchCompletion = completion;
            return requestVersion;
        }

        /// <summary>
        /// 判断响应是否仍属于当前账号、协议和国家码上下文。
        /// </summary>
        /// <param name="requestVersion">请求发起时的商品列表版本号。</param>
        /// <param name="requestUid">请求发起时的 GameUID。</param>
        /// <param name="requestCmdName">请求发起时的协议命令名。</param>
        /// <param name="requestCountryCode">请求发起时的国家或地区代码。</param>
        /// <param name="currentUid">当前 GameUID。</param>
        /// <param name="currentCmdName">当前配置中的协议命令名。</param>
        /// <param name="currentCountryCode">当前有效国家或地区代码。</param>
        /// <returns>响应仍可覆盖商品快照时返回 true。</returns>
        public bool CanApplyResponse(
            int requestVersion,
            string requestUid,
            string requestCmdName,
            string requestCountryCode,
            string currentUid,
            string currentCmdName,
            string currentCountryCode)
        {
            return requestVersion == m_RequestVersion
                && string.Equals(requestUid, currentUid, StringComparison.Ordinal)
                && string.Equals(requestCmdName, currentCmdName, StringComparison.Ordinal)
                && string.Equals(requestCountryCode, currentCountryCode, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 更新最近一次成功拉取的商品快照。
        /// </summary>
        /// <param name="productList">服务端商品列表响应。</param>
        public void SetProductList(PbNetThirdProductListResp productList)
        {
            m_ProductList = productList;
        }

        /// <summary>
        /// 在账号、国家码或释放流程使商品请求失效时，结束当前共享等待并清空在途状态。
        /// </summary>
        public void CompleteFetchAsInvalidated()
        {
            UniTaskCompletionSource<bool> completion = m_FetchCompletion;
            ResetFetchState();
            completion?.TrySetResult(false);
        }

        /// <summary>
        /// 当前完成源仍为指定请求时清空在途状态。
        /// </summary>
        /// <param name="completion">本次后台请求持有的共享完成源。</param>
        public void ClearFetchIfCurrent(UniTaskCompletionSource<bool> completion)
        {
            if (ReferenceEquals(m_FetchCompletion, completion))
            {
                ResetFetchState();
            }
        }

        /// <summary>
        /// 按第三方商品 ID 查找商品快照。
        /// </summary>
        /// <param name="thirdProductId">第三方商品 ID。</param>
        /// <returns>匹配的第三方商品；未命中时返回 null。</returns>
        public PbNetThirdProductInfo FindProductInfo(string thirdProductId)
        {
            if (string.IsNullOrEmpty(thirdProductId) || m_ProductList?.ProductList == null)
            {
                return null;
            }

            foreach (PbNetThirdProductInfo product in m_ProductList.ProductList)
            {
                if (string.Equals(product.ProductId, thirdProductId, StringComparison.Ordinal))
                {
                    return product;
                }
            }

            return null;
        }

        /// <summary>
        /// 清空商品列表在途请求的上下文字段。
        /// </summary>
        private void ResetFetchState()
        {
            m_FetchVersion = 0;
            m_FetchUid = string.Empty;
            m_FetchCmdName = string.Empty;
            m_FetchCountryCode = string.Empty;
            m_FetchCompletion = null;
        }
    }
}
