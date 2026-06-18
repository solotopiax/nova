/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IDataReceiver.cs
 * author:    taoye
 * created:   2026/1/21
 * descrip:   数据接收器接口
 ***************************************************************/

using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 数据接收者接口，定义三种资源加载入口。
    /// OnParseDataAsset 和 OnReleaseDataAsset 为基类内部实现细节，不暴露在接口上。
    /// </summary>
    public interface IDataReceiver
    {
        /// <summary>
        /// 触发资源加载（fire-and-forget）。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        void ReadDataAsset(string assetLocation);

        /// <summary>
        /// 异步触发资源加载、解析与释放的完整流程。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <returns>是否加载并解析成功。</returns>
        UniTask<bool> ReadDataAssetAsync(string assetLocation);

        /// <summary>
        /// 同步触发资源加载、解析与释放的完整流程。
        /// </summary>
        /// <param name="assetLocation">Asset 地址。</param>
        /// <returns>是否加载并解析成功。</returns>
        bool ReadDataAssetSync(string assetLocation);
    }
}
