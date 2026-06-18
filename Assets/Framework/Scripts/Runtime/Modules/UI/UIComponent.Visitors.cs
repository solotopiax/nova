/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIComponent.Visitors.cs
 * author:    taoye
 * created:   2026/02/28
 * descrip:   UI 组件 - 字段与属性访问器
 ***************************************************************/

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// UI 组件。
    /// </summary>
    public sealed partial class UIComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前 UI 管理器类型名称。
        /// </summary>
        [Tooltip("UI 管理器实现类全名")]
        [SerializeField]
        private string m_CurUIManagerTypeName = "NovaFramework.Runtime.UIManager";
        public string CurUIManagerTypeName => m_CurUIManagerTypeName;

        /// <summary>
        /// 当前 UIGroup 辅助器类型名称。
        /// </summary>
        [Tooltip("UIGroup 辅助器实现类全名")]
        [SerializeField]
        private string m_CurUIGroupHelperTypeName = "NovaFramework.Runtime.UIGroupHelper";
        public string CurUIGroupHelperTypeName => m_CurUIGroupHelperTypeName;
        
        /// <summary>
        /// 视图实例对象池自动释放可释放对象的间隔秒数。
        /// </summary>
        [Tooltip("视图实例池自动释放间隔（秒），默认 60")]
        [SerializeField]
        private float m_InstanceAutoReleaseInterval = 60f;
        public float InstanceAutoReleaseInterval
        {
            get => m_UIManager.InstanceAutoReleaseInterval;
            set => m_UIManager.InstanceAutoReleaseInterval = m_InstanceAutoReleaseInterval = value;
        }

        /// <summary>
        /// 视图实例对象池的容量。
        /// </summary>
        [Tooltip("视图实例对象池容量上限，默认 16")]
        [SerializeField]
        private int m_InstanceCapacity = 16;
        public int InstanceCapacity
        {
            get => m_UIManager.InstanceCapacity;
            set => m_UIManager.InstanceCapacity = m_InstanceCapacity = value;
        }

        /// <summary>
        /// 视图实例对象池对象过期秒数。
        /// </summary>
        [Tooltip("视图实例过期时间（秒），默认 60")]
        [SerializeField]
        private float m_InstanceExpireTime = 60f;
        public float InstanceExpireTime
        {
            get => m_UIManager.InstanceExpireTime;
            set => m_UIManager.InstanceExpireTime = m_InstanceExpireTime = value;
        }

        /// <summary>
        /// 视图实例对象池的优先级。
        /// </summary>
        [Tooltip("视图实例对象池优先级，值越大越优先回收")]
        [SerializeField]
        private int m_InstancePriority = 0;
        public int InstancePriority
        {
            get => m_UIManager.InstancePriority;
            set => m_UIManager.InstancePriority = m_InstancePriority = value;
        }

        /// <summary>
        /// 屏幕设计分辨率（宽 x 高）。
        /// </summary>
        [Tooltip("屏幕设计分辨率（宽x高），默认 1366x768")]
        [SerializeField]
        private Vector2 m_ScreenDesignedResolution = new Vector2(1366f, 768f);
        public Vector2 ScreenDesignedResolution => m_ScreenDesignedResolution;

        /// <summary>
        /// 屏幕宽高适配比例阀值（0 偏宽适配，1 偏高适配）。
        /// </summary>
        [Tooltip("宽高适配比例，0 偏宽适配，1 偏高适配")]
        [SerializeField]
        [Range(0f, 1f)]
        private float m_ScreenWidthHeightMatchValue = 1f;
        public float ScreenWidthHeightMatchValue
        {
            get => m_ScreenWidthHeightMatchValue;
            set
            {
                m_ScreenWidthHeightMatchValue = value;
                if (m_InstanceRootCanvasScaler != null)
                {
                    m_InstanceRootCanvasScaler.matchWidthOrHeight = m_ScreenWidthHeightMatchValue;
                }
            }
        }

        /// <summary>
        /// 实例根节点上的 CanvasScaler 缓存（运行时赋值，用于应用设计分辨率与宽高适配）。
        /// </summary>
        private UnityEngine.UI.CanvasScaler m_InstanceRootCanvasScaler;
        public UnityEngine.UI.CanvasScaler InstanceRootCanvasScaler => m_InstanceRootCanvasScaler;

        /// <summary>
        /// 每帧最多销毁的 UI 数量（回收队列每帧处理上限）。
        /// </summary>
        [Tooltip("每帧最多销毁 UI 数量，默认 10")]
        [SerializeField]
        private int m_DestroyMaxNumPerFrame = 10;
        public int DestroyMaxNumPerFrame
        {
            get => m_DestroyMaxNumPerFrame;
            set
            {
                m_DestroyMaxNumPerFrame = value;
                if (m_UIManager != null)
                {
                    m_UIManager.DestroyMaxNumPerFrame = m_DestroyMaxNumPerFrame;
                }
            }
        }

        /// <summary>
        /// 视图实例挂载根节点。
        /// </summary>
        [Tooltip("UI 视图实例挂载的根节点 Transform")]
        [SerializeField]
        private Transform m_InstanceRoot = null;

        /// <summary>
        /// 视图分组深度换算系数。视图分组深度乘以此系数后赋值给 Canvas.sortingOrder，
        /// 控制不同分组之间的层级间隔。
        /// </summary>
        [Tooltip("视图分组深度换算系数（GroupDepth × 此值 = sortingOrder）")]
        [SerializeField]
        private int m_GroupDepthFactor = 1000;
        public int GroupDepthFactor => m_GroupDepthFactor;

        /// <summary>
        /// 视图内部深度换算系数。视图在分组内的深度乘以此系数后叠加到 Canvas.sortingOrder。
        /// </summary>
        [Tooltip("视图内部深度换算系数（DepthInUIGroup × 此值 = 视图层级偏移）")]
        [SerializeField]
        private int m_ViewDepthFactor = 10;
        public int ViewDepthFactor => m_ViewDepthFactor;

        /// <summary>
        /// Inspector 配置的视图分组列表。
        /// </summary>
        [Tooltip("Inspector 配置的 UI 视图分组列表")]
        [SerializeField]
        private UIGroup[] m_UIGroups = null;

        /// <summary>
        /// UI 配置设置（数据源导出路径 + 运行时 AB 路径）。
        /// </summary>
        [Tooltip("UI 配置（数据源导出路径与运行时 AB 路径）")]
        [SerializeField]
        private UISettings m_UISettings;

        /// <summary>
        /// 获取视图分组数量。
        /// </summary>
        public int UIGroupCount => m_UIManager?.UIGroupCount ?? 0;

        /// <summary>
        /// UI 管理器实例。
        /// </summary>
        private IUIManager m_UIManager;

        /// <summary>
        /// 异步加载 UI 视图注册表任务完成源。
        /// </summary>
        private UniTaskCompletionSource<bool> m_LoadTcs;

        /// <summary>
        /// 是否已完成注册表加载（成功时为 true，失败保持 false 可重试）。
        /// </summary>
        public bool IsLoadOver { get; private set; }

        /// <summary>
        /// 内部视图结果缓存列表，用于避免重复分配。
        /// </summary>
        private readonly List<IUIView> m_InternalUIViewResults = new List<IUIView>();

    }
}
