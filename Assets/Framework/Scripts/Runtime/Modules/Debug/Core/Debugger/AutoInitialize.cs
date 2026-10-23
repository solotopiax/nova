/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AutoInitialize.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    public static class AutoInitialize
    {
#if !NOVA_DISABLE_RUNTIME_DEBUGGER
#if UNITY_2018
        private const RuntimeInitializeLoadType InitializeLoadType = RuntimeInitializeLoadType.BeforeSceneLoad;
#else
        private const RuntimeInitializeLoadType InitializeLoadType = RuntimeInitializeLoadType.SubsystemRegistration;
#endif

        /// <summary>
        /// 按当前编译分支在 SubsystemRegistration（UNITY_2018 为 BeforeSceneLoad）注册服务，并在设置启用时创建 Console。
        /// 不读取 DebugComponent 激活策略，也不把服务取得当作完整调试器初始化。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(InitializeLoadType)]
        public static void OnLoadBeforeScene()
        {
            // Populate service manager with types from RuntimeDebugger assembly (asmdef)
            DebugServiceRegistry.RegisterAssembly<IDebugService>();

            if (Settings.Instance.IsEnabled)
            {
                // Initialize console if it hasn't already initialized.
                DebugServiceRegistry.GetService<IConsoleService>();
            }
        }

        /// <summary>
        /// AfterSceneLoad 按 Settings.IsEnabled 调用 Init；不检查 IsInitialized，重复调用沿用既有行为。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void OnLoad()
        {
            if (Settings.Instance.IsEnabled)
            {
                RuntimeDebugger.Init();
            }
        }
#endif
    }
}
