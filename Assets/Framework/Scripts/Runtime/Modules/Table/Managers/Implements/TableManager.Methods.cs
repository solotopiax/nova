/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableManager.Methods.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   Table Binding 数据加载与生成容器构建
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    internal sealed partial class TableManager : TableManagerBase
    {
        /// <summary>
        /// 异步加载一个 Binding 声明的全部原始数据文件。
        /// </summary>
        /// <param name="setting">Binding 类型与 Asset 地址映射。</param>
        /// <returns>Binding 与已加载字节存储。</returns>
        private async UniTask<LoadedBinding> LoadBindingAsync(TableLoadDescriptionSetting setting)
        {
            ILubanTableBinding binding = CreateBinding(setting);
            IReadOnlyList<string> dataFiles = ValidateDataFiles(binding, setting.ResolvedBindingTypeName);
            Dictionary<string, string> addresses = BuildAssetAddressMap(setting, dataFiles);
            var tasks = new List<UniTask<LoadedTableData>>(dataFiles.Count);
            for (int i = 0; i < dataFiles.Count; i++)
            {
                string dataFile = dataFiles[i];
                tasks.Add(LoadDataAsync(dataFile, addresses[dataFile]));
            }

            LoadedTableData[] loaded = await UniTask.WhenAll(tasks);
            return new LoadedBinding(binding, CreateStore(loaded));
        }

        /// <summary>
        /// 同步加载一个 Binding 声明的全部原始数据文件。
        /// </summary>
        /// <param name="setting">Binding 类型与 Asset 地址映射。</param>
        /// <returns>Binding 与已加载字节存储。</returns>
        private LoadedBinding LoadBindingSync(TableLoadDescriptionSetting setting)
        {
            ILubanTableBinding binding = CreateBinding(setting);
            IReadOnlyList<string> dataFiles = ValidateDataFiles(binding, setting.ResolvedBindingTypeName);
            Dictionary<string, string> addresses = BuildAssetAddressMap(setting, dataFiles);
            var loaded = new LoadedTableData[dataFiles.Count];
            for (int i = 0; i < dataFiles.Count; i++)
            {
                string dataFile = dataFiles[i];
                loaded[i] = LoadDataSync(dataFile, addresses[dataFile]);
            }
            return new LoadedBinding(binding, CreateStore(loaded));
        }

        /// <summary>
        /// 创建配置指定的生成 Binding。
        /// </summary>
        /// <param name="setting">Binding 运行时设置。</param>
        /// <returns>新建的 Binding。</returns>
        private static ILubanTableBinding CreateBinding(TableLoadDescriptionSetting setting)
        {
            if (setting == null || string.IsNullOrWhiteSpace(setting.ResolvedBindingTypeName))
            {
                throw new InvalidOperationException("Table 加载描述未解析出 Binding 类型。");
            }
            return Util.TypeCreator.Create<ILubanTableBinding>(setting.ResolvedBindingTypeName);
        }

        /// <summary>
        /// 校验 Binding 提供非空且不重复的 output_data_file 清单。
        /// </summary>
        /// <param name="binding">待校验 Binding。</param>
        /// <param name="bindingTypeName">错误消息使用的类型名。</param>
        /// <returns>可安全加载的数据文件清单。</returns>
        private static IReadOnlyList<string> ValidateDataFiles(ILubanTableBinding binding, string bindingTypeName)
        {
            IReadOnlyList<string> dataFiles = binding.DataFiles;
            if (dataFiles == null || dataFiles.Count == 0)
            {
                throw new InvalidOperationException($"Table Binding 未声明数据文件：{bindingTypeName}。");
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < dataFiles.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(dataFiles[i]) || !unique.Add(dataFiles[i]))
                {
                    throw new InvalidOperationException($"Table Binding 包含空值或重复数据文件：{bindingTypeName}。");
                }
            }
            return dataFiles;
        }

        /// <summary>
        /// 异步加载 TextAsset，并在释放句柄前复制原始字节。
        /// </summary>
        /// <param name="dataFile">Luban output_data_file。</param>
        /// <param name="assetAddress">Asset 地址。</param>
        /// <returns>逻辑文件名与独立字节副本。</returns>
        private async UniTask<LoadedTableData> LoadDataAsync(string dataFile, string assetAddress)
        {
            IAssetHandle<TextAsset> handle = await m_AssetManager.LoadAsync<TextAsset>(assetAddress);
            try
            {
                return CopyAssetBytes(dataFile, assetAddress, handle.Asset);
            }
            finally
            {
                handle.Release();
            }
        }

        /// <summary>
        /// 同步加载 TextAsset，并在释放句柄前复制原始字节。
        /// </summary>
        /// <param name="dataFile">Luban output_data_file。</param>
        /// <param name="assetAddress">Asset 地址。</param>
        /// <returns>逻辑文件名与独立字节副本。</returns>
        private LoadedTableData LoadDataSync(string dataFile, string assetAddress)
        {
            IAssetHandle<TextAsset> handle = m_AssetManager.LoadSync<TextAsset>(assetAddress);
            try
            {
                return CopyAssetBytes(dataFile, assetAddress, handle.Asset);
            }
            finally
            {
                handle.Release();
            }
        }

        /// <summary>
        /// 从 TextAsset 复制独立字节，避免资源句柄释放后继续持有资产内存。
        /// </summary>
        /// <param name="dataFile">Luban output_data_file。</param>
        /// <param name="assetAddress">Asset 地址。</param>
        /// <param name="asset">已加载 TextAsset。</param>
        /// <returns>逻辑文件名与独立字节副本。</returns>
        private static LoadedTableData CopyAssetBytes(string dataFile, string assetAddress, TextAsset asset)
        {
            if (asset == null)
            {
                throw new InvalidOperationException($"Table 数据资源为空：{assetAddress}。");
            }

            byte[] source = asset.bytes;
            var bytes = new byte[source.Length];
            Buffer.BlockCopy(source, 0, bytes, 0, source.Length);
            return new LoadedTableData(dataFile, bytes);
        }

        /// <summary>
        /// 为 Binding 声明的全部逻辑文件建立 YooAsset 地址索引。
        /// </summary>
        /// <param name="setting">当前加载描述。</param>
        /// <param name="dataFiles">Binding 声明的逻辑文件清单。</param>
        /// <returns>按 output_data_file 查询的 Asset 地址。</returns>
        private static Dictionary<string, string> BuildAssetAddressMap(
            TableLoadDescriptionSetting setting, IReadOnlyList<string> dataFiles)
        {
            var addresses = new Dictionary<string, string>(StringComparer.Ordinal);
            IReadOnlyList<TableAssetAddressSetting> assets = setting.Assets;
            if (assets != null)
            {
                for (int i = 0; i < assets.Count; i++)
                {
                    TableAssetAddressSetting asset = assets[i];
                    if (asset == null || string.IsNullOrWhiteSpace(asset.DataFile) ||
                        string.IsNullOrWhiteSpace(asset.AssetAddress) ||
                        !addresses.TryAdd(asset.DataFile, asset.AssetAddress))
                    {
                        throw new InvalidOperationException($"Table 加载描述包含空值或重复 Asset 地址映射：{setting.Name}。");
                    }
                }
            }

            for (int i = 0; i < dataFiles.Count; i++)
            {
                if (!addresses.ContainsKey(dataFiles[i]))
                {
                    throw new InvalidOperationException(
                        $"Table 加载描述缺少数据文件 '{dataFiles[i]}' 的 Asset 地址：{setting.Name}。");
                }
            }
            return addresses;
        }

        /// <summary>
        /// 把已加载数据写入按 output_data_file 查询的字节存储。
        /// </summary>
        /// <param name="loaded">全部已复制数据。</param>
        /// <returns>供生成 Binding 使用的数据存储。</returns>
        private static TableDataStore CreateStore(IReadOnlyList<LoadedTableData> loaded)
        {
            var store = new TableDataStore();
            for (int i = 0; i < loaded.Count; i++)
            {
                store.Add(loaded[i].DataFile, loaded[i].Bytes);
            }
            return store;
        }

        /// <summary>
        /// 在全部 Binding 构造成功后原子替换当前查询缓存。
        /// </summary>
        /// <param name="loadedBindings">已加载的 Binding 与数据。</param>
        /// <returns>是否注册了至少一张表。</returns>
        private bool ReplaceTables(IReadOnlyList<LoadedBinding> loadedBindings)
        {
            var containers = new List<ILubanTables>(loadedBindings.Count);
            for (int i = 0; i < loadedBindings.Count; i++)
            {
                LoadedBinding loaded = loadedBindings[i];
                ILubanTables tables = loaded.Binding.Create(loaded.Store.GetBytes);
                if (tables == null)
                {
                    throw new InvalidOperationException($"Table Binding 未创建 Tables：{loaded.Binding.GetType().FullName}。");
                }
                containers.Add(tables);
            }

            m_Tables.Clear();
            for (int i = 0; i < containers.Count; i++)
            {
                RegisterTables(containers[i]);
            }
            return m_Tables.Count > 0;
        }

        /// <summary>
        /// 保存单张表加载后的逻辑键与独立字节。
        /// </summary>
        private readonly struct LoadedTableData
        {
            /// <summary>
            /// 创建已加载数据值。
            /// </summary>
            /// <param name="dataFile">Luban output_data_file。</param>
            /// <param name="bytes">已复制的原始字节。</param>
            internal LoadedTableData(string dataFile, byte[] bytes)
            {
                DataFile = dataFile;
                Bytes = bytes;
            }

            internal string DataFile { get; }
            internal byte[] Bytes { get; }
        }

        /// <summary>
        /// 保存一个 Binding 及其全部原始数据。
        /// </summary>
        private readonly struct LoadedBinding
        {
            /// <summary>
            /// 创建待构造 Tables 的已加载 Binding。
            /// </summary>
            /// <param name="binding">生成代码提供的 Binding。</param>
            /// <param name="store">按 output_data_file 查询的原始数据。</param>
            internal LoadedBinding(ILubanTableBinding binding, TableDataStore store)
            {
                Binding = binding;
                Store = store;
            }

            internal ILubanTableBinding Binding { get; }
            internal TableDataStore Store { get; }
        }
    }
}
