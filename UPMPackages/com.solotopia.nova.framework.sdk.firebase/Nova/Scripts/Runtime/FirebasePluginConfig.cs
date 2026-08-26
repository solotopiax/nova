/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePluginConfig.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   Firebase 插件运行期初始化配置；存放上报三方标识协议名 cmdName 与 push task 批量发送等框架级配置；
 *            其他 Firebase 运行期配置由 Firebase SDK 自身托管，无需在此暴露。
 ***************************************************************/

#if !UNITY_WEBGL
using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.FirebasePlugin.Runtime
{
    /// <summary>
    /// Firebase 插件初始化所需数据。
    /// 持有上报三方标识协议名 cmdName 与 push task 批量发送等框架级配置；标注 [Serializable] 以便被 ConfigWindow SDKPluginScanner 扫描到，
    /// 并可作为 PlatformChannelEntry.SDKConfigsByMode 的 [SerializeReference] 条目持久化。
    /// </summary>
    [Serializable]
    public sealed class FirebasePluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// 上报 Firebase 标识协议 NetCmd 指令名序列化字段与属性。
        /// </summary>
        [SerializeField, Tooltip("用于向业务服务器上报 Firebase 标识信息的协议名。填写 NetCmd 表中的名称，如 FirebaseReport。")]
        private string m_ReportCmdName;

        /// <summary>
        /// Firebase push task 批量发送协议 NetCmd 指令名序列化字段与属性。
        /// </summary>
        [SerializeField, Tooltip("用于向业务服务器批量创建或取消 Firebase push task 的协议名。填写 NetCmd 表中的名称。")]
        private string m_PushCmdName;

        /// <summary>
        /// Firebase push task 缓存达到时间阈值后的批量发送间隔，单位秒。
        /// </summary>
        [SerializeField, Tooltip("Firebase push task 本地缓存后的批量发送间隔（秒）。默认 100 秒；小于等于 0 时表示写入后立即尝试发送。")]
        private float m_PushFlushIntervalSeconds = 100f;

        /// <summary>
        /// Firebase push task 缓存达到数量阈值后的批量发送数量。
        /// </summary>
        [SerializeField, Tooltip("Firebase push task 本地缓存达到该数量时立即尝试发送。默认 5 条；小于 1 时运行时按 1 处理。")]
        private int m_PushFlushBatchSize = 5;

        /// <summary>
        /// 是否在 Firebase 依赖初始化成功后自动请求通知权限。
        /// </summary>
        [SerializeField, Tooltip("Firebase 初始化完成后是否自动请求通知权限。默认开启；如业务不希望 Firebase 初始化后触发，可在配置中关闭。")]
        private bool m_AutoRequestNotificationPermission = true;

        /// <summary>
        /// 上报 Firebase 标识协议 NetCmd 指令名。
        /// </summary>
        public string ReportCmdName => m_ReportCmdName;

        /// <summary>
        /// Firebase push task 批量发送协议 NetCmd 指令名。
        /// </summary>
        public string PushCmdName => m_PushCmdName;

        /// <summary>
        /// Firebase push task 批量发送时间阈值，单位秒。
        /// </summary>
        public float PushFlushIntervalSeconds => m_PushFlushIntervalSeconds;

        /// <summary>
        /// Firebase push task 批量发送数量阈值。
        /// </summary>
        public int PushFlushBatchSize => m_PushFlushBatchSize;

        /// <summary>
        /// 是否在 Firebase 依赖初始化成功后自动请求通知权限。
        /// </summary>
        public bool AutoRequestNotificationPermission => m_AutoRequestNotificationPermission;

        /// <summary>
        /// ConfigWindow 左树显示的中文名称。
        /// </summary>
        public string DisplayName => "Firebase";

        /// <summary>
        /// 无参构造器；供 ConfigWindow SDKPluginScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public FirebasePluginConfig() { }
    }
}
#endif
