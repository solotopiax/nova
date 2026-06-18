/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConsoleLogControl.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
#pragma warning disable 169
#pragma warning disable 649

namespace NovaFramework.Runtime
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.UI;

    public class ConsoleLogControl : DebugMonoBehaviourEx
    {
        [RequiredField] [SerializeField] private VirtualVerticalLayoutGroup _consoleScrollLayoutGroup;

        [RequiredField] [SerializeField] private ScrollRect _consoleScrollRect;

        /// <summary>
        /// 是否脏。
        /// </summary>
        private bool _isDirty;

        /// <summary>
        /// 滚动位置。
        /// </summary>
        private Vector2? _scrollPosition;

        /// <summary>
        /// 是否显示 Error 级别日志（对应 LogType.Error）。
        /// </summary>  
        private bool _showErrors = true;

        /// <summary>
        /// 是否显示 Info 级别日志（对应 LogType.Log）。
        /// </summary>
        private bool _showInfo = true;

        /// <summary>
        /// 是否显示 Warning 级别日志（对应 LogType.Warning）。
        /// </summary>
        private bool _showWarnings = true;

        /// <summary>
        /// 是否显示 Nova Debug 级别日志（对应 NovaLogLevel.Debug）。
        /// </summary>
        private bool _showDebug = true;

        /// <summary>
        /// 是否显示 Nova Fatal 级别日志（对应 NovaLogLevel.Fatal）。
        /// </summary>
        private bool _showFatal = true;

        /// <summary>
        /// 选择项变更事件。
        /// </summary>
        public Action<ConsoleEntry> SelectedItemChanged;

        /// <summary>
        /// 过滤器。
        /// </summary>
        private string _filter;
        /// <summary>
        /// Tag 过滤集合，空或 null 表示不过滤。
        /// </summary>
        private IReadOnlyCollection<string> m_TagFilter;
        public bool ShowErrors
        {
            get { return _showErrors; }
            set
            {
                _showErrors = value;
                SetIsDirty();
            }
        }

        public bool ShowWarnings
        {
            get { return _showWarnings; }
            set
            {
                _showWarnings = value;
                SetIsDirty();
            }
        }

        public bool ShowInfo
        {
            get { return _showInfo; }
            set
            {
                _showInfo = value;
                SetIsDirty();
            }
        }

        /// <summary>
        /// 是否显示 Nova Debug 级别日志。
        /// </summary>
        public bool ShowDebug
        {
            get { return _showDebug; }
            set
            {
                _showDebug = value;
                SetIsDirty();
            }
        }

        /// <summary>
        /// 是否显示 Nova Fatal 级别日志。
        /// </summary>
        public bool ShowFatal
        {
            get { return _showFatal; }
            set
            {
                _showFatal = value;
                SetIsDirty();
            }
        }

        public bool EnableSelection
        {
            get { return _consoleScrollLayoutGroup.EnableSelection; }
            set { _consoleScrollLayoutGroup.EnableSelection = value; }
        }

        public string Filter
        {
            get { return _filter; }
            set {
                if (_filter != value)
                {
                    _filter = value;
                    _isDirty = true;
                }
            }
        }

        /// <summary>
        /// Tag 过滤集合，空或 null 表示不过滤。
        /// </summary>
        public IReadOnlyCollection<string> TagFilter
        {
            get => m_TagFilter;
            set
            {
                m_TagFilter = value;
                SetIsDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();

            _consoleScrollLayoutGroup.SelectedItemChanged.AddListener(OnSelectedItemChanged);
            Service.Console.Updated += ConsoleOnUpdated;
        }

        protected override void Start()
        {
            base.Start();
            SetIsDirty();
            StartCoroutine(ScrollToBottom());
        }

        IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            _scrollPosition = new Vector2(0,0);
        }

        protected override void OnDestroy()
        {
            if (Service.Console != null)
            {
                Service.Console.Updated -= ConsoleOnUpdated;
            }

            base.OnDestroy();
        }

        private void OnSelectedItemChanged(object arg0)
        {
            var entry = arg0 as ConsoleEntry;

            if (SelectedItemChanged != null)
            {
                SelectedItemChanged(entry);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (_scrollPosition.HasValue)
            {
                _consoleScrollRect.normalizedPosition = _scrollPosition.Value;
                _scrollPosition = null;
            }

            if (_isDirty)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            if (_consoleScrollRect.normalizedPosition.y < 0.01f)
            {
                _scrollPosition = _consoleScrollRect.normalizedPosition;
            }

            _consoleScrollLayoutGroup.ClearItems();

            var entries = Service.Console.Entries;

            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];

                // Fatal: LogType.Exception + NovaLogLevel.Fatal — check before generic Error block
                if (e.LogType == LogType.Exception && e.NovaLogLevel == NovaLogLevel.Fatal)
                {
                    if (!ShowFatal)
                    {
                        if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                        continue;
                    }
                }
                else if (e.LogType == LogType.Error || e.LogType == LogType.Exception || e.LogType == LogType.Assert)
                {
                    if (!ShowErrors)
                    {
                        if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                        continue;
                    }
                }

                if (e.LogType == LogType.Warning && !ShowWarnings)
                {
                    if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                    continue;
                }

                // Debug: LogType.Log + NovaLogLevel.Debug — check before generic Info block
                if (e.LogType == LogType.Log && e.NovaLogLevel == NovaLogLevel.Debug)
                {
                    if (!ShowDebug)
                    {
                        if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                        continue;
                    }
                }
                else if (e.LogType == LogType.Log && !ShowInfo)
                {
                    if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                    continue;
                }

                if (!string.IsNullOrEmpty(Filter))
                {
                    if (e.Message.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                        continue;
                    }
                }

                if (m_TagFilter != null && m_TagFilter.Count > 0)
                {
                    var matched = false;
                    foreach (var tag in m_TagFilter)
                    {
                        if (e.Message.IndexOf(tag, StringComparison.Ordinal) >= 0)
                        {
                            matched = true;
                            break;
                        }
                    }
                    if (!matched)
                    {
                        if (e == _consoleScrollLayoutGroup.SelectedItem) _consoleScrollLayoutGroup.SelectedItem = null;
                        continue;
                    }
                }

                _consoleScrollLayoutGroup.AddItem(e);
            }

            _isDirty = false;
        }

        private void SetIsDirty()
        {
            _isDirty = true;
        }

        private void ConsoleOnUpdated(IConsoleService console)
        {
            SetIsDirty();
        }

        /// <summary>
        /// 返回按当前过滤条件（级别/文本/Tag）筛选后的可见条目列表，与面板实际显示内容一致。
        /// </summary>
        /// <returns>过滤后的条目列表。</returns>
        public List<ConsoleEntry> GetFilteredEntries()
        {
            var result = new List<ConsoleEntry>();
            var entries = Service.Console.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];

                if (e.LogType == LogType.Exception && e.NovaLogLevel == NovaLogLevel.Fatal)
                {
                    if (!ShowFatal) continue;
                }
                else if (e.LogType == LogType.Error || e.LogType == LogType.Exception || e.LogType == LogType.Assert)
                {
                    if (!ShowErrors) continue;
                }

                if (e.LogType == LogType.Warning && !ShowWarnings) continue;

                if (e.LogType == LogType.Log && e.NovaLogLevel == NovaLogLevel.Debug)
                {
                    if (!ShowDebug) continue;
                }
                else if (e.LogType == LogType.Log && !ShowInfo) continue;

                if (!string.IsNullOrEmpty(Filter) && e.Message.IndexOf(Filter, StringComparison.OrdinalIgnoreCase) < 0) continue;

                if (m_TagFilter != null && m_TagFilter.Count > 0)
                {
                    var matched = false;
                    foreach (var tag in m_TagFilter)
                    {
                        if (e.Message.IndexOf(tag, StringComparison.Ordinal) >= 0) { matched = true; break; }
                    }
                    if (!matched) continue;
                }

                result.Add(e);
            }
            return result;
        }
    }
}
