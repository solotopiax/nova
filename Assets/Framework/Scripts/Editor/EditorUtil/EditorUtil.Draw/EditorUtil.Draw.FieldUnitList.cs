/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.FieldUnitList.cs
 * author:    taoye
 * created:   2026/4/23
 * descrip:   编辑器绘制工具-字段单元列表
 ***************************************************************/

using System;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 绘制工具。
        /// </summary>
        public static partial class Draw
        {
            /// <summary>
            /// 字段名行的颜色（浅青）。
            /// </summary>
            private static readonly Color s_FieldUnitNameColor = new Color(0xB8 / 255f, 0xF2 / 255f, 0xF2 / 255f);
            /// <summary>
            /// 标签与路径文字颜色（橙褐）。
            /// </summary>
            private static readonly Color s_FieldUnitSettingsColor = new Color(200f / 255f, 145f / 255f, 120f / 255f);
            /// <summary>
            /// 标签样式（fontSize=10，橙褐色），延迟初始化。
            /// </summary>
            private static GUIStyle s_FieldUnitContentStyle;
            /// <summary>
            /// 输入框样式（fontSize=10，橙褐色），延迟初始化。
            /// </summary>
            private static GUIStyle s_FieldUnitContentFieldStyle;
            /// <summary>
            /// 字段名样式（浅青色），延迟初始化。
            /// </summary>
            private static GUIStyle s_FieldUnitNameStyle;

            /// <summary>
            /// 绘制字段单元列表：先按 defs 同步固定条目（FieldName/FieldDesc），再逐条绘制五行布局
            /// （字段名行、数据位置行、配置分组名行、配置字段名行、Asset 地址行）。
            /// </summary>
            /// <param name="listProp">List 的 SerializedProperty，元素类型为 ConfigFieldAssetSetting。</param>
            /// <param name="defs">固定条目定义数组，每项包含字段枚举标识名和描述，由外部写入只读字段。</param>
            public static void ConfigFieldAssetList(SerializedProperty listProp, (string fieldName, string fieldDesc)[] defs)
            {
                if (listProp == null || defs == null)
                {
                    return;
                }

                for (int d = 0; d < defs.Length; d++)
                {
                    string targetFieldName = defs[d].fieldName;
                    string targetFieldDesc = defs[d].fieldDesc;
                    bool found = false;
                    for (int i = 0; i < listProp.arraySize; i++)
                    {
                        SerializedProperty elem = listProp.GetArrayElementAtIndex(i);
                        SerializedProperty nameProp = elem.FindPropertyRelative("FieldName");
                        if (nameProp != null && nameProp.stringValue == targetFieldName)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        listProp.arraySize++;
                        SerializedProperty newElem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                        SerializedProperty newNameProp = newElem.FindPropertyRelative("FieldName");
                        SerializedProperty newDescProp = newElem.FindPropertyRelative("FieldDesc");
                        if (newNameProp != null)
                        {
                            newNameProp.stringValue = targetFieldName;
                        }
                        if (newDescProp != null)
                        {
                            newDescProp.stringValue = targetFieldDesc;
                        }
                        listProp.serializedObject.ApplyModifiedProperties();
                    }
                }

                EnsureFieldUnitStylesInitialized();
                EditorUtil.Draw.SourceFileTree.EnsureStylesInitialized();

                for (int i = 0; i < listProp.arraySize; i++)
                {
                    DrawFieldUnitRow(listProp.GetArrayElementAtIndex(i), i + 1);
                }
            }

            /// <summary>
            /// 确保字段单元 GUIStyle 已初始化（延迟创建，避免 OnEnable 时 EditorStyles 未就绪）。
            /// </summary>
            private static void EnsureFieldUnitStylesInitialized()
            {
                if (s_FieldUnitContentStyle != null)
                {
                    return;
                }

                s_FieldUnitContentStyle = new GUIStyle(EditorStyles.label);
                s_FieldUnitContentStyle.fontSize = 10;
                s_FieldUnitContentStyle.normal.textColor = s_FieldUnitSettingsColor;
                s_FieldUnitContentStyle.padding = new RectOffset(0, 0, 0, 0);

                s_FieldUnitContentFieldStyle = new GUIStyle(EditorStyles.textField);
                s_FieldUnitContentFieldStyle.fontSize = 10;
                s_FieldUnitContentFieldStyle.normal.textColor = s_FieldUnitSettingsColor;
                s_FieldUnitContentFieldStyle.padding = new RectOffset(2, 2, 2, 2);

                s_FieldUnitNameStyle = new GUIStyle(EditorStyles.label);
                s_FieldUnitNameStyle.normal.textColor = s_FieldUnitNameColor;
                s_FieldUnitNameStyle.padding = new RectOffset(0, 0, 0, 0);
            }

            /// <summary>
            /// 绘制单个字段单元的五行布局：字段名行、数据位置行、配置分组名行、配置字段名行、Asset 地址行。
            /// </summary>
            /// <param name="unitProp">ConfigFieldAssetSetting 元素的 SerializedProperty。</param>
            /// <param name="seq">序号（从 1 开始）。</param>
            private static void DrawFieldUnitRow(SerializedProperty unitProp, int seq)
            {
                if (unitProp == null)
                {
                    return;
                }

                SerializedProperty fieldNameProp = unitProp.FindPropertyRelative("FieldName");
                SerializedProperty fieldDescProp = unitProp.FindPropertyRelative("FieldDesc");
                SerializedProperty datasExportPathProp = unitProp.FindPropertyRelative("DatasExportPath");
                SerializedProperty configGroupNameProp = unitProp.FindPropertyRelative("ConfigGroupName");
                SerializedProperty configFieldNameProp = unitProp.FindPropertyRelative("ConfigFieldName");
                SerializedProperty assetLocationProp = unitProp.FindPropertyRelative("AssetLocation");

                string fieldNameText = fieldNameProp != null ? fieldNameProp.stringValue : string.Empty;
                string fieldDescText = fieldDescProp != null ? fieldDescProp.stringValue : string.Empty;

                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label($"[{seq}] {fieldNameText}", s_FieldUnitNameStyle, false, GUILayout.ExpandWidth(true));
                    EditorUtil.Draw.Label(fieldDescText, s_FieldUnitContentStyle, false);
                });

                if (datasExportPathProp != null)
                {
                    DrawFieldUnitIndentedRow(() =>
                    {
                        bool isValid = EditorUtil.Draw.SourceFileTree.IsValidFilePath(datasExportPathProp.stringValue);
                        EditorUtil.Draw.Label("数据位置：", s_FieldUnitContentStyle, false, GUILayout.Width(90f));
                        EditorUtil.Draw.TextField(datasExportPathProp, s_FieldUnitContentFieldStyle, true, null, GUILayout.ExpandWidth(true));
                        if (!isValid)
                        {
                            EditorUtil.Draw.SourceFileTree.DrawInvalidBorderForLastRect();
                        }
                        EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderForFileDelay("选择数据所在目录", "", datasExportPathProp), GUILayout.Width(60f));
                    });
                }

                if (configGroupNameProp != null)
                {
                    DrawFieldUnitIndentedRow(() =>
                    {
                        EditorUtil.Draw.Label("配置分组名：", s_FieldUnitContentStyle, false, GUILayout.Width(90f));
                        EditorUtil.Draw.TextField(configGroupNameProp, s_FieldUnitContentFieldStyle, true);
                    });
                }

                if (configFieldNameProp != null)
                {
                    DrawFieldUnitIndentedRow(() =>
                    {
                        EditorUtil.Draw.Label("配置字段名：", s_FieldUnitContentStyle, false, GUILayout.Width(90f));
                        EditorUtil.Draw.TextField(configFieldNameProp, s_FieldUnitContentFieldStyle, true);
                    });
                }

                if (assetLocationProp != null)
                {
                    DrawFieldUnitIndentedRow(() =>
                    {
                        EditorUtil.Draw.Label("Asset 地址：", s_FieldUnitContentStyle, false, GUILayout.Width(90f));
                        EditorUtil.Draw.TextField(assetLocationProp, s_FieldUnitContentFieldStyle, true);
                    });
                }
            }

            /// <summary>
            /// 绘制一行缩进归零的内容：保存当前缩进级别并置 0，执行内容绘制后恢复缩进。
            /// </summary>
            /// <param name="drawContent">内容绘制回调。</param>
            private static void DrawFieldUnitIndentedRow(Action drawContent)
            {
                int savedIndent = EditorUtil.Draw.SaveIndentLevel();
                EditorUtil.Draw.SetIndentLevel(0);
                EditorUtil.Draw.Layout.Horizontal(() => drawContent?.Invoke());
                EditorUtil.Draw.RestoreIndentLevel(savedIndent);
            }
        }
    }
}
