/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/5/16
 * descrip:   Prefab 组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class PrefabComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置信息及运行时数据。
        /// </summary>
        private void DrawConfigs()
        {
            // 顶层：实现选择（不加 Foldout，平铺展示）
            EditorUtil.Draw.TypesSelector("Prefab 管理器", m_PrefabManagerTypeNames, m_CurPrefabManagerTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IPrefabManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();

            // 运行时数据：仅在 Play Mode 下展示
            if (!Application.isPlaying || target is not PrefabComponent comp) return;

            if (!EditorUtil.Draw.Foldout("运行时数据", "PrefabRuntimeGroup", true)) return;

            // 实例计数
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("当前实例计数：", comp.RecordedInstanceCount.ToString(), false);
            });

            var instances = comp.RecordedInstances;
            if (instances == null || instances.Count == 0)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "当前无被记录的 Prefab 实例。" }, false, GUILayout.ExpandWidth(true));
                });
                return;
            }

            // 逐行展示：ObjectField（只读，可点选）+ Location 文本
            // 此处 EditorGUILayout.ObjectField / SelectableLabel 为运行时列表专用路径：
            // EditorUtil.Draw 无只读 ObjectField 封装，故保留裸 EditorGUILayout 调用。
            foreach (var item in instances)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorGUILayout.ObjectField("", item.Instance, typeof(GameObject), true, GUILayout.Width(160f));
                    EditorUtil.Draw.SelectableLabel(item.Location, false, GUILayout.ExpandWidth(true));
                });
            }
        }
    }
}
