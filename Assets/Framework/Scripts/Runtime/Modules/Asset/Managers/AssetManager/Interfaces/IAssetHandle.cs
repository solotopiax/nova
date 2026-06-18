/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAssetHandle.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   资源句柄中性接口，不耦合任何第三方资源框架
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 资源句柄中性接口（框架层抽象，不耦合任何第三方资源框架）。
    /// 实际实现由 AssetManager 内部适配，外部仅依赖此接口。
    /// </summary>
    public interface IAssetHandle
    {
        /// <summary>
        /// 句柄是否仍然有效（未释放）。
        /// </summary>
        bool IsValid { get; }

        /// <summary>
        /// 异步加载是否已完成。
        /// </summary>
        bool IsDone { get; }

        /// <summary>
        /// 已加载的资源对象（IsDone 为 false 时为 null）。
        /// </summary>
        UnityEngine.Object AssetObject { get; }

        /// <summary>
        /// 释放句柄（引用计数 -1）。
        /// </summary>
        void Release();
    }

    /// <summary>
    /// 强类型资源句柄，避免外部强转。
    /// </summary>
    /// <typeparam name="T">资源类型。</typeparam>
    public interface IAssetHandle<T> : IAssetHandle where T : UnityEngine.Object
    {
        /// <summary>
        /// 已加载的强类型资源对象（IsDone 为 false 时为 null）。
        /// </summary>
        T Asset { get; }
    }
}
