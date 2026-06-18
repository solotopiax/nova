/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIManager.Visitors.cs
 * author:    taoye
 * created:   2026/02/27
 * descrip:   UI 管理器 - 成员变量（字段、属性）
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Runtime
{
    internal sealed partial class UIManager : UIManagerBase
    {
        /// <summary>
        /// 安全区域数据提供者，由 Initialize 从 config 读取，为 null 时使用默认实现。
        /// </summary>
        private ISafeAreaProvider m_SafeAreaProvider;

        /// <summary>
        /// 资源管理器，在 Initialize 中获取并缓存，供 DataReceiver 委托使用。
        /// </summary>
        private IAssetManager m_AssetManager;

        /// <summary>
        /// 预制体管理器，在 Initialize 中获取并缓存，供视图同步加载和销毁使用。
        /// </summary>
        private IPrefabManager m_PrefabManager;

        /// <summary>
        /// UI 注册表单元设置列表（由 Initialize 写入）。
        /// </summary>
        private List<UIUnitSetting> m_UnitSettings;

        /// <summary>
        /// UI 视图注册表，Key = UIView 类名，Value = 数据行，由 LoadAsync 填充。
        /// </summary>
        private readonly Dictionary<string, IUIViewRow> m_UIViewRegistry;

        /// <summary>
        /// 视图序列编号索引字典，键为序列编号，值为视图实例，用于 O(1) 查找。
        /// </summary>
        private readonly Dictionary<int, IUIView> m_UIViewIndex;

        /// <summary>
        /// 视图分组字典，键为视图分组名称。
        /// </summary>
        private readonly Dictionary<string, UIGroup> m_UIGroups;

        /// <summary>
        /// 正在加载中的视图字典，键为序列编号，值为资源地址。
        /// </summary>
        private readonly Dictionary<int, string> m_UIViewsBeingLoaded;

        /// <summary>
        /// 加载完成后需立即释放的视图序列编号集合。
        /// </summary>
        private readonly HashSet<int> m_UIViewsToReleaseOnLoad;

        /// <summary>
        /// 待回收视图的队列，在每帧更新时统一处理。
        /// </summary>
        private readonly Queue<IUIView> m_RecycleQueue;

        /// <summary>
        /// 对象池管理器实例。
        /// </summary>
        private IObjectPoolManager m_ObjectPoolManager;

        /// <summary>
        /// 视图实例对象池，委托给框架 ObjectPool 基础设施管理。
        /// </summary>
        private IObjectPool<UIViewInstanceObject> m_InstancePool;

        /// <summary>
        /// 视图序列编号计数器，每次打开视图时自增。
        /// </summary>
        private int m_Serial;

        /// <summary>
        /// 是否已关闭，关闭后不再处理视图状态回调。
        /// </summary>
        private bool m_IsShutdown;

        /// <summary>
        /// 缓存的视图查询结果列表，避免查询方法中重复分配。
        /// </summary>
        private readonly List<IUIView> m_CachedUIViewResults;

        /// <summary>
        /// 缓存的视图分组查询结果列表，避免查询方法中重复分配。
        /// </summary>
        private readonly List<IUIGroup> m_CachedUIGroupResults;

        /// <summary>
        /// 缓存的序列编号查询结果列表，避免查询方法中重复分配。
        /// </summary>
        private readonly List<int> m_CachedSerialIDResults;

        /// <summary>
        /// 视图实例对象池自动释放可释放对象的间隔秒数（由 Initialize 写入，CreateInstancePool 消费后由对象池接管）。
        /// </summary>
        private float m_InstanceAutoReleaseInterval;
        public override float InstanceAutoReleaseInterval
        {
            get
            {
                if (m_InstancePool == null)
                {
                    return m_InstanceAutoReleaseInterval;
                }

                return m_InstancePool.AutoReleaseInterval;
            }
            set
            {
                if (m_InstancePool == null)
                {
                    Log.Warning(LogTag.UI, "视图实例对象池尚未创建，无法设置 AutoReleaseInterval。");
                    return;
                }

                m_InstancePool.AutoReleaseInterval = value;
            }
        }

        /// <summary>
        /// 视图实例对象池的容量上限（由 Initialize 写入，CreateInstancePool 消费后由对象池接管）。
        /// </summary>
        private int m_InstanceCapacity;
        public override int InstanceCapacity
        {
            get
            {
                if (m_InstancePool == null)
                {
                    return m_InstanceCapacity;
                }

                return m_InstancePool.Capacity;
            }
            set
            {
                if (m_InstancePool == null)
                {
                    Log.Warning(LogTag.UI, "视图实例对象池尚未创建，无法设置 Capacity。");
                    return;
                }

                m_InstancePool.Capacity = value;
            }
        }

        /// <summary>
        /// 视图实例对象在对象池中的过期秒数（由 Initialize 写入，CreateInstancePool 消费后由对象池接管）。
        /// </summary>
        private float m_InstanceExpireTime;
        public override float InstanceExpireTime
        {
            get
            {
                if (m_InstancePool == null)
                {
                    return m_InstanceExpireTime;
                }

                return m_InstancePool.ExpireTime;
            }
            set
            {
                if (m_InstancePool == null)
                {
                    Log.Warning(LogTag.UI, "视图实例对象池尚未创建，无法设置 ExpireTime。");
                    return;
                }

                m_InstancePool.ExpireTime = value;
            }
        }

        /// <summary>
        /// 视图实例对象池的优先级（由 Initialize 写入，CreateInstancePool 消费后由对象池接管）。
        /// </summary>
        private int m_InstancePriority;
        public override int InstancePriority
        {
            get
            {
                if (m_InstancePool == null)
                {
                    return m_InstancePriority;
                }

                return m_InstancePool.Priority;
            }
            set
            {
                if (m_InstancePool == null)
                {
                    Log.Warning(LogTag.UI, "视图实例对象池尚未创建，无法设置 Priority。");
                    return;
                }

                m_InstancePool.Priority = value;
            }
        }

        /// <summary>
        /// 每帧最多销毁的 UI 数量（回收队列每帧处理上限，由 Initialize 写入）。
        /// </summary>
        private int m_DestroyMaxNumPerFrame;
        public override int DestroyMaxNumPerFrame
        {
            get => m_DestroyMaxNumPerFrame;
            set => m_DestroyMaxNumPerFrame = value;
        }

        /// <summary>
        /// 视图分组深度换算系数（由 Initialize 写入，AddUIGroup 时透传给 UIGroup 与 UIGroupHelper）。
        /// </summary>
        private int m_GroupDepthFactor;

        /// <summary>
        /// 视图内部深度换算系数（由 Initialize 写入，AddUIGroup 时透传给 UIGroup）。
        /// </summary>
        private int m_ViewDepthFactor;

        /// <summary>
        /// 获取视图分组数量。
        /// </summary>
        public override int UIGroupCount => m_UIGroups.Count;
    }
}
