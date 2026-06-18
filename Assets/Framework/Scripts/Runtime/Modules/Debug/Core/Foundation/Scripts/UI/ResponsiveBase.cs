/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ResponsiveBase.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    [ExecuteInEditMode]
    [RequireComponent(typeof (RectTransform))]
    public abstract class ResponsiveBase : DebugMonoBehaviour
    {
        private bool _queueRefresh;

        protected RectTransform RectTransform
        {
            get { return (RectTransform) CachedTransform; }
        }

        protected void OnEnable()
        {
            _queueRefresh = true;
        }

        protected void OnRectTransformDimensionsChange()
        {
            _queueRefresh = true;
        }

        protected void Update()
        {
#if UNITY_EDITOR

            // Refresh whenever we can in the editor, since layout has quirky update behaviour
            // when not in play mode
            if (!Application.isPlaying)
            {
                Refresh();
                return;
            }

#endif

            if (_queueRefresh)
            {
                Refresh();
                _queueRefresh = false;
            }
        }

        protected abstract void Refresh();

        [ContextMenu("Refresh")]
        private void DoRefresh()
        {
            Refresh();
        }
    }
}
