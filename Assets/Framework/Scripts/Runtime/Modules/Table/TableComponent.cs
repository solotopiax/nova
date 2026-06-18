/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponent.cs
 * author:    taoye
 * created:   2026/2/5
 * descrip:   表格组件
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 表格组件。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class TableComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            if (m_Setting == null)
            {
                throw new InvalidOperationException("TableSettings 无效，请检查 TableComponent 配置。");
            }

            m_TableManager = Util.TypeCreator.Create<ITableManager>(m_CurManagerTypeName);
            if (m_TableManager == null)
            {
                throw new InvalidOperationException("TableManager 无效。");
            }
        }

        /// <summary>
        /// 销毁时清理组件级状态。管理器的 Shutdown 由 FrameworkManagersGroup 统一调度。
        /// </summary>
        private void OnDestroy()
        {
            m_LoadTcs = null;
            IsLoadOver = false;
            m_TableManager = null;
        }

        /// <summary>
        /// 开始。
        /// </summary>
        private void Start()
        {
            m_TableManager.Initialize(new TableManagerConfig
            {
                UnitSettings = m_Setting.TableUnitsSettings,
            });
        }

        /// <summary>
        /// 异步加载表格数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public async UniTask<bool> LoadAsync()
        {
            if (IsLoadOver)
            {
                return true;
            }

            if (m_LoadTcs != null)
            {
                return await m_LoadTcs.Task;
            }

            m_LoadTcs = new UniTaskCompletionSource<bool>();
            UniTaskCompletionSource<bool> tcs = m_LoadTcs;

            bool success;

            try
            {
                success = await m_TableManager.LoadTablesAsync();
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Table, "TableComponent.LoadInternalAsync 发生异常：{0}", e);
                success = false;
            }

            IsLoadOver = success;
            tcs.TrySetResult(success);
            m_LoadTcs = null;

            return success;
        }

        /// <summary>
        /// 同步加载表格数据。
        /// </summary>
        /// <returns>是否加载成功。</returns>
        public bool LoadSync()
        {
            if (IsLoadOver)
            {
                return true;
            }

            bool success;
            try
            {
                success = m_TableManager.LoadTablesSync();
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Table, "TableComponent.LoadSync 发生异常：{0}", e);
                success = false;
            }

            IsLoadOver = success;
            return success;
        }

        /// <summary>
        /// 是否包含指定类型的表格。
        /// </summary>
        /// <typeparam name="T">表格类型。</typeparam>
        /// <returns>是否存在。</returns>
        public bool HasTable<T>() where T : class, ITable
        {
            return m_TableManager.HasTable<T>();
        }

        /// <summary>
        /// 是否包含指定类型的表格。
        /// </summary>
        /// <param name="type">表格类型。</param>
        /// <returns>是否存在。</returns>
        public bool HasTable(Type type)
        {
            return m_TableManager.HasTable(type);
        }

        /// <summary>
        /// 获取指定类型的表格。
        /// </summary>
        /// <typeparam name="T">表格类型。</typeparam>
        /// <returns>表格实例，不存在时返回 null。</returns>
        public T GetTable<T>() where T : class, ITable
        {
            return m_TableManager.GetTable<T>();
        }

        /// <summary>
        /// 获取指定类型的表格。
        /// </summary>
        /// <param name="type">表格类型。</param>
        /// <returns>表格实例，不存在时返回 null。</returns>
        public object GetTable(Type type)
        {
            return m_TableManager.GetTable(type);
        }
    }
}
