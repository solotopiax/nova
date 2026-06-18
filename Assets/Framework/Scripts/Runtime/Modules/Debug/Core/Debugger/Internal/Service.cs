/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Service.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{

    public static class Service
    {
        private static IConsoleService _consoleService;
        private static IDebugPanelService _debugPanelService;
        private static IDebugTriggerService _debugTriggerService;
        private static IPinnedUIService _pinnedUiService;
        private static IDebugCameraService _debugCameraService;
        private static IOptionsService _optionsService;
        private static IDockConsoleService _dockConsoleService;

#if UNITY_EDITOR
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void RuntimeInitialize()
        {
            // Clear service references at startup in case of "enter play mode without domain reload"
            _consoleService = null;
            _debugPanelService = null;
            _debugTriggerService = null;
            _pinnedUiService = null;
            _debugCameraService = null;
            _optionsService = null;
            _dockConsoleService = null;
        }
#endif

        public static IConsoleService Console
        {
            get
            {
                if (_consoleService == null)
                {
                    _consoleService = DebugServiceRegistry.GetService<IConsoleService>();
                }

                return _consoleService;
            }
        }

        public static IDockConsoleService DockConsole
        {
            get
            {
                if (_dockConsoleService == null)
                {
                    _dockConsoleService = DebugServiceRegistry.GetService<IDockConsoleService>();
                }

                return _dockConsoleService;
            }
        }

        public static IDebugPanelService Panel
        {
            get
            {
                if (_debugPanelService == null)
                {
                    _debugPanelService = DebugServiceRegistry.GetService<IDebugPanelService>();
                }

                return _debugPanelService;
            }
        }

        public static IDebugTriggerService Trigger
        {
            get
            {
                if (_debugTriggerService == null)
                {
                    _debugTriggerService = DebugServiceRegistry.GetService<IDebugTriggerService>();
                }

                return _debugTriggerService;
            }
        }

        public static IPinnedUIService PinnedUI
        {
            get
            {
                if (_pinnedUiService == null)
                {
                    _pinnedUiService = DebugServiceRegistry.GetService<IPinnedUIService>();
                }

                return _pinnedUiService;
            }
        }

        public static IDebugCameraService DebugCamera
        {
            get
            {
                if (_debugCameraService == null)
                {
                    _debugCameraService = DebugServiceRegistry.GetService<IDebugCameraService>();
                }

                return _debugCameraService;
            }
        }

        public static IOptionsService Options
        {
            get
            {
                if (_optionsService == null)
                {
                    _optionsService = DebugServiceRegistry.GetService<IOptionsService>();
                }

                return _optionsService;
            }
        }
    }
}
