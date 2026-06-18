/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugNumberButton.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using UnityEngine.UI;

namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.EventSystems;

    [AddComponentMenu(ComponentMenuPaths.NumberButton)]
    public class DebugNumberButton : UnityEngine.UI.Button, IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float ExtraThreshold = 3f;
        public const float Delay = 0.4f;
        private float _delayTime;
        private float _downTime;
        private bool _isDown;
        public double Amount = 1;
        public DebugNumberSpinner TargetField;

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);

            if (!interactable)
            {
                return;
            }

            Apply();

            _isDown = true;
            _downTime = Time.realtimeSinceStartup;
            _delayTime = _downTime + Delay;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            base.OnPointerUp(eventData);

            _isDown = false;
        }

        protected virtual void Update()
        {
            if (_isDown)
            {
                if (_delayTime <= Time.realtimeSinceStartup)
                {
                    Apply();

                    var newDelay = Delay*0.5f;

                    var extra = Mathf.RoundToInt((Time.realtimeSinceStartup - _downTime)/ExtraThreshold);

                    for (var i = 0; i < extra; i++)
                    {
                        newDelay *= 0.5f;
                    }

                    _delayTime = Time.realtimeSinceStartup + newDelay;
                }
            }
        }

        private void Apply()
        {
            var currentValue = double.Parse(TargetField.text);
            currentValue += Amount;

            if (currentValue > TargetField.MaxValue)
            {
                currentValue = TargetField.MaxValue;
            }
            if (currentValue < TargetField.MinValue)
            {
                currentValue = TargetField.MinValue;
            }

            TargetField.text = currentValue.ToString();
            TargetField.onEndEdit.Invoke(TargetField.text);
        }
    }
}
