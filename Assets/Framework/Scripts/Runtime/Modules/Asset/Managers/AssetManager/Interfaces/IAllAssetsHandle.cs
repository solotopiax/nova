/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAllAssetsHandle.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   全资源批量句柄中性接口，不耦合任何第三方资源框架
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 全资源批量句柄中性接口（框架层抽象，不耦合任何第三方资源框架）。
    /// <para>
    /// 注意：整批资源共用同一个引用计数句柄，整批同生共死。
    /// 禁止对单个资源执行局部 Release，必须等整批使用完毕后调用 Release。
    /// </para>
    /// </summary>
    public interface IAllAssetsHandle
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
        /// 释放整批句柄（引用计数 -1）。
        /// 整批同生共死，禁止对单个资源单独 Release。
        /// </summary>
        void Release();
    }

    /// <summary>
    /// 强类型全资源批量句柄，避免外部强转。
    /// <para>
    /// 注意：整批资源共用同一个引用计数句柄，整批同生共死。
    /// 禁止对单个资源执行局部 Release，必须等整批使用完毕后调用 Release。
    /// </para>
    /// </summary>
    /// <typeparam name="T">资源类型。</typeparam>
    public interface IAllAssetsHandle<T> : IAllAssetsHandle where T : UnityEngine.Object
    {
        /// <summary>
        /// 已加载的强类型资源数组（IsDone 为 false 时为 null）。
        /// </summary>
        T[] Assets { get; }
    }
}
