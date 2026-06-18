/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  KeyboardShortcutListenerService.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System.Collections.Generic;
    using UnityEngine;
#if ENABLE_INPUT_SYSTEM
    using UnityEngine.InputSystem;
    using UnityEngine.InputSystem.Controls;
#endif


    [Service(typeof (KeyboardShortcutListenerService))]
    public class KeyboardShortcutListenerService : DebugServiceBase<KeyboardShortcutListenerService>
    {
        private List<Settings.KeyboardShortcut> _shortcuts;

        protected override void Awake()
        {
            base.Awake();

            CachedTransform.SetParent(Hierarchy.Get("RuntimeDebugger"));

            _shortcuts = new List<Settings.KeyboardShortcut>(Settings.Instance.KeyboardShortcuts);

#if ENABLE_INPUT_SYSTEM

            foreach (var s in _shortcuts)
            {
                // Cache the actual keycode so we don't have to use strings each time we want to use it.
                string keyName = s.Key.ToString();
                KeyControl keyControl = Keyboard.current[keyName] as KeyControl;

                if (keyControl == null)
                {
                    Debug.LogErrorFormat(
                        "[RuntimeDebugger] Input System: Unable to find shortcut key: {0}. Shortcut ({1}) will not be functional.",
                        keyName, s.Action);
                    s.Cached_KeyCode = Key.None;
                }

                // Find the index for this key control
                for (var index = 0; index < Keyboard.current.allKeys.Count; index++)
                {
                    if (Keyboard.current.allKeys[index] == keyControl)
                    {
                        s.Cached_KeyCode = (Key) (index + 1);
                        break;
                    }
                }
            }

#endif
        }

        private void ToggleTab(DefaultTabs t)
        {
            var activeTab = Service.Panel.ActiveTab;

            if (Service.Panel.IsVisible && activeTab.HasValue && activeTab.Value == t)
            {
                RuntimeDebugger.Instance.HideDebugPanel();
            }
            else
            {
                RuntimeDebugger.Instance.ShowDebugPanel(t);
            }
        }

        private void ExecuteShortcut(Settings.KeyboardShortcut shortcut)
        {
            switch (shortcut.Action)
            {
                case Settings.ShortcutActions.OpenSystemInfoTab:

                    ToggleTab(DefaultTabs.SystemInformation);

                    break;

                case Settings.ShortcutActions.OpenConsoleTab:

                    ToggleTab(DefaultTabs.Console);

                    break;

                case Settings.ShortcutActions.OpenOptionsTab:

                    ToggleTab(DefaultTabs.Options);

                    break;

                case Settings.ShortcutActions.OpenProfilerTab:

                    ToggleTab(DefaultTabs.Profiler);

                    break;

                case Settings.ShortcutActions.OpenBugReporterTab:

                    ToggleTab(DefaultTabs.BugReporter);

                    break;

                case Settings.ShortcutActions.ClosePanel:

                    RuntimeDebugger.Instance.HideDebugPanel();

                    break;

                case Settings.ShortcutActions.OpenPanel:

                    RuntimeDebugger.Instance.ShowDebugPanel();

                    break;

                case Settings.ShortcutActions.TogglePanel:

                    if (RuntimeDebugger.Instance.IsDebugPanelVisible)
                    {
                        RuntimeDebugger.Instance.HideDebugPanel();
                    }
                    else
                    {
                        RuntimeDebugger.Instance.ShowDebugPanel();
                    }

                    break;

                case Settings.ShortcutActions.ShowBugReportPopover:

                    RuntimeDebugger.Instance.ShowBugReportSheet();

                    break;

                case Settings.ShortcutActions.ToggleDockedConsole:

                    RuntimeDebugger.Instance.DockConsole.IsVisible = !RuntimeDebugger.Instance.DockConsole.IsVisible;

                    break;

                case Settings.ShortcutActions.ToggleDockedProfiler:

                    RuntimeDebugger.Instance.IsProfilerDocked = !RuntimeDebugger.Instance.IsProfilerDocked;

                    break;

                default:

                    Debug.LogWarning("[RuntimeDebugger] Unhandled keyboard shortcut: " + shortcut.Action);

                    break;
            }
        }

        protected override void Update()
        {
            base.Update();

#if ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            switch (Settings.Instance.UIInputMode)
            {
                case Settings.UIModes.NewInputSystem:
                    UpdateInputSystem();
                    break;
                case Settings.UIModes.LegacyInputSystem:
                    UpdateLegacyInputSystem();
                    break;
            }
#elif ENABLE_INPUT_SYSTEM
            UpdateInputSystem();
#elif ENABLE_LEGACY_INPUT_MANAGER || (!ENABLE_INPUT_SYSTEM && !UNITY_2019_3_OR_NEWER)
            UpdateLegacyInputSystem();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        private void UpdateInputSystem()
        {
            var keyboard = Keyboard.current;

            if (Settings.Instance.KeyboardEscapeClose && keyboard.escapeKey.isPressed && Service.Panel.IsVisible)
            {
                RuntimeDebugger.Instance.HideDebugPanel();
            }

            var ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            var alt = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;
            var shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            for (var i = 0; i < _shortcuts.Count; i++)
            {
                var s = _shortcuts[i];

                if (s.Control && !ctrl)
                {
                    continue;
                }

                if (s.Shift && !shift)
                {
                    continue;
                }

                if (s.Alt && !alt)
                {
                    continue;
                }
                
                if (!s.Cached_KeyCode.HasValue)
                {
                    continue; // We can't use this shortcut since we didn't find the keycode.
                }

                if (keyboard[s.Cached_KeyCode.Value].wasPressedThisFrame)
                {
                    ExecuteShortcut(s);
                    break;
                }
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER || (!ENABLE_INPUT_SYSTEM && !UNITY_2019_3_OR_NEWER)
        private void UpdateLegacyInputSystem()
        {
            if (Settings.Instance.KeyboardEscapeClose && Input.GetKeyDown(KeyCode.Escape) && Service.Panel.IsVisible)
            {
                RuntimeDebugger.Instance.HideDebugPanel();
            }

            var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            var alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            for (var i = 0; i < _shortcuts.Count; i++)
            {
                var s = _shortcuts[i];

                if (s.Control && !ctrl)
                {
                    continue;
                }

                if (s.Shift && !shift)
                {
                    continue;
                }

                if (s.Alt && !alt)
                {
                    continue;
                }

                if (Input.GetKeyDown(s.Key))
                {
                    ExecuteShortcut(s);
                    break;
                }
            }
        }
#endif
    }
}
