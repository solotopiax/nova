/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ISafeAreaProvider.cs
 * author:    taoye
 * created:   2026/04/24
 * descrip:   设备安全区域数据提供者接口
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 设备安全区域数据提供者接口。
    /// 各平台（默认 / WebGL DYSDK / WebGL WXSDK）通过实现此接口注入平台相关逻辑，UIManager 只依赖此接口。
    /// </summary>
    public interface ISafeAreaProvider
    {
        /// <summary>
        /// 获取当前平台的安全区域原始数据。
        /// </summary>
        /// <returns>安全区域原始数据。</returns>
        SafeAreaData GetSafeArea();
    }
}
