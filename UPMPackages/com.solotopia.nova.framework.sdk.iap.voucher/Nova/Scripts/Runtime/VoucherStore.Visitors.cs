/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VoucherStore.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/20
 * descrip:   VoucherStore 字段、属性、常量
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.IAP.Voucher.Runtime
{
    /// <summary>
    /// VoucherStore 成员变量分区。
    /// </summary>
    public sealed partial class VoucherStore
    {
        /// <summary>
        /// 渠道标识，用于打点事件的 Channel 字段。
        /// </summary>
        protected override string TrackChannel => "voucher";

        /// <summary>
        /// 日志标签字符串，固定为 IAPVoucher。
        /// </summary>
        protected override string StoreLogTag => LogTag.IAPVoucher;

        /// <summary>
        /// 本地余额快照，缓存礼券档位与赠币余额。
        /// </summary>
        private VoucherBalanceSnapshot m_Snapshot;

        /// <summary>
        /// 余额快照是否已从服务端成功拉取过（至少一次）。
        /// </summary>
        private bool m_IsBalanceReady;

        /// <summary>
        /// 当前账号的本地存档统一容器，按 m_GameUID 隔离，由 IAPStoreBase 模板统一加载/保存。
        /// </summary>
        private VoucherStorePersistData m_PersistData;

        /// <summary>
        /// 业务网络 Service，每次发送时按 cmdName 解析 NetCmdRow 并封装三条协议发送。
        /// </summary>
        private VoucherIapNetService m_IapNetService;

        /// <summary>
        /// store 专属配置缓存，提供三条协议的 cmdName，发送时传入 m_IapNetService。
        /// </summary>
        private VoucherStoreConfig m_StoreConfig;
    }
}
