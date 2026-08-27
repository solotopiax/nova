/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TableComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   TableComponent Inspector 绘制与 Luban Project 管理
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using IOPath = System.IO.Path;

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
        /// 绘制 Luban Project、导出描述、加载描述与批量导出入口。
        /// </summary>
        private void DrawTableExport()
        {
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1) 每个 Luban 工程独立管理表格、配置文件和导出描述。",
                "(2) 启用导出描述后，可以按需导出代码、数据或两者。",
            });

            DrawProjects();
            EditorUtil.Draw.Line();
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1) Luban 加载用于指定运行时需要读取哪个工程导出的表格数据。",
                "(2) Asset 地址清单用于配置每份表格数据对应的资源地址。",
            });
            DrawLoadDescriptions();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制作用于所有 Luban 工程已启用导出描述的批量导出入口。
        /// </summary>
        private void DrawExportActions()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Button("导出代码", true, () => RunExport(EditorUtil.Table.Exporter.ExportCode));
                EditorUtil.Draw.Button("导出数据", true, () => RunExport(EditorUtil.Table.Exporter.ExportData));
                EditorUtil.Draw.Button("导出代码和数据", true, () => RunExport(EditorUtil.Table.Exporter.ExportAll));
            });
        }

        /// <summary>
        /// 绘制 Project 列表和新建、添加、复制操作。
        /// </summary>
        private void DrawProjects()
        {
            if (m_Projects == null)
            {
                return;
            }

            List<string> configPaths = ReadProjectConfigPaths();
            bool hasProjectError = false;
            for (int index = 0; index < m_Projects.arraySize; index++)
            {
                SerializedProperty project = m_Projects.GetArrayElementAtIndex(index);
                string projectId = project.FindPropertyRelative("Id").stringValue;
                string configPath = project.FindPropertyRelative("ConfigPath").stringValue;
                TableProjectConfigStatus status = TableProjectConfigStatusResolver.Resolve(
                    configPath, configPaths);
                TableProjectModel model = GetProjectModel(projectId, configPath, status.State);
                if (HasProjectError(project, status, model))
                {
                    hasProjectError = true;
                    break;
                }
            }

            bool isOpen = EditorUtil.Draw.ColoredFoldoutHeader(
                $"Luban 工程 ({m_Projects.arraySize})",
                "TableLubanProjects",
                hasProjectError ? GetErrorColor() : GUI.contentColor,
                () =>
                {
                    EditorUtil.Draw.Button("新建", true, false, CreateProject, GUILayout.Width(50f));
                    EditorUtil.Draw.Button("添加", true, false, AddExistingProject, GUILayout.Width(50f));
                },
                true);
            if (!isOpen)
            {
                return;
            }

            EditorUtil.Draw.Layout.Indented(() =>
            {
                for (int i = 0; i < m_Projects.arraySize; i++)
                {
                    SerializedProperty project = m_Projects.GetArrayElementAtIndex(i);
                    TableProjectConfigStatus status = TableProjectConfigStatusResolver.Resolve(
                        project.FindPropertyRelative("ConfigPath").stringValue, configPaths);
                    DrawProject(project, i, status);
                }
                DrawExportActions();
            });
        }

        /// <summary>
        /// 绘制单个 Project 的路径、Excel 树和导出描述。
        /// </summary>
        /// <param name="project">Project 序列化属性。</param>
        /// <param name="index">Project 索引。</param>
        /// <param name="status">配置文件状态。</param>
        private void DrawProject(SerializedProperty project, int index, TableProjectConfigStatus status)
        {
            SerializedProperty id = project.FindPropertyRelative("Id");
            SerializedProperty name = project.FindPropertyRelative("Name");
            SerializedProperty configPath = project.FindPropertyRelative("ConfigPath");
            string title = string.IsNullOrWhiteSpace(name.stringValue) ? $"工程 {index + 1}" : name.stringValue;
            bool hasOutputPathError = HasAnyExportOutputPathError(
                project.FindPropertyRelative("ExportDescriptions"));
            TableProjectModel model = GetProjectModel(
                id.stringValue, configPath.stringValue, status.State);
            bool hasSchemaError = HasMissingSchemaFile(model);
            Color configStatusColor = GetProjectStatusColor(status.State);
            Color projectStatusColor = hasOutputPathError || hasSchemaError
                ? GetErrorColor()
                : configStatusColor;

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                bool isOpen = EditorUtil.Draw.ColoredFoldoutHeader(
                    title,
                    $"TableProject_{id.stringValue}_{index}",
                    projectStatusColor,
                    () =>
                    {
                        EditorUtil.Draw.Button("复制", 50f, true, () => QueueCopyProject(project));
                        EditorUtil.Draw.DangerButton("删除", 50f, true, () => QueueRemoveProject(project));
                    },
                    false);
                if (!isOpen)
                {
                    return;
                }

                EditorUtil.Draw.Layout.Indented(() =>
                {
                    EditorUtil.Draw.PropertyField(name, "名称", false);
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        Color? configLabelColor = status.State == TableProjectConfigState.Valid
                            ? null
                            : configStatusColor;
                        EditorUtil.Draw.StatusLabel(
                            "配置文件", configLabelColor, GUILayout.Width(EditorGUIUtility.labelWidth));
                        EditorUtil.Draw.ColoredPropertyField(
                            configPath, configStatusColor, true, GUILayout.ExpandWidth(true));
                        EditorUtil.Draw.Button("打开", 52f, true, () => OpenConfig(configPath.stringValue));
                        EditorUtil.Draw.Button("打开文件夹", 82f, true, () => OpenConfigFolder(configPath.stringValue));
                    });
                    if (status.State != TableProjectConfigState.Valid)
                    {
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.StatusLabel(
                                string.Empty, null, GUILayout.Width(EditorGUIUtility.labelWidth));
                            EditorUtil.Draw.ColoredMiniLabel(status.Message, configStatusColor, false);
                        });
                    }
                    DrawSchemaFiles(model);
                    DrawExcelTree(model, id.stringValue);
                    DrawExportDescriptions(project, model);
                });
            }, GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
        }

        /// <summary>
        /// 绘制 luban.conf 声明的全部 Schema 文件；路径只读，文件与目录仍可直接打开。
        /// </summary>
        /// <param name="model">Luban 工程只读模型。</param>
        private static void DrawSchemaFiles(TableProjectModel model)
        {
            List<string> schemaFiles = model?.SchemaFiles ?? new List<string>();
            int rowCount = Math.Max(1, schemaFiles.Count);
            for (int index = 0; index < rowCount; index++)
            {
                string schemaPath = index < schemaFiles.Count ? schemaFiles[index] : string.Empty;
                string displayPath = EditorUtil.FileSystem.GetProjectRelativePath(schemaPath);
                string label = index == 0 ? "Schema 文件" : $"Schema 文件 {index + 1}";
                bool schemaExists = File.Exists(schemaPath);
                Color? statusColor = schemaExists ? null : new Color(1f, 0.35f, 0.3f);
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.StatusLabel(
                        label, statusColor, GUILayout.Width(EditorGUIUtility.labelWidth));
                    EditorUtil.Draw.ReadOnlyTextField(
                        displayPath, statusColor, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Button("打开", 52f, true, () => OpenSchemaFile(schemaPath));
                    EditorUtil.Draw.Button("打开文件夹", 82f, true, () => OpenSchemaFolder(schemaPath));
                });
                if (!schemaExists)
                {
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.StatusLabel(
                            string.Empty, null, GUILayout.Width(EditorGUIUtility.labelWidth));
                        EditorUtil.Draw.ColoredMiniLabel(
                            "Schema 文件不存在。", statusColor.Value, false);
                    });
                }
            }
        }

        /// <summary>
        /// 按目录、Excel、Sheet、Luban 表四层绘制当前 Project 的输入清单。
        /// </summary>
        /// <param name="model">Project 只读模型。</param>
        /// <param name="projectId">Project 唯一标识。</param>
        private void DrawExcelTree(TableProjectModel model, string projectId)
        {
            if (model == null)
            {
                return;
            }

            bool isOpen = EditorUtil.Draw.FoldoutHeader(
                $"Excel 清单 ({model.ExcelFiles.Count})",
                $"TableExcelTree_{projectId}",
                null,
                true);
            if (!isOpen)
            {
                return;
            }

            DrawExcelDirectory(model.ExcelFiles, string.Empty, projectId);
        }

        /// <summary>
        /// 递归绘制 Excel 相对目录，并在叶子节点展示 Excel、Sheet 与 Luban 表。
        /// </summary>
        /// <param name="files">当前 Project 的全部 Excel。</param>
        /// <param name="directory">当前相对目录。</param>
        /// <param name="projectId">Project 唯一标识。</param>
        private void DrawExcelDirectory(IReadOnlyList<TableProjectExcelFile> files, string directory, string projectId)
        {
            string prefix = string.IsNullOrEmpty(directory) ? string.Empty : directory.TrimEnd('/') + "/";
            foreach (string childDirectory in files
                         .Select(file => file.RelativePath)
                         .Where(path => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                         .Select(path => path.Substring(prefix.Length))
                         .Where(path => path.Contains('/'))
                         .Select(path => path.Substring(0, path.IndexOf('/')))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                string childPath = prefix + childDirectory;
                EditorUtil.Draw.Layout.Indented(() =>
                {
                    if (EditorUtil.Draw.Foldout(
                            $"目录: {childDirectory}", $"TableExcelDir_{projectId}_{childPath}"))
                    {
                        DrawExcelDirectory(files, childPath, projectId);
                    }
                });
            }

            foreach (TableProjectExcelFile excel in files
                         .Where(file => string.Equals(
                             NormalizePath(IOPath.GetDirectoryName(file.RelativePath)), directory,
                             StringComparison.OrdinalIgnoreCase))
                         .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
            {
                EditorUtil.Draw.Layout.Indented(() =>
                {
                    string fileName = IOPath.GetFileName(excel.RelativePath);
                    bool isOpen = EditorUtil.Draw.FoldoutHeader(
                        $"Excel: {fileName}",
                        $"TableExcel_{projectId}_{excel.RelativePath}",
                        () =>
                        {
                            EditorUtil.Draw.Button("打开", 52f, true, () => OpenExcel(excel.AbsolutePath));
                            EditorUtil.Draw.Button("打开文件夹", 82f, true, () => OpenExcelFolder(excel.AbsolutePath));
                        });
                    if (!isOpen)
                    {
                        return;
                    }

                    EditorUtil.Draw.Layout.Indented(() =>
                    {
                        foreach (TableProjectExcelSheet sheet in excel.Sheets)
                        {
                            if (!EditorUtil.Draw.Foldout($"Sheet: {sheet.Name}",
                                    $"TableExcelSheet_{projectId}_{excel.RelativePath}_{sheet.Name}"))
                            {
                                continue;
                            }
                            EditorUtil.Draw.Layout.Indented(() =>
                            {
                                if (sheet.TableNames.Count == 0)
                                {
                                    EditorUtil.Draw.Label("未被当前 schema 的 table input 引用", false);
                                }
                                else
                                {
                                    foreach (string tableName in sheet.TableNames)
                                    {
                                        EditorUtil.Draw.Label($"Luban 表: {tableName}", false);
                                    }
                                }
                            });
                        }
                    });
                });
            }
        }

        /// <summary>
        /// 绘制当前 Project 的导出描述列表。
        /// </summary>
        /// <param name="project">Project 序列化属性。</param>
        /// <param name="model">Project 只读模型。</param>
        private void DrawExportDescriptions(SerializedProperty project, TableProjectModel model)
        {
            SerializedProperty descriptions = project.FindPropertyRelative("ExportDescriptions");
            string projectId = project.FindPropertyRelative("Id").stringValue;
            bool hasOutputPathError = HasAnyExportOutputPathError(descriptions);
            bool isOpen = EditorUtil.Draw.ColoredFoldoutHeader(
                $"导出描述 ({descriptions.arraySize})",
                $"TableExportDescriptions_{projectId}",
                hasOutputPathError ? GetErrorColor() : Color.white,
                () => EditorUtil.Draw.Button(
                    "新增", 50f, true, () => ShowExportFormatMenu(projectId)),
                true);
            if (!isOpen)
            {
                return;
            }

            EditorUtil.Draw.Layout.Indented(() =>
            {
                for (int i = 0; i < descriptions.arraySize; i++)
                {
                    DrawExportDescription(descriptions.GetArrayElementAtIndex(i), i, model, projectId);
                }
            });
        }

        /// <summary>
        /// 绘制单个导出描述的全部 Luban 参数与表格范围。
        /// </summary>
        /// <param name="description">导出描述属性。</param>
        /// <param name="index">描述索引。</param>
        /// <param name="model">Project 只读模型。</param>
        /// <param name="projectId">Project 唯一标识。</param>
        private void DrawExportDescription(
            SerializedProperty description,
            int index,
            TableProjectModel model,
            string projectId)
        {
            SerializedProperty id = description.FindPropertyRelative("Id");
            SerializedProperty name = description.FindPropertyRelative("Name");
            SerializedProperty enabled = description.FindPropertyRelative("Enabled");
            string title = string.IsNullOrWhiteSpace(name.stringValue) ? $"导出描述 {index + 1}" : name.stringValue;
            bool hasOutputPathError = HasExportOutputPathError(description);
            EditorUtil.Draw.Layout.TreeItemBox(() =>
            {
                bool isOpen = EditorUtil.Draw.ColoredToggleFoldoutHeader(
                    title,
                    $"TableExportDescription_{projectId}_{id.stringValue}_{index}",
                    GetExportDescriptionColor(enabled.boolValue, hasOutputPathError),
                    enabled.boolValue,
                    out bool newEnabled,
                    () => EditorUtil.Draw.DangerButton(
                        "删除", 50f, true,
                        () => QueueRemoveExportDescription(projectId, id.stringValue, title)),
                    index == 0);
                if (newEnabled != enabled.boolValue)
                {
                    enabled.boolValue = newEnabled;
                    serializedObject.ApplyModifiedProperties();
                }
                if (!isOpen)
                {
                    return;
                }

                Color previousContentColor = GUI.contentColor;
                EditorGUI.BeginDisabledGroup(!enabled.boolValue);
                try
                {
                    if (!enabled.boolValue)
                    {
                        GUI.contentColor = GetExportDescriptionColor(false, false);
                    }

                    // 标题行在展开箭头和 Toggle 后开始显示文字；三级标准缩进使子项
                    // 相对标题文字再向右错开约一个汉字宽度。
                    EditorUtil.Draw.Layout.Indented(() =>
                    {
                        EditorUtil.Draw.PropertyField(name, "名称", false);
                        DrawTargetSelector(description.FindPropertyRelative("Target"), model);
                        SerializedProperty format = description.FindPropertyRelative("Format");
                        EditorGUI.BeginChangeCheck();
                        EditorUtil.Draw.PropertyField(format, "导出方式", false);
                        if (EditorGUI.EndChangeCheck())
                        {
                            ApplyFormatPreset(description);
                        }
                        EditorUtil.Draw.PropertyField(description.FindPropertyRelative("CodeTargets"), "代码Targets", true);
                        EditorUtil.Draw.PropertyField(description.FindPropertyRelative("DataTargets"), "数据Targets", true);
                        EditorUtil.Draw.PropertyField(description.FindPropertyRelative("OutputScope"), "输出表格范围", false);
                        if ((TableOutputScope)description.FindPropertyRelative("OutputScope").enumValueIndex ==
                            TableOutputScope.SelectedTables)
                        {
                            EditorUtil.Draw.Layout.Indented(() => DrawOutputTablePicker(
                                description.FindPropertyRelative("OutputTables"), model, id.stringValue));
                        }
                        SerializedProperty codeOutputPath = description.FindPropertyRelative("CodeOutputPath");
                        SerializedProperty dataOutputPath = description.FindPropertyRelative("DataOutputPath");
                        bool codePathMissing = enabled.boolValue &&
                                               description.FindPropertyRelative("CodeTargets").arraySize > 0 &&
                                               string.IsNullOrWhiteSpace(codeOutputPath.stringValue);
                        bool dataPathMissing = enabled.boolValue &&
                                               description.FindPropertyRelative("DataTargets").arraySize > 0 &&
                                               string.IsNullOrWhiteSpace(dataOutputPath.stringValue);
                        DrawOutputDirectory(
                            codeOutputPath, "代码输出目录", codePathMissing, "选择代码输出目录");
                        DrawOutputDirectory(
                            dataOutputPath, "数据输出目录", dataPathMissing, "选择数据输出目录");
                        SerializedProperty includeTags = description.FindPropertyRelative("IncludeTags");
                        SerializedProperty excludeTags = description.FindPropertyRelative("ExcludeTags");
                        EditorUtil.Draw.PropertyField(includeTags, "包含Tags清单", true);
                        EditorUtil.Draw.PropertyField(excludeTags, "排除Tags清单", true);
                        if (includeTags.arraySize > 0 && excludeTags.arraySize > 0)
                        {
                            EditorUtil.Draw.HelpBox(MessageType.Error, new[]
                            {
                                "(1) 包含Tags清单与排除Tags清单不能同时配置。",
                            });
                        }
                        EditorUtil.Draw.PropertyField(description.FindPropertyRelative("FieldVariants"), "字段变体", true);
                        EditorUtil.Draw.PropertyField(description.FindPropertyRelative("CustomTemplateDirs"), "自定义模板目录", true);
                        EditorUtil.Draw.PropertyField(description.FindPropertyRelative("AdvancedArguments"), "高级 Luban 参数", true);
                    }, 3);
                }
                finally
                {
                    GUI.contentColor = previousContentColor;
                    EditorGUI.EndDisabledGroup();
                }
            }, GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
        }

        /// <summary>
        /// 使用 luban.conf 中的目标清单绘制 Target 选择器。
        /// </summary>
        /// <param name="targetProperty">Target 字符串属性。</param>
        /// <param name="model">Project 只读模型。</param>
        private static void DrawTargetSelector(SerializedProperty targetProperty, TableProjectModel model)
        {
            string[] targets = model?.Targets?.ToArray() ?? Array.Empty<string>();
            if (targets.Length == 0)
            {
                EditorUtil.Draw.PropertyField(targetProperty, "Target目标", false);
                return;
            }

            int current = Math.Max(0, Array.IndexOf(targets, targetProperty.stringValue));
            int selected = EditorUtil.Draw.Popup("Target目标", current, targets);
            if (targetProperty.stringValue != targets[selected])
            {
                targetProperty.stringValue = targets[selected];
            }
        }

        /// <summary>
        /// 绘制带目录选择按钮的输出目录；必填但为空时标签和输入框统一显红。
        /// </summary>
        /// <param name="property">输出目录属性。</param>
        /// <param name="label">目录标签。</param>
        /// <param name="isMissing">当前目录是否必填但为空。</param>
        /// <param name="panelTitle">文件夹选择面板标题。</param>
        private void DrawOutputDirectory(
            SerializedProperty property,
            string label,
            bool isMissing,
            string panelTitle)
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.StatusTextField(
                    property,
                    label,
                    isMissing ? GetErrorColor() : (Color?)null,
                    true,
                    null,
                    GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Button("选择", 52f, true, () =>
                    EditorUtil.Draw.Panel.SelectFolderDelay(
                        panelTitle, string.Empty, string.Empty, property, Repaint));
            });
        }

        /// <summary>
        /// 绘制按 Excel 或按 Luban 表选择的输出范围；持久化结果始终为 Luban 表全名。
        /// </summary>
        /// <param name="outputTables">输出表全名数组。</param>
        /// <param name="model">Project 只读模型。</param>
        /// <param name="descriptionId">描述唯一标识。</param>
        private void DrawOutputTablePicker(
            SerializedProperty outputTables,
            TableProjectModel model,
            string descriptionId)
        {
            string key = "TableOutputPicker_" + descriptionId;
            int mode = m_TablePickerModes.TryGetValue(key, out int savedMode) ? savedMode : 0;
            mode = EditorUtil.Draw.Popup("选择视图", mode, new[] { "按 Excel", "按 Luban 表" });
            m_TablePickerModes[key] = mode;

            List<string> originalTables = ReadStringArray(outputTables);
            var selected = new HashSet<string>(originalTables, StringComparer.Ordinal);
            if (mode == 0)
            {
                foreach (TableProjectExcelFile excel in model?.ExcelFiles ?? new List<TableProjectExcelFile>())
                {
                    List<string> tables = excel.Sheets.SelectMany(sheet => sheet.TableNames).Distinct().ToList();
                    if (tables.Count == 0) continue;
                    bool allSelected = tables.All(selected.Contains);
                    bool next = EditorUtil.Draw.ToggleLeft(excel.RelativePath, allSelected);
                    if (next != allSelected)
                    {
                        foreach (string table in tables)
                        {
                            if (next) selected.Add(table); else selected.Remove(table);
                        }
                    }
                }
            }
            else
            {
                foreach (var row in EnumerateTableRows(model))
                {
                    bool isSelected = selected.Contains(row.TableName);
                    bool next = EditorUtil.Draw.ToggleLeft(
                        $"{row.TableName}  ({row.ExcelPath} / {row.SheetName})", isSelected);
                    if (next) selected.Add(row.TableName); else selected.Remove(row.TableName);
                }
            }
            string[] nextTables = selected.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!originalTables.SequenceEqual(nextTables))
            {
                WriteStringArray(outputTables, nextTables);
            }
        }

        /// <summary>
        /// 绘制运行时加载描述，Binding 类型保持内部自动解析。
        /// </summary>
        private void DrawLoadDescriptions()
        {
            if (m_LoadDescriptions == null)
            {
                return;
            }

            bool hasAssetAddressError = Enumerable.Range(0, m_LoadDescriptions.arraySize)
                .Any(i => HasMissingAssetAddress(m_LoadDescriptions.GetArrayElementAtIndex(i)));
            bool isOpen = EditorUtil.Draw.ColoredFoldoutHeader(
                $"Luban 加载 ({m_LoadDescriptions.arraySize})",
                "TableLoadDescriptions",
                hasAssetAddressError ? GetErrorColor() : GUI.contentColor,
                () => EditorUtil.Draw.Button("新建", 50f, true, ShowLoadProjectMenu),
                true);
            if (!isOpen)
            {
                return;
            }

            EditorUtil.Draw.Layout.Indented(() =>
            {
                for (int i = 0; i < m_LoadDescriptions.arraySize; i++)
                {
                    DrawLoadDescription(m_LoadDescriptions.GetArrayElementAtIndex(i), i);
                }
            });
        }

        /// <summary>
        /// 绘制单个加载描述的来源选择和 output_data_file 到 Asset 地址映射。
        /// </summary>
        /// <param name="load">加载描述属性。</param>
        /// <param name="index">加载描述索引。</param>
        private void DrawLoadDescription(SerializedProperty load, int index)
        {
            SerializedProperty id = load.FindPropertyRelative("Id");
            SerializedProperty name = load.FindPropertyRelative("Name");
            string title = string.IsNullOrWhiteSpace(name.stringValue) ? $"Luban 加载 {index + 1}" : name.stringValue;
            bool hasAssetAddressError = HasMissingAssetAddress(load);
            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                bool isOpen = EditorUtil.Draw.ColoredFoldoutHeader(
                    title,
                    $"TableLoadDescription_{id.stringValue}_{index}",
                    hasAssetAddressError ? GetErrorColor() : GUI.contentColor,
                    () => EditorUtil.Draw.DangerButton(
                        "删除", 50f, true,
                        () => QueueRemoveLoadDescription(id.stringValue, title)),
                    false);
                if (!isOpen)
                {
                    return;
                }

                EditorUtil.Draw.Layout.Indented(() =>
                {
                    EditorUtil.Draw.PropertyField(name, "名称", false);
                    DrawLoadSourceSelectors(load);
                    DrawAssetAddressList(load, id.stringValue, index, hasAssetAddressError);
                });
            }, GUILayout.MinWidth(0f), GUILayout.ExpandWidth(true));
        }

        /// <summary>
        /// 延迟弹出 Luban 加载删除确认框，避免在当前 Foldout 标题布局中修改数组。
        /// </summary>
        /// <param name="loadId">加载项唯一标识。</param>
        /// <param name="loadName">加载项显示名称。</param>
        private void QueueRemoveLoadDescription(string loadId, string loadName)
        {
            UnityEngine.Object sourceTarget = target;
            serializedObject.ApplyModifiedProperties();
            EditorApplication.delayCall += () =>
            {
                if (this == null || target == null || target != sourceTarget)
                {
                    return;
                }
                ConfirmRemoveLoadDescription(loadId, loadName);
            };
        }

        /// <summary>
        /// 二次确认后按唯一标识删除 Luban 加载；取消或目标已不存在时不修改配置。
        /// </summary>
        /// <param name="loadId">加载项唯一标识。</param>
        /// <param name="loadName">加载项显示名称。</param>
        private void ConfirmRemoveLoadDescription(string loadId, string loadName)
        {
            if (!EditorUtility.DisplayDialog(
                    "删除 Luban 加载",
                    $"确定删除 Luban 加载“{loadName}”吗？\n\n此操作只删除加载配置，不删除导出文件或 Asset。",
                    "删除",
                    "取消"))
            {
                return;
            }

            serializedObject.Update();
            int index = FindLoadDescriptionIndex(loadId);
            if (index < 0)
            {
                return;
            }
            m_LoadDescriptions.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 绘制当前 Luban 加载的 Asset 地址清单，左侧编辑地址，右侧展示对应导出文件路径。
        /// </summary>
        /// <param name="load">Luban 加载属性。</param>
        /// <param name="loadId">加载描述唯一标识。</param>
        /// <param name="index">加载描述索引。</param>
        /// <param name="hasAssetAddressError">是否存在未填写的 Asset 地址。</param>
        private void DrawAssetAddressList(
            SerializedProperty load,
            string loadId,
            int index,
            bool hasAssetAddressError)
        {
            if (!EditorUtil.Draw.ColoredFoldout(
                    "Asset 地址清单",
                    $"TableLoadAssetAddresses_{loadId}_{index}",
                    hasAssetAddressError ? GetErrorColor() : GUI.contentColor,
                    true))
            {
                return;
            }

            SerializedProperty assets = load.FindPropertyRelative("Assets");
            EditorUtil.Draw.Layout.Indented(() =>
            {
                for (int assetIndex = 0; assetIndex < assets.arraySize; assetIndex++)
                {
                    SerializedProperty asset = assets.GetArrayElementAtIndex(assetIndex);
                    string assetPath = asset.FindPropertyRelative("AssetPath").stringValue;
                    SerializedProperty assetAddress = asset.FindPropertyRelative("AssetAddress");
                    Color? rowColor = string.IsNullOrWhiteSpace(assetAddress.stringValue)
                        ? GetErrorColor()
                        : null;
                    EditorUtil.Draw.IndexedTextMappingRow(
                        assetIndex + 1,
                        assetAddress,
                        string.IsNullOrWhiteSpace(assetPath) ? "未解析" : assetPath,
                        true,
                        null,
                        rowColor);
                }

            });
        }

        /// <summary>
        /// 判断加载描述是否存在未填写的 Asset 地址。
        /// </summary>
        /// <param name="load">加载描述属性。</param>
        /// <returns>任一地址为空或全空格时返回 true。</returns>
        private static bool HasMissingAssetAddress(SerializedProperty load)
        {
            SerializedProperty assets = load?.FindPropertyRelative("Assets");
            if (assets == null)
            {
                return false;
            }

            for (int index = 0; index < assets.arraySize; index++)
            {
                SerializedProperty address = assets.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("AssetAddress");
                if (address == null || string.IsNullOrWhiteSpace(address.stringValue))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 修复旧配置中的 AssetPath：工程内路径统一为 Assets 相对路径，无效路径清空但不删除数据项。
        /// </summary>
        private void RepairSerializedAssetPaths()
        {
            if (m_LoadDescriptions == null)
            {
                return;
            }

            int repairedCount = 0;
            int clearedCount = 0;
            for (int loadIndex = 0; loadIndex < m_LoadDescriptions.arraySize; loadIndex++)
            {
                SerializedProperty assets = m_LoadDescriptions.GetArrayElementAtIndex(loadIndex)
                    .FindPropertyRelative("Assets");
                for (int assetIndex = 0; assetIndex < assets.arraySize; assetIndex++)
                {
                    SerializedProperty assetPathProperty = assets.GetArrayElementAtIndex(assetIndex)
                        .FindPropertyRelative("AssetPath");
                    string currentPath = assetPathProperty.stringValue;
                    if (string.IsNullOrWhiteSpace(currentPath))
                    {
                        continue;
                    }

                    if (EditorUtil.FileSystem.TryGetProjectAssetPath(currentPath, out string relativePath))
                    {
                        if (!string.Equals(currentPath, relativePath, StringComparison.Ordinal))
                        {
                            assetPathProperty.stringValue = relativePath;
                            repairedCount++;
                        }
                    }
                    else
                    {
                        assetPathProperty.stringValue = string.Empty;
                        clearedCount++;
                    }
                }
            }

            if (repairedCount == 0 && clearedCount == 0)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
            if (clearedCount > 0)
            {
                Log.Warning(LogTag.Editor,
                    "Table 旧配置中存在 {0} 个无效 AssetPath，已清空但保留对应 DataFile 配置项。",
                    clearedCount);
            }
            Log.Debug(LogTag.Editor, "Table AssetPath 迁移完成：转换 {0} 个，清空 {1} 个。", repairedCount, clearedCount);
        }

        /// <summary>
        /// 绘制加载描述关联的 Project、导出描述和运行时数据 Target。
        /// </summary>
        /// <param name="load">加载描述属性。</param>
        private void DrawLoadSourceSelectors(SerializedProperty load)
        {
            string[] projectNames = Enumerable.Range(0, m_Projects.arraySize)
                .Select(i => m_Projects.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue)
                .ToArray();
            if (projectNames.Length == 0)
            {
                EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "(1) 请先创建或添加工程。" });
                return;
            }

            SerializedProperty projectId = load.FindPropertyRelative("ProjectId");
            int projectIndex = FindProjectIndex(projectId.stringValue);
            int nextProject = EditorUtil.Draw.Popup("工程", Math.Max(0, projectIndex), projectNames);
            SerializedProperty project = m_Projects.GetArrayElementAtIndex(nextProject);
            string nextProjectId = project.FindPropertyRelative("Id").stringValue;
            bool sourceChanged = projectId.stringValue != nextProjectId;
            projectId.stringValue = nextProjectId;

            SerializedProperty descriptions = project.FindPropertyRelative("ExportDescriptions");
            string[] descriptionNames = Enumerable.Range(0, descriptions.arraySize)
                .Select(i => descriptions.GetArrayElementAtIndex(i).FindPropertyRelative("Name").stringValue)
                .ToArray();
            if (descriptionNames.Length == 0)
            {
                EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "(1) 当前工程没有导出描述。" });
                return;
            }

            SerializedProperty descriptionId = load.FindPropertyRelative("ExportDescriptionId");
            int descriptionIndex = FindDescriptionIndex(descriptions, descriptionId.stringValue);
            int nextDescription = EditorUtil.Draw.Popup("导出描述", Math.Max(0, descriptionIndex), descriptionNames);
            SerializedProperty description = descriptions.GetArrayElementAtIndex(nextDescription);
            string nextDescriptionId = description.FindPropertyRelative("Id").stringValue;
            sourceChanged |= descriptionId.stringValue != nextDescriptionId;
            descriptionId.stringValue = nextDescriptionId;

            SerializedProperty dataTargets = description.FindPropertyRelative("DataTargets");
            string[] targetNames = ReadStringArray(dataTargets).ToArray();
            SerializedProperty runtimeTarget = load.FindPropertyRelative("RuntimeDataTarget");
            if (targetNames.Length > 0)
            {
                int currentTarget = Math.Max(0, Array.IndexOf(targetNames, runtimeTarget.stringValue));
                int nextTarget = EditorUtil.Draw.Popup("运行时数据Target", currentTarget, targetNames);
                sourceChanged |= runtimeTarget.stringValue != targetNames[nextTarget];
                runtimeTarget.stringValue = targetNames[nextTarget];
            }
            else
            {
                EditorUtil.Draw.PropertyField(runtimeTarget, "运行时数据Target", false);
            }

            TableProjectModel model = GetProjectModel(
                nextProjectId, project.FindPropertyRelative("ConfigPath").stringValue);
            if (model.BindingTypeByTarget.TryGetValue(
                    description.FindPropertyRelative("Target").stringValue, out string bindingType))
            {
                load.FindPropertyRelative("ResolvedBindingTypeName").stringValue = bindingType;
            }
            if (sourceChanged)
            {
                RefreshLoadDescription(load, false);
            }
        }

        /// <summary>
        /// 在应用 Inspector 修改后执行全部已启用导出描述，并刷新加载映射。
        /// </summary>
        /// <param name="export">Table 导出入口。</param>
        private void RunExport(Func<TableSettings, bool> export)
        {
            serializedObject.ApplyModifiedProperties();
            TableSettings settings = GetTableSettings();
            if (settings != null && export(settings))
            {
                serializedObject.Update();
                RefreshAllLoadDescriptions();
                serializedObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                serializedObject.Update();
            }
        }

        /// <summary>
        /// Play Mode 下显示全部加载描述构建出的实际表数量。
        /// </summary>
        private void DrawRuntimeInfos()
        {
            if (!EditorApplication.isPlaying) return;
            TableComponent component = (TableComponent)target;
            string status = component.IsLoadOver ? "已加载" : "未完成";
            m_RuntimeTablesFoldout = EditorUtil.Draw.Foldout(
                ref m_RuntimeTablesFoldout, $"运行时表 ({component.Count}) [{status}]", false);
            if (m_RuntimeTablesFoldout)
            {
                EditorUtil.Draw.Layout.Indented(() => EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                    {
                        "(1) 当前数量来自已加载或直接注册的 Luban Tables 容器。",
                    }));
            }
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 刷新全部 Project 的只读配置与 Excel 模型。
        /// </summary>
        private void RefreshProjectModels()
        {
            m_ProjectModels.Clear();
            m_ProjectConfigStates.Clear();
            if (m_Projects != null)
            {
                List<string> configPaths = ReadProjectConfigPaths();
                for (int i = 0; i < m_Projects.arraySize; i++)
                {
                    SerializedProperty project = m_Projects.GetArrayElementAtIndex(i);
                    string projectId = project.FindPropertyRelative("Id").stringValue;
                    string configPath = project.FindPropertyRelative("ConfigPath").stringValue;
                    if (!string.IsNullOrWhiteSpace(projectId))
                    {
                        m_ProjectConfigStates[projectId] = TableProjectConfigStatusResolver.Resolve(
                            configPath, configPaths).State;
                    }
                    RefreshProjectModel(
                        projectId,
                        configPath);
                }
            }
            SyncProjectFileWatchers();
        }

        /// <summary>
        /// Excel 或工程数据目录发生变化时重建全部工程清单并刷新 Inspector。
        /// </summary>
        private void OnProjectFilesChanged()
        {
            RefreshProjectModels();
        }

        /// <summary>
        /// 按当前 Project 模型重新注册数据目录及全部现有子目录监听。
        /// </summary>
        private void SyncProjectFileWatchers()
        {
            ClearProjectFileWatchers();
            if (m_ProjectFileWatcherCallback == null)
            {
                return;
            }

            IEnumerable<string> dataDirectories = m_ProjectModels.Values
                .SelectMany(model => TableProjectWatchDirectoryResolver.Collect(model.DataDirectory));
            IEnumerable<string> configDirectories = ReadProjectConfigPaths()
                .Select(ResolveExistingConfigDirectory)
                .Where(directory => !string.IsNullOrWhiteSpace(directory));
            foreach (string directory in dataDirectories
                         .Concat(configDirectories)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                EditorUtil.FileWatcher.Watch(directory, m_ProjectFileWatcherCallback);
                m_WatchedProjectDirectories.Add(directory);
            }
        }

        /// <summary>
        /// 取消当前 Inspector 注册的全部工程目录监听。
        /// </summary>
        private void ClearProjectFileWatchers()
        {
            if (m_ProjectFileWatcherCallback != null)
            {
                foreach (string directory in m_WatchedProjectDirectories)
                {
                    EditorUtil.FileWatcher.Unwatch(directory, m_ProjectFileWatcherCallback);
                }
            }
            m_WatchedProjectDirectories.Clear();
        }

        /// <summary>
        /// 返回配置文件路径对应的现有父目录，供缺失配置文件的创建与恢复监听使用。
        /// </summary>
        /// <param name="configPath">配置文件路径。</param>
        /// <returns>规范化的现有父目录；无法解析时返回空字符串。</returns>
        private static string ResolveExistingConfigDirectory(string configPath)
        {
            if (string.IsNullOrWhiteSpace(configPath))
            {
                return string.Empty;
            }

            try
            {
                string directory = IOPath.GetDirectoryName(IOPath.GetFullPath(configPath));
                return !string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)
                    ? NormalizePath(directory)
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 读取全部工程当前填写的 Luban 配置路径，用于重复检测。
        /// </summary>
        private List<string> ReadProjectConfigPaths()
        {
            var paths = new List<string>();
            if (m_Projects == null)
            {
                return paths;
            }

            for (int i = 0; i < m_Projects.arraySize; i++)
            {
                paths.Add(m_Projects.GetArrayElementAtIndex(i)
                    .FindPropertyRelative("ConfigPath").stringValue);
            }
            return paths;
        }

        /// <summary>
        /// 返回工程配置状态对应的 Inspector 颜色。
        /// </summary>
        private static Color GetProjectStatusColor(TableProjectConfigState state)
        {
            switch (state)
            {
                case TableProjectConfigState.Valid:
                    return Color.white;
                case TableProjectConfigState.Missing:
                    return new Color(1f, 0.35f, 0.3f);
                case TableProjectConfigState.Duplicate:
                    return new Color(1f, 0.75f, 0.15f);
                default:
                    return Color.white;
            }
        }

        /// <summary>
        /// 返回 Inspector 配置错误统一使用的红色。
        /// </summary>
        /// <returns>错误状态色。</returns>
        private static Color GetErrorColor()
        {
            return new Color(1f, 0.35f, 0.3f);
        }

        /// <summary>
        /// 判断工程下是否存在需要向上汇总的红色错误。
        /// </summary>
        /// <param name="project">工程序列化属性。</param>
        /// <param name="status">配置文件状态。</param>
        /// <param name="model">工程只读模型。</param>
        /// <returns>配置缺失、Schema 缺失或导出目录错误时返回 true。</returns>
        private static bool HasProjectError(
            SerializedProperty project,
            TableProjectConfigStatus status,
            TableProjectModel model)
        {
            return status.State == TableProjectConfigState.Missing ||
                   HasMissingSchemaFile(model) ||
                   HasAnyExportOutputPathError(project?.FindPropertyRelative("ExportDescriptions"));
        }

        /// <summary>
        /// 判断工程是否没有可用 Schema 文件。
        /// </summary>
        /// <param name="model">工程只读模型。</param>
        /// <returns>Schema 清单为空或任一文件不存在时返回 true。</returns>
        private static bool HasMissingSchemaFile(TableProjectModel model)
        {
            return model == null || model.SchemaFiles.Count == 0 ||
                   model.SchemaFiles.Any(path => string.IsNullOrWhiteSpace(path) || !File.Exists(path));
        }

        /// <summary>
        /// 判断全部导出描述中是否存在已启用且缺少必需输出目录的条目。
        /// </summary>
        /// <param name="descriptions">导出描述数组。</param>
        /// <returns>存在输出目录错误时返回 true。</returns>
        private static bool HasAnyExportOutputPathError(SerializedProperty descriptions)
        {
            if (descriptions == null)
            {
                return false;
            }
            for (int index = 0; index < descriptions.arraySize; index++)
            {
                if (HasExportOutputPathError(descriptions.GetArrayElementAtIndex(index)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 判断单个已启用导出描述是否缺少与代码或数据 Targets 对应的输出目录。
        /// </summary>
        /// <param name="description">导出描述属性。</param>
        /// <returns>缺少必需输出目录时返回 true。</returns>
        private static bool HasExportOutputPathError(SerializedProperty description)
        {
            if (description == null || !description.FindPropertyRelative("Enabled").boolValue)
            {
                return false;
            }

            bool codePathMissing = description.FindPropertyRelative("CodeTargets").arraySize > 0 &&
                                   string.IsNullOrWhiteSpace(
                                       description.FindPropertyRelative("CodeOutputPath").stringValue);
            bool dataPathMissing = description.FindPropertyRelative("DataTargets").arraySize > 0 &&
                                   string.IsNullOrWhiteSpace(
                                       description.FindPropertyRelative("DataOutputPath").stringValue);
            return codePathMissing || dataPathMissing;
        }

        /// <summary>
        /// 返回导出描述启用状态对应的 Foldout 标题颜色。
        /// </summary>
        /// <param name="enabled">是否参与批量导出。</param>
        /// <param name="hasOutputPathError">是否缺少必需输出目录。</param>
        /// <returns>错误时为红色，正常启用时为高亮色，未启用时为灰色。</returns>
        private static Color GetExportDescriptionColor(bool enabled, bool hasOutputPathError)
        {
            if (hasOutputPathError)
            {
                return GetErrorColor();
            }
            return enabled ? Color.white : Color.gray;
        }

        /// <summary>
        /// 刷新指定 Project 模型并重绘 Inspector。
        /// </summary>
        /// <param name="projectId">Project 唯一标识。</param>
        /// <param name="configPath">luban.conf 路径。</param>
        private void RefreshProjectModel(string projectId, string configPath)
        {
            if (!string.IsNullOrWhiteSpace(projectId))
            {
                m_ProjectModels[projectId] = TableProjectModelBuilder.Build(configPath);
            }
            Repaint();
        }

        /// <summary>
        /// 获取 Project 模型；路径或配置状态改变时自动重建。
        /// </summary>
        /// <param name="projectId">Project 唯一标识。</param>
        /// <param name="configPath">luban.conf 路径。</param>
        /// <param name="configState">当前配置状态；为空时不参与缓存判断。</param>
        /// <returns>Project 模型。</returns>
        private TableProjectModel GetProjectModel(
            string projectId,
            string configPath,
            TableProjectConfigState? configState = null)
        {
            bool configStateChanged = configState.HasValue &&
                                      (!m_ProjectConfigStates.TryGetValue(
                                           projectId, out TableProjectConfigState cachedState) ||
                                       cachedState != configState.Value);
            if (!m_ProjectModels.TryGetValue(projectId, out TableProjectModel model) ||
                !string.Equals(model.ConfigPath, NormalizePath(configPath), StringComparison.OrdinalIgnoreCase) ||
                configStateChanged)
            {
                model = TableProjectModelBuilder.Build(configPath);
                m_ProjectModels[projectId] = model;
                if (configState.HasValue)
                {
                    m_ProjectConfigStates[projectId] = configState.Value;
                }
                SyncProjectFileWatchers();
            }
            return model;
        }

        /// <summary>
        /// 新建最小可运行的 Luban Project 并加入引用列表。
        /// </summary>
        private void CreateProject()
        {
            string folder = EditorUtil.Draw.Panel.SelectFolder("选择 Luban 工程目录");
            if (string.IsNullOrWhiteSpace(folder)) return;
            string configPath = NormalizePath(IOPath.Combine(folder, "luban.conf"));
            string schemaPath = NormalizePath(IOPath.Combine(folder, "tables.xml"));
            if (!File.Exists(configPath))
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(configPath,
                    "{\n  \"dataDir\": \".\",\n  \"schemaFiles\": [{ \"fileName\": \"tables.xml\", \"type\": \"\" }],\n  \"targets\": [{ \"name\": \"table\", \"manager\": \"Tables\", \"groups\": [\"c\"], \"topModule\": \"Game\" }],\n  \"groups\": [{ \"names\": [\"c\"], \"default\": true }]\n}\n");
                File.WriteAllText(schemaPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<module>\n</module>\n");
                AssetDatabase.Refresh();
            }
            AppendProject(IOPath.GetFileName(folder), configPath);
        }

        /// <summary>
        /// 添加一个空 Project 引用并打开 luban.conf 选择器。
        /// </summary>
        private void AddExistingProject()
        {
            int index = AppendProject("Luban 工程", string.Empty);
            SerializedProperty project = m_Projects.GetArrayElementAtIndex(index);
            string projectId = project.FindPropertyRelative("Id").stringValue;
            SerializedProperty config = project.FindPropertyRelative("ConfigPath");
            EditorUtil.Draw.Panel.SelectFileDelay("选择配置文件", string.Empty, "conf", config,
                () => CompleteExistingProjectSelection(projectId));
        }

        /// <summary>
        /// 完成已有 Project 选择；用户取消时移除刚创建的空引用。
        /// </summary>
        /// <param name="projectId">待完成的 Project 标识。</param>
        private void CompleteExistingProjectSelection(string projectId)
        {
            serializedObject.Update();
            int index = FindProjectIndex(projectId);
            if (index >= 0 && string.IsNullOrWhiteSpace(
                    m_Projects.GetArrayElementAtIndex(index).FindPropertyRelative("ConfigPath").stringValue))
            {
                m_Projects.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
            }
            else if (index >= 0)
            {
                SerializedProperty project = m_Projects.GetArrayElementAtIndex(index);
                SerializedProperty name = project.FindPropertyRelative("Name");
                if (name.stringValue == "Luban 工程")
                {
                    string configPath = project.FindPropertyRelative("ConfigPath").stringValue;
                    name.stringValue = IOPath.GetFileName(IOPath.GetDirectoryName(configPath));
                    serializedObject.ApplyModifiedProperties();
                }
            }
            RefreshProjectModels();
        }

        /// <summary>
        /// 向序列化数组追加 Project 引用并初始化唯一标识与默认描述。
        /// </summary>
        /// <param name="name">显示名称。</param>
        /// <param name="configPath">luban.conf 路径。</param>
        /// <returns>新 Project 索引。</returns>
        private int AppendProject(string name, string configPath)
        {
            int index = m_Projects.arraySize;
            m_Projects.InsertArrayElementAtIndex(index);
            SerializedProperty project = m_Projects.GetArrayElementAtIndex(index);
            project.FindPropertyRelative("Id").stringValue = Guid.NewGuid().ToString("N");
            project.FindPropertyRelative("Name").stringValue = string.IsNullOrWhiteSpace(name) ? "Luban 工程" : name;
            project.FindPropertyRelative("ConfigPath").stringValue = NormalizePath(configPath);
            SerializedProperty descriptions = project.FindPropertyRelative("ExportDescriptions");
            descriptions.ClearArray();
            serializedObject.ApplyModifiedProperties();
            RefreshProjectModels();
            return index;
        }

        /// <summary>
        /// 记录复制所需的普通值，并把文件选择与配置变更延迟到当前 IMGUI 绘制结束后。
        /// </summary>
        /// <param name="sourceProject">源 Project 属性。</param>
        private void QueueCopyProject(SerializedProperty sourceProject)
        {
            string sourceConfig = sourceProject.FindPropertyRelative("ConfigPath").stringValue;
            string sourceId = sourceProject.FindPropertyRelative("Id").stringValue;
            string sourceName = sourceProject.FindPropertyRelative("Name").stringValue;
            UnityEngine.Object sourceTarget = target;
            serializedObject.ApplyModifiedProperties();
            EditorApplication.delayCall += () =>
            {
                if (this == null || target == null || target != sourceTarget)
                {
                    return;
                }

                CopyProject(sourceId, sourceName, sourceConfig);
            };
        }

        /// <summary>
        /// 深复制 Project 配置目录和 Inspector 导出描述，外部引用保持原路径。
        /// </summary>
        /// <param name="sourceId">源 Project 唯一标识。</param>
        /// <param name="sourceName">源 Project 显示名称。</param>
        /// <param name="sourceConfig">源 luban.conf 路径。</param>
        private void CopyProject(string sourceId, string sourceName, string sourceConfig)
        {
            string sourceDirectory = IOPath.GetDirectoryName(sourceConfig);
            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                EditorUtility.DisplayDialog("复制 Luban 工程", "源工程目录不存在。", "确定");
                return;
            }

            string parent = EditorUtil.Draw.Panel.SelectFolder("选择复制目标父目录");
            if (string.IsNullOrWhiteSpace(parent)) return;
            string destination = NormalizePath(IOPath.Combine(parent, sourceName + " Copy"));
            if (Directory.Exists(destination))
            {
                EditorUtility.DisplayDialog("复制 Luban 工程", $"目标目录已存在：{destination}", "确定");
                return;
            }

            TableSettings settings = GetTableSettings();
            TableLubanProjectSetting source = settings?.Projects?.Find(item => item != null && item.Id == sourceId);
            if (source == null)
            {
                EditorUtility.DisplayDialog("复制 Luban 工程", "无法读取源工程配置。", "确定");
                return;
            }

            CopyDirectory(sourceDirectory, destination);
            TableLubanProjectSetting copy = JsonUtility.FromJson<TableLubanProjectSetting>(JsonUtility.ToJson(source));
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = sourceName + " Copy";
            copy.ConfigPath = NormalizePath(IOPath.Combine(destination, IOPath.GetFileName(sourceConfig)));
            foreach (TableExportDescriptionSetting description in copy.ExportDescriptions)
            {
                description.Id = Guid.NewGuid().ToString("N");
            }
            settings.Projects.Add(copy);
            EditorUtility.SetDirty(target);
            serializedObject.Update();
            AssetDatabase.Refresh();
            RefreshProjectModels();
            EditorUtility.DisplayDialog("复制 Luban 工程",
                "工程目录已复制。luban.conf 中指向目录外的引用仍保持原路径，请按需要检查。", "确定");
        }

        /// <summary>
        /// 延迟弹出 Luban 工程删除方式确认框，避免在当前 IMGUI 布局过程中修改数组或磁盘目录。
        /// </summary>
        /// <param name="project">待删除的工程属性。</param>
        private void QueueRemoveProject(SerializedProperty project)
        {
            string projectId = project.FindPropertyRelative("Id").stringValue;
            string projectName = project.FindPropertyRelative("Name").stringValue;
            string configPath = project.FindPropertyRelative("ConfigPath").stringValue;
            UnityEngine.Object sourceTarget = target;
            serializedObject.ApplyModifiedProperties();
            EditorApplication.delayCall += () =>
            {
                if (this == null || target == null || target != sourceTarget)
                {
                    return;
                }
                ConfirmRemoveProject(projectId, projectName, configPath);
            };
        }

        /// <summary>
        /// 询问仅删除引用或同时删除明确引用的配置与 Schema 文件，并在磁盘操作成功后移除工程引用。
        /// </summary>
        /// <param name="projectId">工程唯一标识。</param>
        /// <param name="projectName">工程显示名称。</param>
        /// <param name="configPath">工程配置文件路径。</param>
        private void ConfirmRemoveProject(string projectId, string projectName, string configPath)
        {
            string displayFiles = ResolveProjectFilesForDisplay(configPath);
            int choice = EditorUtility.DisplayDialogComplex(
                "删除 Luban 工程",
                $"请选择删除方式：\n\n工程：{projectName}\n\n将从磁盘删除的明确文件：\n{displayFiles}\n\n" +
                "不会删除 Excel、目录或任何未被当前配置明确引用的文件。" +
                "Assets 内文件会移到废纸篓，外部文件会永久删除。",
                "仅删除引用",
                "取消",
                "从磁盘删除");
            if (choice == 1)
            {
                return;
            }
            if (choice == 2 && !TryDeleteProjectFiles(configPath, out string error))
            {
                EditorUtility.DisplayDialog("删除 Luban 工程失败", error, "确定");
                return;
            }

            serializedObject.Update();
            int index = FindProjectIndex(projectId);
            if (index < 0)
            {
                return;
            }
            m_Projects.DeleteArrayElementAtIndex(index);
            serializedObject.ApplyModifiedProperties();
            RefreshProjectModels();
        }

        /// <summary>
        /// 返回删除确认框使用的精确文件清单；配置无法解析时显示错误说明。
        /// </summary>
        /// <param name="configPath">工程配置文件路径。</param>
        /// <returns>逐行显示的配置与 Schema 文件路径。</returns>
        private static string ResolveProjectFilesForDisplay(string configPath)
        {
            try
            {
                return string.Join("\n", TableProjectModelBuilder.ResolveExplicitDeletionFiles(configPath)
                    .Select(path => "- " + NormalizePath(path)));
            }
            catch (Exception exception)
            {
                return "（无法解析删除清单：" + exception.Message + "）";
            }
        }

        /// <summary>
        /// 逐个删除当前配置明确引用的 Schema 文件与配置文件，不扫描或删除任何目录。
        /// </summary>
        /// <param name="configPath">工程配置文件路径。</param>
        /// <param name="error">删除失败原因。</param>
        /// <returns>全部现存目标文件删除成功时返回 true。</returns>
        private static bool TryDeleteProjectFiles(string configPath, out string error)
        {
            error = string.Empty;
            try
            {
                List<string> files = TableProjectModelBuilder.ResolveExplicitDeletionFiles(configPath);
                foreach (string file in files)
                {
                    if (!File.Exists(file))
                    {
                        continue;
                    }

                    string assetPath = FileUtil.GetProjectRelativePath(file);
                    if (!string.IsNullOrWhiteSpace(assetPath) &&
                        assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                    {
                        if (!AssetDatabase.MoveAssetToTrash(assetPath))
                        {
                            error = $"无法将文件移到废纸篓：\n{NormalizePath(file)}";
                            return false;
                        }
                    }
                    else
                    {
                        File.Delete(file);
                    }
                }
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception exception)
            {
                error = $"删除工程文件失败：\n{exception.Message}";
                return false;
            }
        }

        /// <summary>
        /// 弹出五种内置导出方式菜单，用户选中后才创建对应导出描述。
        /// </summary>
        /// <param name="projectId">目标工程唯一标识。</param>
        private void ShowExportFormatMenu(string projectId)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("JSON"), false,
                () => AddExportDescription(projectId, TableExportFormat.Json));
            menu.AddItem(new GUIContent("Binary"), false,
                () => AddExportDescription(projectId, TableExportFormat.Binary));
            menu.AddItem(new GUIContent("Protobuf Binary"), false,
                () => AddExportDescription(projectId, TableExportFormat.ProtobufBinary));
            menu.AddItem(new GUIContent("Protobuf JSON"), false,
                () => AddExportDescription(projectId, TableExportFormat.ProtobufJson));
            menu.AddItem(new GUIContent("MsgPack"), false,
                () => AddExportDescription(projectId, TableExportFormat.MsgPack));
            menu.ShowAsContext();
        }

        /// <summary>
        /// 向指定工程新增所选格式的导出描述，并应用对应代码与数据 Target 预设。
        /// </summary>
        /// <param name="projectId">目标工程唯一标识。</param>
        /// <param name="format">用户选择的导出方式。</param>
        private void AddExportDescription(string projectId, TableExportFormat format)
        {
            serializedObject.Update();
            int projectIndex = FindProjectIndex(projectId);
            if (projectIndex < 0)
            {
                return;
            }

            SerializedProperty project = m_Projects.GetArrayElementAtIndex(projectIndex);
            SerializedProperty descriptions = project.FindPropertyRelative("ExportDescriptions");
            TableProjectModel model = GetProjectModel(
                projectId, project.FindPropertyRelative("ConfigPath").stringValue);
            int index = descriptions.arraySize;
            descriptions.InsertArrayElementAtIndex(index);
            SerializedProperty description = descriptions.GetArrayElementAtIndex(index);
            description.FindPropertyRelative("Id").stringValue = Guid.NewGuid().ToString("N");
            description.FindPropertyRelative("Name").stringValue = GetExportFormatName(format);
            description.FindPropertyRelative("Enabled").boolValue = false;
            description.FindPropertyRelative("Target").stringValue = model?.Targets.FirstOrDefault() ?? "table";
            description.FindPropertyRelative("Format").enumValueIndex = (int)format;
            description.FindPropertyRelative("OutputScope").enumValueIndex = (int)TableOutputScope.AllTables;
            ClearDescriptionCollections(description);
            ApplyFormatPreset(description);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 返回导出方式在 Inspector 中使用的标准名称。
        /// </summary>
        /// <param name="format">导出方式。</param>
        /// <returns>与五选菜单一致的显示名称。</returns>
        private static string GetExportFormatName(TableExportFormat format)
        {
            switch (format)
            {
                case TableExportFormat.Binary:
                    return "Binary";
                case TableExportFormat.ProtobufBinary:
                    return "Protobuf Binary";
                case TableExportFormat.ProtobufJson:
                    return "Protobuf JSON";
                case TableExportFormat.MsgPack:
                    return "MsgPack";
                default:
                    return "JSON";
            }
        }

        /// <summary>
        /// 延迟弹出导出描述删除确认框，避免在当前 Foldout 标题布局中修改数组。
        /// </summary>
        /// <param name="projectId">所属工程唯一标识。</param>
        /// <param name="descriptionId">导出描述唯一标识。</param>
        /// <param name="descriptionName">导出描述显示名称。</param>
        private void QueueRemoveExportDescription(
            string projectId,
            string descriptionId,
            string descriptionName)
        {
            UnityEngine.Object sourceTarget = target;
            serializedObject.ApplyModifiedProperties();
            EditorApplication.delayCall += () =>
            {
                if (this == null || target == null || target != sourceTarget)
                {
                    return;
                }
                ConfirmRemoveExportDescription(projectId, descriptionId, descriptionName);
            };
        }

        /// <summary>
        /// 二次确认后按唯一标识删除导出描述；取消或目标已不存在时不修改配置。
        /// </summary>
        /// <param name="projectId">所属工程唯一标识。</param>
        /// <param name="descriptionId">导出描述唯一标识。</param>
        /// <param name="descriptionName">导出描述显示名称。</param>
        private void ConfirmRemoveExportDescription(
            string projectId,
            string descriptionId,
            string descriptionName)
        {
            if (!EditorUtility.DisplayDialog(
                    "删除导出描述",
                    $"确定删除导出描述“{descriptionName}”吗？\n\n此操作只删除当前 Inspector 配置。",
                    "删除",
                    "取消"))
            {
                return;
            }

            serializedObject.Update();
            int projectIndex = FindProjectIndex(projectId);
            if (projectIndex < 0)
            {
                return;
            }
            SerializedProperty descriptions = m_Projects.GetArrayElementAtIndex(projectIndex)
                .FindPropertyRelative("ExportDescriptions");
            int descriptionIndex = FindDescriptionIndex(descriptions, descriptionId);
            if (descriptionIndex < 0)
            {
                return;
            }
            descriptions.DeleteArrayElementAtIndex(descriptionIndex);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 根据五种内置方式更新代码与数据 Targets，不限制用户随后手工修改。
        /// </summary>
        /// <param name="description">导出描述属性。</param>
        private static void ApplyFormatPreset(SerializedProperty description)
        {
            TableExportFormat format = (TableExportFormat)description.FindPropertyRelative("Format").enumValueIndex;
            string[] codeTargets;
            string[] dataTargets;
            switch (format)
            {
                case TableExportFormat.Binary:
                    codeTargets = new[] { "cs-bin" };
                    dataTargets = new[] { "bin" };
                    break;
                case TableExportFormat.ProtobufBinary:
                    codeTargets = new[] { "protobuf3", "cs-newtonsoft-json" };
                    dataTargets = new[] { "protobuf3-bin" };
                    break;
                case TableExportFormat.ProtobufJson:
                    codeTargets = new[] { "protobuf3", "cs-newtonsoft-json" };
                    dataTargets = new[] { "protobuf3-json" };
                    break;
                case TableExportFormat.MsgPack:
                    codeTargets = new[] { "cs-newtonsoft-json" };
                    dataTargets = new[] { "msgpack" };
                    break;
                default:
                    codeTargets = new[] { "cs-newtonsoft-json" };
                    dataTargets = new[] { "json" };
                    break;
            }
            WriteStringArray(description.FindPropertyRelative("CodeTargets"), codeTargets);
            WriteStringArray(description.FindPropertyRelative("DataTargets"), dataTargets);
        }

        /// <summary>
        /// 弹出当前 Luban 工程选择菜单，用户选中工程后才创建加载项。
        /// </summary>
        private void ShowLoadProjectMenu()
        {
            serializedObject.Update();
            if (m_Projects == null || m_Projects.arraySize == 0)
            {
                EditorApplication.delayCall += () => EditorUtility.DisplayDialog(
                    "新建 Luban 加载", "当前没有可选择的 Luban 工程。", "确定");
                return;
            }

            string[] projectNames = Enumerable.Range(0, m_Projects.arraySize)
                .Select(index => m_Projects.GetArrayElementAtIndex(index)
                    .FindPropertyRelative("Name").stringValue)
                .ToArray();
            var duplicateNames = new HashSet<string>(projectNames
                .GroupBy(value => value ?? string.Empty, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key), StringComparer.Ordinal);
            var menu = new GenericMenu();
            for (int index = 0; index < m_Projects.arraySize; index++)
            {
                SerializedProperty project = m_Projects.GetArrayElementAtIndex(index);
                string projectId = project.FindPropertyRelative("Id").stringValue;
                string projectName = projectNames[index];
                string displayName = string.IsNullOrWhiteSpace(projectName) ? $"工程 {index + 1}" : projectName;
                if (duplicateNames.Contains(projectName ?? string.Empty))
                {
                    displayName += $" ({index + 1})";
                }
                menu.AddItem(new GUIContent(displayName), false, () => AddLoadDescription(projectId));
            }
            menu.ShowAsContext();
        }

        /// <summary>
        /// 为用户选择的工程创建 Luban 加载，并默认关联该工程的首个导出描述与数据 Target。
        /// </summary>
        /// <param name="projectId">用户选择的工程唯一标识。</param>
        private void AddLoadDescription(string projectId)
        {
            serializedObject.Update();
            int projectIndex = FindProjectIndex(projectId);
            if (projectIndex < 0)
            {
                return;
            }

            SerializedProperty project = m_Projects.GetArrayElementAtIndex(projectIndex);
            SerializedProperty descriptions = project.FindPropertyRelative("ExportDescriptions");
            string descriptionId = string.Empty;
            string runtimeDataTarget = string.Empty;
            if (descriptions.arraySize > 0)
            {
                SerializedProperty description = descriptions.GetArrayElementAtIndex(0);
                descriptionId = description.FindPropertyRelative("Id").stringValue;
                SerializedProperty dataTargets = description.FindPropertyRelative("DataTargets");
                if (dataTargets.arraySize > 0)
                {
                    runtimeDataTarget = dataTargets.GetArrayElementAtIndex(0).stringValue;
                }
            }
            AppendLoadDescription("Luban 加载", projectId, descriptionId, runtimeDataTarget);
        }

        /// <summary>
        /// 追加一条已确定来源的 Luban 加载配置。
        /// </summary>
        /// <param name="name">加载项名称。</param>
        /// <param name="projectId">关联工程唯一标识。</param>
        /// <param name="descriptionId">关联导出描述唯一标识。</param>
        /// <param name="runtimeDataTarget">运行时数据 Target。</param>
        /// <param name="applyImmediately">是否立即提交序列化修改。</param>
        private void AppendLoadDescription(
            string name,
            string projectId,
            string descriptionId,
            string runtimeDataTarget,
            bool applyImmediately = true)
        {
            int index = m_LoadDescriptions.arraySize;
            m_LoadDescriptions.InsertArrayElementAtIndex(index);
            SerializedProperty load = m_LoadDescriptions.GetArrayElementAtIndex(index);
            load.FindPropertyRelative("Id").stringValue = Guid.NewGuid().ToString("N");
            load.FindPropertyRelative("Name").stringValue = name;
            load.FindPropertyRelative("ProjectId").stringValue = projectId;
            load.FindPropertyRelative("ExportDescriptionId").stringValue = descriptionId;
            load.FindPropertyRelative("RuntimeDataTarget").stringValue = runtimeDataTarget;
            load.FindPropertyRelative("ResolvedBindingTypeName").stringValue = string.Empty;
            load.FindPropertyRelative("Assets").ClearArray();
            if (applyImmediately)
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// 根据导出目录重建单个加载描述的逻辑文件和 Asset 地址映射。
        /// </summary>
        /// <param name="load">加载描述属性。</param>
        /// <param name="preserveManualAddress">自动解析失败时是否保留已有手工地址。</param>
        private void RefreshLoadDescription(SerializedProperty load, bool preserveManualAddress)
        {
            int projectIndex = FindProjectIndex(load.FindPropertyRelative("ProjectId").stringValue);
            if (projectIndex < 0) return;
            SerializedProperty project = m_Projects.GetArrayElementAtIndex(projectIndex);
            SerializedProperty descriptions = project.FindPropertyRelative("ExportDescriptions");
            int descriptionIndex = FindDescriptionIndex(
                descriptions, load.FindPropertyRelative("ExportDescriptionId").stringValue);
            if (descriptionIndex < 0) return;
            SerializedProperty description = descriptions.GetArrayElementAtIndex(descriptionIndex);
            string outputDirectory = description.FindPropertyRelative("DataOutputPath").stringValue;

            SerializedProperty assets = load.FindPropertyRelative("Assets");
            var oldAddresses = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < assets.arraySize; i++)
            {
                SerializedProperty item = assets.GetArrayElementAtIndex(i);
                oldAddresses[item.FindPropertyRelative("DataFile").stringValue] =
                    item.FindPropertyRelative("AssetAddress").stringValue;
            }

            bool hasOutputDirectory = !string.IsNullOrWhiteSpace(outputDirectory) &&
                                      Directory.Exists(outputDirectory);
            string absoluteOutput = hasOutputDirectory ? IOPath.GetFullPath(outputDirectory) : string.Empty;
            string runtimeDataTarget = load.FindPropertyRelative("RuntimeDataTarget").stringValue;
            string expectedExtension = ResolveBuiltInDataExtension(runtimeDataTarget);
            List<string> candidatePaths = hasOutputDirectory
                ? Directory.GetFiles(absoluteOutput, "*", SearchOption.AllDirectories)
                    .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    .Where(path => string.IsNullOrEmpty(expectedExtension) ||
                                   string.Equals(IOPath.GetExtension(path), expectedExtension,
                                       StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList()
                : new List<string>();
            Dictionary<string, string> pathByDataFile = candidatePaths
                .GroupBy(path => NormalizePath(
                    IOPath.ChangeExtension(IOPath.GetRelativePath(absoluteOutput, path), null)),
                    StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            List<string> bindingDataFiles = ResolveBindingDataFiles(
                load.FindPropertyRelative("ResolvedBindingTypeName").stringValue);
            if (bindingDataFiles.Count == 0 && !hasOutputDirectory)
            {
                return;
            }

            List<string> dataFiles;
            List<string> filePaths;
            if (bindingDataFiles.Count == 0)
            {
                filePaths = candidatePaths;
                dataFiles = candidatePaths
                    .Select(path => NormalizePath(IOPath.ChangeExtension(
                        IOPath.GetRelativePath(absoluteOutput, path), null)))
                    .ToList();
            }
            else
            {
                // Binding 的 DataFiles 是运行时完整契约；即使文件暂时缺失，也必须保留对应配置行。
                dataFiles = bindingDataFiles;
                filePaths = bindingDataFiles
                    .Select(dataFile => pathByDataFile.TryGetValue(dataFile, out string filePath)
                        ? filePath
                        : string.Empty)
                    .ToList();
            }

            var assetPaths = new List<string>(dataFiles.Count);
            int rejectedPathCount = 0;
            foreach (string filePath in filePaths)
            {
                if (string.IsNullOrWhiteSpace(filePath))
                {
                    assetPaths.Add(string.Empty);
                    rejectedPathCount++;
                    continue;
                }

                if (!EditorUtil.FileSystem.TryGetProjectAssetPath(filePath, out string assetPath))
                {
                    assetPaths.Add(string.Empty);
                    rejectedPathCount++;
                    continue;
                }

                assetPaths.Add(assetPath);
            }

            if (rejectedPathCount > 0)
            {
                Log.Warning(LogTag.Editor,
                    "Table 有 {0} 个 DataFile 暂未解析到 Assets 目录下的导出文件；配置项已保留，AssetPath 留空。",
                    rejectedPathCount);
            }

            AssetComponent assetComponent = ((TableComponent)target).transform.root.GetComponentInChildren<AssetComponent>(true);
            Dictionary<string, string> addresses;
            try
            {
                addresses = TableAssetAddressResolver.Resolve(assetComponent, assetPaths);
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.Editor, "Table Asset 地址解析失败：{0}", exception.Message);
                addresses = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            assets.ClearArray();
            for (int i = 0; i < dataFiles.Count; i++)
            {
                string dataFile = dataFiles[i];
                assets.InsertArrayElementAtIndex(i);
                SerializedProperty item = assets.GetArrayElementAtIndex(i);
                string assetPath = NormalizePath(assetPaths[i]);
                item.FindPropertyRelative("DataFile").stringValue = dataFile;
                item.FindPropertyRelative("AssetPath").stringValue = assetPath;
                oldAddresses.TryGetValue(dataFile, out string oldAddress);
                addresses.TryGetValue(assetPath, out string automaticAddress);
                bool keepManual = preserveManualAddress && !string.IsNullOrWhiteSpace(oldAddress) &&
                                  !string.Equals(oldAddress, automaticAddress, StringComparison.Ordinal);
                item.FindPropertyRelative("AssetAddress").stringValue = keepManual
                    ? oldAddress
                    : automaticAddress ?? (preserveManualAddress ? oldAddress : string.Empty);
            }
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 刷新全部现有加载描述；为空时为启用且有数据 Target 的描述创建默认项。
        /// </summary>
        private void RefreshAllLoadDescriptions()
        {
            if (m_LoadDescriptions.arraySize == 0)
            {
                for (int projectIndex = 0; projectIndex < m_Projects.arraySize; projectIndex++)
                {
                    SerializedProperty project = m_Projects.GetArrayElementAtIndex(projectIndex);
                    SerializedProperty descriptions = project.FindPropertyRelative("ExportDescriptions");
                    for (int descriptionIndex = 0; descriptionIndex < descriptions.arraySize; descriptionIndex++)
                    {
                        SerializedProperty description = descriptions.GetArrayElementAtIndex(descriptionIndex);
                        if (!description.FindPropertyRelative("Enabled").boolValue ||
                            description.FindPropertyRelative("DataTargets").arraySize == 0)
                        {
                            continue;
                        }
                        AppendLoadDescription(
                            description.FindPropertyRelative("Name").stringValue,
                            project.FindPropertyRelative("Id").stringValue,
                            description.FindPropertyRelative("Id").stringValue,
                            description.FindPropertyRelative("DataTargets").GetArrayElementAtIndex(0).stringValue,
                            false);
                    }
                }
                serializedObject.ApplyModifiedProperties();
            }

            for (int i = 0; i < m_LoadDescriptions.arraySize; i++)
            {
                RefreshLoadDescription(m_LoadDescriptions.GetArrayElementAtIndex(i), true);
            }
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

        /// <summary>
        /// 打开 luban.conf 文件。
        /// </summary>
        /// <param name="configPath">配置文件路径。</param>
        private static void OpenConfig(string configPath)
        {
            if (File.Exists(configPath)) EditorUtil.FileSystem.OpenFile(configPath);
        }

        /// <summary>
        /// 打开 luban.conf 所在文件夹。
        /// </summary>
        /// <param name="configPath">配置文件路径。</param>
        private static void OpenConfigFolder(string configPath)
        {
            string folder = IOPath.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                EditorUtil.FileSystem.OpenFolder(folder);
            }
        }

        /// <summary>
        /// 打开 Schema 文件。
        /// </summary>
        /// <param name="schemaPath">Schema 文件路径。</param>
        private static void OpenSchemaFile(string schemaPath)
        {
            if (File.Exists(schemaPath))
            {
                EditorUtil.FileSystem.OpenFile(schemaPath);
            }
        }

        /// <summary>
        /// 打开 Schema 文件所在文件夹。
        /// </summary>
        /// <param name="schemaPath">Schema 文件路径。</param>
        private static void OpenSchemaFolder(string schemaPath)
        {
            string folder = IOPath.GetDirectoryName(schemaPath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                EditorUtil.FileSystem.OpenFolder(folder);
            }
        }

        /// <summary>
        /// 打开 Excel 文件。
        /// </summary>
        /// <param name="excelPath">Excel 绝对路径。</param>
        private static void OpenExcel(string excelPath)
        {
            if (File.Exists(excelPath))
            {
                EditorUtil.FileSystem.OpenFile(excelPath);
            }
        }

        /// <summary>
        /// 打开 Excel 文件所在目录。
        /// </summary>
        /// <param name="excelPath">Excel 绝对路径。</param>
        private static void OpenExcelFolder(string excelPath)
        {
            string folder = IOPath.GetDirectoryName(excelPath);
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                EditorUtil.FileSystem.OpenFolder(folder);
            }
        }

        /// <summary>
        /// 递归复制目录并保留内部相对结构；跳过 Unity meta，避免复制出重复 GUID。
        /// </summary>
        /// <param name="source">源目录。</param>
        /// <param name="destination">目标目录。</param>
        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source)
                         .Where(path => !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) &&
                                        IOPath.GetFileName(path) != ".DS_Store"))
            {
                File.Copy(file, IOPath.Combine(destination, IOPath.GetFileName(file)), false);
            }
            foreach (string directory in Directory.GetDirectories(source))
            {
                CopyDirectory(directory, IOPath.Combine(destination, IOPath.GetFileName(directory)));
            }
        }

        /// <summary>
        /// 清空新增描述的全部集合字段，避免 Unity 插入元素复制上一项内容。
        /// </summary>
        /// <param name="description">导出描述属性。</param>
        private static void ClearDescriptionCollections(SerializedProperty description)
        {
            foreach (string field in new[]
                     {
                         "CodeTargets", "DataTargets", "OutputTables", "IncludeTags", "ExcludeTags",
                         "FieldVariants", "CustomTemplateDirs", "AdvancedArguments",
                     })
            {
                description.FindPropertyRelative(field).ClearArray();
            }
            description.FindPropertyRelative("CodeOutputPath").stringValue = string.Empty;
            description.FindPropertyRelative("DataOutputPath").stringValue = string.Empty;
        }

        /// <summary>
        /// 查找 Project 标识对应的数组索引。
        /// </summary>
        /// <param name="projectId">Project 标识。</param>
        /// <returns>索引，不存在时为 -1。</returns>
        private int FindProjectIndex(string projectId)
        {
            for (int i = 0; i < m_Projects.arraySize; i++)
            {
                if (m_Projects.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue == projectId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 查找 Luban 加载标识对应的数组索引。
        /// </summary>
        /// <param name="loadId">加载项唯一标识。</param>
        /// <returns>索引，不存在时为 -1。</returns>
        private int FindLoadDescriptionIndex(string loadId)
        {
            for (int index = 0; index < m_LoadDescriptions.arraySize; index++)
            {
                if (m_LoadDescriptions.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("Id").stringValue == loadId)
                {
                    return index;
                }
            }
            return -1;
        }

        /// <summary>
        /// 查找导出描述标识对应的数组索引。
        /// </summary>
        /// <param name="descriptions">描述数组。</param>
        /// <param name="descriptionId">描述标识。</param>
        /// <returns>索引，不存在时为 -1。</returns>
        private static int FindDescriptionIndex(SerializedProperty descriptions, string descriptionId)
        {
            for (int i = 0; i < descriptions.arraySize; i++)
            {
                if (descriptions.GetArrayElementAtIndex(i).FindPropertyRelative("Id").stringValue == descriptionId)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 从字符串数组 SerializedProperty 读取值。
        /// </summary>
        /// <param name="property">字符串数组属性。</param>
        /// <returns>字符串清单。</returns>
        private static List<string> ReadStringArray(SerializedProperty property)
        {
            var values = new List<string>();
            for (int i = 0; i < property.arraySize; i++)
            {
                values.Add(property.GetArrayElementAtIndex(i).stringValue);
            }
            return values;
        }

        /// <summary>
        /// 覆盖写入字符串数组 SerializedProperty。
        /// </summary>
        /// <param name="property">字符串数组属性。</param>
        /// <param name="values">新值。</param>
        private static void WriteStringArray(SerializedProperty property, IEnumerable<string> values)
        {
            string[] array = values?.ToArray() ?? Array.Empty<string>();
            property.arraySize = array.Length;
            for (int i = 0; i < array.Length; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = array[i];
            }
        }

        /// <summary>
        /// 枚举 Project 中可被导出范围选中的 Luban 表及来源位置。
        /// </summary>
        /// <param name="model">Project 模型。</param>
        /// <returns>表选择行。</returns>
        private static IEnumerable<TablePickerRow> EnumerateTableRows(TableProjectModel model)
        {
            foreach (TableProjectExcelFile excel in model?.ExcelFiles ?? new List<TableProjectExcelFile>())
            foreach (TableProjectExcelSheet sheet in excel.Sheets)
            foreach (string tableName in sheet.TableNames)
            {
                yield return new TablePickerRow(tableName, excel.RelativePath, sheet.Name);
            }
        }

        /// <summary>
        /// 统一路径分隔符并把当前目录符号转换为空字符串。
        /// </summary>
        /// <param name="path">待规范化路径。</param>
        /// <returns>规范化路径。</returns>
        private static string NormalizePath(string path)
        {
            string normalized = (path ?? string.Empty).Replace('\\', '/');
            return normalized == "." ? string.Empty : normalized;
        }

        /// <summary>
        /// 返回五种内置 Luban 数据 Target 的实际文件扩展名；自定义 Target 不限制扩展名。
        /// </summary>
        /// <param name="dataTarget">运行时数据 Target。</param>
        /// <returns>含点扩展名，自定义 Target 返回空。</returns>
        private static string ResolveBuiltInDataExtension(string dataTarget)
        {
            switch (dataTarget)
            {
                case "json":
                case "protobuf3-json":
                    return ".json";
                case "bin":
                case "protobuf3-bin":
                case "msgpack":
                    return ".bytes";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// 从已编译生成 Binding 获取 Luban 原始 output_data_file 清单；类型尚未编译时返回空。
        /// </summary>
        /// <param name="bindingTypeName">生成 Binding 类型全名。</param>
        /// <returns>按生成代码顺序排列的逻辑文件清单。</returns>
        private static List<string> ResolveBindingDataFiles(string bindingTypeName)
        {
            if (string.IsNullOrWhiteSpace(bindingTypeName))
            {
                return new List<string>();
            }

            try
            {
                ILubanTableBinding binding = Util.TypeCreator.Create<ILubanTableBinding>(bindingTypeName);
                return binding?.DataFiles?
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(NormalizePath)
                    .ToList() ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// 输出表选择器的一行只读来源信息。
        /// </summary>
        private readonly struct TablePickerRow
        {
            internal TablePickerRow(string tableName, string excelPath, string sheetName)
            {
                TableName = tableName;
                ExcelPath = excelPath;
                SheetName = sheetName;
            }

            internal string TableName { get; }
            internal string ExcelPath { get; }
            internal string SheetName { get; }
        }
    }
}
