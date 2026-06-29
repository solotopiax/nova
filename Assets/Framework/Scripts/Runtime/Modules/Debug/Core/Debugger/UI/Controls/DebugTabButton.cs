/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugTabButton.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    public class DebugTabButton : DebugMonoBehaviourEx, IPointerEnterHandler, IPointerExitHandler
    {
        private bool _isActive;
        private bool _isHovering;

        [RequiredField] public Graphic BackgroundGraphic;

        [RequiredField] public Button Button;

        [RequiredField] public Graphic BorderGraphic;

        [RequiredField] public RectTransform ExtraContentContainer;

        [RequiredField] public Graphic IconGraphic;

        [RequiredField] public Text TitleText;

        public bool IsActive
        {
            get { return _isActive; }
            set
            {
                _isActive = value;
                RefreshVisualState();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            Button.transition = Selectable.Transition.None;
            RefreshVisualState();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            RefreshVisualState();
        }

        public void SetIcon(Sprite sprite)
        {
            var iconImage = IconGraphic as Image;
            if (iconImage == null)
            {
                return;
            }

            iconImage.sprite = sprite;
            iconImage.enabled = sprite != null;
            RefreshVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovering = true;
            RefreshVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovering = false;
            RefreshVisualState();
        }

        public void RefreshVisualState()
        {
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            var textColor = GetTextColor();
            var iconColor = GetIconColor();
            var borderColor = _isActive ? DebuggerUIColor.SideBarTab.ActiveBorder : DebuggerUIColor.SideBarTab.InactiveBorder;
            var backgroundColor = _isActive ? DebuggerUIColor.SideBarTab.ActiveBackground : DebuggerUIColor.SideBarTab.InactiveBackground;

            BackgroundGraphic.color = backgroundColor;
            BorderGraphic.color = borderColor;
            IconGraphic.color = iconColor;
            TitleText.color = textColor;

            var colors = Button.colors;
            colors.normalColor = iconColor;
            colors.highlightedColor = iconColor;
            colors.pressedColor = iconColor;
            colors.selectedColor = iconColor;
            colors.disabledColor = iconColor;
            colors.colorMultiplier = 1f;
            Button.colors = colors;
        }

        private Color GetTextColor()
        {
            if (_isActive)
            {
                return DebuggerUIColor.SideBarTab.ActiveText;
            }

            return _isHovering ? DebuggerUIColor.SideBarTab.HoverText : DebuggerUIColor.SideBarTab.InactiveText;
        }

        private Color GetIconColor()
        {
            if (_isActive)
            {
                return DebuggerUIColor.SideBarTab.ActiveIcon;
            }

            return _isHovering ? DebuggerUIColor.SideBarTab.HoverIcon : DebuggerUIColor.SideBarTab.InactiveIcon;
        }
    }
}