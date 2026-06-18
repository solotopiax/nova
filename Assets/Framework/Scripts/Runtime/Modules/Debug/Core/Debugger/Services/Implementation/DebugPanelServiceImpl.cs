/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugPanelServiceImpl.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine;

    [Service(typeof (IDebugPanelService))]
    public class DebugPanelServiceImpl : ScriptableObject, IDebugPanelService, IDisposable
    {
        private DebugPanelRoot _debugPanelRootObject;
        public event Action<IDebugPanelService, bool> VisibilityChanged;

        private bool _isVisible;

        private bool? _cursorWasVisible;

        private CursorLockMode? _cursorLockMode;

        private DefaultTabs? _lastTab;


        public DebugPanelRoot RootObject
        {
            get { return _debugPanelRootObject; }
        }

        public bool IsLoaded
        {
            get { return _debugPanelRootObject != null; }
        }

        public bool IsVisible
        {
            get { return IsLoaded && _isVisible; }
            set
            {
                if (_isVisible == value)
                {
                    return;
                }

                if (value)
                {
                    var wasLoaded = IsLoaded;
                    if (!IsLoaded)
                    {
                        Load();
                    }

                    RuntimeDebuggerUtil.EnsureEventSystemExists();

                    _debugPanelRootObject.CanvasGroup.alpha = 1.0f;
                    _debugPanelRootObject.CanvasGroup.interactable = true;
                    _debugPanelRootObject.CanvasGroup.blocksRaycasts = true;
                    _cursorWasVisible = Cursor.visible;
                    _cursorLockMode = Cursor.lockState;

                    foreach (var c in _debugPanelRootObject.GetComponentsInChildren<Canvas>())
                    {
                        c.enabled = true;
                    }
                    
                    if (Settings.Instance.AutomaticallyShowCursor)
                    {
                        Cursor.visible = true;
                        Cursor.lockState = CursorLockMode.None;
                    }

                    // 新建或重建后重新订阅 tab 切换事件
                    if (!wasLoaded)
                    {
                        _debugPanelRootObject.TabController.TabController.ActiveTabChanged += OnActiveTabChanged;
                    }

                    // 恢复上次 tab，首次打开默认 Console
                    _debugPanelRootObject.TabController.OpenTab(_lastTab ?? DefaultTabs.Console);
                }
                else
                {
                    if (IsLoaded)
                    {
                        _debugPanelRootObject.CanvasGroup.alpha = 0.0f;
                        _debugPanelRootObject.CanvasGroup.interactable = false;
                        _debugPanelRootObject.CanvasGroup.blocksRaycasts = false;

                        foreach (var c in _debugPanelRootObject.GetComponentsInChildren<Canvas>())
                        {
                            c.enabled = false;
                        }
                    }

                    if (_cursorWasVisible.HasValue)
                    {
                        Cursor.visible = _cursorWasVisible.Value;
                        _cursorWasVisible = null;
                    }

                    if (_cursorLockMode.HasValue)
                    {
                        Cursor.lockState = _cursorLockMode.Value;
                        _cursorLockMode = null;
                    }
                }

                _isVisible = value;

                if (VisibilityChanged != null)
                {
                    VisibilityChanged(this, _isVisible);
                }
            }
        }

        public DefaultTabs? ActiveTab
        {
            get
            {
                if (_debugPanelRootObject == null)
                {
                    return null;
                }

                return _debugPanelRootObject.TabController.ActiveTab;
            }
        }

        public void OpenTab(DefaultTabs tab)
        {
            if (!IsVisible)
            {
                IsVisible = true;
            }

            _debugPanelRootObject.TabController.OpenTab(tab);
        }

        public void Unload()
        {
            if (_debugPanelRootObject == null)
            {
                return;
            }

            _debugPanelRootObject.TabController.TabController.ActiveTabChanged -= OnActiveTabChanged;

            IsVisible = false;

            _debugPanelRootObject.CachedGameObject.SetActive(false);
            Destroy(_debugPanelRootObject.CachedGameObject);

            _debugPanelRootObject = null;
        }

        private void OnActiveTabChanged(DebugTabController ctrl, DebugTab tab)
        {
            _lastTab = ActiveTab;
        }

        private void Load()
        {
            var prefab = Resources.Load<DebugPanelRoot>(RuntimeDebuggerPaths.DebugPanelPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[RuntimeDebugger] Error loading debug panel prefab");
                return;
            }

            _debugPanelRootObject = DebugInstantiate.Instantiate(prefab);
            _debugPanelRootObject.name = "Panel";

            DontDestroyOnLoad(_debugPanelRootObject);

            _debugPanelRootObject.CachedTransform.SetParent(Hierarchy.Get("RuntimeDebugger"), true);

            RuntimeDebuggerUtil.EnsureEventSystemExists();
        }

        public void Dispose()
        {
            if (_debugPanelRootObject != null)
            {
                DestroyImmediate(_debugPanelRootObject.gameObject);
            }
        }
    }
}
