/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateManager.Methods.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动管理器 -- 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
#if NOVA_NICEVIBRATIONS
using Lofelt.NiceVibrations;
#endif

namespace NovaFramework.Runtime
{
    internal sealed partial class VibrateManager : VibrateManagerBase
    {
        /// <summary>
        /// 构造振动数据加载所需的资源加载委托（异步路径）。
        /// </summary>
        /// <param name="loadFunc">输出的资源异步加载委托。</param>
        /// <param name="releaseFunc">输出的资源释放委托。</param>
        private void BuildVibrateDataAsyncDelegates(out DataReceiver.LoadAssetAsyncFunc loadFunc, out DataReceiver.ReleaseAssetAction releaseFunc)
        {
            IAssetManager assetManager = m_AssetManager;
            loadFunc = async (assetLocation) =>
            {
                IAssetHandle<UnityEngine.TextAsset> handle = await assetManager.LoadAsync<UnityEngine.TextAsset>(assetLocation);
                UnityEngine.TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };
            releaseFunc = _ => { };
        }

        /// <summary>
        /// 构造振动数据加载所需的资源加载委托（同步路径）。
        /// </summary>
        /// <param name="syncLoadFunc">输出的资源同步加载委托。</param>
        /// <param name="releaseFunc">输出的资源释放委托。</param>
        private void BuildVibrateDataSyncDelegates(out DataReceiver.LoadAssetSyncFunc syncLoadFunc, out DataReceiver.ReleaseAssetAction releaseFunc)
        {
            IAssetManager assetManager = m_AssetManager;
            syncLoadFunc = (assetLocation) =>
            {
                IAssetHandle<UnityEngine.TextAsset> handle = assetManager.LoadSync<UnityEngine.TextAsset>(assetLocation);
                UnityEngine.TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };
            releaseFunc = _ => { };
        }

        /// <summary>
        /// 遍历振动单元设置列表，同步加载每个有效单元的资产。
        /// </summary>
        /// <param name="units">振动单元设置列表。</param>
        /// <param name="dataCache">数据加载缓存容器。</param>
        /// <param name="syncLoadFunc">资产同步加载委托。</param>
        /// <param name="releaseFunc">资产释放委托。</param>
        private void AddSyncLoadTasks(List<VibrateUnitSetting> units, LubanDataCache dataCache, DataReceiver.LoadAssetSyncFunc syncLoadFunc, DataReceiver.ReleaseAssetAction releaseFunc)
        {
            if (units == null) return;
            for (int i = 0; i < units.Count; i++)
            {
                VibrateUnitSetting unit = units[i];
                if (unit == null || string.IsNullOrEmpty(unit.AssetLocation)) continue;
                new LubanDataReceiver(dataCache, unit, syncLoadFunc, releaseFunc).ReadDataAssetSync(unit.AssetLocation);
            }
        }

        /// <summary>
        /// 遍历振动单元设置列表，向任务列表追加每个有效单元的异步加载任务。
        /// </summary>
        /// <param name="units">振动单元设置列表。</param>
        /// <param name="dataCache">数据加载缓存容器。</param>
        /// <param name="loadFunc">资产加载委托。</param>
        /// <param name="releaseFunc">资产释放委托。</param>
        /// <param name="tasks">输出任务列表。</param>
        private void AddAsyncLoadTasks(List<VibrateUnitSetting> units, LubanDataCache dataCache, DataReceiver.LoadAssetAsyncFunc loadFunc, DataReceiver.ReleaseAssetAction releaseFunc, List<UniTask<bool>> tasks)
        {
            if (units == null) return;
            for (int i = 0; i < units.Count; i++)
            {
                VibrateUnitSetting unit = units[i];
                if (unit == null || string.IsNullOrEmpty(unit.AssetLocation)) continue;
                tasks.Add(new LubanDataReceiver(dataCache, unit, loadFunc, releaseFunc).ReadDataAssetAsync(unit.AssetLocation));
            }
        }

        /// <summary>
        /// 从数据缓存直接构建 Emphasis 和 Custom 分组缓存，Tables 实例仅作局部变量使用后即释放。
        /// </summary>
        /// <param name="dataCache">Phase 1 写入的数据加载缓存，消费后由本方法清空。</param>
        /// <returns>是否构建成功。</returns>
        private bool BuildGroupsFromCache(LubanDataCache dataCache)
        {
            if (dataCache == null)
            {
                Log.Error(LogTag.Vibrate, "数据缓存为 null，无法构建振动数据表。");
                return false;
            }

            Func<string, JArray> loader = key =>
            {
                if (dataCache.DataMap.TryGetValue(key, out object value) && value is JArray jArray)
                {
                    return jArray;
                }
                Log.Warning(LogTag.Vibrate, "数据缓存中未找到振动数据：{0}", key);
                return new JArray();
            };

            const string emphasisTablesClassName = "VibrateEmphasisTables";
            const string customTablesClassName = "VibrateCustomTables";

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.Vibrate, "IConfigManager 未注册，无法加载 Vibrate 数据，请确认场景中存在 ConfigComponent。");
                return false;
            }
            string namespace_ = configManager.Namespace;
            Dictionary<Type, ITable> emphasisTables = LubanTablesLoader.Load(emphasisTablesClassName, namespace_, loader);
            if (emphasisTables != null)
            {
                foreach (var kv in emphasisTables)
                {
                    BuildGroupCacheForTable<IVibrateEmphasisRow>(kv.Value, out m_EmphasisGroups);
                    break;
                }
            }

            Dictionary<Type, ITable> customTables = LubanTablesLoader.Load(customTablesClassName, namespace_, loader);
            if (customTables != null)
            {
                foreach (var kv in customTables)
                {
                    BuildGroupCacheForTable<IVibrateCustomRow>(kv.Value, out m_CustomGroups);
                    break;
                }
            }

            int tableCount = (emphasisTables?.Count ?? 0) + (customTables?.Count ?? 0);
            int fileCount = (m_EmphasisUnitsSettings?.Count ?? 0) + (m_CustomUnitsSettings?.Count ?? 0);
            Log.Debug(LogTag.Vibrate, "Vibrate 成功加载了 {0} 个数据文件，共计 {1} 个表格数据。", fileCount, tableCount);
            return true;
        }

        /// <summary>
        /// 从单张 ITable 构建分组缓存。
        /// 通过 ITable<T> 协变转型直接访问 DataList，完全消除反射开销。
        /// </summary>
        /// <typeparam name="T">数据行接口类型，须实现 IVibrateRow。</typeparam>
        /// <param name="table">目标 ITable 实例。</param>
        /// <param name="groups">输出：分组缓存（组名 -> 数据行列表）。</param>
        private static void BuildGroupCacheForTable<T>(ITable table, out Dictionary<string, List<T>> groups) where T : class, IVibrateRow
        {
            groups = new Dictionary<string, List<T>>();

            if (!(table is ITable<T> typedTable)) return;

            IReadOnlyList<T> dataList = typedTable.DataList;
            if (dataList == null) return;

            for (int i = 0; i < dataList.Count; i++)
            {
                T row = dataList[i];
                if (row == null) continue;
                if (!groups.TryGetValue(row.Name, out List<T> list))
                {
                    list = new List<T>();
                    groups[row.Name] = list;
                }
                list.Add(row);
            }

            foreach (List<T> group in groups.Values)
                group.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

#if NOVA_NICEVIBRATIONS
        /// <summary>
        /// 将振动类型映射为 NiceVibrations 预设类型。
        /// </summary>
        /// <param name="type">振动类型。</param>
        /// <returns>NiceVibrations 预设类型。</returns>
        private HapticPatterns.PresetType GetHapticType(VibrateType type)
        {
            switch (type)
            {
                case VibrateType.Selection: return HapticPatterns.PresetType.Selection;
                case VibrateType.Success: return HapticPatterns.PresetType.Success;
                case VibrateType.Warning: return HapticPatterns.PresetType.Warning;
                case VibrateType.Failure: return HapticPatterns.PresetType.Failure;
                case VibrateType.LightImpact: return HapticPatterns.PresetType.LightImpact;
                case VibrateType.MediumImpact: return HapticPatterns.PresetType.MediumImpact;
                case VibrateType.HeavyImpact: return HapticPatterns.PresetType.HeavyImpact;
                case VibrateType.RigidImpact: return HapticPatterns.PresetType.RigidImpact;
                case VibrateType.SoftImpact: return HapticPatterns.PresetType.SoftImpact;
                default: return HapticPatterns.PresetType.Selection;
            }
        }

        /// <summary>
        /// 延迟播放自定义持续振动。
        /// </summary>
        /// <param name="intensity">振动强度（0~1）。</param>
        /// <param name="sharpness">振动锐度（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="duration">振动持续时长（秒）。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTaskVoid PlayCustomDelayed(float intensity, float sharpness, float preDuration, float duration, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(preDuration), cancellationToken: ct);
                if (Enable)
                {
                    HapticPatterns.PlayConstant(intensity, sharpness, duration);
                }
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>
        /// 延迟播放强调振动。
        /// </summary>
        /// <param name="amplitude">振幅（0~1）。</param>
        /// <param name="frequency">频率（0~1）。</param>
        /// <param name="preDuration">前置等待时长（秒）。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTaskVoid PlayEmphasisDelayed(float amplitude, float frequency, float preDuration, CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(preDuration), cancellationToken: ct);
                if (Enable)
                {
                    HapticPatterns.PlayEmphasis(amplitude, frequency);
                }
            }
            catch (OperationCanceledException) { }
        }
#endif

        /// <summary>
        /// 链式播放自定义振动组合。
        /// </summary>
        /// <param name="name">振动分组名称。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTaskVoid PlayCustomGroupInternal(string name, CancellationToken ct)
        {
            if (m_CustomGroups == null || !m_CustomGroups.TryGetValue(name, out List<IVibrateCustomRow> steps))
            {
                Log.Warning(LogTag.Vibrate, "未找到自定义振动组合：{0}", name);
                return;
            }

            try
            {
                foreach (IVibrateCustomRow step in steps)
                {
                    if (ct.IsCancellationRequested) return;

                    if (step.PreDuration > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(step.PreDuration), cancellationToken: ct);

#if NOVA_NICEVIBRATIONS
                    HapticPatterns.PlayConstant(step.Intensity, step.Sharpness, step.Duration);
#endif

                    if (step.Duration > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(step.Duration), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Log.Warning(LogTag.Vibrate, "播放自定义振动组合异常：{0}", e.Message);
            }
        }

        /// <summary>
        /// 链式播放强调振动组合。
        /// </summary>
        /// <param name="name">振动分组名称。</param>
        /// <param name="ct">取消令牌。</param>
        private async UniTaskVoid PlayEmphasisGroupInternal(string name, CancellationToken ct)
        {
            if (m_EmphasisGroups == null || !m_EmphasisGroups.TryGetValue(name, out List<IVibrateEmphasisRow> steps))
            {
                Log.Warning(LogTag.Vibrate, "未找到强调振动组合：{0}", name);
                return;
            }

            try
            {
                foreach (IVibrateEmphasisRow step in steps)
                {
                    if (ct.IsCancellationRequested) return;

                    if (step.PreDuration > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(step.PreDuration), cancellationToken: ct);

#if NOVA_NICEVIBRATIONS
                    HapticPatterns.PlayEmphasis(step.Amplitude, step.Frequency);
#endif

                    if (step.Interval > 0f)
                        await UniTask.Delay(TimeSpan.FromSeconds(step.Interval), cancellationToken: ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                Log.Warning(LogTag.Vibrate, "播放强调振动组合异常：{0}", e.Message);
            }
        }

        /// <summary>
        /// 取消当前播放并重新创建取消令牌源。
        /// </summary>
        private void CancelAndRecreateCts()
        {
            if (m_PlayCts != null)
            {
                m_PlayCts.Cancel();
                m_PlayCts.Dispose();
            }

            m_PlayCts = new CancellationTokenSource();
        }

        /// <summary>
        /// 确保取消令牌源已创建（不取消已有令牌）。
        /// </summary>
        private void EnsureCts()
        {
            if (m_PlayCts == null)
            {
                m_PlayCts = new CancellationTokenSource();
            }
        }

        /// <summary>
        /// 校验参数是否在 0~1 范围内。
        /// </summary>
        /// <param name="value">待校验的值。</param>
        /// <param name="paramName">参数名称。</param>
        private void ValidateRange01(float value, string paramName)
        {
            if (value < 0f || value > 1f)
                throw new ArgumentOutOfRangeException(paramName, Txt.Format("{0} 必须在 0~1 范围内，当前值：{1}", paramName, value));
        }
    }
}
