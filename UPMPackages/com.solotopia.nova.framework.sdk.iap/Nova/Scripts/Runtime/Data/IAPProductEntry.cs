/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAPProductEntry.cs
 * author:    yingzheng
 * created:   2026/5/22
 * descrip:   商品表单条商品数据条目，供 IAPPluginConfig 序列化存储
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.SDK.IAP.Runtime
{
    /// <summary>
    /// 商品表中的单条商品数据条目。
    /// 由 IAPPluginConfig 持有，作为 ScriptableObject 的序列化数据单元。
    /// </summary>
    [Serializable]
    public sealed class IAPProductEntry
    {
        /// <summary>
        /// 表行 id 序列化字段，作为 IAPPluginConfig 内的主键。
        /// </summary>
        [SerializeField, Tooltip("表行 id（主键），在 IAPPluginConfig 内唯一标识此条目。")]
        private long m_TableId;

        /// <summary>
        /// 产品展示名称序列化字段。
        /// </summary>
        [SerializeField, Tooltip("产品展示名称，用于 UI 列表 / 弹窗等界面文本。")]
        private string m_Name;

        /// <summary>
        /// Google / iOS 平台商品 id 序列化字段。
        /// </summary>
        [SerializeField, Tooltip("Google / iOS 平台商品 id，用于向平台支付渠道发起购买。")]
        private string m_ProductID;

        /// <summary>
        /// 第三方支付渠道商品 id 序列化字段。
        /// </summary>
        [SerializeField, Tooltip("第三方支付渠道商品 id，用于非 Google / iOS 渠道发起购买。")]
        private string m_ThirdProductID;

        /// <summary>
        /// 商品类型序列化字段：消耗品 / 非消耗品 / 订阅。
        /// </summary>
        [SerializeField, Tooltip("商品类型：消耗品 / 非消耗品 / 订阅。")]
        private IAPProductType m_ProductType;

        /// <summary>
        /// 订阅群组 id 序列化字段；同组商品在 iOS 同时只能有一个有效订阅，Android 走升降级流程。
        /// 0 表示该商品不属于任何订阅群组（消耗品 / 非消耗品 / 独立订阅）。
        /// </summary>
        [SerializeField, Tooltip("订阅群组 id；同组商品互斥，0 表示不属于任何订阅群组。")]
        private int m_SubGroupID;

        /// <summary>
        /// 字符串形态的商品价格序列化字段，例如 "0.99"；存为字符串以避免浮点精度问题。
        /// </summary>
        [SerializeField, Tooltip("字符串形态的商品价格，例如 \"0.99\"；存为字符串以避免浮点精度问题。")]
        private string m_Price;

        /// <summary>
        /// ISO 4217 货币码序列化字段，例如 "USD" 或 "CNY"。
        /// </summary>
        [SerializeField, Tooltip("ISO 4217 货币码，例如 \"USD\" 或 \"CNY\"。")]
        private string m_Currency;

        /// <summary>
        /// 编辑器备注；仅在 IAPConfigWindow 中显示，不导出到 IAPPluginConfig。
        /// </summary>
        [SerializeField, Tooltip("编辑器备注，仅供配置阶段使用，不导出到运行时商品表。")]
        private string m_EditorNote;

        /// <summary>
        /// 表行 id（主键），在 IAPPluginConfig 内唯一标识此条目。
        /// </summary>
        public long TableId => m_TableId;

        /// <summary>
        /// 产品展示名称。
        /// </summary>
        public string Name => m_Name;

        /// <summary>
        /// Google / iOS 平台商品 id。
        /// </summary>
        public string ProductID => m_ProductID;

        /// <summary>
        /// 第三方支付渠道商品 id。
        /// </summary>
        public string ThirdProductID => m_ThirdProductID;

        /// <summary>
        /// 商品类型：消耗品 / 非消耗品 / 订阅。
        /// </summary>
        public IAPProductType ProductType => m_ProductType;

        /// <summary>
        /// 订阅群组 id；0 表示该商品不属于任何订阅群组。
        /// </summary>
        public int SubGroupID => m_SubGroupID;

        /// <summary>
        /// 字符串形态的商品价格，例如 "0.99"。
        /// </summary>
        public string Price => m_Price;

        /// <summary>
        /// ISO 4217 货币码，例如 "USD" 或 "CNY"。
        /// </summary>
        public string Currency => m_Currency;

        /// <summary>
        /// 编辑器备注；不导出到 IAPPluginConfig。
        /// </summary>
        public string EditorNote => m_EditorNote;

        /// <summary>
        /// 无参构造器，供序列化系统与编辑器反射使用。
        /// </summary>
        public IAPProductEntry() { }
    }
}
