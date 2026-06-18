/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdResult.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   广告展示结果数据类
 ***************************************************************/

using NovaFramework.Runtime;

namespace NovaFramework.SDK.AdPlugin.Runtime
{
    /// <summary>
    /// 广告展示操作结果数据类，由 IAdPlugin.ShowAsync 返回。
    /// 不变量：Success=false 时 ErrorMessage 非空；Revenue 单位为 USD，保证非负。
    /// </summary>
    public sealed class AdResult
    {
        /// <summary>
        /// 广告展示操作是否成功完成。
        /// </summary>
        public bool Success;

        /// <summary>
        /// 用户是否完整观看了广告。
        /// 仅 AdFormat.Rewarded 有意义；其他格式固定为 false。
        /// </summary>
        public bool UserCompleted;

        /// <summary>
        /// 本次展示对应的广告位唯一标识。
        /// </summary>
        public string PlacementId;

        /// <summary>
        /// 本次展示的广告格式。
        /// </summary>
        public AdFormat Format;

        /// <summary>
        /// 展示失败时的错误描述；成功时为 null。
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// 广告真实投放网络名称（如 "AdMob"、"Meta"）。
        /// Mediation SDK 必须填写，直接接入可留 null。
        /// </summary>
        public string Network;

        /// <summary>
        /// 本次曝光的广告收入，单位 USD，值 >= 0。
        /// Mediation 回传后填入；无法获取时为 0。
        /// </summary>
        public double Revenue;

        /// <summary>
        /// Revenue 对应的货币代码，固定为 "USD"。
        /// </summary>
        public string Currency;

        /// <summary>
        /// 本次展示是否收到了收益回调（RaiseRevenue 已到达）。
        /// 仅 AdFormat.Rewarded 有意义；其他格式固定为 false。
        /// 业务层以此判断是否发放奖励，无需单独订阅 RevenuePaid 事件做标记。
        /// </summary>
        public bool RewardGranted;
    }
}
