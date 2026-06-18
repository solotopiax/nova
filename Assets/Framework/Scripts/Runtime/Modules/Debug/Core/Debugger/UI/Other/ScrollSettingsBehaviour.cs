/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ScrollSettingsBehaviour.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    [RequireComponent(typeof (ScrollRect))]
    public class ScrollSettingsBehaviour : MonoBehaviour
    {
        public const float ScrollSensitivity = 40f;

        private void Awake()
        {
            var scrollRect = GetComponent<ScrollRect>();
            scrollRect.scrollSensitivity = ScrollSensitivity;

            if (!RuntimeDebuggerUtil.IsMobilePlatform)
            {
                scrollRect.movementType = ScrollRect.MovementType.Clamped;
                scrollRect.inertia = false;
            }
        }
    }
}
