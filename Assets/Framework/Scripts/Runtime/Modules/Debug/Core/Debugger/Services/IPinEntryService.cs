/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IPinEntryService.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    using System;
    using System.Collections.Generic;

    public delegate void PinEntryCompleteCallback(bool validPinEntered);

    public interface IPinEntryService
    {
        bool IsShowingKeypad { get; }

        /// <summary>
        /// Show the pin entry form.
        /// </summary>
        /// <param name="requiredPin">List of digits 0-9, length 4.</param>
        /// <param name="message">Message to display to the user on the form.</param>
        /// <param name="callback">Callback to invoke when the pin entry is complete or cancelled.</param>
        /// <param name="allowCancel">True to allow the user to cancel the form.</param>
        void ShowPinEntry(IReadOnlyList<int> requiredPin, string message, PinEntryCompleteCallback callback,
            bool allowCancel = true);
    }
}
