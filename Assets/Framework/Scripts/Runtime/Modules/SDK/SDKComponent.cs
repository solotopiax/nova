/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKComponent.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   SDK 组件（FrameworkComponent 入口，持 Manager，代理 Unity 生命周期）
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// SDK 组件，SDK 系统对外入口。
    /// 持有 ISDKManager，在 Awake 通过 TypeCreator 创建 Manager，在 Start 调用 Initialize。
    /// 对外暴露 InitializeTask / Get / TryGet / GetAll / Login 等薄委托 API。
    /// OnApplicationPause / OnApplicationFocus / OnApplicationQuit 转发至 Manager，异常逐插件隔离。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class SDKComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒：通过 TypeCreator 反射创建 Manager 实例，不做其他任何业务逻辑。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            m_SDKManager = Util.TypeCreator.Create<ISDKManager>(m_CurManagerTypeName);
        }

        /// <summary>
        /// 启动：将 Inspector 序列化的 PluginEntries 传入 Manager.Initialize，完成同步插件实例化。
        /// </summary>
        private void Start()
        {
            m_SDKManager.Initialize(new SDKManagerConfig { PluginEntries = m_PluginEntries });
        }

        /// <summary>
        /// 销毁：清理 Manager 引用，让 InitializeTask 的 Lazy 包装可被 GC。
        /// </summary>
        private void OnDestroy()
        {
            m_InitializeTaskCache = null;
            m_SDKManager = null;
        }

        /// <summary>
        /// 按插件具体类型获取已初始化且可用的插件实例；不可用时抛 SDKUnavailableException。
        /// </summary>
        /// <typeparam name="TPlugin">插件具体类型，必须为 class 且实现 ISDKPlugin。</typeparam>
        /// <returns>对应的可用插件实例。</returns>
        public TPlugin Get<TPlugin>() where TPlugin : class, ISDKPlugin
        {
            return m_SDKManager.Get<TPlugin>();
        }

        /// <summary>
        /// 尝试按插件具体类型获取已初始化且可用的插件实例。
        /// </summary>
        /// <typeparam name="TPlugin">插件具体类型，必须为 class 且实现 ISDKPlugin。</typeparam>
        /// <param name="plugin">输出的插件实例；不可用时为 null。</param>
        /// <returns>插件可用返回 true，否则 false。</returns>
        public bool TryGet<TPlugin>(out TPlugin plugin) where TPlugin : class, ISDKPlugin
        {
            return m_SDKManager.TryGet<TPlugin>(out plugin);
        }

        /// <summary>
        /// 获取所有实现指定接口且当前可用的插件列表。
        /// </summary>
        /// <typeparam name="TInterface">目标插件接口类型，必须为 class 且实现 ISDKPlugin。</typeparam>
        /// <returns>可用插件实例的只读列表，按 Priority 升序；若无可用实例返回空列表。</returns>
        public IReadOnlyList<TInterface> GetAll<TInterface>() where TInterface : class, ISDKPlugin
        {
            return m_SDKManager.GetAll<TInterface>();
        }

        /// <summary>
        /// 通知用户登录，向 EventManager 发送登录事件并广播至所有 ISDKUserListener 插件。
        /// </summary>
        /// <param name="userId">已登录用户的唯一标识。</param>
        public void Login(string userId)
        {
            m_SDKManager.Login(userId);
        }
    }
}
