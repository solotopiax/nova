/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryProtectionManager.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   可选核心包在既有管理器集合中的生命周期接入
 ***************************************************************/

using UnityEngine;
using UnityEngine.Scripting;

namespace NovaFramework.Runtime
{
    /// <summary>包内管理器标识，仅用于检测是否已经注册。</summary>
    internal interface IMemoryProtectionManager { }

    /// <summary>依托现有框架管理器生命周期，无需场景组件或主框架反向引用。</summary>
    internal abstract class MemoryProtectionManagerBase : FrameworkManager, IMemoryProtectionManager
    {
        /// <summary>大于 SDK 的 16，使逆序关闭时先提交防护事件再关闭埋点插件。</summary>
        public override int Priority => 20;
    }

    /// <summary>安装包后自动注册，初始化与轮询均由本包拥有。</summary>
    [Preserve]
    internal sealed class MemoryProtectionManager : MemoryProtectionManagerBase
    {
        /// <summary>每次进入运行态重新登记；兼容关闭 Domain Reload 后再次 Play。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            if (FrameworkManagersGroup.GetManager<IMemoryProtectionManager>() == null)
                _ = new MemoryProtectionManager();
        }

        /// <summary>配置加载后驱动维护、分帧轮换与埋点队列。</summary>
        public override void Update() => MemoryProtection.Update();

        /// <summary>框架关闭时先清理字段及队列，再由管理器集合继续关闭 SDK。</summary>
        public override void Shutdown() => MemoryProtection.Shutdown();
    }
}
