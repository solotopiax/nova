/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IDeviceIdProvider.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   设备唯一标识提供者
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 设备唯一标识提供者。
    /// SDK 插件实现此接口对外暴露设备 ID，由 Nova.SDK.Get<IDeviceIdProvider>() 解析。
    /// </summary>
    public interface IDeviceIdProvider : ISDKPlugin
    {
        /// <summary>
        /// 获取设备唯一标识。
        /// </summary>
        /// <returns>设备 ID 字符串；未就绪或不可用时返回空串。</returns>
        string GetDeviceID();
    }
}
