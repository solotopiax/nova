/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugRetinaScaler.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    /// <summary>
    /// Detects when a screen dpi exceeds what the developer considers
    /// a "retina" level display, and scales the canvas accordingly.
    /// </summary>
    [RequireComponent(typeof (CanvasScaler))]
    [AddComponentMenu(ComponentMenuPaths.RetinaScaler)]
    public class DebugRetinaScaler : DebugMonoBehaviour
    {
        [SerializeField] private bool _disablePixelPerfect = false;

        [SerializeField] private int _designDpi = 120;

        private void Start()
        {
            ApplyScaling();
        }

        private void ApplyScaling()
        {
            var scaler = GetComponent<CanvasScaler>();

            // ScaleWithScreenSize 模式由 CanvasScaler 自动适配，DPI 缩放逻辑不介入
            if (scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                return;
            }

            var dpi = Screen.dpi;

            _lastDpi = dpi;

            if (dpi <= 0)
            {
                return;
            }

#if !UNITY_EDITOR && UNITY_IOS
            // No iOS device has had low dpi for many years - Unity must be reporting it wrong.
            if(dpi < 120)
            {
                dpi = 321;
            }
#endif
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            // Round scale to nearest 0.5
            float scale = dpi / _designDpi;
            scale = Mathf.Max(1, Mathf.Round(scale * 2) / 2.0f);

            scaler.scaleFactor = scale;

            if (_disablePixelPerfect)
            {
                GetComponent<Canvas>().pixelPerfect = false;
            }
        }

        private float _lastDpi;

        void Update()
        {
            if (Screen.dpi != _lastDpi)
            {
                ApplyScaling();
            }
        }

    }
}
