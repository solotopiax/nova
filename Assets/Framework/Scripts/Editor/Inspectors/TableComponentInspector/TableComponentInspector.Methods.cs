/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   TableComponent Inspector 绘制方法
 ***************************************************************/

using System.Collections.Generic;
using System.Reflection;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class TableComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制 Manager 类型选择器。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("Table 管理器", m_ManagerTypeNames, m_CurManagerTypeName,
                true, null, GUILayout.Width(180f));
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制官方 luban.conf、可组合 Profile、Runtime Binding 与导出入口。
        /// </summary>
        private void DrawTableExport()
        {
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1) luban.conf、schema 与数据源共同组成正式 Table Project。",
                "(2) Enabled Profile 会参与批量导出，可同时选择任意一个或多个。",
                "(3) Code Targets、Data Targets、Tags、Variants、模板目录与扩展参数按 Luban 语义传递。",
                "(4) Runtime Bindings 可同时加载多组生成 Tables，表清单和解码方式由各 Binding 提供。",
            });
            EditorUtil.Draw.PropertyField(m_Project, "Luban Project", true);
            EditorUtil.Draw.PropertyField(m_Runtime, "Runtime Bindings", true);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Button("导出代码", true, () => RunExport(EditorUtil.Table.Exporter.ExportCode));
                EditorUtil.Draw.Button("导出数据", true, () => RunExport(EditorUtil.Table.Exporter.ExportData));
                EditorUtil.Draw.Button("导出代码和数据", true, () => RunExport(EditorUtil.Table.Exporter.ExportAll));
            });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 在应用 Inspector 修改后执行全部 Enabled Profile 导出。
        /// </summary>
        /// <param name="export">Table 导出入口。</param>
        private void RunExport(System.Func<TableSettings, bool> export)
        {
            serializedObject.ApplyModifiedProperties();
            TableSettings settings = GetTableSettings();
            if (settings != null && export(settings))
            {
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }
        }

        /// <summary>
        /// Play Mode 下显示全部已注册 Binding 构建出的实际表数量。
        /// </summary>
        private void DrawRuntimeInfos()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            TableComponent component = (TableComponent)target;
            string status = component.IsLoadOver ? "已加载" : "未完成";
            m_RuntimeTablesFoldout = EditorUtil.Draw.Foldout(
                ref m_RuntimeTablesFoldout, $"运行时表 ({component.Count}) [{status}]", false);
            if (m_RuntimeTablesFoldout)
            {
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) 当前数量来自已加载或直接注册的 Luban Tables 容器。",
                });
            }
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 读取当前 TableComponent 的 TableSettings 实例。
        /// </summary>
        /// <returns>当前设置，反射失败时返回 null。</returns>
        private TableSettings GetTableSettings()
        {
            FieldInfo field = typeof(TableComponent).GetField(
                "m_Setting", BindingFlags.NonPublic | BindingFlags.Instance);
            return field?.GetValue(target) as TableSettings;
        }
    }
}
