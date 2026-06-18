/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  CategoryGroup.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    public class CategoryGroup : DebugMonoBehaviourEx
    {
        [RequiredField] public RectTransform Container;
        [RequiredField] public Text Header;
        [RequiredField] public GameObject Background;
        [RequiredField] public Toggle SelectionToggle;

        public GameObject[] EnabledDuringSelectionMode = new GameObject[0];

        private bool _selectionModeEnabled = true;

        public bool IsSelected
        {
            get
            {
                return SelectionToggle.isOn;
            }
            set
            {
                SelectionToggle.isOn = value;

                if (SelectionToggle.graphic != null)
                {
                    SelectionToggle.graphic.CrossFadeAlpha(value ? _selectionModeEnabled ? 1.0f : 0.2f : 0f, 0, true);
                }
            }
        }

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

                for (var i = 0; i < EnabledDuringSelectionMode.Length; i++)
                {
                    EnabledDuringSelectionMode[i].SetActive(_selectionModeEnabled);
                }
            }
        }

    }
}
