/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TextLocalizingInspector.Methods.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   文本本地化组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class TextLocalizingInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制编辑器模式下的译文预览区域。
        /// </summary>
        private void DrawPreview()
        {
            if (EditorApplication.isPlaying)
            {
                DrawRuntimePreview();
                return;
            }

            string currentKey = m_LocalizingKeyName.stringValue;
            if (currentKey != m_LastPreviewKey)
            {
                m_LastPreviewKey = currentKey;
                RefreshPreviewTranslation(currentKey);
            }

            string foldoutLabel = string.IsNullOrEmpty(m_PreviewLanguageName) ? "预览" : $"预览 (当前语言: {m_PreviewLanguageName})";
            if (!EditorUtil.Draw.Foldout(foldoutLabel, "TextLocalizingPreview"))
            {
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            if (string.IsNullOrEmpty(currentKey))
            {
                EditorUtil.Draw.Label("译文: (未设置 Key)", false);
            }
            else if (string.IsNullOrEmpty(m_PreviewTranslation))
            {
                EditorUtil.Draw.Label($"译文: (未找到 Key: {currentKey})", false);
            }
            else
            {
                EditorUtil.Draw.Label("译文:", $"\"{m_PreviewTranslation}\"", false);
            }

            EditorUtil.Draw.DecreaseIndentLevel();
        }

        /// <summary>
        /// 绘制运行时预览（从 Nova.Localization 获取实时数据）。
        /// </summary>
        private void DrawRuntimePreview()
        {
            TextLocalizing textLocalizing = (TextLocalizing)target;
            string languageName = Nova.Localization != null ? Nova.Localization.LanguageName : "N/A";

            string foldoutLabel = $"预览 (当前语言: {languageName})";
            if (!EditorUtil.Draw.Foldout(foldoutLabel, "TextLocalizingPreview"))
            {
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            string keyName = textLocalizing.KeyName;
            if (string.IsNullOrEmpty(keyName))
            {
                EditorUtil.Draw.Label("译文: (未设置 Key)", false);
            }
            else if (Nova.Localization != null && Nova.Localization.HasText(keyName))
            {
                EditorUtil.Draw.Label("译文:", $"\"{Nova.Localization.GetText(keyName)}\"", false);
            }
            else
            {
                EditorUtil.Draw.Label($"译文: (未找到 Key: {keyName})", false);
            }

            EditorUtil.Draw.DecreaseIndentLevel();
        }

        /// <summary>
        /// 尝试加载编辑器模式下的预览数据（从已导出的 JSON 文件中读取）。
        /// </summary>
        private void TryLoadPreviewData()
        {
            if (EditorApplication.isPlaying)
            {
                return;
            }

            LocalizationComponent localizationComponent = UnityEngine.Object.FindFirstObjectByType<LocalizationComponent>();
            if (localizationComponent == null)
            {
                return;
            }

            SerializedObject localizationSO = new SerializedObject(localizationComponent);
            SerializedProperty jsonExportPath = localizationSO.FindProperty("m_TextJsonExportFolderPath");
            SerializedProperty fallbackLanguage = localizationSO.FindProperty("m_FallbackLanguage");

            if (jsonExportPath == null || string.IsNullOrEmpty(jsonExportPath.stringValue))
            {
                return;
            }

            Language language = Language.English;
            if (fallbackLanguage != null)
            {
                language = (Language)fallbackLanguage.enumValueIndex;
            }

            m_PreviewLanguageName = language.ToString();

            string exportPathPattern = jsonExportPath.stringValue;
            string jsonFilePath;
            if (exportPathPattern.Contains("{0}"))
            {
                string relativePath = string.Format(exportPathPattern, m_PreviewLanguageName);
                jsonFilePath = EditorUtil.FileSystem.GetProjectFullPath(relativePath);
            }
            else
            {
                string fullPath = EditorUtil.FileSystem.GetProjectFullPath(exportPathPattern);
                jsonFilePath = Util.SysIO.Path.Combine(fullPath, $"Localization_{m_PreviewLanguageName}.json");
            }

            if (!Util.SysIO.File.Exists(jsonFilePath))
            {
                return;
            }

            try
            {
                string jsonContent = Util.SysIO.File.ReadAllTextSync(jsonFilePath);
                m_CachedLanguageTexts = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Localization, "TextLocalizingInspector 读取预览 JSON 失败: {0}", e.Message);
                m_CachedLanguageTexts = null;
            }
        }

        /// <summary>
        /// 根据 Key 刷新预览译文。
        /// </summary>
        /// <param name="keyName">本地化键名。</param>
        private void RefreshPreviewTranslation(string keyName)
        {
            m_PreviewTranslation = string.Empty;

            if (string.IsNullOrEmpty(keyName) || m_CachedLanguageTexts == null)
            {
                return;
            }

            if (m_CachedLanguageTexts.TryGetValue(keyName, out string translation))
            {
                m_PreviewTranslation = translation;
            }
        }

        /// <summary>
        /// 尝试从字体数据 JSON 中加载可用的字体标记选项列表。
        /// </summary>
        private void TryLoadFontMarkOptions()
        {
            m_FontMarkOptions = null;
            m_HasFontMarkData = false;

            if (EditorApplication.isPlaying)
            {
                return;
            }

            LocalizationComponent localizationComponent = UnityEngine.Object.FindFirstObjectByType<LocalizationComponent>();
            if (localizationComponent == null)
            {
                return;
            }

            SerializedObject localizationSO = new SerializedObject(localizationComponent);
            SerializedProperty fontJsonPath = localizationSO.FindProperty("m_FontJsonExportPath");
            if (fontJsonPath == null || string.IsNullOrEmpty(fontJsonPath.stringValue))
            {
                return;
            }

            string jsonFilePath = EditorUtil.FileSystem.GetProjectFullPath(fontJsonPath.stringValue);
            if (!Util.SysIO.File.Exists(jsonFilePath))
            {
                return;
            }

            try
            {
                string jsonContent = Util.SysIO.File.ReadAllTextSync(jsonFilePath);
                List<LocalizationFontData> fontDatas = JsonConvert.DeserializeObject<List<LocalizationFontData>>(jsonContent);
                if (fontDatas == null || fontDatas.Count == 0)
                {
                    return;
                }

                HashSet<string> marks = new HashSet<string>();
                for (int i = 0; i < fontDatas.Count; i++)
                {
                    if (!string.IsNullOrEmpty(fontDatas[i].Mark))
                    {
                        marks.Add(fontDatas[i].Mark);
                    }
                }

                if (marks.Count > 0)
                {
                    m_FontMarkOptions = new string[marks.Count];
                    marks.CopyTo(m_FontMarkOptions);
                    Array.Sort(m_FontMarkOptions);
                    m_HasFontMarkData = true;
                }
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Localization, "TextLocalizingInspector 读取字体 JSON 失败: {0}", e.Message);
            }
        }

        /// <summary>
        /// 绘制字体标记字段。
        /// 字体 JSON 可用时显示为下拉选择，否则降级为纯文本输入。
        /// </summary>
        private void DrawFontMarkField()
        {
            if (!m_HasFontMarkData)
            {
                EditorUtil.Draw.Property("字体标记", m_LocalizingFontMark, true, GUILayout.Width(175));
                return;
            }

            string currentMark = m_LocalizingFontMark.stringValue;
            int selectedIndex = Array.IndexOf(m_FontMarkOptions, currentMark);
            if (selectedIndex < 0)
            {
                selectedIndex = 0;
                m_LocalizingFontMark.stringValue = m_FontMarkOptions[0];
            }

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.DisabledGroup(EditorApplication.isPlaying, () =>
                {
                    EditorUtil.Draw.Label("字体标记", true, GUILayout.Width(175));
                    EditorGUI.BeginChangeCheck();
                    int newIndex = EditorGUILayout.Popup(selectedIndex, m_FontMarkOptions);
                    if (EditorGUI.EndChangeCheck())
                    {
                        m_LocalizingFontMark.stringValue = m_FontMarkOptions[newIndex];
                    }
                });
            });
        }
    }
}
