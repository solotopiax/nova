/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IDebugTriggerService.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
namespace NovaFramework.Runtime
{
    public interface IDebugTriggerService
    {
        bool IsEnabled { get; set; }
        bool ShowErrorNotification { get; set; }
        PinAlignment Position { get; set; }
    }
}
