/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductCatalogDtos.cs
 * author:    Codex
 * created:   2026/10/19
 * descrip:   Editor IAP 商品目录请求与冻结快照 DTO
 ***************************************************************/

using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using NovaFramework.Editor;

namespace NovaFramework.SDK.IAP.Editor
{
    /// <summary>
    /// 已冻结的 IAP 商品目录协议快照。
    /// </summary>
    public sealed class IAPProductCatalogSnapshot
    {
        /// <summary>
        /// 创建包含上传地址和请求数据的冻结快照。
        /// </summary>
        /// <param name="uploadUri">本次快照使用的上传地址。</param>
        /// <param name="request">已经冻结的最终请求数据。</param>
        internal IAPProductCatalogSnapshot(Uri uploadUri, IAPProductCatalogRequest request)
        {
            UploadUri = uploadUri ?? throw new ArgumentNullException(nameof(uploadUri));
            Request = request ?? throw new ArgumentNullException(nameof(request));
        }

        /// <summary>
        /// 由 ConfigRuntimeSO.DevelopMode 选择的上传地址，不进入请求 JSON。
        /// </summary>
        public Uri UploadUri { get; }

        /// <summary>
        /// 冻结后的请求数据。
        /// </summary>
        public IAPProductCatalogRequest Request { get; }
    }

    /// <summary>
    /// IAP 商品目录请求。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class IAPProductCatalogRequest
    {
        /// <summary>
        /// 创建包含明文 Head 与加密 Body 的 IAP 商品目录请求。
        /// </summary>
        /// <param name="head">保持明文的公共请求头。</param>
        /// <param name="body">Base64 格式的 AES 密文。</param>
        internal IAPProductCatalogRequest(EditorUtil.Network.Http.RequestHeader head, string body)
        {
            Head = head ?? throw new ArgumentNullException(nameof(head));
            if (string.IsNullOrWhiteSpace(body)) throw new ArgumentException("body 密文不能为空。", nameof(body));
            Body = body;
        }

        /// <summary>
        /// 保持明文的公共请求头。
        /// </summary>
        [JsonProperty("head", Order = 1)]
        public EditorUtil.Network.Http.RequestHeader Head { get; }

        /// <summary>
        /// 完整商品目录 Body JSON 加密后的 Base64 密文。
        /// </summary>
        [JsonProperty("body", Order = 2)]
        public string Body { get; }
    }

    /// <summary>
    /// 加密前的 IAP 商品目录 Body。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class IAPProductCatalogBody
    {
        /// <summary>
        /// 创建 IAP 商品目录 Body 并冻结商品数组。
        /// </summary>
        /// <param name="catalogHash">规范化商品数组的 SHA-256。</param>
        /// <param name="products">已经排序的商品数组。</param>
        internal IAPProductCatalogBody(string catalogHash, IReadOnlyList<IAPProductCatalogItem> products)
        {
            CatalogHash = catalogHash ?? throw new ArgumentNullException(nameof(catalogHash));
            if (products == null) throw new ArgumentNullException(nameof(products));

            var copy = new IAPProductCatalogItem[products.Count];
            for (int i = 0; i < products.Count; i++)
            {
                copy[i] = products[i] ?? throw new ArgumentException("products 不能包含 null。", nameof(products));
            }
            Products = new FrozenReadOnlyList<IAPProductCatalogItem>(copy);
            ProductCount = copy.Length;
        }

        /// <summary>
        /// 规范化商品数组的 SHA-256。
        /// </summary>
        [JsonProperty("catalog_hash", Order = 1)]
        internal string CatalogHash { get; }

        /// <summary>
        /// 商品数量。
        /// </summary>
        [JsonProperty("product_count", Order = 2)]
        internal int ProductCount { get; }

        /// <summary>
        /// 按 TableId 升序冻结的全量商品。
        /// </summary>
        [JsonProperty("products", Order = 3)]
        internal IReadOnlyList<IAPProductCatalogItem> Products { get; }
    }

    /// <summary>
    /// 单条 IAP 商品协议数据。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public sealed class IAPProductCatalogItem
    {
        /// <summary>
        /// 创建单条 IAP 商品协议数据。
        /// </summary>
        internal IAPProductCatalogItem(string tableId, string name, string productId, string thirdProductId, string productType, int subGroupId, string price, string currency)
        {
            TableId = tableId ?? throw new ArgumentNullException(nameof(tableId));
            Name = name ?? string.Empty;
            ProductId = productId ?? string.Empty;
            ThirdProductId = thirdProductId ?? string.Empty;
            ProductType = productType ?? throw new ArgumentNullException(nameof(productType));
            SubGroupId = subGroupId;
            Price = price ?? string.Empty;
            Currency = currency ?? string.Empty;
        }

        [JsonProperty("table_id", Order = 1)] public string TableId { get; }
        [JsonProperty("name", Order = 2)] public string Name { get; }
        [JsonProperty("product_id", Order = 3)] public string ProductId { get; }
        [JsonProperty("third_product_id", Order = 4)] public string ThirdProductId { get; }
        [JsonProperty("product_type", Order = 5)] public string ProductType { get; }
        [JsonProperty("sub_group_id", Order = 6)] public int SubGroupId { get; }
        [JsonProperty("price", Order = 7)] public string Price { get; }
        [JsonProperty("currency", Order = 8)] public string Currency { get; }
    }

    /// <summary>
    /// 只暴露 IReadOnlyList 的冻结数组，防止调用方向下转换后修改。
    /// </summary>
    internal sealed class FrozenReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly T[] m_Items;

        /// <summary>
        /// 使用已经复制的数组创建只读列表。
        /// </summary>
        internal FrozenReadOnlyList(T[] items)
        {
            m_Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public int Count => m_Items.Length;
        public T this[int index] => m_Items[index];

        /// <summary>
        /// 获取泛型枚举器。
        /// </summary>
        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)m_Items).GetEnumerator();

        /// <summary>
        /// 获取非泛型枚举器。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => m_Items.GetEnumerator();
    }
}
