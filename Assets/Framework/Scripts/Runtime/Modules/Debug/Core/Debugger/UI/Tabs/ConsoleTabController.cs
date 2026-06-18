/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConsoleTabController.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
//#define DEBUGGER_CONSOLE_TRACE

using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine;
    using UnityEngine.UI;

    public class ConsoleTabController : DebugMonoBehaviourEx
    {
        private const int MaxLength = 5000;

        private Canvas _consoleCanvas;
        private bool _isDirty;

        private static bool _hasWarnedAboutLogHandler;
        private static bool _hasWarnedAboutLoggingDisabled;

        [Import]
        public IConsoleFilterState FilterState;

        [RequiredField]
        public ConsoleLogControl ConsoleLogControl;

        [RequiredField]
        public Toggle PinToggle;
        //public bool IsListening = true;

        [RequiredField]
        public ScrollRect StackTraceScrollRect;
        [RequiredField]
        public Text StackTraceText;
        [RequiredField]
        public Toggle ToggleInfo;
        [RequiredField]
        public Text ToggleInfoText;
        [RequiredField]
        public Toggle ToggleDebug;
        [RequiredField]
        public Text ToggleDebugText;
        [RequiredField]
        public Toggle ToggleWarnings;
        [RequiredField]
        public Text ToggleWarningsText;
        [RequiredField]
        public Toggle ToggleErrors;
        [RequiredField]
        public Text ToggleErrorsText;
        [RequiredField]
        public Toggle ToggleFatal;
        [RequiredField]
        public Text ToggleFatalText;

        [RequiredField]
        public GameObject CopyToClipboardContainer;

        [RequiredField]
        public GameObject CopyToClipboardButton;

        [RequiredField]
        public GameObject CopyToClipboardMessage;

        [RequiredField]
        public CanvasGroup CopyToClipboardMessageCanvasGroup;

        [RequiredField]
        public GameObject LoggingIsDisabledCanvasGroup;

        /// <summary>
        /// 上传日志按钮 GameObject，当 RuntimeDebugger.UploadLogCallback 不为 null 时显示。
        /// </summary>
        [RequiredField]
        public GameObject UploadLogButton;

        [RequiredField]
        public GameObject LogHandlerHasBeenOverridenGroup;

        [RequiredField]
        public Toggle FilterToggle;
        [RequiredField]
        public InputField FilterField;
        [RequiredField]
        public GameObject FilterBarContainer;

        /// <summary>
        /// Tag 过滤面板控件，用于按 Tag 筛选控制台日志条目。
        /// </summary>
        [RequiredField]
        public TagFilterPanelControl TagFilterPanel;

        private ConsoleEntry _selectedItem;

        private Coroutine _fadeButtonCoroutine;

        protected override void Start()
        {
            base.Start();

            _consoleCanvas = GetComponent<Canvas>();
            
            ToggleErrors.isOn = FilterState.GetNovaFilterState(NovaLogLevel.Error);
            ToggleWarnings.isOn = FilterState.GetNovaFilterState(NovaLogLevel.Warning);
            ToggleInfo.isOn = FilterState.GetNovaFilterState(NovaLogLevel.Info);
            ToggleDebug.isOn = FilterState.GetNovaFilterState(NovaLogLevel.Debug);
            ToggleFatal.isOn = FilterState.GetNovaFilterState(NovaLogLevel.Fatal);

            ToggleErrors.onValueChanged.AddListener(isOn => { FilterState.SetNovaFilterState(NovaLogLevel.Error, isOn); _isDirty = true; });
            ToggleWarnings.onValueChanged.AddListener(isOn => { FilterState.SetNovaFilterState(NovaLogLevel.Warning, isOn); _isDirty = true; });
            ToggleInfo.onValueChanged.AddListener(isOn => { FilterState.SetNovaFilterState(NovaLogLevel.Info, isOn); _isDirty = true; });
            ToggleDebug.onValueChanged.AddListener(isOn => { FilterState.SetNovaFilterState(NovaLogLevel.Debug, isOn); _isDirty = true; });
            ToggleFatal.onValueChanged.AddListener(isOn => { FilterState.SetNovaFilterState(NovaLogLevel.Fatal, isOn); _isDirty = true; });

            PinToggle.onValueChanged.AddListener(PinToggleValueChanged);

            FilterToggle.onValueChanged.AddListener(FilterToggleValueChanged);
            FilterBarContainer.SetActive(true);

            ConsoleLogControl.Filter = FilterState.TextFilter;

            FilterField.text = FilterState.TextFilter;
            FilterField.onValueChanged.AddListener(FilterValueChanged);

            TagFilterPanel.gameObject.SetActive(false);
            TagFilterPanel.OnTagFilterChanged = OnTagFilterChanged;
            TagFilterPanel.OnConfirmClicked = () =>
            {
                TagFilterPanel.gameObject.SetActive(false);
                FilterToggle.SetIsOnWithoutNotify(false);
            };
            TagFilterPanel.OnCloseClicked = () =>
            {
                TagFilterPanel.gameObject.SetActive(false);
                FilterToggle.SetIsOnWithoutNotify(false);
            };
            TagFilterPanel.Initialize();

            UploadLogButton.GetComponent<Button>().onClick.AddListener(OnUploadLog);
            UploadLogButton.SetActive(RuntimeDebugger.UploadLogCallback != null);

            ConsoleLogControl.SelectedItemChanged = ConsoleLogSelectedItemChanged;

            Service.Console.Updated += ConsoleOnUpdated;
            Service.Panel.VisibilityChanged += PanelOnVisibilityChanged;

            FilterState.FilterStateChange += OnFilterStateChange;
            FilterState.NovaFilterStateChange += OnNovaFilterStateChange;
            FilterState.TextFilterChange += OnTextFilterChange;

            StackTraceText.supportRichText = Settings.Instance.RichTextInConsole;
            PopulateStackTraceArea(null);

            Refresh();
        }

        private void OnFilterStateChange(LogType logtype, bool newstate)
        {
            switch (logtype)
            {
                case LogType.Error:
                    ToggleErrors.isOn = newstate;
                    break;
                case LogType.Warning:
                    ToggleWarnings.isOn = newstate;
                    break;
                case LogType.Log:
                    ToggleInfo.isOn = newstate;
                    break;
            }
        }

        private void OnNovaFilterStateChange(NovaLogLevel level, bool newState)
        {
            switch (level)
            {
                case NovaLogLevel.Debug:
                    ToggleDebug.isOn = newState;
                    break;
                case NovaLogLevel.Fatal:
                    ToggleFatal.isOn = newState;
                    break;
            }
        }

        private void OnTextFilterChange(string newFilter)
        {
            FilterField.text = newFilter;
            if (!string.IsNullOrWhiteSpace(newFilter))
            {
                FilterToggle.SetIsOnWithoutNotify(true);
            }
        }

        /// <summary>
        /// TagFilterPanel 回调：Tag 过滤集合发生变化时同步更新 ConsoleLogControl 与 FilterState。
        /// </summary>
        /// <param name="tags">当前选中的 Tag 集合，空集合表示不过滤。</param>
        private void OnTagFilterChanged(IReadOnlyCollection<string> tags)
        {
            ConsoleLogControl.TagFilter = tags.Count > 0 ? tags : null;
            FilterState.SetTagFilter(tags);
            _isDirty = true;
        }

        /// <summary>
        /// FilterToggle 开关变更回调：开启时调用 Open 传入当前已提交集合，关闭时仅隐藏面板。
        /// </summary>
        /// <param name="isOn">Toggle 当前是否开启。</param>
        private void FilterToggleValueChanged(bool isOn)
        {
            if (isOn)
                TagFilterPanel.Open(FilterState.TagFilter);
            else
                TagFilterPanel.gameObject.SetActive(false);
        }

        private void FilterValueChanged(string filterText)
        {
            var trimmed = string.IsNullOrEmpty(filterText) ? null : filterText.Trim();
            ConsoleLogControl.Filter = string.IsNullOrEmpty(trimmed) ? null : trimmed;
            FilterState.TextFilter = ConsoleLogControl.Filter;
        }

        private void PanelOnVisibilityChanged(IDebugPanelService debugPanelService, bool b)
        {
            if (_consoleCanvas == null)
            {
                return;
            }

            if (b)
            {
                _consoleCanvas.enabled = true;
            }
            else
            {
                _consoleCanvas.enabled = false;
                StopAnimations();
            }
        }

        private void PinToggleValueChanged(bool isOn)
        {
            Service.DockConsole.IsVisible = isOn;
        }

        protected override void OnDestroy()
        {
            StopAnimations();

            if (Service.Console != null)
            {
                Service.Console.Updated -= ConsoleOnUpdated;
            }

            FilterState.FilterStateChange -= OnFilterStateChange;
            FilterState.NovaFilterStateChange -= OnNovaFilterStateChange;
            FilterState.TextFilterChange -= OnTextFilterChange;

            if (UploadLogButton != null)
                UploadLogButton.GetComponent<Button>()?.onClick.RemoveListener(OnUploadLog);

            base.OnDestroy();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            _isDirty = true;
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            StopAnimations();
        }

        private void ConsoleLogSelectedItemChanged(object item)
        {
            var log = item as ConsoleEntry;
            PopulateStackTraceArea(log);
        }

        protected override void Update()
        {
            base.Update();

            if (_isDirty)
            {
                Refresh();
            }
        }

        private void PopulateStackTraceArea(ConsoleEntry entry)
        {
            if (entry == null)
            {
                SetCopyToClipboardButtonState(CopyToClipboardStates.Hidden);
                StackTraceText.text = "";
            }
            else
            {
                if (RuntimeDebugger.CopyConsoleItemCallback != null)
                {
                    SetCopyToClipboardButtonState(CopyToClipboardStates.Visible);
                }

                var text = entry.Message + Environment.NewLine +
                           (!string.IsNullOrEmpty(entry.StackTrace)
                               ? entry.StackTrace
                               : RuntimeDebuggerStrings.Current.Console_NoStackTrace);

                if (text.Length > MaxLength)
                {
                    text = text.Substring(0, MaxLength);
                    text += "\n" + RuntimeDebuggerStrings.Current.Console_MessageTruncated;
                }

                StackTraceText.text = text;
            }

            StackTraceScrollRect.normalizedPosition = new Vector2(0, 1);
            _selectedItem = entry;
        }

        public void CopyToClipboard()
        {
            if (_selectedItem != null)
            {
                SetCopyToClipboardButtonState(CopyToClipboardStates.Activated);
                if (RuntimeDebugger.CopyConsoleItemCallback != null)
                {
                    RuntimeDebugger.CopyConsoleItemCallback(_selectedItem);
                }
                else
                {
                    Debug.LogError("[RuntimeDebugger] Copy to clipboard is not available.");
                }
            }
        }

        public enum CopyToClipboardStates
        {
            Hidden,
            Visible,
            Activated
        }

        void SetCopyToClipboardButtonState(CopyToClipboardStates state)
        {
            StopAnimations();

            switch (state)
            {
                case CopyToClipboardStates.Hidden:
                    CopyToClipboardContainer.SetActive(false);
                    CopyToClipboardButton.SetActive(false);
                    CopyToClipboardMessage.SetActive(false);
                    break;
                case CopyToClipboardStates.Visible:
                    CopyToClipboardContainer.SetActive(true);
                    CopyToClipboardButton.SetActive(true);
                    CopyToClipboardMessage.SetActive(false);
                    break;
                case CopyToClipboardStates.Activated:
                    CopyToClipboardMessageCanvasGroup.alpha = 1;
                    CopyToClipboardContainer.SetActive(true);
                    CopyToClipboardButton.SetActive(false);
                    CopyToClipboardMessage.SetActive(true);

                    _fadeButtonCoroutine = StartCoroutine(FadeCopyButton());
                    break;
                default:
                    throw new ArgumentOutOfRangeException("state", state, null);
            }
        }

        IEnumerator FadeCopyButton()
        {
            yield return new WaitForSecondsRealtime(2f);

            float startTime = Time.realtimeSinceStartup;
            float endTime = Time.realtimeSinceStartup + 1f;

            while (Time.realtimeSinceStartup < endTime)
            {
                float currentAlpha = Mathf.InverseLerp(endTime, startTime, Time.realtimeSinceStartup);
                CopyToClipboardMessageCanvasGroup.alpha = currentAlpha;
                yield return new WaitForEndOfFrame();
            }

            CopyToClipboardMessageCanvasGroup.alpha = 0;
            _fadeButtonCoroutine = null;
        }

        void StopAnimations()
        {
            if (_fadeButtonCoroutine != null)
            {
                StopCoroutine(_fadeButtonCoroutine);
                _fadeButtonCoroutine = null;
                CopyToClipboardMessageCanvasGroup.alpha = 0;
            }
        }

        private void Refresh()
        {
            // Update total counts labels
            ToggleDebugText.text = RuntimeDebuggerUtil.GetNumberString(Service.Console.DebugCount, 999, "999+");
            ToggleInfoText.text = RuntimeDebuggerUtil.GetNumberString(Service.Console.InfoCount, 999, "999+");
            ToggleWarningsText.text = RuntimeDebuggerUtil.GetNumberString(Service.Console.WarningCount, 999, "999+");
            ToggleErrorsText.text = RuntimeDebuggerUtil.GetNumberString(Service.Console.ErrorCount, 999, "999+");
            ToggleFatalText.text = RuntimeDebuggerUtil.GetNumberString(Service.Console.FatalCount, 999, "999+");

            ConsoleLogControl.ShowDebug = ToggleDebug.isOn;
            ConsoleLogControl.ShowErrors = ToggleErrors.isOn;
            ConsoleLogControl.ShowWarnings = ToggleWarnings.isOn;
            ConsoleLogControl.ShowInfo = ToggleInfo.isOn;
            ConsoleLogControl.ShowFatal = ToggleFatal.isOn;

            PinToggle.isOn = Service.DockConsole.IsVisible;

            _isDirty = false;

            if (!_hasWarnedAboutLogHandler && Service.Console.LogHandlerIsOverriden)
            {
                LogHandlerHasBeenOverridenGroup.SetActive(true);
                _hasWarnedAboutLogHandler = true;
            }

            if (!_hasWarnedAboutLoggingDisabled && !Service.Console.LoggingEnabled)
            {
                LoggingIsDisabledCanvasGroup.SetActive(true);
            }
        }

        private void ConsoleOnUpdated(IConsoleService console)
        {
            _isDirty = true;
        }

        public void Clear()
        {
            Service.Console.Clear();
            _isDirty = true;
        }

        /// <summary>
        /// 上传日志按钮回调：获取当前面板可见条目，格式化为字符串后传给外部 UploadLogCallback。
        /// </summary>
        private void OnUploadLog()
        {
            if (RuntimeDebugger.UploadLogCallback == null)
                return;

            var entries = ConsoleLogControl.GetFilteredEntries();
            var sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                sb.AppendLine(string.Format("[{0}] {1}", e.LogType, e.Message));
                if (!string.IsNullOrEmpty(e.StackTrace))
                    sb.AppendLine(e.StackTrace);
                sb.AppendLine();
            }

            RuntimeDebugger.UploadLogCallback.Invoke(sb.ToString());
        }

        public void LogHandlerHasBeenOverridenOkayButtonPress()
        {
            _hasWarnedAboutLogHandler = true;
            LogHandlerHasBeenOverridenGroup.SetActive(false);
        }

        public void LoggingDisableCloseAndIgnorePressed()
        {
            LoggingIsDisabledCanvasGroup.SetActive(false);
            _hasWarnedAboutLoggingDisabled = true;
        }       
        
        public void LoggingDisableReenablePressed()
        {
            Service.Console.LoggingEnabled = true;
            LoggingIsDisabledCanvasGroup.SetActive(false);

            Debug.Log("[RuntimeDebugger] Re-enabled logging.");
        }
    }
}
