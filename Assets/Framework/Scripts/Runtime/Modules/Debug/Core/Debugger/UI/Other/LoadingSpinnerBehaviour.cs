/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LoadingSpinnerBehaviour.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    public class LoadingSpinnerBehaviour : DebugMonoBehaviour
    {
        private float _dt;
        public int FrameCount = 12;
        public float SpinDuration = 0.8f;

        private void Update()
        {
            _dt += Time.unscaledDeltaTime;

            var localRotation = CachedTransform.localRotation.eulerAngles;
            var r = localRotation.z;

            var fTime = SpinDuration/FrameCount;
            var hasChanged = false;

            while (_dt > fTime)
            {
                r -= 360f/FrameCount;
                _dt -= fTime;
                hasChanged = true;
            }

            if (hasChanged)
            {
                CachedTransform.localRotation = Quaternion.Euler(localRotation.x, localRotation.y, r);
            }
        }
    }
}
