/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistComponent.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   Persist 组件
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Persist 组件。持有三个独立持久化管理器实例，通过属性直接访问。
    /// <para>访问方式：<c>Nova.Persist.PlayerPrefs.SetInt(...)</c></para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class PersistComponent : FrameworkComponent
    {
        /// <summary>
        /// 唤醒：通过 TypeCreator 反射创建三个后端 Manager 实例。
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            m_PlayerPrefsManager = Util.TypeCreator.Create<IPlayerPrefsManager>(m_CurPlayerPrefsManagerTypeName);
            if (m_PlayerPrefsManager == null)
            {
                throw new InvalidOperationException("IPlayerPrefsManager 创建失败，请检查 Inspector 中的 CurPlayerPrefsManagerTypeName。");
            }

            m_FileFragmentManager = Util.TypeCreator.Create<IFileFragmentManager>(m_CurFileFragmentManagerTypeName);
            if (m_FileFragmentManager == null)
            {
                throw new InvalidOperationException("IFileFragmentManager 创建失败，请检查 Inspector 中的 CurFileFragmentManagerTypeName。");
            }

            m_SQLiteManager = Util.TypeCreator.Create<ISQLiteManager>(m_CurSQLiteManagerTypeName);
            if (m_SQLiteManager == null)
            {
                throw new InvalidOperationException("ISQLiteManager 创建失败，请检查 Inspector 中的 CurSQLiteManagerTypeName。");
            }
        }

        /// <summary>
        /// 异步初始化所有 Manager。由 ProcedurePreload 显式 await，保证后续业务读写命中已就绪状态。
        /// 幂等：首次调用启动三后端 Initialize 的 WhenAll，重复调用返回同一 UniTask。
        /// </summary>
        /// <returns>三后端初始化完成的 UniTask。</returns>
        public UniTask LoadAsync()
        {
            if (m_LoadTask.HasValue)
            {
                return m_LoadTask.Value;
            }

            m_LoadTask = RunLoadAsync();
            return m_LoadTask.Value;
        }

        /// <summary>
        /// 内部并行调度三后端 Initialize；任一失败时 Log.Error 并向上抛出。
        /// </summary>
        /// <returns>初始化完成的 UniTask。</returns>
        private async UniTask RunLoadAsync()
        {
            try
            {
                await UniTask.WhenAll(
                    ((PersistManagerBase<PlayerPrefsManagerConfig>)m_PlayerPrefsManager).Initialize(new PlayerPrefsManagerConfig
                    {
                        UseAESEncrypt = m_UseAESForPlayerPrefs,
                        AutoSaveInterval = m_AutoSaveIntervalPlayerPrefs,
                    }),
                    ((PersistManagerBase<FileFragmentManagerConfig>)m_FileFragmentManager).Initialize(new FileFragmentManagerConfig
                    {
                        UseAESEncrypt = m_UseAESForFileFragment,
                        AutoSaveInterval = m_AutoSaveIntervalFileFragment,
                    }),
                    ((PersistManagerBase<SQLiteManagerConfig>)m_SQLiteManager).Initialize(new SQLiteManagerConfig
                    {
                        UseAESEncrypt = m_UseAESForSQLite,
                        CipherPassword = m_SQLiteCipherPassword,
                        AutoSaveInterval = m_AutoSaveIntervalSQLite,
                    })
                );
            }
            catch (System.Exception ex)
            {
                Log.Error(LogTag.Persist, "PersistComponent.LoadAsync 初始化失败: {0}", ex.Message);
                throw;
            }
        }

        /// <summary>
        /// 销毁时清理管理器引用与缓存任务。Shutdown 由 FrameworkManagersGroup 统一调度。
        /// </summary>
        private void OnDestroy()
        {
            m_PlayerPrefsManager = null;
            m_FileFragmentManager = null;
            m_SQLiteManager = null;
            m_LoadTask = null;
        }
    }
}
