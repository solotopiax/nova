/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TextLocalizingValidator.cs
 * author:    taoye
 * created:   2026/4/11
 * descrip:   TextLocalizing 批量验证与修复工具
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// TextLocalizing 批量验证与修复工具。
    /// 扫描工程内所有预制体中的 TextMeshProUGUI，为缺失 TextLocalizing 的节点自动补挂并保存。
    /// 入口为 LocalizationComponent Inspector 面板中的"修复预制体缺失 TextLocalizing"按钮。
    /// </summary>
    internal static class TextLocalizingValidator
    {
        /// <summary>
        /// 默认字体标记。
        /// </summary>
        private const string c_DefaultFontMark = "Main";

        /// <summary>
        /// 扫描工程内所有预制体，为缺失 TextLocalizing 的 TMP 节点补挂并保存。
        /// 遍历 Assets/ 下全部 Prefab，含 inactive 节点，补挂后通过 SerializedObject 写入默认 m_LocalizingFontMark，
        /// 有改动的 Prefab 立即保存，最终统一 AssetDatabase.SaveAssets/Refresh。
        /// </summary>
        internal static void FixMissingInPrefabs()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            int total = guids.Length;
            int fixedPrefabCount = 0;
            int fixedNodeCount = 0;

            for (int i = 0; i < total; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("修复 TextLocalizing", path, i / (float)total);

                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    TextMeshProUGUI[] allTMPs = root.GetComponentsInChildren<TextMeshProUGUI>(true);
                    bool prefabDirty = false;

                    foreach (TextMeshProUGUI tmp in allTMPs)
                    {
                        if (tmp.GetComponent<TextLocalizing>() != null)
                        {
                            continue;
                        }

                        TextLocalizing textLocalizing = tmp.gameObject.AddComponent<TextLocalizing>();
                        SerializedObject so = new SerializedObject(textLocalizing);
                        SerializedProperty fontMarkProp = so.FindProperty("m_LocalizingFontMark");
                        if (fontMarkProp != null && string.IsNullOrEmpty(fontMarkProp.stringValue))
                        {
                            fontMarkProp.stringValue = c_DefaultFontMark;
                            so.ApplyModifiedPropertiesWithoutUndo();
                        }

                        prefabDirty = true;
                        fixedNodeCount++;
                    }

                    if (prefabDirty)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, path);
                        fixedPrefabCount++;
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(LogTag.Localization, "Prefab {0} 处理失败：{1}", path, ex.Message);
                }
                finally
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }

            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (fixedNodeCount > 0)
            {
                Log.Debug(LogTag.Localization, "已为 {0} 个预制体的 {1} 个 TMP 节点补挂 TextLocalizing。", fixedPrefabCount, fixedNodeCount);
                EditorUtility.DisplayDialog("修复完成", $"已为 {fixedPrefabCount} 个预制体的 {fixedNodeCount} 个 TMP 节点补挂 TextLocalizing 组件。", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("验证通过", "工程内所有预制体中的 TextMeshProUGUI 均已挂载 TextLocalizing。", "确定");
            }
        }
    }
}
