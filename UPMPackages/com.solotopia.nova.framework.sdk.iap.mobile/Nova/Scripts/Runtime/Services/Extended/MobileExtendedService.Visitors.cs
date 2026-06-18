/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MobileExtendedService.Visitors.cs
 * author:    yingzheng
 * created:   2026/5/28
 * descrip:   MobileExtendedService 字段与属性
 ***************************************************************/

using UnityEngine.Purchasing;

namespace NovaFramework.SDK.IAP.Mobile.Runtime
{
    internal sealed partial class MobileExtendedService
    {
        /// <summary>
        /// StoreController 引用，由 SetController 在 InitializeAsync 第一步注入，DetachController 在 Dispose 时清空。
        /// 所有平台调用均通过此引用完成；其他 Service 禁止直接持有或访问 Controller。
        /// </summary>
        private StoreController m_Controller;

        /// <summary>
        /// 服务容器，持有共享外部依赖与其他服务引用。
        /// </summary>
        private readonly MobileServiceHub m_Hub;

        /// <summary>
        /// Controller 是否已注入（即 SetController 已被调用且未被 DetachController 清空）。
        /// </summary>
        internal bool IsAttached => m_Controller != null;

        /// <summary>
        /// 商店级事件是否已注册，防止重复 += 导致回调重复触发。
        /// </summary>
        private bool m_StoreCallbacksRegistered;

        /// <summary>
        /// 商品级事件是否已注册，防止重复 += 导致回调重复触发。
        /// </summary>
        private bool m_ProductCallbacksRegistered;

    }
}
