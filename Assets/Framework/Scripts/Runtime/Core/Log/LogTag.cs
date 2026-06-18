/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LogTag.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   日志类型定义
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 日志类型定义。
    /// </summary>
    public static partial class LogTag
    {
        /// <summary>
        /// 编辑器相关日志标签。
        /// </summary>
        [LogTagDescription("编辑器模块")]
        public const string Editor = "[Editor]";

        /// <summary>
        /// 基础模块日志标签。
        /// </summary>
        [LogTagDescription("基础模块")]
        public const string Base = "[Base]";

        /// <summary>
        /// 组件模块日志标签。
        /// </summary>
        [LogTagDescription("组件模块")]
        public const string Component = "[Component]";

        /// <summary>
        /// App 模块日志标签。
        /// </summary>
        [LogTagDescription("App 模块")]
        public const string App = "[App]";

        /// <summary>
        /// 资源模块日志标签。
        /// </summary>
        [LogTagDescription("资源模块")]
        public const string Asset = "[Asset]";

        /// <summary>
        /// Prefab 实例化模块日志标签。
        /// </summary>
        [LogTagDescription("预制体模块")]
        public const string Prefab = "[Prefab]";

        /// <summary>
        /// 配置模块日志标签。
        /// </summary>
        [LogTagDescription("配置模块")]
        public const string Config = "[Config]";

        /// <summary>
        /// 本地化模块日志标签。
        /// </summary>
        [LogTagDescription("本地化模块")]
        public const string Localization = "[Localization]";
        
        /// <summary>
        /// UI 模块日志标签。
        /// </summary>
        [LogTagDescription("UI 模块")]
        public const string UI = "[UI]";

        /// <summary>
        /// 事件模块日志标签。
        /// </summary>
        [LogTagDescription("事件模块")]
        public const string Event = "[Event]";

        /// <summary>
        /// 热更新模块日志标签。
        /// </summary>
        [LogTagDescription("热更新模块")]
        public const string Hotfix = "[Hotfix]";

        /// <summary>
        /// 对象池模块日志标签。
        /// </summary>
        [LogTagDescription("对象池模块")]
        public const string ObjectPool = "[ObjectPool]";

        /// <summary>
        /// Debug 模块日志标签。
        /// </summary>
        [LogTagDescription("日志模块")]
        public const string Debug = "[Debug]";

        /// <summary>
        /// 流程模块日志标签。
        /// </summary>
        [LogTagDescription("流程模块")]
        public const string Procedure = "[Procedure]";

        /// <summary>
        /// 引用模块日志标签。
        /// </summary>
        [LogTagDescription("引用模块")]
        public const string Reference = "[Reference]";

        /// <summary>
        /// SDK 模块日志标签。
        /// </summary>
        [LogTagDescription("SDK 模块")]
        public const string SDK = "[SDK]";

        /// <summary>
        /// AppsFlyer 模块日志标签。
        /// </summary>
        [LogTagDescription("AppsFlyer 插件")]
        public const string AppsFlyer = "[SDK][AppsFlyer]";

        /// <summary>
        /// Firebase 模块日志标签。
        /// </summary>
        [LogTagDescription("Firebase 插件")]
        public const string Firebase = "[SDK][Firebase]";

        /// <summary>
        /// TGA 模块日志标签。
        /// </summary>
        [LogTagDescription("TGA 插件")]
        public const string TGA = "[SDK][TGA]";

        /// <summary>
        /// 广告模块日志标签。
        /// </summary>
        [LogTagDescription("广告聚合插件")]
        public const string AD = "[SDK][AD]";

        /// <summary>
        /// MAX 模块日志标签。
        /// </summary>
        [LogTagDescription("MAX 插件")]
        public const string Max = "[SDK][AD][Max]";

        /// <summary>
        /// IAP 插件日志标签。
        /// </summary>
        [LogTagDescription("支付插件")]
        public const string IAPPlugin = "[SDK][IAP]";

        /// <summary>
        /// IAP 移动端官方内购 store 日志标签。
        /// </summary>
        [LogTagDescription("支付苹果/谷歌商店")]
        public const string IAPMobile = "[SDK][IAP][Mobile]";

        /// <summary>
        /// IAP 第三方支付 store 日志标签。
        /// </summary>
        [LogTagDescription("第三方支付商店")]
        public const string IAPThirdPay = "[SDK][IAP][ThirdPay]";

        /// <summary>
        /// IAP 代金券/金币 store 日志标签。
        /// </summary>
        [LogTagDescription("代金券/金币商店")]
        public const string IAPVoucher = "[SDK][IAP][Voucher]";

        /// <summary>
        /// 表格模块日志标签。
        /// </summary>
        [LogTagDescription("表格模块")]
        public const string Table = "[Table]";

        /// <summary>
        /// 网络模块日志标签。
        /// </summary>
        [LogTagDescription("网络模块")]
        public const string Network = "[Network]";

        /// <summary>
        /// DoH 模块日志标签。
        /// </summary>
        [LogTagDescription("DoH 模块")]
        public const string DoH = "[Network][DoH]";

        /// <summary>
        /// HTTP 模块日志标签。
        /// </summary>
        [LogTagDescription("网络短连接模块")]
        public const string Http = "[Network][Http]";

        /// <summary>
        /// WebSocket 模块日志标签。
        /// </summary>
        [LogTagDescription("网络长连接模块")]
        public const string WebSocket = "[Network][WebSocket]";
        
        /// <summary>
        /// 持久化模块日志标签。
        /// </summary>
        [LogTagDescription("持久化模块")]
        public const string Persist = "[Persist]";

        /// <summary>
        /// 声音模块日志标签。
        /// </summary>
        [LogTagDescription("声音模块")]
        public const string Sound = "[Sound]";

        /// <summary>
        /// 振动模块日志标签。
        /// </summary>
        [LogTagDescription("振动模块")]
        public const string Vibrate = "[Vibrate]";


        /// <summary>
        /// Util 模块日志标签。
        /// </summary>
        [LogTagDescription("工具模块")]
        public const string Util = "[Util]";

        /// <summary>
        /// 类型转换工具日志标签。
        /// </summary>
        [LogTagDescription("类型转换工具")]
        public const string Convert = "[Util][Convert]";

        /// <summary>
        /// 加密工具日志标签。
        /// </summary>
        [LogTagDescription("加密工具")]
        public const string Encrypt = "[Util][Encrypt]";

        /// <summary>
        /// SysIO 模块日志标签。
        /// </summary>
        [LogTagDescription("SysIO 工具")]
        public const string SysIO = "[Util][SysIO]";

        /// <summary>
        /// NovaBehaviour 模块日志标签。
        /// </summary>
        [LogTagDescription("脚本模块")]
        public const string Behaviour = "[Behaviour]";
    }
}
