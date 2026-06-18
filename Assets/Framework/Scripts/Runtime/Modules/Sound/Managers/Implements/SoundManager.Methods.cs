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
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 构造声音数据加载所需的资源加载委托（异步路径）。
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
        /// 构造声音数据加载所需的资源加载委托（同步路径）。
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

            Func<string, JArray> loader = key =>
            {
                if (dataCache.DataMap.TryGetValue(key, out object value) && value is JArray jArray)
                {
                    return jArray;
                }
                Log.Warning(LogTag.Sound, "数据缓存中未找到声音数据：{0}", key);
                return new JArray();
            };

            IConfigManager configManager = FrameworkManagersGroup.GetManager<IConfigManager>();
            if (configManager == null)
            {
                Log.Error(LogTag.Sound, "IConfigManager 未注册，无法加载 Sound 数据，请确认场景中存在 ConfigComponent。");
                return false;
            }
            string namespace_ = configManager.Namespace;
            Dictionary<Type, ITable> tables = LubanTablesLoader.Load(c_SoundTablesClassName, namespace_, loader);
            if (tables == null)
            {
                return false;
            }

            foreach (var kv in tables)
            {
                m_SoundTableDatas[kv.Key] = kv.Value;
            }

            Log.Debug(LogTag.Sound, "Sound 成功加载了 {0} 个数据文件，共计 {1} 个表格数据。", fileCount, m_SoundTableDatas.Count);
            BuildSoundRowsFromTables();
            return true;
        }

        /// <summary>
        /// 将所有声音表中的数据行扁平化到 m_SoundRows，以 ISoundRow.Name 为键。
        /// </summary>
        private void BuildSoundRowsFromTables()
        {
            m_SoundRows.Clear();
            foreach (KeyValuePair<Type, ITable> kv in m_SoundTableDatas)
            {
                if (kv.Value is ITable<ISoundRow> soundTable)
                {
                    foreach (ISoundRow row in soundTable.DataList)
                    {
                        m_SoundRows[row.Name] = row;
                    }
                }
            }

            Log.Debug(LogTag.Sound, "BuildSoundRowsFromTables 完成，共缓存 {0} 条声音行。", m_SoundRows.Count);
        }

        /// <summary>
        /// 异步加载声音资源并在加载完成后播放。
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
                    playSoundInfo.SerialID, clipHandle.Asset, clipHandle, playSoundInfo.PlaySoundParams, out errorCode);
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
