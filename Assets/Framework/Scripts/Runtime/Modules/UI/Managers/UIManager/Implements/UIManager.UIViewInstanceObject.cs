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
            public UIViewInstanceObject()
            {
            }

            /// <summary>
            /// 创建视图实例对象。
            /// </summary>
            /// <param name="name">实例名称（用于对象池检索）。</param>
            /// <param name="uiViewTarget">视图目标对象。</param>
            /// <returns>视图实例对象。</returns>
            public static UIViewInstanceObject Create(string name, object uiViewTarget)
            {
                if (uiViewTarget == null)
                {
                    throw new ArgumentNullException(nameof(uiViewTarget), "视图实例无效。");
                }

                UIViewInstanceObject obj = ReferencePool.Get<UIViewInstanceObject>();
                obj.Initialize(name, uiViewTarget);
                return obj;
            }

            public override void Clear()
            {
                base.Clear();
            }

            protected internal override void Release(bool isShutdown)
            {
                GameObject go = Target as GameObject;
                if (go != null)
                {
                    FrameworkManagersGroup.GetManager<IPrefabManager>().Destroy(go);
                }
            }
        }
    }
}
