/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CustomConfigs.cs
 * author:    taoye
 * created:   2026/7/24
 * descrip:   Custom 本地数据与运行时查询结构
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 单条 Custom 本地配置；Key 使用 JSONPath，Value 是本地默认字符串。
    /// </summary>
    [Serializable]
    public sealed class CustomConfigEntry
    {
        /// <summary>
        /// 配置键；空键在运行时会被忽略。
        /// </summary>
        [Tooltip("JSONPath 配置键，例如 User.Level 或 Rewards[0].Id。")]
        public string Key;

        /// <summary>
        /// 本地默认字符串值；云端快照缺少该键时回退到此值。
        /// </summary>
        [Tooltip("本地默认字符串值；云端路径不存在时使用此值。")]
        public string Value;
    }

    /// <summary>
    /// Custom 本地配置数据；仅承载 Config 中编辑的有序路径键值。
    /// </summary>
    [Serializable]
    public sealed class CustomConfigData
    {
        /// <summary>
        /// 本地路径键值列表；ConfigWindow 直接绘制行，不向使用者显示本字段层级。
        /// </summary>
        [Tooltip("本地默认路径键值列表。")]
        public List<CustomConfigEntry> Entries = new();
    }

    /// <summary>
    /// Custom 运行时只读查询入口；业务通过 JSONPath 获取字符串或基础类型。
    /// </summary>
    public sealed class CustomConfig
    {
        private readonly IAppConfigManager m_Manager;

        /// <summary>
        /// 创建绑定到 ConfigManager 的查询对象。
        /// </summary>
        /// <param name="manager">实际执行路径查询与回退的内部管理器。</param>
        internal CustomConfig(IAppConfigManager manager)
        {
            m_Manager = manager;
        }

        /// <summary>
        /// 按 JSONPath 读取字符串。
        /// </summary>
        public string GetString(string path, string defaultValue = null) => m_Manager?.GetString(path, defaultValue) ?? defaultValue;

        /// <summary>
        /// 按 JSONPath 读取整数。
        /// </summary>
        public int GetInt(string path, int defaultValue = default) => m_Manager != null ? m_Manager.GetInt(path, defaultValue) : defaultValue;

        /// <summary>
        /// 按 JSONPath 读取浮点数。
        /// </summary>
        public float GetFloat(string path, float defaultValue = default) => m_Manager != null ? m_Manager.GetFloat(path, defaultValue) : defaultValue;

        /// <summary>
        /// 按 JSONPath 读取布尔值。
        /// </summary>
        public bool GetBool(string path, bool defaultValue = default) => m_Manager != null ? m_Manager.GetBool(path, defaultValue) : defaultValue;
    }
}
