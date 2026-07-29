/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableManager.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   基于 Luban 生成 Binding 的 Table 管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 加载任意数量的 Luban 生成 Binding，并按表类型提供统一查询。
    /// </summary>
    internal sealed partial class TableManager : TableManagerBase
    {
        /// <summary>
        /// 初始化 Table 管理器并复制运行时加载描述。
        /// </summary>
        /// <param name="config">运行时 Binding 配置。</param>
        public override void Initialize(TableManagerConfig config)
        {
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
            m_LoadDescriptions.Clear();
            if (config?.LoadDescriptions != null)
            {
                for (int i = 0; i < config.LoadDescriptions.Count; i++)
                {
                    TableLoadDescriptionSetting setting = config.LoadDescriptions[i];
                    if (setting != null)
                    {
                        m_LoadDescriptions.Add(setting);
                    }
                }
            }
            m_Tables.Clear();
        }

        /// <summary>
        /// Table 当前没有逐帧状态。
        /// </summary>
        public override void Update()
        {
        }

        /// <summary>
        /// 清理全部已注册表、Binding 配置和运行时依赖。
        /// </summary>
        public override void Shutdown()
        {
            m_Tables.Clear();
            m_LoadDescriptions.Clear();
            m_AssetManager = null;
        }

        /// <summary>
        /// 异步预加载全部 Binding 声明的数据，再交给生成代码完成解码和 Tables 构造。
        /// </summary>
        /// <returns>是否加载或保留了至少一张表。</returns>
        public override async UniTask<bool> LoadTablesAsync()
        {
            if (m_LoadDescriptions.Count == 0)
            {
                return m_Tables.Count > 0;
            }
            if (!ValidateAssetManager())
            {
                return false;
            }

            var loadedBindings = new List<LoadedBinding>(m_LoadDescriptions.Count);
            for (int i = 0; i < m_LoadDescriptions.Count; i++)
            {
                loadedBindings.Add(await LoadBindingAsync(m_LoadDescriptions[i]));
            }
            return ReplaceTables(loadedBindings);
        }

        /// <summary>
        /// 同步加载全部 Binding 声明的数据，再交给生成代码完成解码和 Tables 构造。
        /// </summary>
        /// <returns>是否加载或保留了至少一张表。</returns>
        public override bool LoadTablesSync()
        {
            if (m_LoadDescriptions.Count == 0)
            {
                return m_Tables.Count > 0;
            }
            if (!ValidateAssetManager())
            {
                return false;
            }

            var loadedBindings = new List<LoadedBinding>(m_LoadDescriptions.Count);
            for (int i = 0; i < m_LoadDescriptions.Count; i++)
            {
                loadedBindings.Add(LoadBindingSync(m_LoadDescriptions[i]));
            }
            return ReplaceTables(loadedBindings);
        }

        /// <summary>
        /// 注册一个已经由 Luban 原生构造器创建的 Tables 容器。
        /// </summary>
        /// <param name="tables">待解析引用并注册的 Tables 容器。</param>
        /// <returns>是否注册了至少一张表。</returns>
        public override bool RegisterTables(ILubanTables tables)
        {
            if (tables == null)
            {
                Log.Error(LogTag.Table, "待注册的 ILubanTables 不能为空。");
                return false;
            }

            tables.ResolveRef();
            IReadOnlyList<ITable> generatedTables = tables.GetAllTables();
            if (generatedTables == null || generatedTables.Count == 0)
            {
                Log.Error(LogTag.Table, "ILubanTables 未提供任何生成表。");
                return false;
            }

            for (int i = 0; i < generatedTables.Count; i++)
            {
                ITable table = generatedTables[i];
                if (table == null)
                {
                    throw new InvalidOperationException("ILubanTables.GetAllTables() 包含空表实例。");
                }
                m_Tables[table.GetType()] = table;
            }
            return true;
        }

        /// <summary>
        /// 判断是否已注册指定表类型。
        /// </summary>
        /// <typeparam name="T">生成表类型。</typeparam>
        /// <returns>是否存在。</returns>
        public override bool HasTable<T>()
        {
            return m_Tables.ContainsKey(typeof(T));
        }

        /// <summary>
        /// 判断是否已注册指定表类型。
        /// </summary>
        /// <param name="type">生成表类型。</param>
        /// <returns>是否存在。</returns>
        public override bool HasTable(Type type)
        {
            return type != null && m_Tables.ContainsKey(type);
        }

        /// <summary>
        /// 获取指定生成表实例。
        /// </summary>
        /// <typeparam name="T">生成表类型。</typeparam>
        /// <returns>表实例，不存在时返回 null。</returns>
        public override T GetTable<T>()
        {
            return m_Tables.TryGetValue(typeof(T), out ITable table) ? table as T : null;
        }

        /// <summary>
        /// 按运行时类型获取生成表实例。
        /// </summary>
        /// <param name="type">生成表类型。</param>
        /// <returns>表实例，不存在时返回 null。</returns>
        public override object GetTable(Type type)
        {
            return type != null && m_Tables.TryGetValue(type, out ITable table) ? table : null;
        }

        /// <summary>
        /// 校验资源管理器可用于 Binding 数据加载。
        /// </summary>
        /// <returns>资源管理器是否存在。</returns>
        private bool ValidateAssetManager()
        {
            if (m_AssetManager != null)
            {
                return true;
            }

            Log.Error(LogTag.Table, "IAssetManager 未注册，无法加载 Table Binding 数据。");
            return false;
        }
    }
}
