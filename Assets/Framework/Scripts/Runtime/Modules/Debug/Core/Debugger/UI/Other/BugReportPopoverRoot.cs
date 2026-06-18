/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BugReportPopoverRoot.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using UnityEngine;

    public class BugReportPopoverRoot : DebugMonoBehaviourEx
    {
        [RequiredField] public CanvasGroup CanvasGroup;

        [RequiredField] public RectTransform Container;
    }
}
