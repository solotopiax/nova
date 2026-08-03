/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Luban.InspectorDataFormat.cs
 * author:    taoye
 * created:   2026/7/31
 * descrip:   专用模块表格数据格式 Inspector 绘制
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Luban
        {
            /// <summary>
            /// 绘制专用模块的数据格式，并在格式切换时更新标准数据后缀。
            /// </summary>
            /// <param name="dataFormat">模块的数据格式序列化属性。</param>
            /// <param name="label">Inspector 显示标签。</param>
            /// <param name="scopeDescription">该格式选项统一影响的数据范围。</param>
            /// <param name="unitLists">需要同步标准后缀的单元列表。</param>
            internal static void DrawDataFormat(
                SerializedProperty dataFormat,
                string label,
                string scopeDescription,
                params SerializedProperty[] unitLists)
            {
                if (dataFormat == null)
                {
                    return;
                }

                LubanDataFormat previous = (LubanDataFormat)dataFormat.enumValueIndex;
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(dataFormat, new GUIContent(label));
                if (EditorGUI.EndChangeCheck())
                {
                    LubanDataFormat current = (LubanDataFormat)dataFormat.enumValueIndex;
                    string previousSuffix = previous == LubanDataFormat.Binary ? ".bytes" : ".json";
                    string currentSuffix = current == LubanDataFormat.Binary ? ".bytes" : ".json";
                    for (int i = 0; i < unitLists.Length; i++)
                    {
                        UpdateStandardDataSuffixes(unitLists[i], previousSuffix, currentSuffix);
                    }
                }

                DrawDataFormatHelpBox(scopeDescription);
            }

            /// <summary>
            /// 绘制 JSON/Binary 格式选择的统一编号说明。
            /// </summary>
            /// <param name="scopeDescription">当前格式选项统一影响的数据范围。</param>
            internal static void DrawDataFormatHelpBox(string scopeDescription)
            {
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1) JSON 是默认格式，内容可以直接查看，适合开发时检查数据。",
                    "(2) Binary 使用 Luban 二进制格式，文件后缀为 .bytes，不能按 JSON 文本查看。",
                    $"(3) 当前选择会统一用于{scopeDescription}的后续导出和运行时加载。",
                    "(4) 切换格式时只会自动替换标准 .json / .bytes 后缀；自定义后缀需要手动修改。",
                    "(5) 导出成功后会自动删除同名的另一种格式文件及其 .meta，避免旧数据被误用。"
                });
            }

            /// <summary>
            /// 仅把标准 JSON/Binary 后缀替换为新格式后缀，自定义后缀保持不变。
            /// </summary>
            /// <param name="unitList">数据单元序列化列表。</param>
            /// <param name="previousSuffix">切换前的标准后缀。</param>
            /// <param name="currentSuffix">切换后的标准后缀。</param>
            private static void UpdateStandardDataSuffixes(
                SerializedProperty unitList,
                string previousSuffix,
                string currentSuffix)
            {
                if (unitList == null || !unitList.isArray)
                {
                    return;
                }

                for (int i = 0; i < unitList.arraySize; i++)
                {
                    SerializedProperty path = unitList.GetArrayElementAtIndex(i)
                        .FindPropertyRelative("DatasExportPath");
                    if (path == null || string.IsNullOrEmpty(path.stringValue) ||
                        !path.stringValue.EndsWith(previousSuffix, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    path.stringValue = path.stringValue.Substring(
                        0, path.stringValue.Length - previousSuffix.Length) + currentSuffix;
                }
            }
        }
    }
}
