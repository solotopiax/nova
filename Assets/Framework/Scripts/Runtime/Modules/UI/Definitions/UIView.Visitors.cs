/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIView.Visitors.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 视图基类 - 字段与属性访问器
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 视图基类。
    /// </summary>
    public abstract partial class UIView : MonoBehaviour, IUIView
    {
        /// <summary>
        /// 视图资源地址（格式由业务层约定，框架侧透传）。
        /// </summary>
        private string m_AssetLocation;
        public string AssetLocation => m_AssetLocation;

        /// <summary>
        /// 获取或设置视图名称（即 GameObject 名称）。
        /// </summary>
        public string Name
        {
            get => gameObject.name;
            set => gameObject.name = value;
        }

        /// <summary>
        /// 视图序列编号（由框架分配，回收时重置为 0）。
        /// </summary>
        private int m_SerialID;
        public int SerialID => m_SerialID;

        /// <summary>
        /// 视图所属的视图分组。
        /// </summary>
        private IUIGroup m_UIGroup;
        public IUIGroup UIGroup => m_UIGroup;

        /// <summary>
        /// 视图在所属视图分组中的深度（由框架在 OnDepthChanged 时更新）。
        /// </summary>
        private int m_DepthInUIGroup;
        public int DepthInUIGroup => m_DepthInUIGroup;

        /// <summary>
        /// 是否暂停被当前视图覆盖的下层视图。
        /// </summary>
        private bool m_PauseCoveredUIView;
        public bool PauseCoveredUIView => m_PauseCoveredUIView;

        /// <summary>
        /// 是否启用对象池缓存。
        /// true 表示关闭后回收到对象池等待复用，false 表示关闭后直接销毁。
        /// </summary>
        private bool m_InObjectPools = true;
        public bool InObjectPools => m_InObjectPools;

        /// <summary>
        /// 视图实例（即当前 GameObject）。
        /// </summary>
        public object Target => gameObject;

        /// <summary>
        /// 缓存自己的 Transform 引用，避免反复调用 GetComponent。
        /// </summary>
        private Transform m_CachedTransform = null;
        public Transform CachedTransform => m_CachedTransform;
        
        /// <summary>
        /// 视图当前是否可用（已完成初始化且未被关闭）。
        /// </summary>
        private bool m_Available = false;
        public bool Available => m_Available;

        /// <summary>
        /// 视图当前是否可见。
        /// 读取时同时要求 Available 为 true；
        /// 写入时若视图不可用或值未变则忽略。
        /// </summary>
        private bool m_Visible = false;
        public bool Visible
        {
            get => m_Available && m_Visible;
            set
            {
                if (!m_Available)
                {
                    Log.Warning(LogTag.UI, "视图 '{0}' 不可用，无法设置可见性。", Name);
                    return;
                }

                if (m_Visible == value)
                {
                    return;
                }

                m_Visible = value;
                InternalSetVisible(value);
            }
        }

        /// <summary>
        /// 视图 GameObject 在打开前所处的原始 Layer，关闭时用于还原。
        /// </summary>
        private int m_OriginalLayer = 0;

        /// <summary>
        /// Canvas 初始 sortingOrder（OnInit 时记录），用于深度增量计算。
        /// </summary>
        public int OriginalDepth { get; private set; }

        /// <summary>
        /// 当前 Canvas 的 sortingOrder（即实际渲染深度）。
        /// </summary>
        public int Depth => m_CachedCanvas.sortingOrder;

        /// <summary>
        /// 全局主字体，OnInit 时应用到所有子 Text 组件。
        /// </summary>
        private static Font s_MainFont = null;

        /// <summary>
        /// 缓存的 Canvas 组件引用。
        /// </summary>
        private Canvas m_CachedCanvas = null;

        /// <summary>
        /// 遍历子 Canvas 的临时缓存列表，避免重复分配。
        /// </summary>
        private List<Canvas> m_CachedCanvasContainer = new List<Canvas>();

    }
}
