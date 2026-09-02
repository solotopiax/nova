/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGAPluginConfig.cs
 * descrip:   TGA 插件运行期初始化配置；作为 ISDKPluginConfig 由 ConfigMasterSO
 *            静态配置，SDKManager 按 ConfigType 自动注入给
 *            TGAPlugin.OnInitializeAsync。
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.TGAPlugin.Runtime
{
    /// <summary>
    /// TGA 数据上报模式。数值与厂商 TDMode 保持一致，配置层不直接依赖厂商 API。
    /// </summary>
    public enum TGAReportMode
    {
        Normal = 0,
        Debug = 1,
        DebugOnly = 2,
    }

    /// <summary>
    /// TGA 数据时区。数值与厂商 TDTimeZone 保持一致，配置层不直接依赖厂商 API。
    /// </summary>
    public enum TGATimeZone
    {
        Local = 0,
        UTC = 1,
        Asia_Shanghai = 2,
        Asia_Tokyo = 3,
        America_Los_Angeles = 4,
        America_New_York = 5,
        Other = 100,
    }

    /// <summary>
    /// TGA 插件初始化所需数据。
    /// 标注 [Serializable] 以便被 ConfigWindow SDKPluginScanner 扫描到，并可作为
    /// PlatformChannelEntry.SDKConfigsByMode 的 [SerializeReference] 条目持久化。
    /// 由 Editor 面板直接编辑字段值；Game 层亦可通过带参构造器手工生成。
    /// </summary>
    [Serializable]
    public sealed class TGAPluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// 应用 ID 序列化字段。
        /// </summary>
        [SerializeField, Tooltip("TGA 应用 ID。填写 TGA 后台为当前应用分配的 AppID。")]
        private string m_AppID;

        /// <summary>
        /// TGA SDK 上报模式。
        /// </summary>
        [SerializeField, Tooltip("TGA 数据上报模式。调试阶段可用 Debug 或 DebugOnly，正式环境建议使用 Normal。")]
        private TGAReportMode m_Mode = TGAReportMode.Normal;

        /// <summary>
        /// TGA SDK 时区。
        /// </summary>
        [SerializeField, Tooltip("TGA SDK 时区。默认使用本地时区 Local。")]
        private TGATimeZone m_TimeZone = TGATimeZone.Local;

        /// <summary>
        /// 是否开启 SDK 日志序列化字段。
        /// </summary>
        [SerializeField, Tooltip("是否输出 TGA 调试日志。排查问题时开启，正式发布建议关闭。")]
        private bool m_LogEnable;

        /// <summary>
        /// 上报服务器对应的网络指令名序列化字段（对应 INetworkCmdRow.Name）；为空时跳过 TGA 初始化。
        /// </summary>
        [SerializeField, Tooltip("用于获取 TGA 上报埋点的网络指令名。填写 NetworkCmds 表中的名称；留空则不启用 TGA。")]
        private string m_ServerCmdName;

        /// <summary>
        /// 上报 TGA 标识协议 NetCmd 指令名序列化字段与属性。
        /// </summary>
        [SerializeField, Tooltip("用于向业务服务器上报 TGA 用户标识的协议名。填写 NetCmd 表中的名称，如 TGAReport。")]
        private string m_ReportCmdName;

        /// <summary>
        /// 测试用户标识序列化字段，对应 TGA 字段 nova_test。
        /// </summary>
        [SerializeField, Tooltip("是否按测试用户上报。测试环境可开启，正式发版前应关闭。")]
        private bool m_IsTestUser = true;

        /// <summary>
        /// 是否在 TGA 初始化后将 DeviceId 设置为 DistinctId。
        /// </summary>
        [SerializeField, Tooltip("是否在 TGA 初始化后将 DeviceId 设置为 DistinctId。开启后会调用 TDAnalytics.SetDistinctId(TDAnalytics.GetDeviceId())。")]
        private bool m_AssignDeviceIdToDistinctId;

        /// <summary>
        /// 应用 ID。
        /// </summary>
        public string AppID => m_AppID;

        /// <summary>
        /// TGA SDK 上报模式。
        /// </summary>
        public TGAReportMode Mode => m_Mode;

        /// <summary>
        /// TGA SDK 时区。
        /// </summary>
        public TGATimeZone TimeZone => m_TimeZone;

        /// <summary>
        /// 是否开启 SDK 日志。
        /// </summary>
        public bool LogEnable => m_LogEnable;

        /// <summary>
        /// 上报服务器对应的网络指令名（对应 INetworkCmdRow.Name）；为空时跳过 TGA 初始化。
        /// </summary>
        public string ServerCmdName => m_ServerCmdName;

        /// <summary>
        /// 上报 TGA 标识协议 NetCmd 指令名。
        /// </summary>
        public string ReportCmdName => m_ReportCmdName;

        /// <summary>
        /// 测试用户标识；用于区分测试用户与正式用户，对应 TGA 字段 nova_test。
        /// </summary>
        public bool IsTestUser => m_IsTestUser;

        /// <summary>
        /// 是否在 TGA 初始化后将 DeviceId 设置为 DistinctId。
        /// </summary>
        public bool AssignDeviceIdToDistinctId => m_AssignDeviceIdToDistinctId;

        /// <summary>
        /// ConfigWindow 左树展示的中文名称。
        /// </summary>
        public string DisplayName => "TGA 数据分析";

        /// <summary>
        /// 无参构造器；供 ConfigWindow SDKPluginScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public TGAPluginConfig() { }

        /// <summary>
        /// 构造 TGAPluginConfig 实例。
        /// </summary>
        /// <param name="appID">应用 ID。</param>
        /// <param name="mode">TGA SDK 上报模式。</param>
        /// <param name="logEnable">是否开启 SDK 日志。</param>
        /// <param name="serverCmdName">上报服务器对应的网络指令名（NetworkCmds 表 Name），留空则跳过 TGA 初始化。</param>
        /// <param name="isTestUser">测试用户标识，对应 TGA 字段 nova_test。</param>
        /// <param name="assignDeviceIdToDistinctId">是否在 TGA 初始化后将 DeviceId 设置为 DistinctId。</param>
        /// <param name="timeZone">TGA SDK 时区。</param>
        public TGAPluginConfig(string appID, TGAReportMode mode = TGAReportMode.Normal, bool logEnable = false, string serverCmdName = "", bool isTestUser = true, bool assignDeviceIdToDistinctId = false, TGATimeZone timeZone = TGATimeZone.Local)
        {
            m_AppID = appID;
            m_Mode = mode;
            m_TimeZone = timeZone;
            m_LogEnable = logEnable;
            m_ServerCmdName = serverCmdName;
            m_IsTestUser = isTestUser;
            m_AssignDeviceIdToDistinctId = assignDeviceIdToDistinctId;
        }
    }
}
