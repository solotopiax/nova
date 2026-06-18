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
#if UNITY_2018
        private const RuntimeInitializeLoadType InitializeLoadType = RuntimeInitializeLoadType.BeforeSceneLoad;
#else
        private const RuntimeInitializeLoadType InitializeLoadType = RuntimeInitializeLoadType.SubsystemRegistration;
#endif

        /// <summary>
        /// Initialize the console service before the scene has loaded to catch more of the initialization log.
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
        /// Initialize RuntimeDebugger after the scene has loaded.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void OnLoad()
        {
            if (Settings.Instance.IsEnabled)
            {
                RuntimeDebugger.Init();
            }
        }
    }
}