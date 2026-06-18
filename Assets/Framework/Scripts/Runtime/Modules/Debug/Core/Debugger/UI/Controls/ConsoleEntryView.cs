/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConsoleEntryView.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof (RectTransform))]
    public class ConsoleEntryView : DebugMonoBehaviourEx, IVirtualView
    {
        public const string ConsoleBlobDebug = "Console_Debug_Blob";
        public const string ConsoleBlobInfo = "Console_Info_Blob";
        public const string ConsoleBlobWarning = "Console_Warning_Blob";
        public const string ConsoleBlobError = "Console_Error_Blob";
        public const string ConsoleBlobFatal = "Console_Fatal_Blob";

        private const string c_ColorDebug = "#00E7FF";
        private const string c_ColorInfo = "#00BF0F";
        private const string c_ColorWarning = "#FDFF00";
        private const string c_ColorError = "#FF0000";
        private const string c_ColorFatal = "#FF00BF";
        private const int c_MinMessagePreviewLength = 48;
        private const int c_MaxMessagePreviewLength = 900;
        private const float c_MessageWidthEpsilon = 1f;

        private int _count;
        private bool _hasCount;
        private bool _colorDirty;
        private float _messageWidth;
        private ConsoleEntry _prevData;
        private RectTransform _rectTransform;

        [RequiredField] public Text Count;

        [RequiredField] public CanvasGroup CountContainer;

        [RequiredField] public StyleComponent ImageStyle;

        [RequiredField] public Text Message;

        [RequiredField] public Text StackTrace;

        public void SetDataContext(object data)
        {
            var msg = data as ConsoleEntry;

            if (msg == null)
            {
                throw new Exception("Data should be a ConsoleEntry");
            }

            // Always check for updates on "Count", as it can change
            if (msg.Count > 1)
            {
                if (!_hasCount)
                {
                    CountContainer.alpha = 1f;
                    _hasCount = true;
                }

                if (msg.Count != _count)
                {
                    Count.text = RuntimeDebuggerUtil.GetNumberString(msg.Count, 999, "999+");
                    _count = msg.Count;
                }
            }
            else if (_hasCount)
            {
                CountContainer.alpha = 0f;
                _hasCount = false;
            }

            // Apply color every time — StyleComponent may reset Message.color on style refresh
            ApplyStyle(msg);

            // Only update text/layout if data context and available width have not changed.
            if (msg == _prevData && !HasMessageWidthChanged())
            {
                return;
            }

            _prevData = msg;

            RefreshTextAndLayout();
        }

        private void ApplyStyle(ConsoleEntry msg)
        {
            switch (msg.LogType)
            {
                case LogType.Log:
                    if (msg.NovaLogLevel == NovaLogLevel.Debug)
                    {
                        ImageStyle.StyleKey = ConsoleBlobDebug;
                        ColorUtility.TryParseHtmlString(c_ColorDebug, out var colorDebug);
                        Message.color = colorDebug;
                    }
                    else
                    {
                        ImageStyle.StyleKey = ConsoleBlobInfo;
                        ColorUtility.TryParseHtmlString(c_ColorInfo, out var colorInfo);
                        Message.color = colorInfo;
                    }
                    break;

                case LogType.Warning:
                    ImageStyle.StyleKey = ConsoleBlobWarning;
                    ColorUtility.TryParseHtmlString(c_ColorWarning, out var colorWarning);
                    Message.color = colorWarning;
                    break;

                case LogType.Exception:
                    if (msg.NovaLogLevel == NovaLogLevel.Fatal)
                    {
                        ImageStyle.StyleKey = ConsoleBlobFatal;
                        ColorUtility.TryParseHtmlString(c_ColorFatal, out var colorFatal);
                        Message.color = colorFatal;
                    }
                    else
                    {
                        ImageStyle.StyleKey = ConsoleBlobError;
                        ColorUtility.TryParseHtmlString(c_ColorError, out var colorError);
                        Message.color = colorError;
                    }
                    break;

                case LogType.Assert:
                case LogType.Error:
                    ImageStyle.StyleKey = ConsoleBlobError;
                    ColorUtility.TryParseHtmlString(c_ColorError, out var colorErr);
                    Message.color = colorErr;
                    break;
            }
        }

        // Called by StyleRoot.BroadcastMessage after StyleSheet changes
        private void DebugStyleDirty()
        {
            _colorDirty = true;
        }

        private void LateUpdate()
        {
            if (_colorDirty && _prevData != null)
            {
                _colorDirty = false;
                ApplyStyle(_prevData);
            }

            if (_prevData != null && HasMessageWidthChanged())
            {
                RefreshTextAndLayout();
            }
        }

        protected override void Awake()
        {
            base.Awake();

            _rectTransform = CachedTransform as RectTransform;
            CountContainer.alpha = 0f;

            Message.supportRichText = Settings.Instance.RichTextInConsole;
        }

        private void RefreshTextAndLayout()
        {
            var previewLength = GetDynamicMessagePreviewLength(_prevData);
            Message.text = string.Format("[{0}]{1}", _prevData.Timestamp.ToString("HH:mm:ss.fff"),
                _prevData.GetMessagePreview(previewLength));
            StackTrace.text = _prevData.GetStackTracePreview(120);

            if (string.IsNullOrEmpty(StackTrace.text))
            {
                Message.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 2, _rectTransform.rect.height - 4);
            }
            else
            {
                Message.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 12, _rectTransform.rect.height - 14);
            }
        }

        private bool HasMessageWidthChanged()
        {
            var width = Message.rectTransform.rect.width;
            if (Mathf.Abs(width - _messageWidth) <= c_MessageWidthEpsilon)
            {
                return false;
            }

            _messageWidth = width;
            return true;
        }

        private int GetDynamicMessagePreviewLength(ConsoleEntry entry)
        {
            var width = Message.rectTransform.rect.width;
            if (width <= 0f)
            {
                return c_MinMessagePreviewLength;
            }

            var low = c_MinMessagePreviewLength;
            var high = c_MaxMessagePreviewLength;
            while (low < high)
            {
                var mid = (low + high + 1) / 2;
                if (DoesMessagePreviewFit(entry, mid, width))
                {
                    low = mid;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return low;
        }

        private bool DoesMessagePreviewFit(ConsoleEntry entry, int previewLength, float width)
        {
            var text = string.Format("[{0}]{1}", entry.Timestamp.ToString("HH:mm:ss.fff"),
                entry.GetMessagePreview(previewLength));
            var settings = Message.GetGenerationSettings(new Vector2(100000f, Message.rectTransform.rect.height));
            var preferredWidth = Message.cachedTextGeneratorForLayout.GetPreferredWidth(text, settings) / Message.pixelsPerUnit;
            return preferredWidth <= width;
        }
    }
}
