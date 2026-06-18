/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  StyleRoot.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    [ExecuteInEditMode]
    [AddComponentMenu(ComponentMenuPaths.StyleRoot)]
    public sealed class StyleRoot : DebugMonoBehaviour
    {
        private StyleSheet _activeStyleSheet;
        public StyleSheet StyleSheet;

        public Style GetStyle(string key)
        {
            if (StyleSheet == null)
            {
                Debug.LogWarning("[StyleRoot] StyleSheet is not set.", this);
                return null;
            }

            return StyleSheet.GetStyle(key);
        }

        private void OnEnable()
        {
            _activeStyleSheet = null;

            if (StyleSheet != null)
            {
                OnStyleSheetChanged();
            }
        }

        private void OnDisable()
        {
            OnStyleSheetChanged();
        }

        private void Update()
        {
            if (_activeStyleSheet != StyleSheet)
            {
                OnStyleSheetChanged();
            }
        }

        private void OnStyleSheetChanged()
        {
            _activeStyleSheet = StyleSheet;

            BroadcastMessage("DebugStyleDirty", SendMessageOptions.DontRequireReceiver);
        }

        public void SetDirty()
        {
            _activeStyleSheet = null;
        }
    }
}
