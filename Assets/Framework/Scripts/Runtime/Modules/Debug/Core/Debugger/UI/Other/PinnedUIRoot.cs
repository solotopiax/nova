/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PinnedUIRoot.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;
    using UnityEngine.UI;

    public class PinnedUIRoot : DebugMonoBehaviourEx
    {
        [RequiredField] public Canvas Canvas;

        [RequiredField] public RectTransform Container;

        [RequiredField] public DockConsoleController DockConsoleController;

        [RequiredField] public GameObject Options;

        [RequiredField] public FlowLayoutGroup OptionsLayoutGroup;

        [RequiredField] public GameObject Profiler;

        [RequiredField] public HandleManager ProfilerHandleManager;

        [RequiredField] public VerticalLayoutGroup ProfilerVerticalLayoutGroup;
    }
}
