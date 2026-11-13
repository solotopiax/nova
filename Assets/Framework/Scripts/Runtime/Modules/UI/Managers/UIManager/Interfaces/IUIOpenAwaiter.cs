/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IUIOpenAwaiter.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   可选页面打开完成能力
 ***************************************************************/

using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>管理器可选择实现的打开完成能力；旧 IUIManager 实现无需增加成员。</summary>
    public interface IUIOpenAwaiter
    {
        /// <summary>按视图类型查表并等待本次打开终态；取消只终止本次请求。</summary>
        /// <typeparam name="T">已在 UI 表注册且 Prefab 预挂的视图类型。</typeparam>
        /// <param name="userData">本次打开的业务数据。</param>
        /// <param name="cancellationToken">本次请求的取消令牌。</param>
        /// <returns>初始化、打开及分组刷新后的结果，或准确的失败/关闭终态。</returns>
        UniTask<UIViewOpenResult> OpenUIViewAwaitAsync<T>(object userData = null, CancellationToken cancellationToken = default) where T : UIView;

        /// <summary>按 UI 表 Name 等待本次打开终态，不把资源地址当作名称。</summary>
        /// <param name="uiViewName">表内精确名称。</param>
        /// <param name="userData">本次业务数据。</param>
        /// <param name="cancellationToken">本次请求取消令牌。</param>
        /// <returns>单次请求的准确终态。</returns>
        UniTask<UIViewOpenResult> OpenUIViewAwaitAsync(string uiViewName, object userData = null, CancellationToken cancellationToken = default);

        /// <summary>按资源地址唯一匹配表行后等待打开；存在歧义时返回失败。</summary>
        /// <param name="assetLocation">表内资源地址。</param>
        /// <param name="userData">本次业务数据。</param>
        /// <param name="cancellationToken">本次请求取消令牌。</param>
        /// <returns>单次请求的准确终态。</returns>
        UniTask<UIViewOpenResult> OpenUIViewByAssetAwaitAsync(string assetLocation, object userData = null, CancellationToken cancellationToken = default);
    }
}
