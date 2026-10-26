/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManager.Methods.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器 -- 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 构造异步表资源委托；取 TextAsset 后立即释放句柄再返回，接收器后续释放委托为空。
        /// </summary>
        /// <param name="loadFunc">输出的资源异步加载委托。</param>
        /// <param name="releaseFunc">输出的资源释放委托。</param>
        private void BuildSoundDataAsyncDelegates(out DataReceiver.LoadAssetAsyncFunc loadFunc, out DataReceiver.ReleaseAssetAction releaseFunc)
        {
            IAssetManager assetManager = m_AssetManager;
            loadFunc = async (assetLocation) =>
            {
                IAssetHandle<TextAsset> handle = await assetManager.LoadAsync<TextAsset>(assetLocation);
                TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };
            releaseFunc = _ => { };
        }

        /// <summary>
        /// 构造同步表资源委托；取 TextAsset 后立即释放句柄再返回，未复制资产字节。
        /// </summary>
        /// <param name="releaseFunc">输出的资源释放委托。</param>
        /// <param name="syncLoadFunc">输出的资源同步加载委托。</param>
        private void BuildSoundDataSyncDelegates(out DataReceiver.ReleaseAssetAction releaseFunc, out DataReceiver.LoadAssetSyncFunc syncLoadFunc)
        {
            IAssetManager assetManager = m_AssetManager;
            syncLoadFunc = (assetLocation) =>
            {
                IAssetHandle<TextAsset> handle = assetManager.LoadSync<TextAsset>(assetLocation);
                TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };
            releaseFunc = _ => { };
        }

        /// <summary>
        /// 校验声音单元设置列表是否为空，为空时输出跳过日志并返回 false。
        /// </summary>
        /// <returns>列表非空时返回 true，可继续加载；为空时返回 false。</returns>
        private bool ValidateSoundUnitsSettings()
        {
            if (m_SoundUnitsSettings == null || m_SoundUnitsSettings.Count == 0)
            {
                Log.Debug(LogTag.Sound, "声音单元设置列表为空，跳过数据加载。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取指定名称的声音组。
        /// </summary>
        /// <param name="soundGroupName">声音组名称。</param>
        /// <returns>声音组，不存在时返回 null。</returns>
        private SoundGroup GetSoundGroup(string soundGroupName)
        {
            if (string.IsNullOrEmpty(soundGroupName))
            {
                throw new ArgumentException("soundGroupName 无效。", nameof(soundGroupName));
            }

            if (m_SoundGroups.TryGetValue(soundGroupName, out SoundGroup soundGroup))
            {
                return soundGroup;
            }

            return null;
        }

        /// <summary>
        /// 反射构造 SoundTables 实例，将所有 ITable 提取到 m_SoundTableDatas。
        /// </summary>
        /// <param name="dataCache">Phase 1 写入的数据加载缓存，消费后由本方法清空。</param>
        /// <param name="fileCount">数据文件数量（用于日志）。</param>
        /// <returns>是否构建成功。</returns>
        private bool BuildSoundTablesFromCache(LubanDataCache dataCache, int fileCount)
        {
            if (dataCache.DataMap.Count == 0)
            {
                return true;
            }

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.Sound, "IConfigManager 未注册，无法加载 Sound 数据，请确认场景中存在 ConfigComponent。");
                return false;
            }
            string namespace_ = configManager.Namespace;
            Dictionary<Type, ITable> tables = LubanRuntimeData.LoadTables(
                m_DataFormat, c_SoundTablesClassName, namespace_, dataCache, LogTag.Sound, "Sound");
            if (tables == null)
            {
                return false;
            }

            foreach (var kv in tables)
            {
                m_SoundTableDatas[kv.Key] = kv.Value;
            }

            Log.Debug(LogTag.Sound, "Sound 成功加载了 {0} 个数据文件，共计 {1} 个表格数据。", fileCount, m_SoundTableDatas.Count);
            return BuildSoundRowsFromTables();
        }

        /// <summary>
        /// 将当前声音表构建为唯一名称索引；空行、空名称或重名时拒绝整个索引，不留下部分可播放数据。
        /// </summary>
        /// <returns>所有声音表及名称均有效时返回 true。</returns>
        private bool BuildSoundRowsFromTables()
        {
            m_SoundRows.Clear();
            Dictionary<string, ISoundRow> rows = new Dictionary<string, ISoundRow>(StringComparer.Ordinal);
            foreach (KeyValuePair<Type, ITable> kv in m_SoundTableDatas)
            {
                if (!(kv.Value is ITable<ISoundRow> soundTable) || soundTable.DataList == null)
                {
                    Log.Error(LogTag.Sound, "声音表 '{0}' 未实现 ITable<ISoundRow> 或 DataList 为空，已拒绝构建声音索引。", kv.Key);
                    return false;
                }

                foreach (ISoundRow row in soundTable.DataList)
                {
                    if (row == null || string.IsNullOrWhiteSpace(row.Name) || rows.ContainsKey(row.Name))
                    {
                        Log.Error(LogTag.Sound, "声音表 '{0}' 存在空行、空 Name 或重复 Name '{1}'，已拒绝构建声音索引。", kv.Key, row?.Name);
                        return false;
                    }

                    rows.Add(row.Name, row);
                }
            }

            foreach (KeyValuePair<string, ISoundRow> row in rows)
            {
                m_SoundRows.Add(row.Key, row.Value);
            }

            Log.Debug(LogTag.Sound, "BuildSoundRowsFromTables 完成，共缓存 {0} 条声音行。", m_SoundRows.Count);
            return true;
        }

        /// <summary>
        /// 校验已解析表行与声音组后创建请求；表内配置保持由行提供，参数对象只补充运行时上下文。
        /// </summary>
        /// <param name="row">按 Name 或唯一 AssetLocation 解析出的声音表行。</param>
        /// <param name="playSoundParams">调用后由 Manager 接管的表外运行时参数。</param>
        /// <returns>请求序列编号；配置无效时为 -1。</returns>
        private int PlaySoundRow(ISoundRow row, PlaySoundParams playSoundParams)
        {
            if (row == null || string.IsNullOrWhiteSpace(row.AssetLocation) || string.IsNullOrWhiteSpace(row.GroupName))
            {
                Log.Warning(LogTag.Sound, "声音行 '{0}' 缺少 AssetLocation 或 GroupName，已拒绝播放。", row?.Name);
                return RejectSoundRequest(playSoundParams);
            }

            SoundGroup soundGroup = GetSoundGroup(row.GroupName);
            if (soundGroup == null)
            {
                Log.Warning(LogTag.Sound, "声音行 '{0}' 配置的声音组 '{1}' 不存在，errorCode={2}", row.Name, row.GroupName, PlaySoundErrorCode.SoundGroupNotExist);
                return RejectSoundRequest(playSoundParams);
            }

            if (soundGroup.SoundAgentCount <= 0)
            {
                Log.Warning(LogTag.Sound, "声音组 '{0}' 没有声音代理，errorCode={1}", row.GroupName, PlaySoundErrorCode.SoundGroupHasNotEnoughAgent);
                return RejectSoundRequest(playSoundParams);
            }

            playSoundParams = playSoundParams ?? PlaySoundParams.Create();
            int serialID = ++m_Serial;
            m_SoundsLoading.Add(serialID);
            PlaySoundInfo playSoundInfo = PlaySoundInfo.Create(serialID, soundGroup, row, playSoundParams);
            LoadAndPlaySoundAsync(playSoundInfo, row.AssetLocation).Forget();
            return serialID;
        }

        /// <summary>
        /// 归还已转移所有权的参数并返回拒绝标识；拒绝请求不生成有效序列编号。
        /// </summary>
        /// <param name="playSoundParams">本次接管的参数，允许为 null。</param>
        /// <returns>固定为 -1。</returns>
        private static int RejectSoundRequest(PlaySoundParams playSoundParams)
        {
            if (playSoundParams != null)
            {
                ReferencePool.Put(playSoundParams);
            }

            return -1;
        }

        /// <summary>
        /// 异步加载并交代理播放，常规终态归还上下文及参数；放弃标记不等于取消物理加载，关闭没有代次隔离。
        /// </summary>
        /// <param name="playSoundInfo">播放声音信息。</param>
        /// <param name="assetLocation">Asset 地址。</param>
        private async UniTaskVoid LoadAndPlaySoundAsync(PlaySoundInfo playSoundInfo, string assetLocation)
        {
            IAssetHandle<UnityEngine.AudioClip> clipHandle;
            try
            {
                clipHandle = await m_AssetManager.LoadAsync<UnityEngine.AudioClip>(assetLocation);
            }
            catch (Exception e)
            {
                m_SoundsLoading.Remove(playSoundInfo.SerialID);
                Log.Error(LogTag.Sound, "加载声音资源异常：serialID={0}，{1}", playSoundInfo.SerialID, e.Message);
                ReferencePool.Put(playSoundInfo);
                return;
            }

            try
            {
                if (m_SoundsToReleaseOnLoad.Contains(playSoundInfo.SerialID))
                {
                    m_SoundsToReleaseOnLoad.Remove(playSoundInfo.SerialID);
                    m_SoundsLoading.Remove(playSoundInfo.SerialID);
                    clipHandle.Release();
                    ReferencePool.Put(playSoundInfo);
                    return;
                }

                m_SoundsLoading.Remove(playSoundInfo.SerialID);

                if (clipHandle.Asset == null)
                {
                    clipHandle.Release();
                    Log.Warning(LogTag.Sound, "声音资源加载结果为空：serialID={0}，assetLocation={1}", playSoundInfo.SerialID, assetLocation);
                    ReferencePool.Put(playSoundInfo);
                    return;
                }

                PlaySoundErrorCode? errorCode;
                SoundAgent soundAgent = playSoundInfo.SoundGroup.PlaySound(
                    playSoundInfo.SerialID, clipHandle.Asset, clipHandle, playSoundInfo.SoundRow, playSoundInfo.PlaySoundParams, out errorCode);
                if (soundAgent == null)
                {
                    m_SoundsToReleaseOnLoad.Remove(playSoundInfo.SerialID);
                    clipHandle.Release();
                    Log.Warning(LogTag.Sound, "声音组 '{0}' 播放声音 '{1}' 失败，errorCode={2}", playSoundInfo.SoundGroup.Name, assetLocation, errorCode);
                }

                ReferencePool.Put(playSoundInfo);
            }
            catch (Exception e)
            {
                clipHandle?.Release();
                Log.Error(LogTag.Sound, "处理声音资源加载结果异常：serialID={0}，{1}", playSoundInfo.SerialID, e.Message);
                ReferencePool.Put(playSoundInfo);
            }
        }
    }
}
