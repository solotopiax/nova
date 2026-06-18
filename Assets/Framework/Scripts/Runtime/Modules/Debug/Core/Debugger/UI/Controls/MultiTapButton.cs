/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MultiTapButton.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.EventSystems;

    public class MultiTapButton : UnityEngine.UI.Button
    {
        private float _lastTap;
        private int _tapCount;
        public int RequiredTapCount = 3;
        public float ResetTime = 0.5f;

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (Time.unscaledTime - _lastTap > ResetTime)
            {
                _tapCount = 0;
            }

            _lastTap = Time.unscaledTime;
            _tapCount++;

            if (_tapCount == RequiredTapCount)
            {
                base.OnPointerClick(eventData);
                _tapCount = 0;
            }
        }
    }
}
