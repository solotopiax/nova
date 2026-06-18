/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableManager.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   表格管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 表格管理器。
    /// </summary>
    internal sealed partial class TableManager : TableManagerBase
    {
        /// <summary>
        /// 构造方法。
        /// </summary>
        public TableManager()
        {
        }

        /// <summary>
        /// 初始化表格管理器。
        /// </summary>
        /// <param name="config">表格管理器配置。</param>
        public override void Initialize(TableManagerConfig config)
        {
            m_UnitSettings = config.UnitSettings ?? new List<TableUnitSetting>();
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
            m_Tables.Clear();
            m_DataCache = new LubanDataCache();
        }

        /// <summary>
        /// 管理器轮询。
        /// </summary>
        public override void Update()
        {
        }

        /// <summary>
        /// 关闭并清理管理器。
        /// </summary>
        public override void Shutdown()
        {
            m_Tables.Clear();
            m_DataCache?.Clear();
            m_DataCache = null;
            m_UnitSettings = null;
        }

        /// <summary>
        /// 异步加载所有表格数据。
        /// Phase 1：并行加载所有 AB 资源并将数据拆分缓存到 m_DataCache。
        /// Phase 2：反射构造 TableTables，提取所有 ITable 到 m_Tables。
        /// </summary>
        /// <returns>是否全部加载并构建成功。</returns>
        public override async UniTask<bool> LoadTablesAsync()
        {
            IAssetManager am = m_AssetManager;
            DataReceiver.LoadAssetAsyncFunc loadFunc = async (assetLocation) =>
            {
                IAssetHandle<UnityEngine.TextAsset> handle = await am.LoadAsync<UnityEngine.TextAsset>(assetLocation);
                UnityEngine.TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };

            List<UniTask<bool>> tasks = new List<UniTask<bool>>(m_UnitSettings.Count);
            for (int i = 0; i < m_UnitSettings.Count; i++)
            {
                TableUnitSetting unit = m_UnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }
                string location = unit.AssetLocation;
                DataReceiver.ReleaseAssetAction releaseFunc = _ => { };
                tasks.Add(InternalLoadUnitAsync(unit, loadFunc, releaseFunc));
            }

            if (tasks.Count > 0)
            {
                bool[] results = await UniTask.WhenAll(tasks);
                for (int i = 0; i < results.Length; i++)
                {
                    if (!results[i])
                    {
                        return false;
                    }
                }
            }

            return BuildTablesFromCache();
        }

        /// <summary>
        /// 同步加载所有表格数据。
        /// Phase 1：遍历 m_UnitSettings，对每个有效 unit 同步加载 JSON 数据到 m_DataCache。
        /// Phase 2：反射构造 TableTables，提取所有 ITable 到 m_Tables。
        /// </summary>
        /// <returns>是否全部加载并构建成功。</returns>
        public override bool LoadTablesSync()
        {
            DataReceiver.LoadAssetSyncFunc syncLoadFunc = (assetLocation) =>
            {
                IAssetHandle<TextAsset> handle = m_AssetManager.LoadSync<TextAsset>(assetLocation);
                TextAsset asset = handle.Asset;
                handle.Release();
                return asset;
            };

            for (int i = 0; i < m_UnitSettings.Count; i++)
            {
                TableUnitSetting unit = m_UnitSettings[i];
                if (string.IsNullOrEmpty(unit.AssetLocation))
                {
                    continue;
                }
                string location = unit.AssetLocation;
                DataReceiver.ReleaseAssetAction releaseFunc = _ => { };
                if (!InternalLoadUnitSync(unit, syncLoadFunc, releaseFunc))
                {
                    return false;
                }
            }

            return BuildTablesFromCache();
        }

        /// <summary>
        /// 是否包含指定类型的表格。
        /// </summary>
        /// <typeparam name="T">表格类型。</typeparam>
        /// <returns>是否存在。</returns>
        public override bool HasTable<T>()
        {
            return m_Tables.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 是否包含指定类型的表格。
        /// </summary>
        /// <param name="type">表格类型。</param>
        /// <returns>是否存在。</returns>
        public override bool HasTable(Type type)
        {
            return type != null && m_Tables.ContainsKey(type);
        }

        /// <summary>
        /// 获取指定类型的表格。
        /// </summary>
        /// <typeparam name="T">表格类型。</typeparam>
        /// <returns>表格实例，不存在时返回 null。</returns>
        public override T GetTable<T>()
        {
            return m_Tables.TryGetValue(typeof(T), out ITable table) ? table as T : null;
        }

        /// <summary>
        /// 获取指定类型的表格。
        /// </summary>
        /// <param name="type">表格类型。</param>
        /// <returns>表格实例，不存在时返回 null。</returns>
        public override object GetTable(Type type)
        {
            if (type == null)
            {
                return null;
            }
            return m_Tables.TryGetValue(type, out ITable table) ? table : null;
        }
    }
}
