/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LoginKitConfig.cs
 * descrip:   登录 Kit 固有配置；作为 IKitConfig 由 ConfigWindow「Kit 配置」
 *            全局静态存储，Login.Async 方法内按需拉取 LoginCmdName，
 *            Login.DeleteAsync 方法内按需拉取 DeleteCmdName。
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.Serialization;

namespace NovaFramework.Kit.Network.GameLogin.Runtime
{
    /// <summary>
    /// 登录 Kit 固有配置。
    /// 标注 [Serializable] 以便被 ConfigWindow KitConfigScanner 扫描到，并可作为全局 KitConfigs 的 [SerializeReference] 条目持久化。
    /// 由 Editor 面板直接编辑字段值；运行时由 Login.Async / Login.DeleteAsync 通过 Nova.Config.GetKitConfig 拉取。
    /// </summary>
    [Serializable]
    public sealed class LoginKitConfig : IKitConfig
    {
        /// <summary>
        /// 登录协议 NetCmd 指令名序列化字段与属性。
        /// [FormerlySerializedAs("m_CmdName")] 保证存量 .asset 中旧字段名自动迁移到新字段，无需重填。
        /// </summary>
        [FormerlySerializedAs("m_CmdName")]
        [SerializeField, Tooltip("用于账号登录的协议名。填写 NetCmd 表中的名称，如 GameLogin。")]
        private string m_LoginCmdName;
        /// <summary>
        /// 登录协议 NetCmd 指令名。
        /// </summary>
        public string LoginCmdName => m_LoginCmdName;

        /// <summary>
        /// 账号删除协议 NetCmd 指令名序列化字段与属性。
        /// </summary>
        [SerializeField, Tooltip("用于删除当前账号的协议名。填写 NetCmd 表中的名称。")]
        private string m_DeleteCmdName;
        /// <summary>
        /// 账号删除协议 NetCmd 指令名。
        /// </summary>
        public string DeleteCmdName => m_DeleteCmdName;

        /// <summary>
        /// ConfigWindow 左树展示的名称。
        /// </summary>
        public string DisplayName => "Login 登录";

        /// <summary>
        /// 无参构造器；供 ConfigWindow KitConfigScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public LoginKitConfig() { }
    }
}
