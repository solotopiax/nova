/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AIHelpPluginConfig.cs
 * author:    taoye
 * created:   2026/7/9
 * descrip:   AIHelp 插件运行期初始化配置；作为 ISDKPluginConfig 由
 *            ConfigMasterSO 静态配置，SDKManager 按 RequiredConfigType 自动
 *            注入给 AIHelpPlugin.OnInitializeAsync。
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.AIHelp.Runtime
{
    /// <summary>
    /// AIHelp 插件初始化所需数据：ServerCmdName / AppId 由 AIHelp 后台提供（ServerCmdName 为
    /// netcmd 指令名，运行时经 INetworkManager 解析出完整 URL 再取域名传入 vendor），InitialLanguage
    /// 为初始语言码（空则用 AIHelp 默认），EnableLogging 控制 vendor 日志开关。
    /// 标注 [Serializable] 以便被 ConfigWindow SDKPluginScanner 扫描到，并可作为
    /// SDKConfigsByMode 的 [SerializeReference] 条目持久化；由 Editor 面板直接编辑字段值。
    /// </summary>
    [Serializable]
    public sealed class AIHelpPluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// netcmd 指令名序列化字段，运行时经 INetworkManager 解析域名后传入 vendor Initialize。
        /// </summary>
        [SerializeField, Tooltip("netcmd 指令名（如 AIHelpServerUrl）。运行时从 netcmd 表解析该指令的完整 URL，去掉 https:// 取域名传给 AIHelp。")]
        private string m_ServerCmdName;

        /// <summary>
        /// AIHelp 应用 ID 序列化字段，传入 vendor Initialize。
        /// </summary>
        [SerializeField, Tooltip("AIHelp 应用 ID。填写 AIHelp 控制台为当前应用分配的 AppId。")]
        private string m_AppId;

        /// <summary>
        /// 初始语言码序列化字段，空则用 AIHelp 默认；运行时可经 SetLanguage 切换。
        /// </summary>
        [SerializeField, Tooltip("初始语言码（如 en、zh-CN）。留空则用 AIHelp 默认；运行时可经 SetLanguage 切换。")]
        private string m_InitialLanguage;

        /// <summary>
        /// vendor 日志开关序列化字段，开发期建议开。
        /// </summary>
        [SerializeField, Tooltip("是否开启 AIHelp SDK 日志。开发期建议勾选。")]
        private bool m_EnableLogging;

        /// <summary>
        /// netcmd 指令名，运行时经 INetworkManager 解析出完整 URL 后取域名传入 vendor Initialize。
        /// </summary>
        public string ServerCmdName => m_ServerCmdName;

        /// <summary>
        /// AIHelp 应用 ID，传入 vendor Initialize。
        /// </summary>
        public string AppId => m_AppId;

        /// <summary>
        /// 初始语言码，可为空（空则用 AIHelp 默认）。
        /// </summary>
        public string InitialLanguage => m_InitialLanguage;

        /// <summary>
        /// vendor 日志开关。
        /// </summary>
        public bool EnableLogging => m_EnableLogging;

        /// <summary>
        /// ConfigWindow 左树展示的名称。
        /// </summary>
        public string DisplayName => "AIHelp";

        /// <summary>
        /// 无参构造器；供 ConfigWindow SDKPluginScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public AIHelpPluginConfig() { }

        /// <summary>
        /// 构造 AIHelpPluginConfig 实例。
        /// </summary>
        /// <param name="serverCmdName">netcmd 指令名，运行时解析出域名传入 vendor Initialize。</param>
        /// <param name="appId">AIHelp 应用 ID。</param>
        /// <param name="initialLanguage">初始语言码，可为空。</param>
        /// <param name="enableLogging">是否开启 vendor 日志。</param>
        public AIHelpPluginConfig(string serverCmdName, string appId, string initialLanguage = null, bool enableLogging = false)
        {
            m_ServerCmdName = serverCmdName;
            m_AppId = appId;
            m_InitialLanguage = initialLanguage;
            m_EnableLogging = enableLogging;
        }
    }
}
