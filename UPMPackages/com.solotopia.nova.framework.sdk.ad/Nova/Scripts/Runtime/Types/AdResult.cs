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
    /// 广告展示操作结果数据类，由 AdPlugin.Events 的展示、失败与关闭事件发布。
    /// 不变量：Success=false 时 ErrorMessage 非空；Revenue 单位为 USD，保证非负。
    /// </summary>
    public sealed class AdResult
    {
        /// <summary>
        /// 广告展示操作是否成功完成。
        /// </summary>
        public bool Success;

        /// <summary>
        /// 用户是否满足激励广告的完成条件；MAX 以官方奖励回调状态为准。
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
        /// 当前回调携带的奖励资格标记，不表示业务已经实际发放奖励。
        /// 由渠道回调直接设置：MAX 插屏展示成功和关闭时为 true，激励关闭时取官方奖励回调状态，开屏为 false。
        /// 基类关闭入口不会根据收益回调改写此字段。
        /// 判断激励广告是否满足完成条件可使用 UserCompleted。
        /// </summary>
        public bool RewardGranted;
    }
}
