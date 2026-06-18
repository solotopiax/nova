/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class LocalizationComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置区域：管理器类型选择器、命名空间列表、回退语言、字体适配开关、修复缺失 TextLocalizing 按钮。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("本地化管理器", m_LocalizationManagerTypeNames, m_CurLocalizationManagerTypeName, true, null, GUILayout.Width(c_LabelWidth));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 ILocalizationManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();

            EditorUtil.Draw.EnumSelector<Language>("编辑器语言类型：", m_EditorLanguage, true, null, GUILayout.Width(c_LabelWidth));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)仅编辑器内生效，终端运行时忽略",
                "(2)非 Unspecified 且在支持列表中时强制使用此语言",
                "(3)否则按 持久化 > 系统 > 回退 解析"
            });

            EditorUtil.Draw.Toggle("终端语言类型优先策略：", m_RuntimeLanguagePrefer, true, null, null, GUILayout.Width(c_LabelWidth));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)仅终端运行时生效，编辑器内忽略",
                "(2)启用：按 持久化 > 系统 > 回退 解析",
                "(3)禁用：强制使用回退语言"
            });

            EditorUtil.Draw.EnumSelector<Language>("回退语言类型：", m_FallbackLanguage, true, null, GUILayout.Width(c_LabelWidth));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)当持久化语言与系统语言都不在支持列表中时使用",
                "(2)终端禁用优先策略时直接使用此语言"
            });

            EditorUtil.Draw.Toggle("字体自动适配：", m_AutoFontAdapt, true, null, null, GUILayout.Width(c_LabelWidth));
            EditorUtil.Draw.Line();

            EditorUtil.Draw.Button("修复预制体缺失 TextLocalizing", true, TextLocalizingValidator.FixMissingInPrefabs);
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)扫描 Assets/ 目录下全部预制体",
                "(2)为缺失 TextLocalizing 的 TextMeshProUGUI 节点补挂组件",
                "(3)新增组件默认 LocalizingFontMark = \"Main\"",
                "(4)修复完成后自动保存预制体"
            });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制文本数据导出区域：模板路径、数据源目录、文件树及每文件的导出设置。
        /// </summary>
        private void DrawTextExportSection()
        {
            if (m_TextSourceDirPath == null || m_TextUnitsSettings == null)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout("本地化文本表格导出", "LocalizationTextExport", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            DrawTemplatePathHintReadOnly(m_TextSourceDirPath, TemplateFileName, "模板文件位置：");

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("表格目录位置：", false, GUILayout.Width(c_LabelWidth));
                EditorUtil.Draw.TextField(m_TextSourceDirPath, true);
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay("选择文本数据源文件夹", "", "", m_TextSourceDirPath), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(m_TextSourceDirPath.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthLarge));
            });

            string textDirPath = m_TextSourceDirPath.stringValue;
            if (!string.IsNullOrEmpty(textDirPath) && Util.SysIO.Directory.Exists(textDirPath))
            {
                EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders(textDirPath, m_TextUnitsSettings, m_FolderFoldoutState, 5, null, DrawLocalizationSourceFileRow);
            }

            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)文本数据导出路径须以 {0} 作为语言占位符",
                "(2)运行时占位符自动替换为对应语言名称",
                "(3)示例 LocalizationTexts_{0}.json（位于工程内任意 Resources/Jsons/ 子目录下）"
            });

            DrawSupportedLanguagesFields();

            bool canExportText = !string.IsNullOrEmpty(m_TextSourceDirPath?.stringValue) && m_TextUnitsSettings != null && m_TextUnitsSettings.arraySize > 0;
            EditorUtil.Draw.DisabledGroup(!canExportText, () =>
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Button("导出所有本地化数据和类型", true, DoExportAllTextData);
                });
            });

            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制字体数据导出区域：模板路径、数据源目录、文件树及每文件的导出设置。
        /// </summary>
        private void DrawFontExportSection()
        {
            if (m_FontSourceDirPath == null || m_FontUnitsSettings == null)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout("本地化字体表格导出", "LocalizationFontExport", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            DrawTemplatePathHintReadOnly(m_FontSourceDirPath, c_FontTemplateFileName, "模板文件位置：");

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("表格目录位置：", false, GUILayout.Width(c_LabelWidth));
                EditorUtil.Draw.TextField(m_FontSourceDirPath, true);
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay("选择字体数据源文件夹", "", "", m_FontSourceDirPath), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(m_FontSourceDirPath.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthLarge));
            });

            string fontDirPath = m_FontSourceDirPath.stringValue;
            if (!string.IsNullOrEmpty(fontDirPath) && Util.SysIO.Directory.Exists(fontDirPath))
            {
                EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders(fontDirPath, m_FontUnitsSettings, m_FontFolderFoldoutState, 5, null, DrawLocalizationSourceFileRow);
            }

            bool canExportFont = !string.IsNullOrEmpty(m_FontSourceDirPath?.stringValue) && m_FontUnitsSettings != null && m_FontUnitsSettings.arraySize > 0;
            EditorUtil.Draw.DisabledGroup(!canExportFont, () =>
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Button("导出所有本地化字体配置", true, DoExportAllFontData);
                });
            });

            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制语言列表配置字段：导出路径、Asset 地址。内嵌于文本导出区域底部。
        /// </summary>
        private void DrawSupportedLanguagesFields()
        {
            if (m_SupportedLanguagesJsonExportPath == null || m_SupportedLanguagesAssetLocation == null)
            {
                return;
            }

            EditorUtil.Draw.SourceFileTree.EnsureStylesInitialized();

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                bool isValid = EditorUtil.Draw.SourceFileTree.IsValidFilePath(m_SupportedLanguagesJsonExportPath.stringValue);
                EditorUtil.Draw.Label("语言列表导出位置：", false, GUILayout.Width(c_LangListLabelWidth));
                EditorUtil.Draw.TextField(m_SupportedLanguagesJsonExportPath, EditorUtil.Draw.SourceFileTree.ContentFieldStyle, true, null, GUILayout.ExpandWidth(true));
                if (!isValid)
                {
                    EditorUtil.Draw.SourceFileTree.DrawInvalidBorderForLastRect();
                }
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderForFileDelay("选择语言列表导出所在目录", "", m_SupportedLanguagesJsonExportPath), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => OpenFolderFromFilePath(m_SupportedLanguagesJsonExportPath.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthMedium));
            });

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                bool isValid = !string.IsNullOrEmpty(m_SupportedLanguagesAssetLocation.stringValue);
                EditorUtil.Draw.Label("语言列表 Asset 地址：", false, GUILayout.Width(c_LangListLabelWidth));
                EditorUtil.Draw.TextField(m_SupportedLanguagesAssetLocation, EditorUtil.Draw.SourceFileTree.ContentFieldStyle, true);
                if (!isValid)
                {
                    EditorUtil.Draw.SourceFileTree.DrawInvalidBorderForLastRect();
                }
            });
        }

        /// <summary>
        /// 绘制单个数据源文件的所有行：文件名行、数据导出行、类型导出行、Asset 地址行。
        /// Localization 不支持单文件导出，因此数据导出行和类型导出行不含导出按钮。
        /// </summary>
        /// <param name="filePath">文件完整路径。</param>
        /// <param name="capturedRelativePath">文件相对路径。</param>
        /// <param name="seq">序号。</param>
        /// <param name="indentSpace">缩进像素。</param>
        /// <param name="savedIndent">保存的缩进级别。</param>
        /// <param name="detailProp">当前文件的单元设置属性。</param>
        /// <param name="sourceUnitsSettingsProperty">全部单元设置列表属性。</param>
        private void DrawLocalizationSourceFileRow(string filePath, string capturedRelativePath, int seq, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty)
        {
            EditorUtil.Draw.SourceFileTree.DrawDefaultFileNameRow(filePath, seq, indentSpace, savedIndent);
            DrawLocalizationDataExportRow(detailProp, indentSpace, savedIndent);
            DrawLocalizationClassExportRow(detailProp, indentSpace, savedIndent);
            EditorUtil.Draw.SourceFileTree.DrawAssetLocationRow(detailProp, indentSpace, savedIndent);
        }

        /// <summary>
        /// 绘制数据导出位置行：标签、路径输入框、选择 / 打开文件夹按钮（无导出按钮）。
        /// Localization 不支持单文件数据导出，因此不绘制导出按钮。
        /// </summary>
        /// <param name="detailProp">当前文件的单元设置属性。</param>
        /// <param name="indentSpace">缩进像素。</param>
        /// <param name="savedIndent">保存的缩进级别。</param>
        private void DrawLocalizationDataExportRow(SerializedProperty detailProp, float indentSpace, int savedIndent)
        {
            SerializedProperty datasProp = detailProp?.FindPropertyRelative("DatasExportPath");
            if (datasProp == null)
            {
                return;
            }

            EditorUtil.Draw.SourceFileTree.DrawIndentedRow(indentSpace, savedIndent, () =>
            {
                bool isValid = EditorUtil.Draw.SourceFileTree.IsValidFilePath(datasProp.stringValue);
                EditorUtil.Draw.Label("数据导出位置：", EditorUtil.Draw.SourceFileTree.ContentStyle, false, GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ExportLabelWidth));
                EditorUtil.Draw.TextField(datasProp, EditorUtil.Draw.SourceFileTree.ContentFieldStyle, true, null, GUILayout.ExpandWidth(true));
                if (!isValid)
                {
                    EditorUtil.Draw.SourceFileTree.DrawInvalidBorderForLastRect();
                }
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderForFileDelay("选择导出数据所在目录", "", datasProp), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => OpenFolderFromFilePath(datasProp.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthMedium));
            });
        }

        /// <summary>
        /// 绘制类型导出位置行：标签、路径输入框、选择 / 打开文件夹按钮（无导出按钮）。
        /// Localization 不支持单文件类型导出，因此不绘制导出按钮。
        /// </summary>
        /// <param name="detailProp">当前文件的单元设置属性。</param>
        /// <param name="indentSpace">缩进像素。</param>
        /// <param name="savedIndent">保存的缩进级别。</param>
        private void DrawLocalizationClassExportRow(SerializedProperty detailProp, float indentSpace, int savedIndent)
        {
            SerializedProperty classesProp = detailProp?.FindPropertyRelative("ClassesExportPath");
            if (classesProp == null)
            {
                return;
            }

            EditorUtil.Draw.SourceFileTree.DrawIndentedRow(indentSpace, savedIndent, () =>
            {
                bool isValid = EditorUtil.Draw.SourceFileTree.IsValidDirectoryPath(classesProp.stringValue);
                EditorUtil.Draw.Label("类型导出位置：", EditorUtil.Draw.SourceFileTree.ContentStyle, false, GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ExportLabelWidth));
                EditorUtil.Draw.TextField(classesProp, EditorUtil.Draw.SourceFileTree.ContentFieldStyle, true, null, GUILayout.ExpandWidth(true));
                if (!isValid)
                {
                    EditorUtil.Draw.SourceFileTree.DrawInvalidBorderForLastRect();
                }
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay("选择导出类型位置", "", "", classesProp), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(classesProp.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthMedium));
            });
        }

        /// <summary>
        /// 导出所有文本数据和类型：委托 EditorUtil.Localization.TextExporter 完成全链路导出。
        /// </summary>
        private void DoExportAllTextData()
        {
            serializedObject.ApplyModifiedProperties();

            string textDirPath = m_TextSourceDirPath?.stringValue;
            if (string.IsNullOrEmpty(textDirPath))
            {
                return;
            }

            LocalizationSettings settings = GetLocalizationSettings();
            if (settings == null || settings.TextUnitsSettings == null || settings.TextUnitsSettings.Count == 0)
            {
                Log.Warning(LogTag.Localization, "文本单元设置为空，导出已跳过。");
                return;
            }

            string classExportPath = string.Empty;
            foreach (LocalizationTextUnitSetting unit in settings.TextUnitsSettings)
            {
                if (!string.IsNullOrEmpty(unit.ClassesExportPath))
                {
                    classExportPath = unit.ClassesExportPath;
                    break;
                }
            }

            string supportedLanguagesExportPath = m_SupportedLanguagesJsonExportPath?.stringValue;
            string[] customTemplateDirs = EditorUtil.Luban.ExportHelper.GetLubanCustomTemplateDirs("localization-text");

            EditorUtil.Localization.TextExporter.ExportTextAll(settings, textDirPath, m_TextUnitsSettings, serializedObject, classExportPath, customTemplateDirs, supportedLanguagesExportPath);
        }

        /// <summary>
        /// 导出所有字体数据：委托 EditorUtil.Localization.FontExporter 完成全链路导出。
        /// </summary>
        private void DoExportAllFontData()
        {
            serializedObject.ApplyModifiedProperties();

            string fontDirPath = m_FontSourceDirPath?.stringValue;
            if (string.IsNullOrEmpty(fontDirPath))
            {
                return;
            }

            LocalizationSettings settings = GetLocalizationSettings();
            if (settings == null || settings.FontUnitsSettings == null || settings.FontUnitsSettings.Count == 0)
            {
                Log.Warning(LogTag.Localization, "字体单元设置为空，导出已跳过。");
                return;
            }

            string classExportPath = string.Empty;
            foreach (LocalizationFontUnitSetting unit in settings.FontUnitsSettings)
            {
                if (!string.IsNullOrEmpty(unit.ClassesExportPath))
                {
                    classExportPath = unit.ClassesExportPath;
                    break;
                }
            }

            EditorUtil.Localization.FontExporter.ExportFontAll(settings, fontDirPath, m_FontUnitsSettings, serializedObject, classExportPath);
        }

        /// <summary>
        /// 通过反射从 LocalizationComponent 读取 m_LocalizationSettings 实例。
        /// </summary>
        /// <returns>本地化设置实例，读取失败时返回 null。</returns>
        private LocalizationSettings GetLocalizationSettings()
        {
            LocalizationComponent component = (LocalizationComponent)target;
            return component.LocalizationSettings;
        }

        /// <summary>
        /// 从文件路径中提取所在目录并打开。
        /// </summary>
        /// <param name="filePath">文件路径（工程相对路径）。</param>
        private static void OpenFolderFromFilePath(string filePath)
        {
            string dir = string.IsNullOrEmpty(filePath) ? string.Empty : (Util.SysIO.Path.GetDirectoryName(filePath) ?? string.Empty);
            EditorUtil.FileSystem.OpenFolder(dir);
        }

    }
}
