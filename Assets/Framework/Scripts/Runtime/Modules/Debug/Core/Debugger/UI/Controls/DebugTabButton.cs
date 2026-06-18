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
    using UnityEngine.UI;

    public class DebugTabButton : DebugMonoBehaviourEx
    {
        [RequiredField] public Behaviour ActiveToggle;

        [RequiredField] public UnityEngine.UI.Button Button;

        [RequiredField] public RectTransform ExtraContentContainer;

        [RequiredField] public StyleComponent IconStyleComponent;

        [RequiredField] public Text TitleText;

        public bool IsActive
        {
            get { return ActiveToggle.enabled; }
            set { ActiveToggle.enabled = value; }
        }
    }
}
