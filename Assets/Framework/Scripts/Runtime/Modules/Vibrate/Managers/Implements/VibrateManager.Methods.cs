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
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
#if NOVA_NICEVIBRATIONS
using Lofelt.NiceVibrations;
#endif

namespace NovaFramework.Runtime
{
    internal sealed partial class VibrateManager : VibrateManagerBase
    {
        /// <summary>
        /// 构造异步数据委托；先取 TextAsset 并 Release 句柄再返回给解析器，后续 release 委托为空。
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
        /// 构造同步数据委托；先取 TextAsset 并 Release 句柄再返回给解析器，未复制资产数据。
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
        /// <returns>所有有效单元读取成功时返回 true，失败时不继续构建分组。</returns>
        private bool AddSyncLoadTasks(List<VibrateUnitSetting> units, LubanDataCache dataCache, DataReceiver.LoadAssetSyncFunc syncLoadFunc, DataReceiver.ReleaseAssetAction releaseFunc)
        {
            if (units == null) return true;
            for (int i = 0; i < units.Count; i++)
            {
                VibrateUnitSetting unit = units[i];
                if (unit == null || string.IsNullOrEmpty(unit.AssetLocation)) continue;
                if (!LubanRuntimeData.CreateReceiver(m_DataFormat, dataCache, unit, syncLoadFunc, releaseFunc)
                    .ReadDataAssetSync(unit.AssetLocation))
                {
                    return false;
                }
            }
            return true;
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
                tasks.Add(LubanRuntimeData.CreateReceiver(m_DataFormat, dataCache, unit, loadFunc, releaseFunc)
                    .ReadDataAssetAsync(unit.AssetLocation));
            }
        }

        /// <summary>
        /// 从本次加载缓存构建两路完整分组；任一路失败都保留此前缓存，不发布半组数据。
        /// </summary>
        /// <param name="dataCache">本次资源读取产生的数据缓存。</param>
        /// <returns>是否构建成功。</returns>
        private bool BuildGroupsFromCache(LubanDataCache dataCache)
        {
            if (dataCache == null)
            {
                Log.Error(LogTag.Vibrate, "数据缓存为 null，无法构建振动数据表。");
                return false;
            }

            const string emphasisTablesClassName = "VibrateEmphasisTables";
            const string customTablesClassName = "VibrateCustomTables";

            bool hasEmphasisUnits = HasLoadableUnits(m_EmphasisUnitsSettings);
            bool hasCustomUnits = HasLoadableUnits(m_CustomUnitsSettings);
            if (!hasEmphasisUnits && !hasCustomUnits)
            {
                return TryReplaceGroupsFromTables(Array.Empty<ITable>(), Array.Empty<ITable>());
            }

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.Vibrate, "IConfigManager 未注册，无法加载 Vibrate 数据，请确认场景中存在 ConfigComponent。");
                return false;
            }
            string namespace_ = configManager.Namespace;
            Dictionary<Type, ITable> emphasisTables = hasEmphasisUnits
                ? LubanRuntimeData.LoadTables(m_DataFormat, emphasisTablesClassName, namespace_, dataCache, LogTag.Vibrate, "Vibrate Emphasis")
                : new Dictionary<Type, ITable>();
            Dictionary<Type, ITable> customTables = hasCustomUnits
                ? LubanRuntimeData.LoadTables(m_DataFormat, customTablesClassName, namespace_, dataCache, LogTag.Vibrate, "Vibrate Custom")
                : new Dictionary<Type, ITable>();
            if (emphasisTables == null || customTables == null ||
                (hasEmphasisUnits && emphasisTables.Count == 0) ||
                (hasCustomUnits && customTables.Count == 0))
            {
                Log.Error(LogTag.Vibrate, "已配置的振动区域未能构建数据表，保留上次完整分组。");
                return false;
            }

            if (!TryReplaceGroupsFromTables(emphasisTables.Values, customTables.Values))
            {
                return false;
            }

            int tableCount = emphasisTables.Count + customTables.Count;
            int fileCount = (m_EmphasisUnitsSettings?.Count ?? 0) + (m_CustomUnitsSettings?.Count ?? 0);
            Log.Debug(LogTag.Vibrate, "Vibrate 成功加载了 {0} 个数据文件，共计 {1} 个表格数据。", fileCount, tableCount);
            return true;
        }

        /// <summary>
        /// 先构建两路候选分组，全部通过后再一次替换可播放缓存；空区域会移除其旧分组。
        /// </summary>
        /// <param name="emphasisTables">本次 Emphasis 区域的全部表。</param>
        /// <param name="customTables">本次 Custom 区域的全部表。</param>
        /// <returns>两路分组均有效且已经替换时返回 true；失败时保留两路旧缓存。</returns>
        private bool TryReplaceGroupsFromTables(IEnumerable<ITable> emphasisTables, IEnumerable<ITable> customTables)
        {
            if (!BuildGroupCacheForTables<IVibrateEmphasisRow>(emphasisTables, out var emphasisGroups) ||
                !BuildGroupCacheForTables<IVibrateCustomRow>(customTables, out var customGroups))
            {
                return false;
            }

            m_EmphasisGroups = emphasisGroups;
            m_CustomGroups = customGroups;
            return true;
        }

        /// <summary>
        /// 判断区域内是否存在实际参与资源加载的单元；与加载阶段使用相同过滤条件。
        /// </summary>
        /// <param name="units">区域单元列表，允许为空。</param>
        /// <returns>是否至少存在一个非空且配置了 AssetLocation 的单元。</returns>
        private static bool HasLoadableUnits(List<VibrateUnitSetting> units)
        {
            return units != null && units.Any(unit => unit != null && !string.IsNullOrEmpty(unit.AssetLocation));
        }

        /// <summary>
        /// 合并区域内全部表，按 Name 分组并稳定按 Order 排序；同序号保留表和行的遍历顺序。
        /// </summary>
        /// <typeparam name="T">数据行接口类型，须实现 IVibrateRow。</typeparam>
        /// <param name="tables">区域内本次生成的全部表。</param>
        /// <param name="groups">输出：分组缓存（组名 -> 数据行列表）。</param>
        /// <returns>表接口及所有非空行的 Name 有效时返回 true；失败的候选缓存不可发布。</returns>
        private static bool BuildGroupCacheForTables<T>(IEnumerable<ITable> tables, out Dictionary<string, List<T>> groups) where T : class, IVibrateRow
        {
            groups = new Dictionary<string, List<T>>();

            foreach (ITable table in tables)
            {
                if (!(table is ITable<T> typedTable))
                {
                    Log.Error(LogTag.Vibrate, "振动表未实现预期行接口：{0}。", typeof(T).Name);
                    return false;
                }

                IReadOnlyList<T> dataList = typedTable.DataList;
                if (dataList == null) continue;

                for (int i = 0; i < dataList.Count; i++)
                {
                    T row = dataList[i];
                    if (row == null) continue;
                    if (string.IsNullOrEmpty(row.Name))
                    {
                        Log.Error(LogTag.Vibrate, "振动数据行 Name 为空，无法建立可播放的分组。");
                        return false;
                    }
                    if (!groups.TryGetValue(row.Name, out List<T> list))
                    {
                        list = new List<T>();
                        groups[row.Name] = list;
                    }
                    list.Add(row);
                }
            }

            foreach (string name in groups.Keys.ToArray())
                groups[name] = groups[name].OrderBy(row => row.Order).ToList();

            return true;
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
        /// 通过小于 0/大于 1 的比较校验范围；NaN 不会被这两个比较拒绝，不是有限数校验。
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
