/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AdLoadEvent.cs
 * author:    yingzheng
 * created:   2026/5/13
 * descrip:   广告加载结果数据类
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 广告加载结果载荷，由 RequestAsync 返回，并由渠道加载成功/失败事件携带。
    /// Success=true 表示加载成功；Success=false 时 ErrorCode / ErrorMessage 描述失败原因。
    /// </summary>
    public sealed class AdLoadResult
    {
        /// <summary>
        /// 广告加载是否成功。
        /// </summary>
        public bool Success;
        /// <summary>
        /// 广告格式。
        /// </summary>
        public AdFormat Format;
        /// <summary>
        /// 广告位唯一标识。
        /// </summary>
        public string PlacementId;
        /// <summary>
        /// 实际投放网络名称（如 "AdMob"、"Meta"）。
        /// </summary>
        public string Network;
        /// <summary>
        /// 加载时预测收入，单位 USD，无法获取时为 0。
        /// </summary>
        public double Revenue;
        /// <summary>
        /// Revenue 对应的货币代码，固定为 "USD"。
        /// </summary>
        public string Currency;
        /// <summary>
        /// SDK 返回的错误码；Success=true 时为 0。
        /// </summary>
        public int ErrorCode;
        /// <summary>
        /// 错误描述文本；Success=true 时为 null。
        /// </summary>
        public string ErrorMessage;
        /// <summary>
        /// 渠道侧附加的自定义打点属性；RaiseAdLoaded 打 nova_ad_fill 时自动合并进去。
        /// MAX 等渠道可在此注入 SDK 特有字段（network_placement、waterfall_name 等）。
        /// </summary>
        public Dictionary<string, object> CustomProps;
    }
}
