/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProfilerGraphAxisLabel.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof (RectTransform))]
    public class ProfilerGraphAxisLabel : DebugMonoBehaviourEx
    {
        private float _prevFrameTime;
        private float? _queuedFrameTime;
        private float _yPosition;

        [RequiredField] public Text Text;

        protected override void Update()
        {
            base.Update();

            if (_queuedFrameTime.HasValue)
            {
                SetValueInternal(_queuedFrameTime.Value);
                _queuedFrameTime = null;
            }
        }

        public void SetValue(float frameTime, float yPosition)
        {
            if (_prevFrameTime == frameTime && _yPosition == yPosition)
            {
                return;
            }

            _queuedFrameTime = frameTime;
            _yPosition = yPosition;
        }

        private void SetValueInternal(float frameTime)
        {
            _prevFrameTime = frameTime;

            var ms = Mathf.FloorToInt(frameTime*1000);
            var fps = Mathf.RoundToInt(1f/frameTime);

            Text.text = "{0}ms ({1}FPS)".Fmt(ms, fps);

            var r = (RectTransform) CachedTransform;
            r.anchoredPosition = new Vector2(r.rect.width*0.5f + 10f, _yPosition);
        }
    }
}
