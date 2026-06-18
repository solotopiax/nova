/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugComponent.cs
 * author:    taoye
 * created:   2025/3/17
 * descrip:   调试组件（RuntimeDebugger 入口）。
 ***************************************************************/

using System.Reflection;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 调试组件，负责在框架启动阶段创建 DebugManager 并按激活条件初始化 RuntimeDebugger。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class DebugComponent : FrameworkComponent
    {
        /// <summary>
        /// 框架组件初始化：反射创建 DebugManager。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_DebugManager = Util.TypeCreator.Create<IDebugManager>(m_CurManagerTypeName);
            if (m_DebugManager == null)
            {
                Log.Fatal(LogTag.Debug, "DebugManager 无效。");
                return;
            }

            if (IsDebuggerActive())
            {
                RuntimeDebugger.Init(new RuntimeDebugger.InitOptions
                {
                    LogTagType = typeof(LogTag),
                    LogTagDescriptionResolver = f => f.GetCustomAttribute<LogTagDescriptionAttribute>()?.Description,
                    MaximumConsoleEntries = m_MaximumConsoleEntries,
                });
            }
        }

        /// <summary>
        /// 启动时根据激活类型决定是否初始化 RuntimeDebugger，再初始化 DebugManager。
        /// </summary>
        private void Start()
        {
            if (m_DebugManager == null) return;

    
            m_DebugManager.Initialize(new DebugManagerConfig
            {
                DiskCheckingConfigs = m_DiskCheckingConfigs,
            });
        }

    }
}
