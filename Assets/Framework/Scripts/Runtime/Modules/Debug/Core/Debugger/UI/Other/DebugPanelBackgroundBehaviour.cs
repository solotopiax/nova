/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugPanelBackgroundBehaviour.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    [RequireComponent(typeof (StyleComponent))]
    public class DebugPanelBackgroundBehaviour : DebugMonoBehaviour
    {
        private StyleComponent _styleComponent;
        public string TransparentStyleKey = "";

        [SerializeField]
        private StyleSheet _styleSheet;

        private void Awake()
        {
            _styleComponent = GetComponent<StyleComponent>();

            if (Settings.Instance.EnableBackgroundTransparency)
            {
                // Update transparent style to have the transparency set in the settings menu.
                Style style = _styleSheet.GetStyle(TransparentStyleKey);
                Color c = style.NormalColor;
                c.a = Settings.Instance.BackgroundTransparency;
                style.NormalColor = c;

                _styleComponent.StyleKey = TransparentStyleKey;
            }
        }
    }
}
