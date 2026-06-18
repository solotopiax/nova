/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StandardConsoleService.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using System;
using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    using UnityEngine;

    [Service(typeof (IConsoleService))]
    public class StandardConsoleService : IConsoleService, IDisposable
    {
        private readonly bool _collapseEnabled;
        private bool _hasCleared;

        private readonly CircularBuffer<ConsoleEntry> _allConsoleEntries;
        private CircularBuffer<ConsoleEntry> _consoleEntries;
        private readonly object _threadLock = new object();

        private ILogHandler _expectedLogHandler;

        public StandardConsoleService()
        {
            Application.logMessageReceivedThreaded += UnityLogCallback;
            _expectedLogHandler = Debug.unityLogger.logHandler;
            
            DebugServiceRegistry.RegisterService<IConsoleService>(this);
            _collapseEnabled = Settings.Instance.CollapseDuplicateLogEntries;
            _allConsoleEntries = new CircularBuffer<ConsoleEntry>(Settings.Instance.MaximumConsoleEntries);
        }

        public void Dispose()
        {
            Application.logMessageReceivedThreaded -= UnityLogCallback;
            if (_consoleEntries != null)
            {
                _consoleEntries.Clear();
            }

            _allConsoleEntries.Clear();
        }

        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public int InfoCount { get; private set; }
        /// <summary>
        /// Nova Debug 级别日志的计数。
        /// </summary>
        public int DebugCount { get; private set; }
        /// <summary>
        /// Nova Fatal 级别日志的计数。
        /// </summary>
        public int FatalCount { get; private set; }

        public event ConsoleUpdatedEventHandler Updated;
        public event ConsoleUpdatedEventHandler Error;

        public bool LoggingEnabled
        {
            get { return Debug.unityLogger.logEnabled; }
            set { Debug.unityLogger.logEnabled = value; }
        }

        public bool LogHandlerIsOverriden
        {
            get
            {
                return Debug.unityLogger.logHandler != _expectedLogHandler;
            }
        }

        public IReadOnlyList<ConsoleEntry> Entries
        {
            get
            {
                if (!_hasCleared)
                {
                    return _allConsoleEntries;
                }

                return _consoleEntries;
            }
        }

        public IReadOnlyList<ConsoleEntry> AllEntries
        {
            get { return _allConsoleEntries; }
        }

        public void Clear()
        {
            lock (_threadLock)
            {
                _hasCleared = true;

                if (_consoleEntries == null)
                {
                    _consoleEntries = new CircularBuffer<ConsoleEntry>(Settings.Instance.MaximumConsoleEntries);
                }
                else
                {
                    _consoleEntries.Clear();
                }

                ErrorCount = WarningCount = InfoCount = DebugCount = FatalCount = 0;
            }

            OnUpdated();
        }

        protected void OnEntryAdded(ConsoleEntry entry)
        {
            if (_hasCleared)
            {
                if (_consoleEntries.IsFull)
                {
                    var evictedEntry = _consoleEntries.Front();
                    AdjustCounter(evictedEntry.LogType, evictedEntry.NovaLogLevel, -1);
                    _consoleEntries.PopFront();
                }

                _consoleEntries.PushBack(entry);
            }
            else
            {
                if (_allConsoleEntries.IsFull)
                {
                    var evictedEntry = _allConsoleEntries.Front();
                    AdjustCounter(evictedEntry.LogType, evictedEntry.NovaLogLevel, -1);
                    _allConsoleEntries.PopFront();
                }
            }

            _allConsoleEntries.PushBack(entry);
            OnUpdated();
        }

        protected void OnEntryDuplicated(ConsoleEntry entry)
        {
            entry.Count++;
            OnUpdated();

            // If has cleared, add this entry again for the current list
            if (_hasCleared && _consoleEntries.Count == 0)
            {
                OnEntryAdded(new ConsoleEntry(entry) {Count = 1});
            }
        }

        private void OnUpdated()
        {
            if (Updated != null)
            {
                try
                {
                    Updated(this);
                }
                catch {}
            }
        }

        private void UnityLogCallback(string condition, string stackTrace, LogType type)
        {
            lock (_threadLock)
            {
                var prevMessage = _collapseEnabled && _allConsoleEntries.Count > 0
                    ? _allConsoleEntries[_allConsoleEntries.Count - 1]
                    : null;

                NovaLogLevel? novaLevel = ParseNovaLogLevel(condition);
                AdjustCounter(type, novaLevel, 1);

                if (prevMessage != null && prevMessage.LogType == type && prevMessage.Message == condition &&
                    prevMessage.StackTrace == stackTrace && prevMessage.NovaLogLevel == novaLevel)
                {
                    OnEntryDuplicated(prevMessage);
                }
                else
                {
                    var newEntry = new ConsoleEntry
                    {
                        LogType = type,
                        StackTrace = stackTrace,
                        Message = condition,
                        NovaLogLevel = novaLevel,
                        Timestamp = DateTime.Now
                    };

                    OnEntryAdded(newEntry);
                }
            }
        }

        /// <summary>
        /// 从消息字符串中解析 Nova 日志级别前缀。
        /// </summary>
        /// <param name="message">Unity 日志回调中的 condition 字符串。</param>
        /// <returns>识别到的 Nova 级别；非 Nova 格式消息返回 null。</returns>
        private static NovaLogLevel? ParseNovaLogLevel(string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            if (message.Contains("[Nova][Fatal]"))
                return NovaLogLevel.Fatal;
            if (message.Contains("[Nova][Error]"))
                return NovaLogLevel.Error;
            if (message.Contains("[Nova][Warning]"))
                return NovaLogLevel.Warning;
            if (message.Contains("[Nova][Info]"))
                return NovaLogLevel.Info;
            if (message.Contains("[Nova][Debug]"))
                return NovaLogLevel.Debug;

            return null;
        }

        /// <summary>
        /// 调整各级别日志计数。Fatal 日志计入 FatalCount 而非 ErrorCount；Debug 日志计入 DebugCount 而非 InfoCount。
        /// </summary>
        /// <param name="type">Unity 原始 LogType。</param>
        /// <param name="novaLevel">Nova 解析级别（可为 null）。</param>
        /// <param name="amount">计数变化量（+1 或 -1）。</param>
        private void AdjustCounter(LogType type, NovaLogLevel? novaLevel, int amount)
        {
            switch (type)
            {
                case LogType.Exception:
                    if (novaLevel == NovaLogLevel.Fatal)
                    {
                        FatalCount += amount;
                    }
                    else
                    {
                        ErrorCount += amount;
                        Error?.Invoke(this);
                    }
                    break;

                case LogType.Assert:
                case LogType.Error:
                    ErrorCount += amount;
                    Error?.Invoke(this);
                    break;

                case LogType.Warning:
                    WarningCount += amount;
                    break;

                case LogType.Log:
                    if (novaLevel == NovaLogLevel.Debug)
                        DebugCount += amount;
                    else
                        InfoCount += amount;
                    break;
            }
        }
    }
}
