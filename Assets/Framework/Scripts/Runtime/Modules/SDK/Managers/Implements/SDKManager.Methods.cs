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

        /// <summary>
        /// 对单个已实例化插件执行 InitializeAsync，统一从 IConfigManager 按 RequiredConfigType 拉取 config 并注入。
        /// RequiredConfigType 为 null 的插件表示无需 config，直接传 null 进入初始化。
        /// </summary>
        /// <param name="plugin">已完成实例化的插件实例。</param>
        /// <param name="ct">由 InitializeAsync 串联的取消令牌。</param>
        /// <returns>初始化任务（失败时已捕获，不向上传播）。</returns>
        private async UniTask InitializePluginAsync(ISDKPlugin plugin, CancellationToken ct)
        {
            Type pluginType = plugin.GetType();
            Type requiredConfigType = (plugin as SDKPluginBase)?.RequiredConfigType;
            ISDKPluginConfig config = null;

            if (requiredConfigType != null)
            {
                if (m_ConfigManager == null)
                {
                    Log.Error(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 配置注入失败：IConfigManager 不可用。", pluginType.FullName));
                    return;
                }

                config = m_ConfigManager.GetSDKPluginConfig(requiredConfigType);
                if (config == null)
                {
                    Log.Warning(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 未从 IConfigManager 取到 '{1}'，该插件未启用或配置缺失，跳过初始化。", pluginType.FullName, requiredConfigType.FullName));
                    return;
                }
            }

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await plugin.InitializeAsync(config, ct);
                sw.Stop();
                Log.Debug(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 初始化成功，耗时 {1} ms。", plugin.Name, sw.ElapsedMilliseconds));
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
            }
#endif
            catch (Exception e)
            {
                sw.Stop();
                Log.Error(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 初始化异常（已隔离）：{1}", plugin.Name, e));
            }
        }

        /// <summary>
        /// 将 m_SortedPlugins 按插件自身 Priority 值分桶，相同 Priority 归入同一桶。
        /// 返回按插件自身 Priority 升序排列的桶列表，每桶包含一个或多个插件。
        /// </summary>
        /// <returns>按插件自身 Priority 升序排列的分桶列表；每个元素为同 Priority 插件的列表。</returns>
        private List<List<ISDKPlugin>> GroupByPriority()
        {
            List<List<ISDKPlugin>> buckets = new List<List<ISDKPlugin>>();
            if (m_SortedPlugins.Count == 0)
            {
                return buckets;
            }

            List<ISDKPlugin> currentBucket = new List<ISDKPlugin> { m_SortedPlugins[0] };
            int currentPriority = m_SortedPlugins[0].Priority;

            for (int i = 1; i < m_SortedPlugins.Count; i++)
            {
                ISDKPlugin plugin = m_SortedPlugins[i];
                if (plugin.Priority == currentPriority)
                {
                    currentBucket.Add(plugin);
                }
                else
                {
                    buckets.Add(currentBucket);
                    currentBucket = new List<ISDKPlugin> { plugin };
                    currentPriority = plugin.Priority;
                }
            }

            buckets.Add(currentBucket);
            return buckets;
        }

        /// <summary>
        /// 对一个 Priority 桶内的所有插件并行执行 InitializePluginAsync（UniTask.WhenAll）。
        /// 单插件失败已在 InitializePluginAsync 内隔离，此方法不再捕获。
        /// </summary>
        /// <param name="bucket">同 Priority 的插件桶。</param>
        /// <param name="ct">取消令牌。</param>
        /// <returns>桶内所有插件并行初始化完成的任务。</returns>
        private async UniTask InitializeBucketAsync(List<ISDKPlugin> bucket, CancellationToken ct)
        {
            UniTask[] tasks = new UniTask[bucket.Count];
            for (int i = 0; i < bucket.Count; i++)
            {
                tasks[i] = InitializePluginAsync(bucket[i], ct);
            }

            await UniTask.WhenAll(tasks);
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
