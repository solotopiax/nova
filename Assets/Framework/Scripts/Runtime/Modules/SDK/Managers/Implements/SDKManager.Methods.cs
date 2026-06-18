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
        /// 反射实例化单条 Entry，将结果写入 m_Plugins / m_SortedPlugins。
        /// Enabled==false 或 IsMissing==true 的条目跳过实例化并记录日志。
        /// </summary>
        /// <param name="entry">来自 SDKManagerConfig.PluginEntries 的单条插件配置项。</param>
        private void InstantiateEntry(SDKPluginEntry entry)
        {
            if (!entry.Enabled)
            {
                return;
            }

            if (string.IsNullOrEmpty(entry.TypeName))
            {
                entry.IsMissing = true;
                Log.Warning(LogTag.SDK, "SDK 插件条目 TypeName 为空，已跳过实例化。");
                return;
            }

            Type pluginType = Type.GetType(entry.TypeName);
            if (pluginType == null)
            {
                entry.IsMissing = true;
                Log.Warning(LogTag.SDK, Txt.Format("SDK 插件类型未找到，已跳过实例化：{0}", entry.TypeName));
                return;
            }

            ISDKPlugin plugin;
            try
            {
                plugin = (ISDKPlugin)Activator.CreateInstance(pluginType);
            }
            catch (Exception e)
            {
                Log.Error(LogTag.SDK, Txt.Format("SDK 插件反射实例化失败 '{0}'：{1}", pluginType.FullName, e));
                return;
            }

            m_Plugins[pluginType] = plugin;
            m_SortedPlugins.Add(plugin);
        }

        /// <summary>
        /// 按 Priority 对 m_SortedPlugins 升序排序。
        /// 在所有 Entry 实例化完毕后调用一次。
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
            catch (Exception e)
            {
                sw.Stop();
                Log.Error(LogTag.SDK, Txt.Format("SDK 插件 '{0}' 初始化异常（已隔离）：{1}", plugin.Name, e));
            }
        }

        /// <summary>
        /// 将 m_SortedPlugins 按 Priority 值分桶，相同 Priority 归入同一桶。
        /// 返回按 Priority 升序排列的桶列表，每桶包含一个或多个插件。
        /// </summary>
        /// <returns>按 Priority 升序排列的分桶列表；每个元素为同 Priority 插件的列表。</returns>
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

    }
}
