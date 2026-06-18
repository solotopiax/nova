/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Unselectable.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.EventSystems;

    /// <summary>
    /// Do not allow an object to become select (automatically unfocus when receiving selection callback)
    /// </summary>
    [AddComponentMenu(ComponentMenuPaths.Unselectable)]
    public sealed class Unselectable : DebugMonoBehaviour, ISelectHandler
    {
        private bool _suspectedSelected;

        public void OnSelect(BaseEventData eventData)
        {
            _suspectedSelected = true;
        }

        private void Update()
        {
            if (!_suspectedSelected)
            {
                return;
            }

            if (EventSystem.current.currentSelectedGameObject == CachedGameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            _suspectedSelected = false;
        }
    }
}
