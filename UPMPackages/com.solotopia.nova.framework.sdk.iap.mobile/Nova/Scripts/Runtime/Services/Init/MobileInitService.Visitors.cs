/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileInitService.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/25
 * descrip:   MobileInitService 字段与属性
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileInitService
    {
        /// <summary>
        /// 服务容器，持有共享外部依赖与其他服务引用。
        /// </summary>
        private readonly MobileServiceHub m_Hub;

        /// <summary>
        /// 商店运行时状态机，封装连接态与初始化状态。
        /// Controller 引用已迁移至 ExtendedService，此处仅保留状态跟踪。
        /// </summary>
        private MobileRuntimeContext m_RuntimeContext;

        /// <summary>
        /// 初始化完成信号，桥接 OnStoreConnected / FailInitialization 到 InitializeAsync 的 await 点。
        /// </summary>
        private UniTaskCompletionSource<bool> m_InitTcs;

        /// <summary>
        /// 待拉取的商品定义列表，InitializeAsync 阶段填充，OnStoreConnected 后触发 FetchProducts 时使用。
        /// </summary>
        private List<ProductDefinition> m_PendingProductDefs;

        /// <summary>
        /// Unity IAP 是否已成功初始化（OnStoreConnected 后为 true，Dispose 后重置为 false）。
        /// </summary>
        internal bool IsReady { get; private set; }
    }
}
