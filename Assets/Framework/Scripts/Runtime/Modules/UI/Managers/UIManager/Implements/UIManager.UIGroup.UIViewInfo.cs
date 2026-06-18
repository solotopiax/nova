/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManager.UIGroup.UIViewInfo.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 管理器 - 视图分组视图信息
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    internal sealed partial class UIManager : UIManagerBase
    {
        private sealed partial class UIGroup : IUIGroup
        {
            /// <summary>
            /// 视图分组视图信息。
            /// </summary>
            private sealed class UIViewInfo : IReference
            {
                /// <summary>
                /// 视图实例。
                /// </summary>
                private IUIView m_UIView;
                public IUIView UIView => m_UIView;

                /// <summary>
                /// 视图是否处于暂停状态。
                /// </summary>
                private bool m_Paused;
                public bool Paused
                {
                    get => m_Paused;
                    set => m_Paused = value;
                }

                /// <summary>
                /// 视图是否处于被遮挡状态。
                /// </summary>
                private bool m_Covered;
                public bool Covered
                {
                    get => m_Covered;
                    set => m_Covered = value;
                }

                /// <summary>
                /// 初始化视图分组视图信息的新实例。
                /// </summary>
                public UIViewInfo()
                {
                    m_UIView = null;
                    m_Paused = false;
                    m_Covered = false;
                }

                /// <summary>
                /// 创建视图分组视图信息。
                /// </summary>
                /// <param name="uiView">视图。</param>
                /// <returns>视图分组视图信息。</returns>
                public static UIViewInfo Create(IUIView uiView)
                {
                    if (uiView == null)
                    {
                        throw new ArgumentNullException(nameof(uiView), "视图无效。");
                    }

                    UIViewInfo info = ReferencePool.Get<UIViewInfo>();
                    info.m_UIView = uiView;
                    info.m_Paused = true;
                    info.m_Covered = true;
                    return info;
                }

                /// <summary>
                /// 清理视图分组视图信息。
                /// </summary>
                public void Clear()
                {
                    m_UIView = null;
                    m_Paused = false;
                    m_Covered = false;
                }
            }
        }
    }
}
