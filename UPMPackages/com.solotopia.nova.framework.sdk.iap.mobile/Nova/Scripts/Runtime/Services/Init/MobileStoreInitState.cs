/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileStoreInitState.cs
 * author:    yingzheng
 * created:   2026/6/11
 * descrip:   MobileStore 初始化阶段状态
 ***************************************************************/

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    /// <summary>
    /// MobileStore 初始化阶段枚举。
    /// </summary>
    internal enum MobileStoreInitState
    {
        /// <summary>
        /// 尚未开始初始化。
        /// </summary>
        None,

        /// <summary>
        /// 正在执行初始化流程。
        /// </summary>
        Initializing,

        /// <summary>
        /// 初始化已完成且商店可进入可用状态。
        /// </summary>
        Ready,

        /// <summary>
        /// 初始化已失败。
        /// </summary>
        Failed,
    }
}
