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
    /// Nova 资源加载策略。
    /// </summary>
    /// <remarks>
    /// 资源策略与平台能力解耦；底层文件系统由 AssetManager 按当前平台选择。
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
    }
}
