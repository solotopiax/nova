/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   Persist 组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class PersistComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 当前选中的 PlayerPrefs Manager 类型名（序列化属性）。
        /// </summary>
        private SerializedProperty m_CurPlayerPrefsManagerTypeName;

        /// <summary>
        /// PlayerPrefs 是否启用 AES 加密（序列化属性）。
        /// </summary>
        private SerializedProperty m_UseAESForPlayerPrefs;

        /// <summary>
        /// PlayerPrefs 自动保存间隔（序列化属性）。
        /// </summary>
        private SerializedProperty m_AutoSaveIntervalPlayerPrefs;

        /// <summary>
        /// 反射收集到的所有 IPlayerPrefsManager 实现类型名列表。
        /// </summary>
        private List<string> m_PlayerPrefsManagerTypeNames;

        /// <summary>
        /// 当前选中的 FileFragment Manager 类型名（序列化属性）。
        /// </summary>
        private SerializedProperty m_CurFileFragmentManagerTypeName;

        /// <summary>
        /// FileFragment 是否启用 AES 加密（序列化属性）。
        /// </summary>
        private SerializedProperty m_UseAESForFileFragment;

        /// <summary>
        /// FileFragment 自动保存间隔（序列化属性）。
        /// </summary>
        private SerializedProperty m_AutoSaveIntervalFileFragment;

        /// <summary>
        /// 反射收集到的所有 IFileFragmentManager 实现类型名列表。
        /// </summary>
        private List<string> m_FileFragmentManagerTypeNames;

        /// <summary>
        /// 当前选中的 SQLite Manager 类型名（序列化属性）。
        /// </summary>
        private SerializedProperty m_CurSQLiteManagerTypeName;

        /// <summary>
        /// SQLite 是否启用 AES 加密（序列化属性）。
        /// </summary>
        private SerializedProperty m_UseAESForSQLite;

        /// <summary>
        /// SQLite 自动保存间隔（序列化属性）。
        /// </summary>
        private SerializedProperty m_AutoSaveIntervalSQLite;

        /// <summary>
        /// SQLite Cipher 数据库级加密密码（序列化属性，空字符串表示不加密）。
        /// </summary>
        private SerializedProperty m_SQLiteCipherPassword;

        /// <summary>
        /// 编辑器临时 Cipher 密码输入缓冲，与 m_SQLiteCipherPassword 不同时"存档转换"按钮变为可用。
        /// </summary>
        private string m_TmpSQLiteCipherPassword;

        /// <summary>
        /// 反射收集到的所有 ISQLiteManager 实现类型名列表。
        /// </summary>
        private List<string> m_SQLiteManagerTypeNames;

        /// <summary>
        /// 全局搜索关键词（跨所有后端，不区分大小写）。
        /// </summary>
        private string m_GlobalSearchText = string.Empty;

        /// <summary>
        /// PlayerPrefs 后端搜索关键词。
        /// </summary>
        private string m_PPSearchText = string.Empty;

        /// <summary>
        /// FileFragment 后端搜索关键词。
        /// </summary>
        private string m_FFSearchText = string.Empty;

        /// <summary>
        /// SQLite 后端搜索关键词。
        /// </summary>
        private string m_SQLSearchText = string.Empty;

        /// <summary>
        /// Editor 模式下从 PlayerPrefs 读取的原始值，<classify, <item, rawValue>>。
        /// </summary>
        private SortedDictionary<string, SortedDictionary<string, string>> m_PP_Values = new();

        /// <summary>
        /// Editor 模式编辑缓冲区，<classify, <item, editBuffer>>。
        /// </summary>
        private SortedDictionary<string, SortedDictionary<string, string>> m_PP_EditBuffers = new();

        /// <summary>
        /// Editor 模式条目是否处于编辑状态，<classify, <item, isEditing>>。
        /// </summary>
        private SortedDictionary<string, SortedDictionary<string, bool>> m_PP_EditStates = new();

        /// <summary>
        /// Editor 模式下从文件读取的原始值，<classify, <item, storedValue>>。
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> m_FF_Values = new();

        /// <summary>
        /// Editor 模式编辑缓冲区，<classify, <item, editBuffer>>。
        /// </summary>
        private Dictionary<string, Dictionary<string, string>> m_FF_EditBuffers = new();

        /// <summary>
        /// Editor 模式条目是否处于编辑状态，<classify, <item, isEditing>>。
        /// </summary>
        private Dictionary<string, Dictionary<string, bool>> m_FF_EditStates = new();

#if !UNITY_WEBGL
        /// <summary>
        /// Editor 模式下从 DB 读取的原始值，<classify, <item, rawValue>>。
        /// </summary>
        private SortedDictionary<string, SortedDictionary<string, string>> m_SQL_Values = new();

        /// <summary>
        /// Editor 模式编辑缓冲区，<classify, <item, editBuffer>>。
        /// </summary>
        private SortedDictionary<string, SortedDictionary<string, string>> m_SQL_EditBuffers = new();

        /// <summary>
        /// Editor 模式条目是否处于编辑状态，<classify, <item, isEditing>>。
        /// </summary>
        private SortedDictionary<string, SortedDictionary<string, bool>> m_SQL_EditStates = new();

        /// <summary>
        /// Editor 模式 SQLite 连接（非运行时打开，运行时关闭）。
        /// </summary>
        private SqlCipher4Unity3D.SQLiteConnection m_SQLiteConnection;
#endif
    }
}
