/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebuggerTabController.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using System.Linq;
    using UnityEngine;

    public class DebuggerTabController : DebugMonoBehaviourEx
    {
        private DebugTab _aboutTabInstance;
        private DefaultTabs? _activeTab;
        private bool _hasStarted;
        public DebugTab AboutTab;

        [RequiredField] public DebugTabController TabController;

        public DefaultTabs? ActiveTab
        {
            get
            {
                var key = TabController.ActiveTab.Key;

                if (string.IsNullOrEmpty(key))
                {
                    return null;
                }

                var t = Enum.Parse(typeof (DefaultTabs), key);

                if (!Enum.IsDefined(typeof (DefaultTabs), t))
                {
                    return null;
                }

                return (DefaultTabs) t;
            }
        }

        protected override void Start()
        {
            base.Start();

            _hasStarted = true;

            // Loads all available tabs from resources
            var tabs = Resources.LoadAll<DebugTab>("Debug/Prefabs/Tabs");
            var defaultTabs = Enum.GetNames(typeof (DefaultTabs));

            foreach (var srTab in tabs)
            {
                var enabler = srTab.GetComponent(typeof (IEnableTab)) as IEnableTab;

                if (enabler != null && !enabler.IsEnabled)
                {
                    continue;
                }

                if (defaultTabs.Contains(srTab.Key))
                {
                    var tabValue = Enum.Parse(typeof (DefaultTabs), srTab.Key);

                    if (Enum.IsDefined(typeof (DefaultTabs), tabValue) &&
                        Settings.Instance.DisabledTabs.Contains((DefaultTabs) tabValue))
                    {
                        continue;
                    }
                }

                TabController.AddTab(DebugInstantiate.Instantiate(srTab));
            }

            // Add about tab without creating a sidebar button.
            if (AboutTab != null)
            {
                _aboutTabInstance = DebugInstantiate.Instantiate(AboutTab);
                TabController.AddTab(_aboutTabInstance, false);
            }

            // DebugPanelServiceImpl 负责每次打开时恢复上次 tab，Start 只做兜底
            if (!OpenTab(_activeTab ?? DefaultTabs.Console))
            {
                TabController.ActiveTab = TabController.Tabs.FirstOrDefault();
            }
        }

        public bool OpenTab(DefaultTabs tab)
        {
            if (!_hasStarted)
            {
                _activeTab = tab;
                return true;
            }

            var tabName = tab.ToString();

            foreach (var t in TabController.Tabs)
            {
                if (t.Key == tabName)
                {
                    TabController.ActiveTab = t;
                    return true;
                }
            }

            return false;
        }

        public void ShowAboutTab()
        {
            if (_aboutTabInstance != null)
            {
                TabController.ActiveTab = _aboutTabInstance;
            }
        }
    }
}
