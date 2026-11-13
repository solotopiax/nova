/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManager.UIViewInstanceObject.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 管理器 - 视图实例对象
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class UIManager : UIManagerBase
    {
        /// <summary>
        /// 视图实例对象，继承 ObjectBase 以接入框架对象池基础设施。
        /// </summary>
        private sealed class UIViewInstanceObject : ObjectBase
        {
            private IPrefabManager m_PrefabOwner;

            /// <summary>建立由引用池管理的视图包装。</summary>
            public UIViewInstanceObject()
            {
            }

            /// <summary>
            /// 创建视图实例对象。
            /// </summary>
            /// <param name="name">实例名称（用于对象池检索）。</param>
            /// <param name="uiViewTarget">视图目标对象。</param>
            /// <returns>视图实例对象。</returns>
            /// <param name="prefabOwner">新完成入口捕获的实例拥有者；旧入口留空并沿用原查找路径。</param>
            public static UIViewInstanceObject Create(string name, object uiViewTarget, IPrefabManager prefabOwner = null)
            {
                if (uiViewTarget == null)
                {
                    throw new ArgumentNullException(nameof(uiViewTarget), "视图实例无效。");
                }

                UIViewInstanceObject obj = ReferencePool.Get<UIViewInstanceObject>();
                obj.Initialize(name, uiViewTarget);
                obj.m_PrefabOwner = prefabOwner;
                return obj;
            }

            /// <summary>归池时断开捕获的拥有者引用。</summary>
            public override void Clear()
            {
                m_PrefabOwner = null;
                base.Clear();
            }

            /// <summary>仅在新等待入口接管池命中时补记原实例接口；不改变旧入口的默认查找行为。</summary>
            /// <param name="owner">本次明确的实例拥有者。</param>
            public void CapturePrefabOwner(IPrefabManager owner)
            {
                m_PrefabOwner ??= owner;
            }

            /// <summary>经对应实例拥有者销毁目标；旧包装仍沿用全局接口查询。</summary>
            /// <param name="isShutdown">对象池是否正在关闭。</param>
            protected internal override void Release(bool isShutdown)
            {
                GameObject go = Target as GameObject;
                if (go != null)
                {
                    (m_PrefabOwner ?? FrameworkManagersGroup.GetManager<IPrefabManager>()).Destroy(go);
                }
            }
        }
    }
}
