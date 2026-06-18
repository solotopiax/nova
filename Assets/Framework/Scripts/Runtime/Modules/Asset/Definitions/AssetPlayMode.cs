/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetPlayMode.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   YooAsset 资源运行模式
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// YooAsset 资源运行模式。
    /// </summary>
    /// <remarks>
    /// 与 YooAsset 的初始化参数派生类型一一对应。
    /// </remarks>
    public enum AssetPlayMode : byte
    {
        /// <summary>
        /// 编辑器模拟模式。
        /// </summary>
        EditorSimulateMode = 0,

        /// <summary>
        /// 离线运行模式。
        /// </summary>
        OfflinePlayMode = 1,

        /// <summary>
        /// 联机运行模式。
        /// </summary>
        HostPlayMode = 2,

        /// <summary>
        /// WebGL 运行模式。
        /// </summary>
        WebPlayMode = 3,
    }
}
