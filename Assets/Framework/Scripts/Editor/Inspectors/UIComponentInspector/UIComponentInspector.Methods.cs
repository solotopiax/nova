/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  UIComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   UI 组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class UIComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置区域：UI 管理器类型选择器、命名空间列表、导出设置、实例池设置与视图分组列表。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("UI 管理器", m_UIManagerTypeNames, m_CurUIManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.TypesSelector("UIGroup 辅助器", m_UIGroupHelperTypeNames, m_CurUIGroupHelperTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IUIManager、IUIGroupHelper 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();

            DrawUIExport();

            EditorUtil.Draw.Property("实例根节点", m_InstanceRoot, true, GUILayout.Width(175));
            EditorUtil.Draw.Line();

            EditorUtil.Draw.Property("视图分组深度换算系数", m_GroupDepthFactor, true, GUILayout.Width(175));
            EditorUtil.Draw.Property("视图内部深度换算系数", m_ViewDepthFactor, true, GUILayout.Width(175));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1) 视图分组深度换算系数：视图分组深度乘以此值后得到 Canvas.sortingOrder",
                "(2) 视图内部深度换算系数：视图在分组内的深度乘以此值后叠加到 Canvas.sortingOrder",
                "(3) sortingOrder 取值范围 -32768 ~ 32767，越界将被 Clamp 并记录 Error 日志"
            });
            EditorUtil.Draw.Line();

            EditorUtil.Draw.Property("屏幕设计分辨率", m_ScreenDesignedResolution, true, GUILayout.Width(175));
            EditorUtil.Draw.FloatSlider("屏幕宽高适配比例阀值 (W<->H)", m_ScreenWidthHeightMatchValue, 0f, 1f, true, null, null, GUILayout.Width(175));
            EditorUtil.Draw.Property("每帧最多销毁 UI 数量", m_DestroyMaxNumPerFrame, true, GUILayout.Width(175));
            EditorUtil.Draw.Line();

            EditorUtil.Draw.FloatSlider("实例自动释放间隔(秒)", m_InstanceAutoReleaseInterval, 0f, c_InstanceAutoReleaseIntervalMax);
            EditorUtil.Draw.IntSlider("实例池容量", m_InstanceCapacity, 0, c_InstanceCapacityMax);
            EditorUtil.Draw.FloatSlider("实例过期时间(秒)", m_InstanceExpireTime, 0f, c_InstanceExpireTimeMax);
            EditorUtil.Draw.IntSlider("实例优先级", m_InstancePriority, c_InstancePriorityMin, c_InstancePriorityMax);
            EditorUtil.Draw.Line();

            DrawUIGroups();
        }

        /// <summary>
        /// 绘制 UI 表格导出区域：模板路径提示、表格目录位置（选择/打开文件夹）、若目录有效则绘制可折叠的数据源文件树及每文件的导出设置与操作按钮、以及「导出所有数据和类型」按钮。
        /// </summary>
        private void DrawUIExport()
        {
            if (m_SourceDirPath == null || m_UIUnitsSettings == null)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout("UI 表格导出", "UIExport", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            DrawTemplatePathHintReadOnly(m_SourceDirPath, TemplateFileName, "模板文件位置：");

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("表格目录位置：", false, GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_DirLabelWidth));
                EditorUtil.Draw.TextField(m_SourceDirPath, true);
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay("选择表格目录位置", "", "", m_SourceDirPath), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(m_SourceDirPath.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthLarge));
            });

            string directoryPath = m_SourceDirPath.stringValue;
            if (!string.IsNullOrEmpty(directoryPath) && Util.SysIO.Directory.Exists(directoryPath))
            {
                if (!m_IsLubanConfigExists)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "Luban 配置目录 (_configs/) 尚未初始化，首次导出时将自动创建。" });
                }

                EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders(directoryPath, m_UIUnitsSettings, m_FolderFoldoutState, customDrawSourceFileRow: DrawUISourceFileRow);
                EditorUtil.Draw.Button("导出所有数据和类型", true, () =>
                {
                    EditorUtil.Luban.DataTypeNameHelper.DoRefreshAllDataTypeNames(directoryPath, m_UIUnitsSettings, serializedObject);
                    DoExportAllDataAndTypes(directoryPath, m_UIUnitsSettings);
                });
            }

            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 自定义 UI 数据源文件行绘制：文件名行、数据导出行、类型导出行、Asset 地址行。
        /// </summary>
        private void DrawUISourceFileRow(string filePath, string capturedRelativePath, int seq, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty)
        {
            EditorUtil.Draw.SourceFileTree.DrawDefaultFileNameRow(filePath, seq, indentSpace, savedIndent);
            EditorUtil.Draw.SourceFileTree.DrawDataExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, OnExportDataForFile, DoRefreshDataTypeNames);
            EditorUtil.Draw.SourceFileTree.DrawClassExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, OnExportClassForFile, DoRefreshDataTypeNames);
            EditorUtil.Draw.SourceFileTree.DrawAssetLocationRow(detailProp, indentSpace, savedIndent);
        }

        /// <summary>
        /// 刷新单个文件的 DataTypeNames（委托适配）。
        /// </summary>
        private void DoRefreshDataTypeNames(string filePath, SerializedProperty dataTypeNamesProp)
        {
            EditorUtil.Luban.DataTypeNameHelper.DoRefreshDataTypeNames(filePath, dataTypeNamesProp, serializedObject);
        }

        /// <summary>
        /// Luban _configs/ 文件变更回调（由 FileWatcher 在主线程触发）。
        /// </summary>
        private void OnLubanConfigChanged()
        {
            m_IsLubanConfigExists = true;
            Repaint();
        }

        /// <summary>
        /// 绘制视图分组列表。编辑模式展示序列化配置，运行时展示 target 实际分组详情。
        /// </summary>
        private void DrawUIGroups()
        {
            if (EditorApplication.isPlaying)
            {
                DrawRuntimeUIGroups();
                return;
            }

            if (!EditorUtil.Draw.Foldout($"视图分组列表({m_UIGroups.arraySize})", "UIGroups"))
            {
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            int newSize = EditorUtil.Draw.IntField("数量", m_UIGroups.arraySize);
            if (newSize != m_UIGroups.arraySize && newSize >= 0)
            {
                m_UIGroups.arraySize = newSize;
                m_UIGroups.serializedObject.ApplyModifiedProperties();
            }

            for (int i = 0; i < m_UIGroups.arraySize; i++)
            {
                SerializedProperty element = m_UIGroups.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = element.FindPropertyRelative("m_Name");
                SerializedProperty depthProp = element.FindPropertyRelative("m_Depth");

                string header = string.IsNullOrEmpty(nameProp?.stringValue) ? $"视图分组 {i}" : nameProp.stringValue;
                if (!EditorUtil.Draw.Foldout(header, $"UIGroups_Element_{i}"))
                {
                    continue;
                }

                EditorUtil.Draw.IncreaseIndentLevel();
                EditorUtil.Draw.Property("名称", nameProp);
                EditorUtil.Draw.Property("深度", depthProp);
                EditorUtil.Draw.DecreaseIndentLevel();
            }

            EditorUtil.Draw.DecreaseIndentLevel();
        }

        /// <summary>
        /// 绘制运行时实际视图分组列表（仅 Play 模式）。
        /// </summary>
        private void DrawRuntimeUIGroups()
        {
            UIComponent t = (UIComponent)target;
            if (!EditorUtil.Draw.Foldout($"视图分组列表({t.UIGroupCount})", "UIGroups"))
            {
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            List<IUIGroup> uiGroups = new List<IUIGroup>();
            t.GetAllUIGroups(uiGroups);
            foreach (IUIGroup uiGroup in uiGroups)
            {
                DrawRuntimeUIGroup(uiGroup);
            }
            EditorUtil.Draw.DecreaseIndentLevel();
        }

        /// <summary>
        /// 绘制单个运行时视图分组信息。
        /// </summary>
        /// <param name="uiGroup">要绘制的视图分组。</param>
        private void DrawRuntimeUIGroup(IUIGroup uiGroup)
        {
            if (!EditorUtil.Draw.Foldout($"{uiGroup.Name} ({uiGroup.UIViewCount})", $"RuntimeUIGroup_{uiGroup.Name}"))
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                EditorUtil.Draw.Label("名称", uiGroup.Name, false);
                EditorUtil.Draw.Label("深度", uiGroup.Depth.ToString(), false);
                EditorUtil.Draw.Label("暂停", uiGroup.Pause.ToString(), false);
                EditorUtil.Draw.Label("视图数量", uiGroup.UIViewCount.ToString(), false);

                IUIView currentView = uiGroup.CurrentUIView;
                EditorUtil.Draw.Label("当前视图", currentView != null ? $"{currentView.AssetLocation}  (SerialID:{currentView.SerialID})" : "—", false);

                List<IUIView> uiViews = new List<IUIView>();
                uiGroup.GetAllUIViews(uiViews);
                if (uiViews.Count > 0)
                {
                    float[] colWidths = { c_UIColSerialID, c_UIColDepth, c_UIColPauseCovered };
                    EditorUtil.Draw.TableRow("Name", new[] { "SerialID", "Depth", "PauseCovered" }, colWidths);
                    foreach (IUIView uiView in uiViews)
                    {
                        EditorUtil.Draw.TableRow(
                            uiView.AssetLocation,
                            new[] { uiView.SerialID.ToString(), uiView.DepthInUIGroup.ToString(), uiView.PauseCoveredUIView.ToString() },
                            colWidths
                        );
                    }
                }
            });
            EditorUtil.Draw.Separator();
        }

    }
}
