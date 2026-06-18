/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  OptionsControlBase.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine.UI;

    public abstract class OptionsControlBase : DebugMonoBehaviourEx
    {
        private bool _selectionModeEnabled;

        [RequiredField] public Toggle SelectionModeToggle;

        public OptionDefinition Option;

        public bool SelectionModeEnabled
        {
            get { return _selectionModeEnabled; }

            set
            {
                if (value == _selectionModeEnabled)
                {
                    return;
                }

                _selectionModeEnabled = value;

                SelectionModeToggle.gameObject.SetActive(_selectionModeEnabled);

                if (SelectionModeToggle.graphic != null)
                {
                    SelectionModeToggle.graphic.CrossFadeAlpha(IsSelected ? _selectionModeEnabled ? 1.0f : 0.2f : 0f, 0,
                        true);
                }
            }
        }

        public bool IsSelected
        {
            get { return SelectionModeToggle.isOn; }
            set
            {
                SelectionModeToggle.isOn = value;

                if (SelectionModeToggle.graphic != null)
                {
                    SelectionModeToggle.graphic.CrossFadeAlpha(value ? _selectionModeEnabled ? 1.0f : 0.2f : 0f, 0, true);
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();

            IsSelected = false;
            SelectionModeToggle.gameObject.SetActive(false);
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            // Reapply selection indicator alpha (is reset when disabled / reenabled)
            if (SelectionModeToggle.graphic != null)
            {
                SelectionModeToggle.graphic.CrossFadeAlpha(IsSelected ? _selectionModeEnabled ? 1.0f : 0.2f : 0f, 0,
                    true);
            }
        }

        public virtual void Refresh() {}
    }
}
