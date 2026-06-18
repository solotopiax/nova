/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistComponent.Visitors.cs
 * author:    taoye
 * created:   2026/3/18
 * descrip:   Persist组件 —— 属性与字段
 ***************************************************************/

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public sealed partial class PersistComponent : FrameworkComponent
    {
        /// <summary>
        /// LoadAsync 的惰性任务缓存，首次调用时启动三后端 Initialize 的 WhenAll，后续调用返回同一 UniTask。
        /// </summary>
        private UniTask? m_LoadTask;

        // ---- PlayerPrefs Manager

        /// <summary>
        /// PlayerPrefs 管理器类型名称（Inspector 填写，运行时反射创建实例）。
        /// </summary>
        [Tooltip("PlayerPrefs 管理器实现类全名")]
        [SerializeField]
        private string m_CurPlayerPrefsManagerTypeName = "NovaFramework.Runtime.PlayerPrefsManager";
        public string CurPlayerPrefsManagerTypeName => m_CurPlayerPrefsManagerTypeName;

        /// <summary>
        /// 是否对 PlayerPrefs 存储的值启用 AES 加密。
        /// </summary>
        [Tooltip("对 PlayerPrefs 存储的值启用 AES 加密")]
        [SerializeField]
        private bool m_UseAESForPlayerPrefs = true;

        /// <summary>
        /// PlayerPrefs 自动保存间隔（秒），0 或负数表示禁用。
        /// </summary>
        [Tooltip("PlayerPrefs 自动保存间隔（秒），0 表示禁用")]
        [SerializeField]
        private float m_AutoSaveIntervalPlayerPrefs = 60f;

        /// <summary>
        /// PlayerPrefs 持久化管理器实例（IPlayerPrefsManager 承载 PlayerPrefs 后端）。
        /// </summary>
        private IPlayerPrefsManager m_PlayerPrefsManager;

        /// <summary>
        /// 获取 PlayerPrefs 持久化管理器，可直接调用其全部 CRUD 接口。
        /// </summary>
        public IPlayerPrefsManager PlayerPrefs => m_PlayerPrefsManager;

        // ---- FileFragment Manager

        /// <summary>
        /// FileFragment 管理器类型名称（Inspector 填写，运行时反射创建实例）。
        /// </summary>
        [Tooltip("FileFragment 管理器实现类全名")]
        [SerializeField]
        private string m_CurFileFragmentManagerTypeName = "NovaFramework.Runtime.FileFragmentManager";
        public string CurFileFragmentManagerTypeName => m_CurFileFragmentManagerTypeName;

        /// <summary>
        /// 是否对文件片段内容启用 AES 加密。
        /// </summary>
        [Tooltip("对文件片段内容启用 AES 加密")]
        [SerializeField]
        private bool m_UseAESForFileFragment = true;

        /// <summary>
        /// FileFragment 自动保存间隔（秒），0 或负数表示禁用。
        /// </summary>
        [Tooltip("FileFragment 自动保存间隔（秒），0 表示禁用")]
        [SerializeField]
        private float m_AutoSaveIntervalFileFragment = 60f;

        /// <summary>
        /// 文件片段持久化管理器实例（IFileFragmentManager 承载 FileFragment 后端）。
        /// </summary>
        private IFileFragmentManager m_FileFragmentManager;

        /// <summary>
        /// 获取文件片段持久化管理器，可直接调用其全部 CRUD 接口。
        /// </summary>
        public IFileFragmentManager FileFragment => m_FileFragmentManager;

        // ---- SQLite Manager

        /// <summary>
        /// SQLite 管理器类型名称（Inspector 填写，运行时反射创建实例）。
        /// </summary>
        [Tooltip("SQLite 管理器实现类全名")]
        [SerializeField]
        private string m_CurSQLiteManagerTypeName = "NovaFramework.Runtime.SQLiteManager";
        public string CurSQLiteManagerTypeName => m_CurSQLiteManagerTypeName;

        /// <summary>
        /// 是否对 SQLite 存储的值启用 AES 加密。
        /// </summary>
        [Tooltip("对 SQLite 存储的值启用 AES 加密")]
        [SerializeField]
        private bool m_UseAESForSQLite = true;

        /// <summary>
        /// SQLite 数据库 Cipher 加密密码，为空时不启用数据库级加密。
        /// </summary>
        [Tooltip("SQLite Cipher 加密密码，留空则不启用数据库级加密")]
        [SerializeField]
        private string m_SQLiteCipherPassword;

        /// <summary>
        /// SQLite 自动保存间隔（秒），0 或负数表示禁用。
        /// </summary>
        [Tooltip("SQLite 自动保存间隔（秒），0 表示禁用")]
        [SerializeField]
        private float m_AutoSaveIntervalSQLite = 60f;

        /// <summary>
        /// SQLite 持久化管理器实例（ISQLiteManager 承载 SQLite 后端）。
        /// </summary>
        private ISQLiteManager m_SQLiteManager;

        /// <summary>
        /// 获取 SQLite 持久化管理器，可直接调用其全部 CRUD 接口。
        /// </summary>
        public ISQLiteManager SQLite => m_SQLiteManager;
    }
}
