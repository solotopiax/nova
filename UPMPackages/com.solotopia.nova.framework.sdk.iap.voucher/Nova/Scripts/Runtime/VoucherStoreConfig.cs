/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherStoreConfig.cs
 * author:    yingzheng
 * created:   2026/5/26
 * descrip:   礼券/代金券 store 专属配置，实现 IIAPStoreConfig
 ***************************************************************/

using System;
using UnityEngine;
using NovaFramework.SDK.IAP.Runtime;

using NovaFramework.Runtime;
namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// 礼券/代金券 store 专属配置。
    /// 在 IAPPluginConfig Inspector 中以 [SerializeReference] 多态条目添加。
    /// </summary>
    [Serializable]
    public sealed class VoucherStoreConfig : IIAPStoreConfig
    {
        /// <summary>
        /// 当前配置对应的 store 渠道类型，固定为 Voucher。
        /// </summary>
        public IAPStoreType StoreType => IAPStoreType.Voucher;

        /// <summary>
        /// 当前 store 是否启用；false 时 IAPPlugin 跳过初始化。
        /// </summary>
        [SerializeField, Tooltip("默认是否启用 Voucher Store；运行时可通过 IAPPlugin.SetStoreEnabled 覆盖")]
        private bool m_Enabled = true;
        /// <summary>
        /// 当前 store 是否启用。
        /// </summary>
        public bool Enabled => m_Enabled;

        /// <summary>
        /// 获取礼券列表的协议名（对应 INetworkCmdRow.Name）。
        /// 初始化时由 store 层通过 NetworkManager.ResolveNetCmdRow 解析为 INetworkCmdRow 注入 VoucherIapNetService。
        /// </summary>
        [SerializeField, Tooltip("获取礼券列表协议名，如 GetGiftVoucherList")]
        private string m_GetVoucherListCmdName = string.Empty;
        /// <summary>
        /// 获取礼券列表协议名。
        /// </summary>
        public string GetVoucherListCmdName => m_GetVoucherListCmdName;

        /// <summary>
        /// 扣减代金券/金币的协议名（对应 INetworkCmdRow.Name）。
        /// 初始化时由 store 层通过 NetworkManager.ResolveNetCmdRow 解析为 INetworkCmdRow 注入 VoucherIapNetService。
        /// </summary>
        [SerializeField, Tooltip("扣减代金券/金币协议名，如 DeductGiftVoucher")]
        private string m_DeductVoucherCmdName = string.Empty;
        /// <summary>
        /// 扣减代金券/金币协议名。
        /// </summary>
        public string DeductVoucherCmdName => m_DeductVoucherCmdName;

        /// <summary>
        /// 测试发放礼券的协议名（对应 INetworkCmdRow.Name）。
        /// 初始化时由 store 层通过 NetworkManager.ResolveNetCmdRow 解析为 INetworkCmdRow 注入 VoucherIapNetService。
        /// </summary>
        [SerializeField, Tooltip("测试发放礼券协议名，如 TestGrantGiftVoucher")]
        private string m_TestGrantVoucherCmdName = string.Empty;
        /// <summary>
        /// 测试发放礼券协议名。
        /// </summary>
        public string TestGrantVoucherCmdName => m_TestGrantVoucherCmdName;
    }
}
