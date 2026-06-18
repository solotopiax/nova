/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugTabController.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;

    public class DebugTabController : DebugMonoBehaviourEx
    {
        private readonly DebugList<DebugTab> _tabs = new DebugList<DebugTab>();
        private DebugTab _activeTab;

        [RequiredField] public RectTransform TabButtonContainer;

        [RequiredField] public DebugTabButton TabButtonPrefab;

        [RequiredField] public RectTransform TabContentsContainer;

        [RequiredField] public RectTransform TabHeaderContentContainer;

        [RequiredField] public Text TabHeaderText;

        public DebugTab ActiveTab
        {
            get { return _activeTab; }
            set { MakeActive(value); }
        }

        public IList<DebugTab> Tabs
        {
            get { return _tabs.AsReadOnly(); }
        }

        public event Action<DebugTabController, DebugTab> ActiveTabChanged;

        public void AddTab(DebugTab tab, bool visibleInSidebar = true)
        {
            tab.CachedTransform.SetParent(TabContentsContainer, false);
            tab.CachedGameObject.SetActive(false);

            if (visibleInSidebar)
            {
                // Create a tab button for this tab
                var button = DebugInstantiate.Instantiate(TabButtonPrefab);
                button.CachedTransform.SetParent(TabButtonContainer, false);
                button.TitleText.text = tab.Title.ToUpper();

                if (tab.IconExtraContent != null)
                {
                    var extraContent = DebugInstantiate.Instantiate(tab.IconExtraContent);
                    extraContent.SetParent(button.ExtraContentContainer, false);
                }

                button.IsActive = false;
                button.IconStyleComponent.StyleKey = tab.IconStyleKey;
                button.IconStyleComponent.Refresh(true);

                button.Button.onClick.AddListener(() => MakeActive(tab));

                tab.TabButton = button;
            }

            _tabs.Add(tab);
            SortTabs();

            if (_tabs.Count == 1)
            {
                ActiveTab = tab;
            }
        }

        private void MakeActive(DebugTab tab)
        {
            if (!_tabs.Contains(tab))
            {
                throw new ArgumentException("tab is not a member of this tab controller", "tab");
            }

            if (_activeTab != null)
            {
                _activeTab.CachedGameObject.SetActive(false);

                if (_activeTab.TabButton != null)
                {
                    _activeTab.TabButton.IsActive = false;
                }

                if (_activeTab.HeaderExtraContent != null)
                {
                    _activeTab.HeaderExtraContent.gameObject.SetActive(false);
                }
            }

            _activeTab = tab;

            if (_activeTab != null)
            {
                _activeTab.CachedGameObject.SetActive(true);
                TabHeaderText.text = _activeTab.LongTitle;

                if (_activeTab.TabButton != null)
                {
                    _activeTab.TabButton.IsActive = true;
                }

                if (_activeTab.HeaderExtraContent != null)
                {
                    _activeTab.HeaderExtraContent.SetParent(TabHeaderContentContainer, false);
                    _activeTab.HeaderExtraContent.gameObject.SetActive(true);
                }
            }

            if (ActiveTabChanged != null)
            {
                ActiveTabChanged(this, _activeTab);
            }
        }

        private void SortTabs()
        {
            _tabs.Sort((t1, t2) => t1.SortIndex.CompareTo(t2.SortIndex));

            for (var i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].TabButton != null)
                {
                    _tabs[i].TabButton.CachedTransform.SetSiblingIndex(i);
                }
            }
        }
    }
}
