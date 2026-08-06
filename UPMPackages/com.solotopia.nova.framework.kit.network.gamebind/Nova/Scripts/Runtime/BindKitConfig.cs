/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BindKitConfig.cs
 * descrip:   账号绑定 Kit 固有配置；作为 IKitConfig 由 ConfigWindow「Kit 配置」
 *            全局静态存储，Bind 各接口方法内按需拉取对应协议指令名。
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Kit.Network.GameBind.Runtime
{
    /// <summary>
    /// 账号绑定 Kit 固有配置。
    /// 标注 [Serializable] 以便被 ConfigWindow KitConfigScanner 扫描到，并可作为全局 KitConfigs 的 [SerializeReference] 条目持久化。
    /// 由 Editor 面板直接编辑字段值；运行时由 Bind 各接口通过 Nova.Config.GetKitConfig 拉取。
    /// </summary>
    [Serializable]
    public sealed class BindKitConfig : IKitConfig
    {
        /// <summary>
        /// 绑定协议 NetCmd 指令名序列化字段。
        /// </summary>
        [SerializeField, Tooltip("用于为当前账号绑定三方号的协议名。填写 NetCmd 表中的名称，如 GameAccountBind。")]
        private string m_BindCmdName;
        /// <summary>
        /// 绑定协议 NetCmd 指令名。
        /// </summary>
        public string BindCmdName => m_BindCmdName;

        /// <summary>
        /// 绑定状态查询协议 NetCmd 指令名序列化字段。
        /// </summary>
        [SerializeField, Tooltip("用于查询指定 OpenID 是否已绑定的协议名。填写 NetCmd 表中的名称，如 GameAccountBindingQuery。")]
        private string m_BindingQueryCmdName;
        /// <summary>
        /// 绑定状态查询协议 NetCmd 指令名。
        /// </summary>
        public string BindingQueryCmdName => m_BindingQueryCmdName;

        /// <summary>
        /// 冲突查询协议 NetCmd 指令名序列化字段。
        /// </summary>
        [SerializeField, Tooltip("用于查询绑定冲突详情的协议名。填写 NetCmd 表中的名称，如 GameAccountBindConflict。")]
        private string m_BindConflictCmdName;
        /// <summary>
        /// 冲突查询协议 NetCmd 指令名。
        /// </summary>
        public string BindConflictCmdName => m_BindConflictCmdName;

        /// <summary>
        /// 裁决协议 NetCmd 指令名序列化字段。
        /// </summary>
        [SerializeField, Tooltip("用于绑定冲突二选一裁决的协议名。填写 NetCmd 表中的名称，如 GameAccountBindResolve。")]
        private string m_BindResolveCmdName;
        /// <summary>
        /// 裁决协议 NetCmd 指令名。
        /// </summary>
        public string BindResolveCmdName => m_BindResolveCmdName;

        /// <summary>
        /// ConfigWindow 左树展示的名称。
        /// </summary>
        public string DisplayName => "Bind 账号绑定";

        /// <summary>
        /// 无参构造器；供 ConfigWindow KitConfigScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public BindKitConfig() { }
    }
}
