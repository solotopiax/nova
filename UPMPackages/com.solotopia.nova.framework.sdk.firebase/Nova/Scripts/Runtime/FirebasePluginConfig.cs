/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebasePluginConfig.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   Firebase 插件运行期初始化配置；仅存放上报三方标识协议名 cmdName，
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
    /// 仅持有上报三方标识协议名 cmdName；标注 [Serializable] 以便被 ConfigWindow SDKPluginScanner 扫描到，
    /// 并可作为 PlatformChannelEntry.SDKConfigsByMode 的 [SerializeReference] 条目持久化。
    /// </summary>
    [Serializable]
    public sealed class FirebasePluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// 上报 Firebase 标识协议 NetCmd 指令名序列化字段与属性。
        /// </summary>
        [SerializeField, Tooltip("用于向业务服务器上报 Firebase 标识信息的协议名。填写 NetCmd 表中的名称，如 FirebaseReport。")]
        private string m_ReportCmdName = "FirebaseReport";
        /// <summary>
        /// 上报 Firebase 标识协议 NetCmd 指令名。
        /// </summary>
        public string ReportCmdName => m_ReportCmdName;

        /// <summary>
        /// ConfigWindow 左树展示的中文名称。
        /// </summary>
        public string DisplayName => "Firebase";

        /// <summary>
        /// 无参构造器；供 ConfigWindow SDKPluginScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public FirebasePluginConfig() { }
    }
}
#endif
