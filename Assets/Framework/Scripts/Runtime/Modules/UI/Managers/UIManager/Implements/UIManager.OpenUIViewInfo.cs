/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManager.OpenUIViewInfo.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 管理器 - 打开视图信息
 ***************************************************************/

namespace NovaFramework.Runtime
{
    internal sealed partial class UIManager : UIManagerBase
    {
        /// <summary>
        /// 打开视图信息。
        /// 它是异步调用链的参数信封，保证调用时的意图能被完整地传递到回调时。
        /// </summary>
        private sealed class OpenUIViewInfo : IReference
        {
            /// <summary>
            /// 视图序列编号。
            /// </summary>
            private int m_SerialID;

            /// <summary>
            /// 视图所属的视图分组。
            /// </summary>
            private UIGroup m_UIGroup;

            /// <summary>
            /// 是否暂停被该视图覆盖的下层视图。
            /// </summary>
            private bool m_PauseCoveredUIView;

            /// <summary>
            /// 是否启用对象池缓存。
            /// true 表示关闭后回收到对象池等待复用，false 表示关闭后直接销毁。
            /// </summary>
            private bool m_InObjectPools;

            /// <summary>
            /// 用户自定义数据，透传至视图生命周期回调。
            /// </summary>
            private object m_UserData;

            /// <summary>
            /// 初始化打开视图信息的新实例。
            /// </summary>
            public OpenUIViewInfo()
            {
                m_SerialID = 0;
                m_UIGroup = null;
                m_PauseCoveredUIView = false;
                m_InObjectPools = true;
                m_UserData = null;
            }

            /// <summary>
            /// 获取视图序列编号。
            /// </summary>
            public int SerialID => m_SerialID;

            /// <summary>
            /// 获取视图所属的视图分组。
            /// </summary>
            public UIGroup UIGroup => m_UIGroup;

            /// <summary>
            /// 获取是否暂停被覆盖的视图。
            /// </summary>
            public bool PauseCoveredUIView => m_PauseCoveredUIView;

            /// <summary>
            /// 获取是否启用对象池缓存。
            /// </summary>
            public bool InObjectPools => m_InObjectPools;

            /// <summary>
            /// 获取用户自定义数据。
            /// </summary>
            public object UserData => m_UserData;

            /// <summary>
            /// 创建打开视图信息。
            /// </summary>
            /// <param name="serialID">视图序列编号。</param>
            /// <param name="uiGroup">视图所属的视图分组。</param>
            /// <param name="pauseCoveredUIView">是否暂停被覆盖的视图。</param>
            /// <param name="inObjectPools">是否启用对象池缓存。</param>
            /// <param name="userData">用户自定义数据。</param>
            /// <returns>打开视图信息。</returns>
            public static OpenUIViewInfo Create(int serialID, UIGroup uiGroup, bool pauseCoveredUIView, bool inObjectPools, object userData)
            {
                OpenUIViewInfo info = ReferencePool.Get<OpenUIViewInfo>();
                info.m_SerialID = serialID;
                info.m_UIGroup = uiGroup;
                info.m_PauseCoveredUIView = pauseCoveredUIView;
                info.m_InObjectPools = inObjectPools;
                info.m_UserData = userData;
                return info;
            }

            /// <summary>
            /// 清理打开视图信息。
            /// </summary>
            public void Clear()
            {
                m_SerialID = 0;
                m_UIGroup = null;
                m_PauseCoveredUIView = false;
                m_InObjectPools = true;
                m_UserData = null;
            }
        }
    }
}
