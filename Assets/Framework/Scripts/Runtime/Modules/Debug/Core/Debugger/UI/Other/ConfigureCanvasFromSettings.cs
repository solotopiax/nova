/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigureCanvasFromSettings.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using System;
using System.ComponentModel;
using UnityEngine.UI;

namespace NovaFramework.Runtime
{
    using UnityEngine;

    [RequireComponent(typeof (Canvas))]
    public class ConfigureCanvasFromSettings : DebugMonoBehaviour
    {
        private Canvas _canvas;
        private CanvasScaler _canvasScaler;

        private float _originalScale;
        private float _lastSetScale;
        private Settings _settings;

        private void Start()
        {
            _canvas = GetComponent<Canvas>();
            _canvasScaler = GetComponent<CanvasScaler>();

            RuntimeDebuggerUtil.ConfigureCanvas(_canvas);

            _settings = RuntimeDebugger.Instance.Settings;
            _originalScale = _canvasScaler.scaleFactor;
            _canvasScaler.scaleFactor = _originalScale * _settings.UIScale;

            // Track the last set scale in case it is modified by the retina scaler.
            _lastSetScale = _canvasScaler.scaleFactor;

            _settings.PropertyChanged += SettingsOnPropertyChanged;
        }

        private void OnDestroy()
        {
            if (_settings != null)
            {
                _settings.PropertyChanged -= SettingsOnPropertyChanged;
            }
        }

        private void SettingsOnPropertyChanged(object sender, PropertyChangedEventArgs propertyChangedEventArgs)
        {
            // If the last set scale does not match the current scale factor, then it is likely the retina scaler has applied a change.
            // Treat the new value as the original scale.
            if (_canvasScaler.scaleFactor != _lastSetScale) _originalScale = _canvasScaler.scaleFactor;

            _canvasScaler.scaleFactor = _originalScale * _settings.UIScale;
            _lastSetScale = _canvasScaler.scaleFactor;
        }
    }
}
