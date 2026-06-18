/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TriggerRoot.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.Serialization;

    public class TriggerRoot : DebugMonoBehaviourEx
    {
        [RequiredField] public Canvas Canvas;

        [RequiredField] public LongPressButton TapHoldButton;

        [RequiredField] public UnityEngine.UI.Text TapHoldFPSText;
        
        [RequiredField] public RectTransform TriggerTransform;

        [RequiredField] public ErrorNotifier ErrorNotifier;

        [RequiredField] [FormerlySerializedAs("TriggerButton")] public MultiTapButton TripleTapButton;

         [RequiredField] public UnityEngine.UI.Text TripleTapFPSText;

    }
}
