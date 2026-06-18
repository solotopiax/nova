/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SaveKitConfig.cs
 * descrip:   游戏存档 Kit 固有配置；作为 IKitConfig 由 ConfigWindow「Kit 配置」
 *            按 Platform×Channel×DevelopMode 三维矩阵存储，Save 各接口方法内按需拉取 Get/Set 指令名。
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Kit.Network.GameSave.Runtime
{
    /// <summary>
    /// 游戏存档 Kit 固有配置。
    /// 标注 [Serializable] 以便被 ConfigWindow KitConfigScanner 扫描到，并可作为 PlatformChannelEntry.KitConfigsByMode 的 [SerializeReference] 条目持久化。
    /// Get 系接口（GetAsync / GetFullAsync）共用 GetCmdName；Set 系接口（SetAsync / SetFullAsync）共用 SetCmdName。
    /// </summary>
    [Serializable]
    public sealed class SaveKitConfig : IKitConfig
    {
        /// <summary>
        /// 获取存档协议 NetCmd 指令名序列化字段。
        /// </summary>
        [SerializeField, Tooltip("用于读取云存档的协议名。填写 NetCmd 表中的名称，如 GameSaveGet。")]
        private string m_GetCmdName;

        /// <summary>
        /// 上传存档协议 NetCmd 指令名序列化字段。
        /// </summary>
        [SerializeField, Tooltip("用于写入云存档的协议名。填写 NetCmd 表中的名称，如 GameSaveSet。")]
        private string m_SetCmdName;

        /// <summary>
        /// 获取存档协议 NetCmd 指令名。
        /// </summary>
        public string GetCmdName => m_GetCmdName;

        /// <summary>
        /// 上传存档协议 NetCmd 指令名。
        /// </summary>
        public string SetCmdName => m_SetCmdName;

        /// <summary>
        /// ConfigWindow 左树展示的名称。
        /// </summary>
        public string DisplayName => "Save 云存储";

        /// <summary>
        /// 无参构造器；供 ConfigWindow KitConfigScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public SaveKitConfig() { }
    }
}
