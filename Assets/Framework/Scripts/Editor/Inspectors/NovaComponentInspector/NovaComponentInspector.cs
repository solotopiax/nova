/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaComponentInspector.cs
 * author:    taoye
 * created:   2026/1/14
 * descrip:   Nova组件编辑器面板定制
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Nova 组件编辑器面板定制。
    /// </summary>
    [CustomEditor(typeof(NovaFramework.Runtime.Nova))]
    internal sealed partial class NovaComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 启用。
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();
            // m_FeishuDocumentUrl = EditorPath.Readme.Uri.SolarVibrate;

            m_FrameRate = serializedObject.FindProperty("m_FrameRate");
            m_GameSpeed = serializedObject.FindProperty("m_GameSpeed");
            m_RunInBackground = serializedObject.FindProperty("m_RunInBackground");
            m_NeverSleep = serializedObject.FindProperty("m_NeverSleep");
            m_ReferenceStrictCheckType = serializedObject.FindProperty("m_ReferenceStrictCheckType");
            m_CurTxtHelperTypeName = serializedObject.FindProperty("m_CurTxtHelperTypeName");
            m_CurLogHelperTypeName = serializedObject.FindProperty("m_CurLogHelperTypeName");
            m_CurReferenceHelperTypeName = serializedObject.FindProperty("m_CurReferenceHelperTypeName");

            m_TxtHelperTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(ITxtHelper)));
            m_LogHelperTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(ILogHelper)));
            m_ReferenceHelperTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IReferenceHelper)));
        }

        /// <summary>
        /// 绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            // 顶层：实现选择（Helper 等价 Manager 选择器，置顶平铺）
            EditorUtil.Draw.TypesSelector("Txt 助手", m_TxtHelperTypeNames, m_CurTxtHelperTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.TypesSelector("Log 助手", m_LogHelperTypeNames, m_CurLogHelperTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.TypesSelector("Reference 助手", m_ReferenceHelperTypeNames, m_CurReferenceHelperTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 ITxtHelper、ILogHelper、IReferenceHelper 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();

            EditorUtil.Draw.EnumSelector<ReferenceStrictCheckType>("引用强制检查类型", m_ReferenceStrictCheckType, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "引用池强制检查类型开启情况下将极大地影响其性能，建议正式版本关闭该选项。" });
            EditorUtil.Draw.Line();
            
            var t = (NovaFramework.Runtime.Nova)target;
            EditorUtil.Draw.IntSlider("运行帧率", m_FrameRate, 1, 120, false, (value) => { t.FrameRate = value; }, null, GUILayout.Width(180f));
            EditorUtil.Draw.FloatSlider("运行速率", m_GameSpeed, 0, 8, false, (value) => { t.GameSpeed = value; }, null, GUILayout.Width(180f));
            EditorUtil.Draw.FloatSelectionGrid(m_GameSpeed, s_GameSpeedTexts, GetSelectedGameSpeed, GetGameSpeed, false, (value) => { t.GameSpeed = value; });
            EditorUtil.Draw.Line();

            EditorUtil.Draw.Toggle("后台运行", m_RunInBackground, true, (value) => { t.RunInBackground = value; }, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)禁用时可在编辑器模式下模拟应用挂起与恢复回调",
                "(2)仅用于编辑器环境，移动设备上无效"
            });
            EditorUtil.Draw.Toggle("屏幕常亮", m_NeverSleep, true, (value) => { t.NeverSleep = value; }, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "游戏运行时是否保持屏幕常亮，避免设备自动锁屏。", });
            EditorUtil.Draw.Line();

            FinalRefreshInspectorGUI();
        }

    }
}
