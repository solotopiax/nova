/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Nova.Visitors.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   Nova框架根节点组件-访问器
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Nova 框架根节点组件。
    /// </summary>
    /// <remarks>
    /// 所有静态属性仅在主线程设置和访问。后台线程不应直接读取这些属性。
    /// </remarks>
    [DefaultExecutionOrder(-1000)]
    public sealed partial class Nova : FrameworkComponent
    {
        /// <summary>
        /// Nova 版本号。
        /// </summary>
        public const string Version = "0.5.31";

        /// <summary>
        /// Nova 自身引用。
        /// </summary>
        public static Nova Self { get; private set; }

        /// <summary>
        /// App 组件。
        /// </summary>
        public static AppComponent App { get; private set; }

        /// <summary>
        /// 资源组件。
        /// </summary>
        public static AssetComponent Asset { get; private set; }

        /// <summary>
        /// 配置组件。
        /// </summary>
        public static ConfigComponent Config { get; private set; }

        /// <summary>
        /// 预制体组件。
        /// </summary>
        public static PrefabComponent Prefab { get; private set; }

        /// <summary>
        /// 事件组件。
        /// </summary>
        public static EventComponent Event { get; private set; }

        /// <summary>
        /// 表格组件。
        /// </summary>
        public static TableComponent Table { get; private set; }

        /// <summary>
        /// 本地化组件。
        /// </summary>
        public static LocalizationComponent Localization { get; private set; }

        /// <summary>
        /// 视图组件。
        /// </summary>
        public static UIComponent UI { get; private set; }

        /// <summary>
        /// 网络组件。
        /// </summary>
        public static NetworkComponent Network { get; private set; }

        /// <summary>
        /// 流程组件。
        /// </summary>
        public static ProcedureComponent Procedure { get; private set; }

        /// <summary>
        /// 对象池组件。
        /// </summary>
        public static ObjectPoolComponent ObjectPool { get; private set; }

        /// <summary>
        /// 持久化组件。
        /// </summary>
        public static PersistComponent Persist { get; private set; }

        /// <summary>
        /// 声音组件。
        /// </summary>
        public static SoundComponent Sound { get; private set; }

        /// <summary>
        /// 振动组件。
        /// </summary>
        public static VibrateComponent Vibrate { get; private set; }

        /// <summary>
        /// SDK 组件。
        /// </summary>
        public static SDKComponent SDK { get; private set; }

        /// <summary>
        /// 调试组件。
        /// </summary>
        public static DebugComponent Debug { get; private set; }

        /// <summary>
        /// Domain Reload 时重置所有静态字段，防止 Enter Play Mode 设置关闭 Domain Reload 后产生脏数据。
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ClearStaticReferences();
        }

        /// <summary>
        /// 清空所有静态组件引用。
        /// 在 OnDestroy 时调用，防止持有已销毁对象的引用。
        /// </summary>
        private static void ClearStaticReferences()
        {
            Self = null;
            App = null;
            Asset = null;
            Config = null;
            Prefab = null;
            Event = null;
            Table = null;
            Localization = null;
            UI = null;
            Network = null;
            Procedure = null;
            ObjectPool = null;
            Persist = null;
            Sound = null;
            Vibrate = null;
            SDK = null;
            Debug = null;
        }

        /// <summary>
        /// 运行帧率。
        /// 游戏运行时每秒的最高帧数。
        /// </summary>
        [Tooltip("目标帧率，推荐 30/60，默认 60")]
        [SerializeField]
        private int m_FrameRate = 60;

        /// <summary>
        /// 获取或设置游戏运行帧率。
        /// </summary>
        public int FrameRate { get => m_FrameRate; set => Application.targetFrameRate = m_FrameRate = value; }

        /// <summary>
        /// 运行速率。
        /// 游戏运行时的最快速率。
        /// </summary>
        [Tooltip("游戏运行速率，1 为正常速度，0 为暂停")]
        [SerializeField]
        private float m_GameSpeed = 1f;

        /// <summary>
        /// 获取或设置游戏运行速率。
        /// 设置值小于 0 时自动截断为 0。
        /// </summary>
        public float GameSpeed { get => m_GameSpeed; set => Time.timeScale = m_GameSpeed = value >= 0f ? value : 0f; }

        /// <summary>
        /// 启用后台运行。
        /// 游戏挂起时系统后台是否继续运行游戏。
        /// </summary>
        [Tooltip("允许应用挂起时后台继续运行")]
        [SerializeField]
        private bool m_RunInBackground = true;

        /// <summary>
        /// 获取或设置是否启用后台运行。
        /// </summary>
        public bool RunInBackground { get => m_RunInBackground; set => Application.runInBackground = m_RunInBackground = value; }

        /// <summary>
        /// 启用屏幕常亮。
        /// 游戏运行一段时间后屏幕是否常亮以避免设备进入锁定状态。
        /// </summary>
        [Tooltip("保持屏幕常亮，防止设备自动锁屏")]
        [SerializeField]
        private bool m_NeverSleep = true;

        /// <summary>
        /// 获取或设置是否启用屏幕常亮。
        /// </summary>
        public bool NeverSleep { get => m_NeverSleep; set { m_NeverSleep = value; Screen.sleepTimeout = value ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting; } }

        /// <summary>
        /// 引用的强制检查类型。
        /// </summary>
        [Tooltip("引用计数强制检查策略，AlwaysEnable 最严格")]
        [SerializeField]
        private ReferenceStrictCheckType m_ReferenceStrictCheckType = ReferenceStrictCheckType.AlwaysEnable;

        /// <summary>
        /// 当前 Txt 助手类型名称。
        /// </summary>
        [Tooltip("文本工具助手实现类全名")]
        [SerializeField]
        private string m_CurTxtHelperTypeName;

        /// <summary>
        /// 获取当前 Txt 助手类型名称。
        /// </summary>
        public string CurTxtHelperTypeName => m_CurTxtHelperTypeName;

        /// <summary>
        /// 当前 Log 助手类型名称。
        /// </summary>
        [Tooltip("日志工具助手实现类全名")]
        [SerializeField]
        private string m_CurLogHelperTypeName;

        /// <summary>
        /// 获取当前 Log 助手类型名称。
        /// </summary>
        public string CurLogHelperTypeName => m_CurLogHelperTypeName;

        /// <summary>
        /// 当前 Ref 助手类型名称。
        /// </summary>
        [Tooltip("引用计数工具助手实现类全名")]
        [SerializeField]
        private string m_CurReferenceHelperTypeName;

        /// <summary>
        /// 获取当前引用助手类型名称。
        /// </summary>
        public string CurReferenceHelperTypeName => m_CurReferenceHelperTypeName;

        /// <summary>
        /// 获取游戏是否暂停。
        /// </summary>
        public bool IsGamePaused => m_GameSpeed <= 0f;

        /// <summary>
        /// 获取游戏是否为正常速度。
        /// </summary>
        public bool IsNormalGameSpeed => m_GameSpeed == 1f;

        /// <summary>
        /// 游戏缓存速率（用于取消暂停时游戏速率的恢复）。
        /// </summary>
        private float m_GameSpeedCache = 1f;
    }
}
