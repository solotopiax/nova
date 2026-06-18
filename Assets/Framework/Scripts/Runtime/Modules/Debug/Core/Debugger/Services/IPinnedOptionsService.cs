/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IPinnedOptionsService.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
using UnityEngine;

namespace NovaFramework.Runtime
{
    using System;

    public interface IPinnedUIService
    {
        event Action<OptionDefinition, bool> OptionPinStateChanged;
        event Action<RectTransform> OptionsCanvasCreated;

        bool IsProfilerPinned { get; set; }
        void Pin(OptionDefinition option, int order = -1);
        void Unpin(OptionDefinition option);
        void UnpinAll();
        bool HasPinned(OptionDefinition option);
    }
}
