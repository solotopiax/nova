/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistComponentInspector.cs
 * author:    taoye
 * created:   2026/3/16
 * descrip:   Persist 组件编辑器面板定制 —— 主入口
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Persist 组件编辑器面板定制。
    /// 划分为 PlayerPrefs / FileFragment / SQLite 三个 partial 文件，各自独立管理数据状态与 GUI 绘制。
    /// </summary>
    [CustomEditor(typeof(PersistComponent))]
    internal sealed partial class PersistComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            // PlayerPrefs
            m_CurPlayerPrefsManagerTypeName = serializedObject.FindProperty("m_CurPlayerPrefsManagerTypeName");
            m_UseAESForPlayerPrefs = serializedObject.FindProperty("m_UseAESForPlayerPrefs");
            m_AutoSaveIntervalPlayerPrefs = serializedObject.FindProperty("m_AutoSaveIntervalPlayerPrefs");
            m_PlayerPrefsManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IPlayerPrefsManager)));

            // FileFragment
            m_CurFileFragmentManagerTypeName = serializedObject.FindProperty("m_CurFileFragmentManagerTypeName");
            m_UseAESForFileFragment = serializedObject.FindProperty("m_UseAESForFileFragment");
            m_AutoSaveIntervalFileFragment = serializedObject.FindProperty("m_AutoSaveIntervalFileFragment");
            m_FileFragmentManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IFileFragmentManager)));

            // SQLite
            m_CurSQLiteManagerTypeName = serializedObject.FindProperty("m_CurSQLiteManagerTypeName");
            m_UseAESForSQLite = serializedObject.FindProperty("m_UseAESForSQLite");
            m_AutoSaveIntervalSQLite = serializedObject.FindProperty("m_AutoSaveIntervalSQLite");
            m_SQLiteCipherPassword = serializedObject.FindProperty("m_SQLiteCipherPassword");
            m_SQLiteManagerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(ISQLiteManager)));
            m_TmpSQLiteCipherPassword = m_SQLiteCipherPassword?.stringValue ?? string.Empty;

            // 各后端初始化
            OnEnablePlayerPrefs();
            OnEnableFileFragment();
#if !UNITY_WEBGL
            OnEnableSQLite();
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

        /// <summary>
        /// 禁用。
        /// </summary>
        protected void OnDisable()
        {
#if !UNITY_WEBGL
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.delayCall -= DelayedOnEnableSQLite;
            OnDisableSQLite();
#endif
        }

#if !UNITY_WEBGL
        /// <summary>
        /// Play Mode 状态变化回调：进入播放前关闭 Editor 连接，退出播放后重新打开。
        /// </summary>
        /// <param name="state">状态变化阶段。</param>
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                OnDisableSQLite();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                EditorApplication.delayCall += DelayedOnEnableSQLite;
            }
        }

        /// <summary>
        /// delayCall 包装：在执行前验证本 Inspector 实例仍然有效（target 未销毁、serializedObject 可用），并刷新密码属性引用。
        /// Play Mode 切换会重建 Inspector，旧实例的 serializedObject 在 OnDisable 后失效，直接调用 OnEnableSQLite 会因访问失效的 SerializedProperty 而抛异常。
        /// 同时跳过已持有连接的实例，避免新实例的 OnEnable 与 delayCall 重复建立连接。
        /// </summary>
        private void DelayedOnEnableSQLite()
        {
            if (target == null || serializedObject == null || serializedObject.targetObject == null)
            {
                return;
            }

            if (m_SQLiteConnection != null)
            {
                return;
            }

            serializedObject.Update();
            m_SQLiteCipherPassword = serializedObject.FindProperty("m_SQLiteCipherPassword");
            m_TmpSQLiteCipherPassword = m_SQLiteCipherPassword?.stringValue ?? string.Empty;
            OnEnableSQLite();
        }
#endif

        /// <summary>
        /// 绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            DrawConfigs();
            DrawDataSection();
            FinalRefreshInspectorGUI();
        }
    }
}
