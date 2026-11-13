/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIComponent.Await.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   页面打开等待能力的可选门面
 ***************************************************************/

using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    public sealed partial class UIComponent
    {
        /// <summary>主动等待按类型打开的页面；旧 OpenUIViewAsync 的 int 签名保持不变。</summary>
        /// <typeparam name="T">表内已注册视图类型。</typeparam>
        /// <param name="userData">本次业务数据。</param>
        /// <param name="cancellationToken">仅本次请求的取消令牌。</param>
        /// <returns>页面完成打开或终止时的快照结果。</returns>
        public UniTask<UIViewOpenResult> OpenUIViewAwaitAsync<T>(object userData = null, CancellationToken cancellationToken = default) where T : UIView
            => RequireOpenAwaiter().OpenUIViewAwaitAsync<T>(userData, cancellationToken);

        /// <summary>主动等待按表内 Name 打开的页面，不改变旧调用和表格策略。</summary>
        /// <param name="uiViewName">精确表内名称。</param>
        /// <param name="userData">本次业务数据。</param>
        /// <param name="cancellationToken">本次取消令牌。</param>
        /// <returns>单次请求终态。</returns>
        public UniTask<UIViewOpenResult> OpenUIViewAwaitAsync(string uiViewName, object userData = null, CancellationToken cancellationToken = default)
            => RequireOpenAwaiter().OpenUIViewAwaitAsync(uiViewName, userData, cancellationToken);

        /// <summary>主动等待按唯一资源地址打开的页面；歧义由注册表拒绝。</summary>
        /// <param name="assetLocation">表内精确资源地址。</param>
        /// <param name="userData">本次业务数据。</param>
        /// <param name="cancellationToken">本次取消令牌。</param>
        /// <returns>单次请求终态。</returns>
        public UniTask<UIViewOpenResult> OpenUIViewByAssetAwaitAsync(string assetLocation, object userData = null, CancellationToken cancellationToken = default)
            => RequireOpenAwaiter().OpenUIViewByAssetAwaitAsync(assetLocation, userData, cancellationToken);

        /// <summary>只检测可选能力；旧自定义管理器不支持时明确拒绝，不降级轮询或偷偷改用旧入口。</summary>
        /// <returns>已明确实现的完成能力。</returns>
        private IUIOpenAwaiter RequireOpenAwaiter()
            => m_UIManager as IUIOpenAwaiter ?? throw new NotSupportedException("当前 UI 管理器没有实现可选的 IUIOpenAwaiter 能力。");
    }
}
