/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ISceneHandle.cs
 * author:    taoye
 * created:   2026/5/26
 * descrip:   场景句柄中性接口，不耦合任何第三方资源框架
 ***************************************************************/

using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 场景句柄中性接口（框架层抽象，不耦合任何第三方资源框架）。
    /// <para>
    /// 场景加载完成后持有此句柄；调用 UnloadAsync 卸载场景并归还引用计数。
    /// 使用完毕后必须调用 UnloadAsync，禁止直接丢弃句柄（否则引用计数泄漏）。
    /// </para>
    /// </summary>
    public interface ISceneHandle
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
        /// 异步卸载场景并释放句柄（引用计数 -1）。
        /// </summary>
        /// <returns>等待卸载完成的 UniTask。</returns>
        UniTask UnloadAsync();
    }
}
