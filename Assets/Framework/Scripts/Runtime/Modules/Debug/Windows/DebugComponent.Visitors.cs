/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugComponent.Visitors.cs
 * author:    taoye
 * created:   2025/3/17
 * descrip:   调试组件 —— 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public sealed partial class DebugComponent : FrameworkComponent
    {
        /// <summary>
        /// Debugger 激活类型，决定 RuntimeDebugger 在何种构建环境下初始化。
        /// </summary>
        [SerializeField]
        private DebuggerActiveType m_DebuggerActiveType = DebuggerActiveType.OnlyEnableWhenDevelopment;
        public DebuggerActiveType DebuggerActiveType => m_DebuggerActiveType;

        /// <summary>
        /// Console 最大日志条数，超出后自动淘汰最旧日志。
        /// </summary>
        [SerializeField]
        private int m_MaximumConsoleEntries = 3000;
        public int MaximumConsoleEntries => m_MaximumConsoleEntries;

        /// <summary>
        /// DebugManager 实现类型的完全限定名，由 TypeCreator 反射创建。
        /// </summary>
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.DebugManager";
        public string CurManagerTypeName => m_CurManagerTypeName;

        /// <summary>
        /// 磁盘监控配置集合（Inspector 序列化，由 DebugComponentInspector 的磁盘监控面板读写）。
        /// </summary>
        [SerializeField]
        private List<DiskCheckingConfig> m_DiskCheckingConfigs = new List<DiskCheckingConfig>()
        {
            new DiskCheckingConfig("Editor"),
            new DiskCheckingConfig("Android"),
            new DiskCheckingConfig("iOS"),
        };

        /// <summary>
        /// DebugManager 接口引用，Awake 时通过 TypeCreator 创建。
        /// </summary>
        private IDebugManager m_DebugManager;
    }
}
