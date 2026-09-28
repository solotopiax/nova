/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SDKManager.Methods.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   SDK 管理器 —— 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    internal sealed partial class SDKManager
    {
        /// <summary>
        /// 注册已通过 ConfigMaster.EnabledSDKs 准入的插件实例。
        /// </summary>
        private void RegisterPlugin(Type pluginType, ISDKPlugin plugin)
        {
            m_Plugins[pluginType] = plugin;
            m_SortedPlugins.Add(plugin);
        }

        /// <summary>
        /// 按插件自身 Priority 对 m_SortedPlugins 升序排序。
        /// </summary>
        private void SortPluginsByPriority()
        {
            m_SortedPlugins.Sort((a, b) => a.Priority.CompareTo(b.Priority));
        }

        /// <summary>建立全部插件的完成信号，保证并发启动时依赖者总能找到等待对象。</summary>
        private void PreparePluginInitialization()
        {
            foreach (ISDKPlugin plugin in m_SortedPlugins)
            {
                m_PluginStates[plugin] = SDKPluginInitializationState.Pending;
                m_PluginCompletionSources[plugin] = new UniTaskCompletionSource<SDKPluginInitializationState>();
            }
            m_PluginsDiscoveredTcs.TrySetResult();
        }

        /// <summary>已启用的能力提供者必须唯一；缺失的可选能力不参与等待。</summary>
        private Dictionary<ISDKPlugin, List<(ISDKPlugin provider, bool required)>> ResolveInitializationDependencies()
        {
            var graph = new Dictionary<ISDKPlugin, List<(ISDKPlugin provider, bool required)>>();
            foreach (ISDKPlugin plugin in m_SortedPlugins)
            {
                var resolved = new List<(ISDKPlugin provider, bool required)>();
                graph[plugin] = resolved;
                if (plugin is not ISDKInitializationDependencies declarations)
                    continue;

                IReadOnlyList<SDKInitializationDependency> declared;
                try { declared = declarations.InitializationDependencies; }
                catch (Exception e)
                {
                    BlockPlugin(plugin, $"读取依赖声明失败：{e.Message}");
                    continue;
                }
                if (declared == null) continue;

                foreach (SDKInitializationDependency dependency in declared)
                {
                    Type capability = dependency.CapabilityType;
                    if (capability == null || !typeof(ISDKPlugin).IsAssignableFrom(capability))
                    {
                        BlockPlugin(plugin, "依赖必须是 ISDKPlugin 能力接口");
                        continue;
                    }

                    ISDKPlugin provider = null;
                    bool ambiguous = false;
                    foreach (ISDKPlugin candidate in m_SortedPlugins)
                    {
                        if (!capability.IsInstanceOfType(candidate)) continue;
                        if (provider != null) { ambiguous = true; break; }
                        provider = candidate;
                    }

                    if (ambiguous || ReferenceEquals(provider, plugin))
                    {
                        BlockPlugin(plugin, $"依赖 {capability.Name} 有多个提供者或指向自身");
                    }
                    else if (provider == null)
                    {
                        if (dependency.Required) BlockPlugin(plugin, $"必需能力 {capability.Name} 未启用");
                    }
                    else
                    {
                        resolved.Add((provider, dependency.Required));
                    }
                }
            }

            // Kahn 检查会把环以及依赖环的插件一并标为 Blocked，其他分支仍可启动。
            var remaining = new Dictionary<ISDKPlugin, int>();
            var queue = new Queue<ISDKPlugin>();
            foreach (var pair in graph)
            {
                remaining[pair.Key] = pair.Value.Count;
                if (pair.Value.Count == 0) queue.Enqueue(pair.Key);
            }
            while (queue.Count > 0)
            {
                ISDKPlugin completed = queue.Dequeue();
                foreach (var pair in graph)
                {
                    foreach (var dependency in pair.Value)
                    {
                        if (!ReferenceEquals(dependency.provider, completed)) continue;
                        if (--remaining[pair.Key] == 0) queue.Enqueue(pair.Key);
                    }
                }
            }
            foreach (var pair in remaining)
            {
                if (pair.Value > 0) BlockPlugin(pair.Key, "初始化依赖存在循环");
            }
            return graph;
        }

        /// <summary>在必需能力失败时阻止插件，并唤醒等待该插件的其他插件。</summary>
        private void BlockPlugin(ISDKPlugin plugin, string reason)
        {
            if (m_PluginStates[plugin] != SDKPluginInitializationState.Pending) return;
            Log.Error(LogTag.SDK, "SDK 插件 '{0}' 无法初始化：{1}。", plugin.Name, reason);
            CompletePlugin(plugin, SDKPluginInitializationState.Blocked);
        }

        /// <summary>等待本插件声明的依赖后执行初始化；无依赖的插件立即并发启动。</summary>
        private async UniTask InitializeScheduledPluginAsync(
            ISDKPlugin plugin, List<(ISDKPlugin provider, bool required)> dependencies, CancellationToken ct)
        {
            if (m_PluginStates[plugin] != SDKPluginInitializationState.Pending) return;
            try
            {
                foreach (var dependency in dependencies)
                {
                    SDKPluginInitializationState providerState = await m_PluginCompletionSources[dependency.provider]
                        .Task.AttachExternalCancellation(ct);
                    if (dependency.required && providerState != SDKPluginInitializationState.Ready)
                    {
                        BlockPlugin(plugin, $"必需能力 {dependency.provider.Name} 未就绪：{providerState}");
                        return;
                    }
                }

                m_PluginStates[plugin] = SDKPluginInitializationState.Initializing;
                SDKPluginInitializationState state = await InitializePluginAsync(plugin, ct);
                if (state != SDKPluginInitializationState.Ready)
                    await CleanupFailedPluginAsync(plugin);
                CompletePlugin(plugin, state);
            }
            catch (OperationCanceledException)
            {
                await CleanupFailedPluginAsync(plugin);
                CompletePlugin(plugin, SDKPluginInitializationState.Cancelled);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.SDK, "SDK 插件 '{0}' 调度异常：{1}", plugin.Name, e);
                await CleanupFailedPluginAsync(plugin);
                CompletePlugin(plugin, SDKPluginInitializationState.Failed);
            }
        }

        private static async UniTask CleanupFailedPluginAsync(ISDKPlugin plugin)
        {
            try { await plugin.DisposeAsync(CancellationToken.None); }
            catch (Exception e) { Log.Warning(LogTag.SDK, "SDK 插件 '{0}' 失败清理异常：{1}", plugin.Name, e); }
        }

        /// <summary>只完成一次插件状态；成功时补交当前账号并释放单插件等待者。</summary>
        private void CompletePlugin(ISDKPlugin plugin, SDKPluginInitializationState state)
        {
            m_PluginStates[plugin] = state;
            if (state == SDKPluginInitializationState.Ready)
                DeliverCurrentLogin(plugin);
            m_PluginCompletionSources[plugin].TrySetResult(state);
        }

        private void CompleteUnfinishedPlugins(SDKPluginInitializationState state)
        {
            foreach (ISDKPlugin plugin in m_SortedPlugins)
            {
                if (!m_PluginCompletionSources.ContainsKey(plugin)) continue;
                if (m_PluginStates[plugin] is SDKPluginInitializationState.Pending or SDKPluginInitializationState.Initializing)
                    CompletePlugin(plugin, state);
            }
        }

        /// <summary>把当前 UID 至多一次交给本插件；接收失败时保留可重试状态。</summary>
        private void DeliverCurrentLogin(ISDKPlugin plugin)
        {
            if ((plugin is not ISDKLoginReceiver && plugin is not ISDKLoginContextReceiver) || string.IsNullOrEmpty(m_CurrentUserId) ||
                !m_PluginStates.TryGetValue(plugin, out var state) || state != SDKPluginInitializationState.Ready)
                return;
            if (m_DeliveredSessionIds.TryGetValue(plugin, out long delivered) && delivered == m_CurrentSessionId)
                return;
            try
            {
                if (plugin is ISDKLoginContextReceiver contextReceiver)
                    contextReceiver.OnSDKLogin(m_CurrentUserId, m_CurrentSessionId, m_CurrentUserProperties);
                else
                    ((ISDKLoginReceiver)plugin).OnSDKLogin(m_CurrentUserId, m_CurrentSessionId);
                m_DeliveredSessionIds[plugin] = m_CurrentSessionId;
            }
            catch (Exception e)
            {
                Log.Error(LogTag.SDK, "SDK 插件 '{0}' 同步登录 UID 失败：{1}", plugin.Name, e);
            }
        }

        /// <summary>
        /// 对单个已实例化插件执行 InitializeAsync，统一从 IConfigManager 按 RequiredConfigType 拉取 config 并注入。
        /// RequiredConfigType 为 null 的插件表示无需 config，直接传 null 进入初始化。
        /// </summary>
        /// <param name="plugin">已完成实例化的插件实例。</param>
        /// <param name="ct">由 InitializeAsync 串联的取消令牌。</param>
        /// <returns>初始化任务（失败时已捕获，不向上传播）。</returns>
        private async UniTask<SDKPluginInitializationState> InitializePluginAsync(ISDKPlugin plugin, CancellationToken ct)
        {
            Type pluginType = plugin.GetType();
            Type requiredConfigType = (plugin as SDKPluginBase)?.RequiredConfigType;
            ISDKPluginConfig config = null;

            if (requiredConfigType != null)
            {
                if (m_ConfigManager == null)
                {
                    Log.Error(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 配置注入失败：IConfigManager 不可用。", pluginType.FullName));
                    return SDKPluginInitializationState.Failed;
                }

                config = m_ConfigManager.GetSDKPluginConfig(requiredConfigType);
                if (config == null)
                {
                    Log.Warning(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 未从 IConfigManager 取到 '{1}'，该插件未启用或配置缺失，跳过初始化。", pluginType.FullName, requiredConfigType.FullName));
                    return SDKPluginInitializationState.Failed;
                }
            }

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await plugin.InitializeAsync(config, ct);
                sw.Stop();
                Log.Debug(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 初始化成功，耗时 {1} ms。", plugin.Name, sw.ElapsedMilliseconds));
                return plugin.IsAvailable ? SDKPluginInitializationState.Ready : SDKPluginInitializationState.Failed;
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                throw;
            }
#if UNITY_EDITOR
            catch (PlatformNotSupportedException e)
            {
                sw.Stop();
                Log.Warning(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 不支持在 Unity Editor 中运行，已跳过初始化：{1}", plugin.Name, e.Message));
                return SDKPluginInitializationState.Failed;
            }
#endif
            catch (Exception e)
            {
                sw.Stop();
                Log.Error(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 初始化异常（已隔离）：{1}", plugin.Name, e));
                return SDKPluginInitializationState.Failed;
            }
        }

        /// <summary>
        /// 依据 ConfigMaster.EnabledSDKs 唯一实例化已启用 SDK 插件。
        /// 构造前通过泛型基类或配置类型特性读取元数据；无静态元数据的插件不走此路径，避免误启用和构造副作用。
        /// 多个插件声明同一 ConfigType 时，只注册第一个命中的插件并记录后续冲突。
        /// </summary>
        private void InstantiateEnabledPluginsFromConfig()
        {
            if (m_ConfigManager == null)
            {
                Log.Error(LogTag.SDK, "SDK 插件实例化失败：IConfigManager 不可用。");
                return;
            }

            IReadOnlyCollection<ISDKPluginConfig> enabledConfigs = m_ConfigManager.GetAllPluginConfigs();
            if (enabledConfigs == null || enabledConfigs.Count == 0)
            {
                Log.Debug(LogTag.SDK, "SDKManager.InitializeAsync：ConfigMaster 未启用任何 SDK 配置，跳过插件实例化。");
                return;
            }

            HashSet<Type> enabledConfigTypes = new HashSet<Type>();
            foreach (ISDKPluginConfig cfg in enabledConfigs)
            {
                if (cfg != null)
                {
                    enabledConfigTypes.Add(cfg.GetType());
                }
            }

            InstantiateEnabledPlugins(EnumerateConcreteSDKPluginTypes(), enabledConfigTypes);
        }

        /// <summary>
        /// 从候选类型中实例化配置已启用的 SDK 插件。
        /// 候选集合由调用方提供，便于把程序集发现边界与配置匹配、构造逻辑分开验证。
        /// </summary>
        /// <param name="pluginTypes">已完成程序集边界过滤的插件候选类型。</param>
        /// <param name="enabledConfigTypes">ConfigMaster 当前启用的 SDK 配置类型。</param>
        private void InstantiateEnabledPlugins(IEnumerable<Type> pluginTypes, HashSet<Type> enabledConfigTypes)
        {
            HashSet<Type> coveredConfigTypes = new HashSet<Type>();
            foreach (Type pluginType in pluginTypes)
            {
                if (m_Plugins.ContainsKey(pluginType))
                {
                    continue;
                }

                if (!SDKPluginBase.TryGetRequiredConfigType(pluginType, out Type configType))
                {
                    Log.Warning(LogTag.SDK, Txt.Format("SDK 插件未通过 PluginBase<TConfig> 或 SDKPluginConfigTypeAttribute 静态声明配置类型，已跳过且不会构造：{0}", pluginType.FullName));
                    continue;
                }

                if (!enabledConfigTypes.Contains(configType))
                {
                    continue;
                }

                if (coveredConfigTypes.Contains(configType))
                {
                    Log.Warning(LogTag.SDK, Txt.Format("SDK 插件配置类型重复，已跳过后续插件：Config={0}, Plugin={1}", configType.FullName, pluginType.FullName));
                    continue;
                }

                ISDKPlugin plugin;
                try
                {
                    plugin = (ISDKPlugin)Activator.CreateInstance(pluginType);
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.SDK, Txt.Format("SDK 插件实例化失败 '{0}'：{1}", pluginType.FullName, e.Message));
                    continue;
                }

                Type instanceConfigType = (plugin as SDKPluginBase)?.RequiredConfigType;
                if (instanceConfigType != configType)
                {
                    Log.Error(LogTag.SDK, Txt.Format(
                        "SDK 插件静态配置声明与实例声明不一致，已跳过：Plugin={0}, Static={1}, Instance={2}",
                        pluginType.FullName,
                        configType.FullName,
                        instanceConfigType?.FullName ?? "<null>"));
                    continue;
                }

                RegisterPlugin(pluginType, plugin);
                coveredConfigTypes.Add(configType);
                Log.Debug(LogTag.SDK, Txt.Format("SDK 插件按 ConfigMaster 启用实例化：{0}", pluginType.FullName));
            }
        }

        /// <summary>
        /// 枚举当前已加载程序集中所有可实例化的 ISDKPlugin 实现类型（非抽象、非接口、含无参构造）。
        /// 测试程序集中的插件仅用于验证，不属于运行时候选，因此在读取类型前统一排除。
        /// 运行时反射扫描，供 InstantiateEnabledPluginsFromConfig 按启用配置实例化使用。
        /// 单个程序集类型加载异常被隔离，不影响其余程序集扫描。
        /// </summary>
        /// <returns>可实例化的 ISDKPlugin 具体类型集合。</returns>
        private static List<Type> EnumerateConcreteSDKPluginTypes()
        {
            List<Type> result = new List<Type>();
            Type pluginInterface = typeof(ISDKPlugin);

            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
#if UNITY_EDITOR
                // 测试程序集只会参与 Editor Play；Player 不解析程序集引用，避免 HybridCLR 依赖解析影响插件发现。
                if (IsTestAssembly(assembly))
                {
                    continue;
                }
#endif

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException e)
                {
                    types = e.Types;
                }
                catch (Exception)
                {
                    continue;
                }

                if (types == null)
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    if (type == null || type.IsAbstract || type.IsInterface)
                    {
                        continue;
                    }
                    if (pluginInterface.IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) != null)
                    {
                        result.Add(type);
                    }
                }
            }

            return result;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 判断程序集是否为 Unity 测试程序集，避免 Editor Play 时把测试夹具当作真实 SDK 插件。
        /// 仅检查引用程序集名称，不在 Runtime 建立对 NUnit 或 Test Runner 类型的编译依赖。
        /// </summary>
        /// <param name="assembly">待判断的已加载程序集。</param>
        /// <returns>引用 NUnit 或 Unity Test Runner 时返回 true。</returns>
        private static bool IsTestAssembly(System.Reflection.Assembly assembly)
        {
            foreach (System.Reflection.AssemblyName referencedAssembly in assembly.GetReferencedAssemblies())
            {
                string name = referencedAssembly.Name;
                if (string.Equals(name, "nunit.framework", StringComparison.Ordinal) ||
                    string.Equals(name, "UnityEngine.TestRunner", StringComparison.Ordinal) ||
                    string.Equals(name, "UnityEditor.TestRunner", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
#endif

    }
}
