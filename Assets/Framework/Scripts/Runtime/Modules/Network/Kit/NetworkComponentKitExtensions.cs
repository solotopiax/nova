/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponentKitExtensions.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   NetworkComponent Kit 扩展方法，规避主框架反向依赖 Kit 程序集
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// NetworkComponent 的 Kit 扩展方法集合。
    /// 本类已下沉至框架主程序集 NovaFramework.Runtime，
    /// 通过扩展方法挂载在 NetworkComponent 上，与 NetService 同程序集，无额外依赖。
    /// </summary>
    public static class NetworkComponentKitExtensions
    {
        /// <summary>
        /// 设置 NetService 全局调试模式开关。
        /// 调试模式下跳过 AES 加解密，发送 X-Debug-Plain 头。
        /// 等同于直接调用 NetService.SetDebugMode(debugMode)。
        /// </summary>
        /// <param name="network">NetworkComponent 实例（扩展方法接收者）。</param>
        /// <param name="debugMode">是否启用调试模式。</param>
        public static void SetDebugMode(this NetworkComponent network, bool debugMode)
        {
            NetService.SetDebugMode(debugMode);
        }
    }
}
