/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.SourceFileTree.cs
 * author:    taoye
 * created:   2026/4/25
 * descrip:   数据源文件树绘制与命名空间列表编辑的静态工具方法集
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Draw
        {
            /// <summary>
            /// 数据源文件树绘制与命名空间列表编辑的静态工具方法集。
            /// </summary>
            public static class SourceFileTree
            {
                /// <summary>
                /// 自定义单个数据源文件行绘制的委托。
                /// </summary>
                /// <param name="filePath">文件完整路径。</param>
                /// <param name="capturedRelativePath">文件相对于根目录的路径。</param>
                /// <param name="seq">序号。</param>
                /// <param name="indentSpace">缩进像素。</param>
                /// <param name="savedIndent">保存的缩进级别。</param>
                /// <param name="detailProp">当前文件的单元设置属性。</param>
                /// <param name="sourceUnitsSettingsProperty">全部单元设置列表属性。</param>
                public delegate void DrawSourceFileRowDelegate(string filePath, string capturedRelativePath, int seq, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty);

                /// <summary>
                /// 表格文件名在列表中的显示颜色（浅青）。
                /// </summary>
                public static readonly Color SourceFileNameColor = new Color(0xB8 / 255f, 0xF2 / 255f, 0xF2 / 255f);

                /// <summary>
                /// 设置标签与路径文字使用的颜色（橙褐色）。
                /// </summary>
                public static readonly Color SettingsColor = new Color(200f / 255f, 145f / 255f, 120f / 255f);

                /// <summary>
                /// 表格目录位置标签宽度。
                /// </summary>
                public const float c_DirLabelWidth = 106f;

                /// <summary>
                /// 导出区域标签宽度（数据导出位置、类型导出位置）。
                /// </summary>
                public const float c_ExportLabelWidth = 82f;

                /// <summary>
                /// Asset 地址标签宽度。
                /// </summary>
                public const float c_FieldLabelWidth = 82f;

                /// <summary>
                /// 按钮宽度：选择、打开、导出。
                /// </summary>
                public const float c_ButtonWidthSmall = 60f;

                /// <summary>
                /// 按钮宽度：打开文件夹（中）。
                /// </summary>
                public const float c_ButtonWidthMedium = 100f;

                /// <summary>
                /// 按钮宽度：打开文件夹（长）。
                /// </summary>
                public const float c_ButtonWidthLarge = 163f;

                /// <summary>
                /// Unity 每层缩进的像素宽度。
                /// </summary>
                public const float c_IndentPixelsPerLevel = 15f;

                /// <summary>
                /// 导出区域：内容行（数据 / 类型导出位置等）的标签样式（静态延迟初始化）。
                /// </summary>
                public static GUIStyle ContentStyle;

                /// <summary>
                /// 导出区域：Asset 地址输入框的样式（静态延迟初始化）。
                /// </summary>
                public static GUIStyle ContentFieldStyle;

                /// <summary>
                /// 导出区域：数据源文件名的样式（静态延迟初始化）。
                /// </summary>
                public static GUIStyle SourceFileNameStyle;

                /// <summary>
                /// 路径/Asset 地址校验未通过时的描边颜色（纯红）。
                /// </summary>
                public static readonly Color InvalidBorderColor = new Color(1f, 0.2f, 0.2f, 1f);

                /// <summary>
                /// 路径/Asset 地址校验未通过时的描边像素粗细。
                /// </summary>
                public const float c_InvalidBorderThickness = 1f;

                /// <summary>
                /// 确保 GUIStyle 已初始化（延迟创建，避免 OnEnable 时 EditorStyles 未就绪）。
                /// </summary>
                public static void EnsureStylesInitialized()
                {
                    if (ContentStyle != null)
                    {
                        return;
                    }

                    ContentStyle = new GUIStyle(EditorStyles.label);
                    ContentStyle.fontSize = 10;
                    ContentStyle.normal.textColor = SettingsColor;
                    ContentStyle.padding = new RectOffset(0, 0, 0, 0);

                    ContentFieldStyle = new GUIStyle(EditorStyles.textField);
                    ContentFieldStyle.fontSize = 10;
                    ContentFieldStyle.normal.textColor = SettingsColor;
                    ContentFieldStyle.padding = new RectOffset(2, 2, 2, 2);

                    SourceFileNameStyle = new GUIStyle(EditorStyles.label);
                    SourceFileNameStyle.normal.textColor = SourceFileNameColor;
                    SourceFileNameStyle.padding = new RectOffset(0, 0, 0, 0);
                }

                /// <summary>
                /// 判定路径是否为合法的「文件路径」：非空、非目录、文件名形如 xxx.xxx（必须含非空文件名主干 + 非空扩展名）。
                /// 文件可不存在，仅看路径形态；xxxx（无扩展名）与 xxxx.（仅末尾点号）均视为非法。
                /// </summary>
                /// <param name="path">待校验路径（工程相对或绝对均可）。</param>
                /// <returns>是否合法的文件路径形态。</returns>
                public static bool IsValidFilePath(string path)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        return false;
                    }
                    if (path.EndsWith("/") || path.EndsWith("\\"))
                    {
                        return false;
                    }
                    string fileName = Util.SysIO.Path.GetFileName(path);
                    if (string.IsNullOrEmpty(fileName))
                    {
                        return false;
                    }
                    int dotIndex = fileName.LastIndexOf('.');
                    if (dotIndex <= 0)
                    {
                        return false;
                    }
                    if (dotIndex == fileName.Length - 1)
                    {
                        return false;
                    }
                    return true;
                }

                /// <summary>
                /// 判定路径是否为合法的「目录路径」形态：非空且最后一段不形如 xxx.xxx（无文件扩展名）。
                /// 目录可不存在，仅看路径形态；末尾允许带或不带分隔符。
                /// </summary>
                /// <param name="path">待校验路径（工程相对或绝对均可）。</param>
                /// <returns>是否合法的目录路径形态。</returns>
                public static bool IsValidDirectoryPath(string path)
                {
                    if (string.IsNullOrEmpty(path))
                    {
                        return false;
                    }
                    string trimmed = path.TrimEnd('/', '\\');
                    if (string.IsNullOrEmpty(trimmed))
                    {
                        return false;
                    }
                    string lastSegment = Util.SysIO.Path.GetFileName(trimmed);
                    if (string.IsNullOrEmpty(lastSegment))
                    {
                        return true;
                    }
                    int dotIndex = lastSegment.LastIndexOf('.');
                    if (dotIndex > 0 && dotIndex < lastSegment.Length - 1)
                    {
                        return false;
                    }
                    return true;
                }

                /// <summary>
                /// 在最近一次绘制的控件矩形（GUILayoutUtility.GetLastRect）外沿绘制纯红色描边，用作输入框校验失败时的视觉警示。
                /// 仅在 EventType.Repaint 阶段绘制，避免重复 Layout。
                /// </summary>
                public static void DrawInvalidBorderForLastRect()
                {
                    if (Event.current.type != EventType.Repaint)
                    {
                        return;
                    }

                    Rect rect = GUILayoutUtility.GetLastRect();
                    float t = c_InvalidBorderThickness;
                    Color prev = GUI.color;
                    GUI.color = InvalidBorderColor;
                    GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, t), EditorGUIUtility.whiteTexture);
                    GUI.DrawTexture(new Rect(rect.x, rect.yMax - t, rect.width, t), EditorGUIUtility.whiteTexture);
                    GUI.DrawTexture(new Rect(rect.x, rect.y, t, rect.height), EditorGUIUtility.whiteTexture);
                    GUI.DrawTexture(new Rect(rect.xMax - t, rect.y, t, rect.height), EditorGUIUtility.whiteTexture);
                    GUI.color = prev;
                }

                /// <summary>
                /// 按目录层级绘制可折叠的数据源文件列表：构建目录树，清除孤儿条目，递归绘制根节点及子文件夹。
                /// </summary>
                /// <param name="directoryPath">数据源目录完整路径。</param>
                /// <param name="sourceUnitsSettingsProperty">UnitSettings 列表的 SerializedProperty。</param>
                /// <param name="foldoutState">目录树折叠状态字典。</param>
                /// <param name="minHeaderRowCount">有效 Sheet 的最小表头行数，默认 5。</param>
                /// <param name="fileFilter">文件路径过滤器；为 null 时不过滤。</param>
                /// <param name="customDrawSourceFileRow">自定义文件行绘制委托；为 null 时使用默认绘制。</param>
                public static void DrawSourceFilesListWithFolders(string directoryPath, SerializedProperty sourceUnitsSettingsProperty, Dictionary<string, bool> foldoutState, int minHeaderRowCount = 5, Func<string[], string[]> fileFilter = null, DrawSourceFileRowDelegate customDrawSourceFileRow = null)
                {
                    if (foldoutState == null)
                    {
                        return;
                    }

                    string[] allFiles = EditorUtil.FileSystem.GetFiles(directoryPath, EditorUtil.Excel.c_SearchPattern, EditorUtil.Excel.c_ExcludePrefix);
                    if (allFiles == null || allFiles.Length == 0)
                    {
                        return;
                    }

                    if (fileFilter != null)
                    {
                        allFiles = fileFilter(allFiles);
                        if (allFiles == null || allFiles.Length == 0)
                        {
                            return;
                        }
                    }

                    string rootPathNorm = directoryPath.TrimEnd('/', '\\');
                    FileFolderTree.TreeNode root = FileFolderTree.BuildTree(rootPathNorm, allFiles);
                    if (root == null)
                    {
                        return;
                    }

                    HashSet<string> diskRelativePaths = new HashSet<string>();
                    CollectRelativePaths(root, rootPathNorm, diskRelativePaths);
                    RemoveOrphanUnits(sourceUnitsSettingsProperty, diskRelativePaths);

                    EnsureStylesInitialized();
                    DrawFolderNode(root, rootPathNorm, sourceUnitsSettingsProperty, foldoutState, customDrawSourceFileRow);
                }

                /// <summary>
                /// 递归绘制目录树节点：若为根节点则绘制根目录名 Foldout 并维护缩进；再绘制所有子文件夹 Foldout 及子节点；
                /// 最后绘制当前节点下的数据源文件行。
                /// </summary>
                /// <param name="node">当前目录树节点。</param>
                /// <param name="rootPathNorm">数据源根目录规范化路径（无末尾斜杠）。</param>
                /// <param name="sourceUnitsSettingsProperty">UnitSettings 列表属性。</param>
                /// <param name="foldoutState">目录树折叠状态字典。</param>
                /// <param name="customDrawSourceFileRow">自定义文件行绘制委托；为 null 时使用默认绘制。</param>
                public static void DrawFolderNode(FileFolderTree.TreeNode node, string rootPathNorm, SerializedProperty sourceUnitsSettingsProperty, Dictionary<string, bool> foldoutState, DrawSourceFileRowDelegate customDrawSourceFileRow = null)
                {
                    bool isRoot = string.IsNullOrEmpty(node.SegmentName);
                    if (isRoot)
                    {
                        string rootDisplayName = Util.SysIO.Path.GetFileName(rootPathNorm);
                        if (string.IsNullOrEmpty(rootDisplayName))
                        {
                            rootDisplayName = rootPathNorm;
                        }
                        if (!foldoutState.TryGetValue(rootPathNorm, out bool rootExpanded))
                        {
                            foldoutState[rootPathNorm] = rootExpanded = true;
                        }
                        int rootFileCount = node.TotalFileCount();
                        rootExpanded = EditorUtil.Draw.Foldout(ref rootExpanded, $"{rootDisplayName} ({rootFileCount})", true);
                        foldoutState[rootPathNorm] = rootExpanded;
                        if (!rootExpanded)
                        {
                            return;
                        }
                        EditorUtil.Draw.IncreaseIndentLevel();
                    }

                    foreach (var child in node.Children.OrderBy(c => c.SegmentName))
                    {
                        if (!foldoutState.TryGetValue(child.FullPath, out bool expanded))
                        {
                            foldoutState[child.FullPath] = expanded = true;
                        }
                        int childFileCount = child.TotalFileCount();
                        expanded = EditorUtil.Draw.Foldout(ref expanded, $"{child.SegmentName} ({childFileCount})", true);
                        foldoutState[child.FullPath] = expanded;
                        if (expanded)
                        {
                            EditorUtil.Draw.IncreaseIndentLevel();
                            DrawFolderNode(child, rootPathNorm, sourceUnitsSettingsProperty, foldoutState, customDrawSourceFileRow);
                            EditorUtil.Draw.DecreaseIndentLevel();
                        }
                    }

                    int savedIndent = EditorUtil.Draw.SaveIndentLevel();
                    float indentSpace = savedIndent * c_IndentPixelsPerLevel;

                    var orderedFiles = node.FileFullPaths.OrderBy(f => f).ToList();
                    for (int index = 0; index < orderedFiles.Count; index++)
                    {
                        string filePath = orderedFiles[index];
                        string capturedRelativePath = Util.SysIO.Path.GetRelativePath(rootPathNorm, filePath);
                        SerializedProperty detailProp = GetOrCreateDetailSettingsForFile(sourceUnitsSettingsProperty, capturedRelativePath);
                        if (customDrawSourceFileRow != null)
                        {
                            customDrawSourceFileRow(filePath, capturedRelativePath, index + 1, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty);
                        }
                        else
                        {
                            DrawDefaultSourceFileRow(filePath, capturedRelativePath, index + 1, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty);
                        }
                    }

                    if (isRoot)
                    {
                        EditorUtil.Draw.DecreaseIndentLevel();
                    }
                }

                /// <summary>
                /// 绘制单个数据源文件的所有行（文件名行、数据导出行、类型导出行、Asset 地址行）。
                /// 作为 DrawSourceFileRowDelegate 的默认实现。
                /// </summary>
                /// <param name="filePath">文件完整路径。</param>
                /// <param name="capturedRelativePath">文件相对于根目录的路径。</param>
                /// <param name="seq">序号。</param>
                /// <param name="indentSpace">缩进像素。</param>
                /// <param name="savedIndent">保存的缩进级别。</param>
                /// <param name="detailProp">当前文件的单元设置属性。</param>
                /// <param name="sourceUnitsSettingsProperty">全部单元设置列表属性。</param>
                public static void DrawDefaultSourceFileRow(string filePath, string capturedRelativePath, int seq, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty)
                {
                    DrawDefaultFileNameRow(filePath, seq, indentSpace, savedIndent);
                    DrawDataExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty);
                    DrawClassExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty);
                    DrawAssetLocationRow(detailProp, indentSpace, savedIndent);
                }

                /// <summary>
                /// 绘制文件名行：序号、文件名标签、打开 / 打开文件夹按钮。
                /// </summary>
                /// <param name="filePath">文件完整路径。</param>
                /// <param name="seq">序号。</param>
                /// <param name="indentSpace">缩进像素。</param>
                /// <param name="savedIndent">保存的缩进级别。</param>
                public static void DrawDefaultFileNameRow(string filePath, int seq, float indentSpace, int savedIndent)
                {
                    string fileName = Util.SysIO.Path.GetFileName(filePath);

                    EditorUtil.Draw.SetIndentLevel(0);
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(indentSpace);
                        EditorUtil.Draw.Label($"[{seq}] {fileName}", SourceFileNameStyle, false, GUILayout.ExpandWidth(true));
                        EditorUtil.Draw.Button("打开", false, () => EditorUtil.FileSystem.OpenFile(filePath), GUILayout.Width(c_ButtonWidthSmall));
                        EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(filePath), GUILayout.Width(c_ButtonWidthMedium));
                    });
                    EditorUtil.Draw.RestoreIndentLevel(savedIndent);
                }

                /// <summary>
                /// 绘制数据导出位置行：标签、路径输入框、选择 / 导出 / 打开文件夹按钮。
                /// </summary>
                /// <param name="filePath">文件完整路径。</param>
                /// <param name="capturedRelativePath">文件相对路径。</param>
                /// <param name="indentSpace">缩进像素。</param>
                /// <param name="savedIndent">保存的缩进级别。</param>
                /// <param name="detailProp">当前文件的单元设置属性。</param>
                /// <param name="sourceUnitsSettingsProperty">全部单元设置列表属性。</param>
                /// <param name="onExportData">数据导出回调（filePath, dataExportPath, detailProp）；为 null 时导出按钮无操作。</param>
                /// <param name="doRefreshDataTypeNames">刷新类型名回调（filePath, dataTypeNamesProp）；为 null 时跳过刷新。</param>
                public static void DrawDataExportRow(string filePath, string capturedRelativePath, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty, Action<string, string, SerializedProperty> onExportData = null, Action<string, SerializedProperty> doRefreshDataTypeNames = null)
                {
                    SerializedProperty datasProp = detailProp?.FindPropertyRelative("DatasExportPath");
                    if (datasProp == null)
                    {
                        return;
                    }

                    DrawIndentedRow(indentSpace, savedIndent, () =>
                    {
                        bool isValid = IsValidFilePath(datasProp.stringValue);
                        EditorUtil.Draw.Label("数据导出位置：", ContentStyle, false, GUILayout.Width(c_ExportLabelWidth));
                        EditorUtil.Draw.TextField(datasProp, ContentFieldStyle, true, null, GUILayout.ExpandWidth(true));
                        if (!isValid)
                        {
                            DrawInvalidBorderForLastRect();
                        }
                        EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderForFileDelay("选择导出数据所在目录", "", datasProp), GUILayout.Width(c_ButtonWidthSmall));
                        EditorUtil.Draw.Button("导出", true, () =>
                        {
                            SerializedProperty freshDetail = GetOrCreateDetailSettingsForFile(sourceUnitsSettingsProperty, capturedRelativePath);
                            doRefreshDataTypeNames?.Invoke(filePath, freshDetail?.FindPropertyRelative("DataTypeNames"));
                            onExportData?.Invoke(filePath, datasProp.stringValue, freshDetail);
                        }, GUILayout.Width(c_ButtonWidthSmall));
                        EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(datasProp.stringValue), GUILayout.Width(c_ButtonWidthMedium));
                    });
                }

                /// <summary>
                /// 绘制类型导出位置行：标签、路径输入框、选择 / 导出 / 打开文件夹按钮。
                /// </summary>
                /// <param name="filePath">文件完整路径。</param>
                /// <param name="capturedRelativePath">文件相对路径。</param>
                /// <param name="indentSpace">缩进像素。</param>
                /// <param name="savedIndent">保存的缩进级别。</param>
                /// <param name="detailProp">当前文件的单元设置属性。</param>
                /// <param name="sourceUnitsSettingsProperty">全部单元设置列表属性。</param>
                /// <param name="onExportClass">类型导出回调（filePath, classExportPath, detailProp）；为 null 时导出按钮无操作。</param>
                /// <param name="doRefreshDataTypeNames">刷新类型名回调（filePath, dataTypeNamesProp）；为 null 时跳过刷新。</param>
                public static void DrawClassExportRow(string filePath, string capturedRelativePath, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty, Action<string, string, SerializedProperty> onExportClass = null, Action<string, SerializedProperty> doRefreshDataTypeNames = null)
                {
                    SerializedProperty classesProp = detailProp?.FindPropertyRelative("ClassesExportPath");
                    if (classesProp == null)
                    {
                        return;
                    }

                    DrawIndentedRow(indentSpace, savedIndent, () =>
                    {
                        bool isValid = IsValidDirectoryPath(classesProp.stringValue);
                        EditorUtil.Draw.Label("类型导出位置：", ContentStyle, false, GUILayout.Width(c_ExportLabelWidth));
                        EditorUtil.Draw.TextField(classesProp, ContentFieldStyle, true, null, GUILayout.ExpandWidth(true));
                        if (!isValid)
                        {
                            DrawInvalidBorderForLastRect();
                        }
                        EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay("选择导出类型位置", "", "", classesProp), GUILayout.Width(c_ButtonWidthSmall));
                        EditorUtil.Draw.Button("导出", true, () =>
                        {
                            SerializedProperty freshDetail = GetOrCreateDetailSettingsForFile(sourceUnitsSettingsProperty, capturedRelativePath);
                            doRefreshDataTypeNames?.Invoke(filePath, freshDetail?.FindPropertyRelative("DataTypeNames"));
                            onExportClass?.Invoke(filePath, classesProp.stringValue, freshDetail);
                        }, GUILayout.Width(c_ButtonWidthSmall));
                        EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(classesProp.stringValue), GUILayout.Width(c_ButtonWidthMedium));
                    });
                }

                /// <summary>
                /// 绘制 Asset 地址行。
                /// </summary>
                /// <param name="detailProp">当前文件的单元设置属性。</param>
                /// <param name="indentSpace">缩进像素。</param>
                /// <param name="savedIndent">保存的缩进级别。</param>
                public static void DrawAssetLocationRow(SerializedProperty detailProp, float indentSpace, int savedIndent)
                {
                    SerializedProperty assetLocationProp = detailProp?.FindPropertyRelative("AssetLocation");
                    if (assetLocationProp == null)
                    {
                        return;
                    }

                    DrawIndentedRow(indentSpace, savedIndent, () =>
                    {
                        bool isValid = !string.IsNullOrEmpty(assetLocationProp.stringValue);
                        EditorUtil.Draw.Label("Asset 地址：", ContentStyle, false, GUILayout.Width(c_FieldLabelWidth));
                        EditorUtil.Draw.TextField(assetLocationProp, ContentFieldStyle, true);
                        if (!isValid)
                        {
                            DrawInvalidBorderForLastRect();
                        }
                    });
                }

                /// <summary>
                /// 绘制一行带缩进偏移的内容：将缩进级别置 0、插入空白偏移、执行内容绘制、再恢复缩进。
                /// </summary>
                /// <param name="indentSpace">缩进像素。</param>
                /// <param name="savedIndent">保存的缩进级别。</param>
                /// <param name="drawContent">内容绘制回调。</param>
                public static void DrawIndentedRow(float indentSpace, int savedIndent, Action drawContent)
                {
                    EditorUtil.Draw.SetIndentLevel(0);
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Space(indentSpace);
                        drawContent?.Invoke();
                    });
                    EditorUtil.Draw.RestoreIndentLevel(savedIndent);
                }

                /// <summary>
                /// 绘制命名空间列表编辑区域：标题行（带 + 按钮）、各条目输入框（带 - 按钮）。
                /// </summary>
                /// <param name="namespacesProp">命名空间数组的 SerializedProperty。</param>
                /// <param name="labelWidth">标题标签宽度。</param>
                /// <param name="spaceWidth">条目行缩进宽度。</param>
                /// <param name="onNamespacesChanged">命名空间变更后的回调；为 null 时不额外回调。</param>
                public static void DrawNamespacesList(SerializedProperty namespacesProp, float labelWidth = 90f, float spaceWidth = 93f, Action onNamespacesChanged = null)
                {
                    if (namespacesProp == null)
                    {
                        return;
                    }

                    EditorGUI.BeginChangeCheck();

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Label("命名空间列表：", true, GUILayout.Width(labelWidth));
                        EditorUtil.Draw.Button("+", true, () =>
                        {
                            namespacesProp.InsertArrayElementAtIndex(namespacesProp.arraySize);
                            namespacesProp.GetArrayElementAtIndex(namespacesProp.arraySize - 1).stringValue = string.Empty;
                            namespacesProp.serializedObject.ApplyModifiedProperties();
                        }, GUILayout.Width(24));
                    });

                    for (int i = 0; i < namespacesProp.arraySize; i++)
                    {
                        int index = i;
                        EditorUtil.Draw.Layout.Horizontal(() =>
                        {
                            EditorUtil.Draw.Space(spaceWidth);
                            EditorUtil.Draw.TextField(namespacesProp.GetArrayElementAtIndex(index), true, null, GUILayout.ExpandWidth(true));
                            EditorUtil.Draw.Button("-", true, () =>
                            {
                                namespacesProp.DeleteArrayElementAtIndex(index);
                                namespacesProp.serializedObject.ApplyModifiedProperties();
                            }, GUILayout.Width(24));
                        });
                    }

                    if (EditorGUI.EndChangeCheck() && onNamespacesChanged != null)
                    {
                        namespacesProp.serializedObject.ApplyModifiedProperties();
                        onNamespacesChanged.Invoke();
                    }
                }

                /// <summary>
                /// 根据数据源相对路径在 UnitSettings 列表中查找对应项；若不存在则追加新元素并写入 SourcePath。
                /// </summary>
                /// <param name="sourceUnitsSettingsProperty">UnitSettings 列表的 SerializedProperty。</param>
                /// <param name="sourceRelativePath">数据源文件相对于数据源根目录的路径。</param>
                /// <returns>找到或新建的 UnitSetting 元素的 SerializedProperty。</returns>
                public static SerializedProperty GetOrCreateDetailSettingsForFile(SerializedProperty sourceUnitsSettingsProperty, string sourceRelativePath)
                {
                    if (sourceUnitsSettingsProperty == null)
                    {
                        return null;
                    }
                    for (int i = 0; i < sourceUnitsSettingsProperty.arraySize; i++)
                    {
                        SerializedProperty elem = sourceUnitsSettingsProperty.GetArrayElementAtIndex(i);
                        SerializedProperty pathProp = elem.FindPropertyRelative("SourcePath");
                        if (pathProp != null && pathProp.stringValue == sourceRelativePath)
                        {
                            return elem;
                        }
                    }
                    sourceUnitsSettingsProperty.arraySize++;
                    SerializedProperty newElem = sourceUnitsSettingsProperty.GetArrayElementAtIndex(sourceUnitsSettingsProperty.arraySize - 1);
                    SerializedProperty newPathProp = newElem.FindPropertyRelative("SourcePath");
                    if (newPathProp != null)
                    {
                        newPathProp.stringValue = sourceRelativePath;
                    }
                    return newElem;
                }

                /// <summary>
                /// 递归收集目录树中所有文件节点的相对路径，写入结果集合。
                /// </summary>
                /// <param name="node">当前目录树节点。</param>
                /// <param name="rootPathNorm">数据源根目录规范化路径（无末尾斜杠），用于计算相对路径。</param>
                /// <param name="result">收集结果集合，追加写入，不清空。</param>
                public static void CollectRelativePaths(FileFolderTree.TreeNode node, string rootPathNorm, HashSet<string> result)
                {
                    foreach (string filePath in node.FileFullPaths)
                    {
                        result.Add(Util.SysIO.Path.GetRelativePath(rootPathNorm, filePath));
                    }
                    foreach (var child in node.Children)
                    {
                        CollectRelativePaths(child, rootPathNorm, result);
                    }
                }

                /// <summary>
                /// 倒序遍历 unit 列表，删除 SourcePath 不在磁盘文件集合中的孤儿条目。
                /// </summary>
                /// <param name="unitsProperty">UnitSettings 列表的 SerializedProperty。</param>
                /// <param name="diskRelativePaths">磁盘上实际存在的文件相对路径集合。</param>
                public static void RemoveOrphanUnits(SerializedProperty unitsProperty, HashSet<string> diskRelativePaths)
                {
                    if (unitsProperty == null)
                    {
                        return;
                    }

                    for (int i = unitsProperty.arraySize - 1; i >= 0; i--)
                    {
                        SerializedProperty pathProp = unitsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("SourcePath");
                        if (pathProp == null || !diskRelativePaths.Contains(pathProp.stringValue))
                        {
                            unitsProperty.DeleteArrayElementAtIndex(i);
                        }
                    }
                }
            }
        }
    }
}
