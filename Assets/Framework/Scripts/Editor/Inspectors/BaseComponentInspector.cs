/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BaseComponentInspector.cs
 * author:    taoye
 * created:   2026/1/12
 * descrip:   编辑器面板定制基础类
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 编辑器面板定制基础类。
    /// </summary>
    public abstract class BaseComponentInspector : UnityEditor.Editor
    {
        /// <summary>
        /// DevelopMode 头部标签样式（支持富文本着色）。
        /// </summary>
        private static GUIStyle s_DevelopModeHeaderStyle;

        /// <summary>
        /// 是否正在编译中。
        /// </summary>
        private bool m_IsCompiling = false;

        /// <summary>
        /// 绘制前缓存的 labelWidth，FinalRefreshInspectorGUI 中恢复。
        /// </summary>
        private float m_CachedLabelWidth;

        /// <summary>
        /// Inspector 前缀标签宽度（走 EditorGUIUtility.labelWidth 的 Toggle/Slider/Property 等）。
        /// 默认 180f 以容纳中文长字段名。子类可 override 调整。
        /// </summary>
        protected virtual float InspectorLabelWidth => 180f;

        /// <summary>
        /// Gitlab 链接。
        /// </summary>
        protected GUIContent m_GitlabGUIContent;

        /// <summary>
        /// 飞书文档链接。
        /// </summary>
        protected GUIContent m_FeishuDocumentGUIContent;

        /// <summary>
        /// 飞书文档链接。
        /// </summary>
        protected string m_FeishuDocumentUrl = null;

        /// <summary>
        /// 模板文件名（如 "TableTemplate.xlsx"），需要模板功能的子类应覆写此属性。
        /// </summary>
        protected virtual string TemplateFileName => string.Empty;

        /// <summary>
        /// 模板文件路径提示行的标签宽度，子类可覆写以自定义对齐。
        /// </summary>
        protected virtual float TemplateLabelWidth => 95f;

        /// <summary>
        /// TemplatePath 的 SerializedProperty。
        /// </summary>
        protected SerializedProperty m_TemplatePath;

        /// <summary>
        /// 模板文件路径提示行：灰色标签样式（延迟初始化，避免静态构造时 EditorStyles 未就绪）。
        /// </summary>
        private static GUIStyle s_GreyLabelStyle;

        /// <summary>
        /// 模板文件路径提示行：灰色文本框样式（延迟初始化，避免静态构造时 EditorStyles 未就绪）。
        /// </summary>
        private static GUIStyle s_GreyTextFieldStyle;

        /// <summary>
        /// 启用。
        /// </summary>
        protected virtual void OnEnable()
        {
            // m_GitlabGUIContent = new GUIContent();
            // m_GitlabGUIContent.image = AssetDatabase.LoadAssetAtPath<Texture>(EditorPath.Editor.GitLabIconFilePath);
            // m_GitlabGUIContent.text = "Git";
            // m_GitlabGUIContent.tooltip = $"去往远端仓库：{EditorPath.Editor.GitLabUrl}";
            //
            // m_FeishuDocumentGUIContent = new GUIContent();
            // m_FeishuDocumentGUIContent.image = AssetDatabase.LoadAssetAtPath<Texture>(EditorPath.Editor.FeishuDocumentIconFilePath);
            // m_FeishuDocumentGUIContent.text = "文档";
            // m_FeishuDocumentGUIContent.tooltip = "去往飞书知识库文档";
        }

        /// <summary>
        /// 绘制。
        /// </summary>
        public override void OnInspectorGUI()
        {
            m_CachedLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = InspectorLabelWidth;

            if (m_IsCompiling && !EditorApplication.isCompiling)
            {
                m_IsCompiling = false;
                OnCompileComplete();
            }
            else if (!m_IsCompiling && EditorApplication.isCompiling)
            {
                m_IsCompiling = true;
                OnCompileStart();
            }

            // 当鼠标按下时取消焦点。
            Event e = Event.current;
            if (e.type == EventType.MouseDown)
            {
                GUI.FocusControl(null);
            }

            // if(!string.IsNullOrEmpty(m_FeishuDocumentUrl))
            // {
            //     EditorGUILayout.BeginHorizontal();
            //     {
            //         GUILayout.FlexibleSpace();
            //         if (GUILayout.Button(m_FeishuDocumentGUIContent, "Label"))
            //         {
            //             Application.OpenURL(m_FeishuDocumentUrl);
            //             GUIUtility.ExitGUI();
            //         }
            //         EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
            //         GUILayout.Space(5);
            //         if (GUILayout.Button(m_GitlabGUIContent, "Label"))
            //         {
            //             Application.OpenURL(EditorPath.Editor.GitLabUrl);
            //             GUIUtility.ExitGUI();
            //         }
            //         EditorGUIUtility.AddCursorRect(GUILayoutUtility.GetLastRect(), MouseCursor.Link);
            //         GUILayout.Space(5);
            //     }
            //     EditorGUILayout.EndHorizontal();
            // }

            serializedObject.Update();
            DrawDevelopModeSnapshot();
            if (EditorApplication.isPlaying || serializedObject.hasModifiedProperties)
                Repaint();
        }

        /// <summary>
        /// 编译开始。
        /// </summary>
        protected virtual void OnCompileStart()
        {
        }

        /// <summary>
        /// 编译完成。
        /// </summary>
        protected virtual void OnCompileComplete()
        {
        }

        /// <summary>
        /// 最后刷新 Inspector。
        /// </summary>
        public void FinalRefreshInspectorGUI()
        {
            serializedObject.ApplyModifiedProperties();
            Repaint();

            EditorGUIUtility.labelWidth = m_CachedLabelWidth;
        }

        /// <summary>
        /// 绘制只读的 DevelopMode 场景快照提示。
        /// </summary>
        private void DrawDevelopModeSnapshot()
        {
            SerializedProperty developModeProperty = serializedObject.FindProperty("m_DevelopMode");
            if (developModeProperty == null)
            {
                return;
            }

            if (s_DevelopModeHeaderStyle == null)
            {
                s_DevelopModeHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    richText = true
                };
            }

            string developModeText = ResolveDevelopModeRichText(developModeProperty);
            string colorHex = ResolveDevelopModeColorHex(developModeProperty);
            EditorUtil.Draw.Label($"<color={colorHex}>开发模式：{developModeText}（随 Config 导出自动回写，不可手动修改）</color>", s_DevelopModeHeaderStyle, false);
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 生成带颜色的 DevelopMode 富文本。
        /// </summary>
        private static string ResolveDevelopModeRichText(SerializedProperty developModeProperty)
        {
            if (developModeProperty.hasMultipleDifferentValues)
            {
                return "多值";
            }

            return (DevelopMode)developModeProperty.enumValueIndex switch
            {
                DevelopMode.Debug => "Debug",
                DevelopMode.Release => "Release",
                _ => ((DevelopMode)developModeProperty.enumValueIndex).ToString()
            };
        }

        /// <summary>
        /// 获取整行 DevelopMode 标签颜色。
        /// </summary>
        private static string ResolveDevelopModeColorHex(SerializedProperty developModeProperty)
        {
            if (developModeProperty.hasMultipleDifferentValues)
            {
                return "#C9A227";
            }

            return (DevelopMode)developModeProperty.enumValueIndex switch
            {
                DevelopMode.Debug => "#2D8CFF",
                DevelopMode.Release => "#20A162",
                _ => "#C9A227"
            };
        }

        /// <summary>
        /// 初始化模板路径属性：绑定 SerializedProperty，若为空则写入默认路径。
        /// 应在子类 OnEnable 中获取 TemplatePath 属性后调用。
        /// </summary>
        /// <param name="templatePathProperty">TemplatePath 的 SerializedProperty。</param>
        protected void InitializeTemplatePath(SerializedProperty templatePathProperty)
        {
            m_TemplatePath = templatePathProperty;
            if (m_TemplatePath != null && string.IsNullOrEmpty(m_TemplatePath.stringValue))
            {
                m_TemplatePath.stringValue = EditorUtil.FileSystem.ResolveTemplatePath(TemplateFileName);
                m_TemplatePath.serializedObject.ApplyModifiedProperties();
            }
        }

        /// <summary>
        /// 根据当前模板路径（m_TemplatePath）解析并返回模板文件的操作系统绝对路径。
        /// Packages/ 开头的路径直接调用 GetFullPath，其余视为工程相对路径拼接 Application.dataPath/../。
        /// </summary>
        /// <returns>模板文件的绝对路径。</returns>
        protected string GetTemplateFullPath()
        {
            return GetTemplateFullPath(m_TemplatePath);
        }

        /// <summary>
        /// 根据指定模板路径属性解析并返回模板文件的操作系统绝对路径。
        /// Packages/ 开头的路径直接调用 GetFullPath，其余视为工程相对路径拼接 Application.dataPath/../。
        /// </summary>
        /// <param name="templatePathProperty">模板路径的 SerializedProperty。</param>
        /// <returns>模板文件的绝对路径。</returns>
        protected string GetTemplateFullPath(SerializedProperty templatePathProperty)
        {
            return templatePathProperty.stringValue.StartsWith("Packages/")
                ? Util.SysIO.Path.GetFullPath(templatePathProperty.stringValue)
                : Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(Application.dataPath, "../" + templatePathProperty.stringValue));
        }

        /// <summary>
        /// 绘制模板文件路径提示行（灰色标签 + 灰色文本框 + 创建 / 打开文件夹按钮），使用默认 m_TemplatePath。
        /// 当源目录为空时，创建按钮将被禁用，但模板路径仍然可见。
        /// </summary>
        /// <param name="sourceDirPathProperty">表格目录位置属性，为空时禁用创建按钮。</param>
        protected void DrawTemplatePathHint(SerializedProperty sourceDirPathProperty)
        {
            DrawTemplatePathHint(sourceDirPathProperty, m_TemplatePath, "模板文件位置：");
        }

        /// <summary>
        /// 绘制模板文件路径提示行（灰色标签 + 灰色文本框 + 创建 / 打开文件夹按钮）。
        /// 当源目录为空时，创建按钮将被禁用，但模板路径仍然可见。
        /// </summary>
        /// <param name="sourceDirPathProperty">表格目录位置属性，为空时禁用创建按钮。</param>
        /// <param name="templatePathProperty">模板路径的 SerializedProperty。</param>
        /// <param name="label">行首标签文本。</param>
        protected void DrawTemplatePathHint(SerializedProperty sourceDirPathProperty, SerializedProperty templatePathProperty, string label)
        {
            if (s_GreyLabelStyle == null)
            {
                s_GreyLabelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.grey } };
            }
            if (s_GreyTextFieldStyle == null)
            {
                s_GreyTextFieldStyle = new GUIStyle(EditorStyles.textField) { normal = { textColor = Color.grey }, focused = { textColor = Color.grey } };
            }

            bool sourceDirEmpty = string.IsNullOrEmpty(sourceDirPathProperty.stringValue);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label(label, s_GreyLabelStyle, false, GUILayout.Width(TemplateLabelWidth));
                string newPath = EditorUtil.Draw.TextField(templatePathProperty.stringValue, s_GreyTextFieldStyle, false, GUILayout.ExpandWidth(true));
                if (newPath != templatePathProperty.stringValue)
                {
                    templatePathProperty.stringValue = newPath;
                }
                EditorUtil.Draw.DisabledGroup(sourceDirEmpty, () =>
                {
                    EditorUtil.Draw.Button("创建", true, () => GenerateFromTemplate(sourceDirPathProperty, templatePathProperty), GUILayout.Width(60));
                });
                EditorUtil.Draw.Button("打开文件夹", false, () =>
                {
                    EditorUtil.FileSystem.OpenFolder(Util.SysIO.Path.GetDirectoryName(GetTemplateFullPath(templatePathProperty)));
                }, GUILayout.Width(163));
            });
        }

        /// <summary>
        /// 绘制只读模板文件路径提示行（灰色标签 + 灰色只读标签 + 创建 / 打开文件夹按钮）。
        /// 模板路径由 ResolveTemplatePath 动态计算，不走序列化。
        /// </summary>
        /// <param name="sourceDirPathProperty">数据源目录属性，为空时禁用创建按钮。</param>
        /// <param name="templateFileName">模板文件名（如 "TableListTemplate.xlsx"）。</param>
        /// <param name="label">行首标签文本。</param>
        /// <param name="labelWidthOverride">标签宽度覆盖值，null 时使用 TemplateLabelWidth。</param>
        protected void DrawTemplatePathHintReadOnly(SerializedProperty sourceDirPathProperty, string templateFileName, string label, float? labelWidthOverride = null)
        {
            if (s_GreyLabelStyle == null)
            {
                s_GreyLabelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.grey } };
            }

            string templatePath = EditorUtil.FileSystem.ResolveTemplatePath(templateFileName);
            bool sourceDirEmpty = string.IsNullOrEmpty(sourceDirPathProperty.stringValue);
            float labelWidth = labelWidthOverride ?? TemplateLabelWidth;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label(label, s_GreyLabelStyle, false, GUILayout.Width(labelWidth));
                EditorUtil.Draw.Label(templatePath, s_GreyLabelStyle, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.DisabledGroup(sourceDirEmpty, () =>
                {
                    EditorUtil.Draw.Button("创建", true, () => GenerateFromTemplateByPath(sourceDirPathProperty, templatePath), GUILayout.Width(60));
                });
                EditorUtil.Draw.Button("打开文件夹", false, () =>
                {
                    string fullPath = ResolveTemplateFullPath(templatePath);
                    EditorUtil.FileSystem.OpenFolder(Util.SysIO.Path.GetDirectoryName(fullPath));
                }, GUILayout.Width(163));
            });
        }

        /// <summary>
        /// 绘制只读模板文件路径提示行，"创建"按钮将模板复制到指定目标目录（而非 sourceDirPathProperty 的值）。
        /// 适用于有地域子目录的模块：传入 region 子目录作为创建目标。
        /// </summary>
        /// <param name="templateFileName">模板文件名。</param>
        /// <param name="label">行首标签文本。</param>
        /// <param name="targetDir">创建按钮的目标目录。为空时禁用创建按钮。</param>
        /// <param name="labelWidthOverride">标签宽度覆盖值，null 时使用 TemplateLabelWidth。</param>
        protected void DrawTemplatePathHintReadOnly(string templateFileName, string label, string targetDir, float? labelWidthOverride = null)
        {
            if (s_GreyLabelStyle == null)
            {
                s_GreyLabelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.grey } };
            }

            string templatePath = EditorUtil.FileSystem.ResolveTemplatePath(templateFileName);
            bool targetEmpty = string.IsNullOrEmpty(targetDir);
            float labelWidth = labelWidthOverride ?? TemplateLabelWidth;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label(label, s_GreyLabelStyle, false, GUILayout.Width(labelWidth));
                EditorUtil.Draw.Label(templatePath, s_GreyLabelStyle, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.DisabledGroup(targetEmpty, () =>
                {
                    EditorUtil.Draw.Button("创建", true, () =>
                    {
                        Util.SysIO.Directory.CreateIfNotExist(targetDir);
                        GenerateTemplateToDirectory(targetDir, templatePath);
                    }, GUILayout.Width(60));
                });
                EditorUtil.Draw.Button("打开文件夹", false, () =>
                {
                    string fullPath = ResolveTemplateFullPath(templatePath);
                    EditorUtil.FileSystem.OpenFolder(Util.SysIO.Path.GetDirectoryName(fullPath));
                }, GUILayout.Width(163));
            });
        }

        /// <summary>
        /// 绘制只读模板文件路径提示行（灰色标签 + 灰色只读标签 + 打开文件夹按钮）。
        /// 适用于只需要展示模板位置、不需要创建按钮的面板。
        /// </summary>
        /// <param name="templateFileName">模板文件名。</param>
        /// <param name="label">行首标签文本。</param>
        /// <param name="labelWidthOverride">标签宽度覆盖值，null 时使用 TemplateLabelWidth。</param>
        protected void DrawTemplatePathHintReadOnlyOpenFolderOnly(string templateFileName, string label, float? labelWidthOverride = null)
        {
            if (s_GreyLabelStyle == null)
            {
                s_GreyLabelStyle = new GUIStyle(EditorStyles.label) { normal = { textColor = Color.grey } };
            }

            string templatePath = EditorUtil.FileSystem.ResolveTemplatePath(templateFileName);
            float labelWidth = labelWidthOverride ?? TemplateLabelWidth;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label(label, s_GreyLabelStyle, false, GUILayout.Width(labelWidth));
                EditorUtil.Draw.Label(templatePath, s_GreyLabelStyle, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Button("打开文件夹", false, () =>
                {
                    string fullPath = ResolveTemplateFullPath(templatePath);
                    EditorUtil.FileSystem.OpenFolder(Util.SysIO.Path.GetDirectoryName(fullPath));
                }, GUILayout.Width(163));
            });
        }

        /// <summary>
        /// 将指定模板路径的文件复制到数据源目录，不依赖 SerializedProperty。
        /// </summary>
        /// <param name="sourceDirPathProperty">数据源目录属性。</param>
        /// <param name="templatePath">模板文件的工程相对路径。</param>
        protected void GenerateFromTemplateByPath(SerializedProperty sourceDirPathProperty, string templatePath)
        {
            string targetDir = sourceDirPathProperty.stringValue;
            Util.SysIO.Directory.CreateIfNotExist(targetDir);
            GenerateTemplateToDirectory(targetDir, templatePath);
        }

        /// <summary>
        /// 将指定模板路径的文件复制到目标目录。
        /// </summary>
        /// <param name="targetDir">目标目录路径。</param>
        /// <param name="templatePath">模板文件的工程相对路径。</param>
        protected static void GenerateTemplateToDirectory(string targetDir, string templatePath)
        {
            string templateFullPath = ResolveTemplateFullPath(templatePath);
            if (!Util.SysIO.File.Exists(templateFullPath))
            {
                EditorUtility.DisplayDialog("错误", $"模板文件不存在：{templatePath}", "确定");
                return;
            }

            if (string.IsNullOrEmpty(targetDir) || !Util.SysIO.Directory.Exists(targetDir))
            {
                EditorUtility.DisplayDialog("错误", "数据源目录不存在，请先配置目录路径。", "确定");
                return;
            }

            string fileName = Util.SysIO.Path.GetFileName(templateFullPath);
            string destPath = Util.SysIO.Path.Combine(targetDir, fileName);
            Util.SysIO.File.Copy(templateFullPath, destPath, true);

            EditorUtil.FileSystem.RefreshDelayed();
        }

        /// <summary>
        /// 解析模板文件路径为操作系统绝对路径。
        /// Packages/ 开头直接 GetFullPath，其余为工程相对路径。
        /// </summary>
        /// <param name="templatePath">模板文件的工程相对路径。</param>
        /// <returns>模板文件的绝对路径。</returns>
        private static string ResolveTemplateFullPath(string templatePath)
        {
            return templatePath.StartsWith("Packages/")
                ? Util.SysIO.Path.GetFullPath(templatePath)
                : Util.SysIO.Path.GetFullPath(Util.SysIO.Path.Combine(Application.dataPath, "../" + templatePath));
        }

        /// <summary>
        /// 将默认模板文件直接复制到数据源目录，最后刷新资源库。
        /// </summary>
        /// <param name="sourceDirPathProperty">SourceDirPath 序列化属性，指向数据源目录路径。</param>
        protected void GenerateFromTemplate(SerializedProperty sourceDirPathProperty)
        {
            GenerateFromTemplate(sourceDirPathProperty, m_TemplatePath);
        }

        /// <summary>
        /// 将指定模板文件直接复制到数据源目录，最后刷新资源库。
        /// </summary>
        /// <param name="sourceDirPathProperty">SourceDirPath 序列化属性，指向数据源目录路径。</param>
        /// <param name="templatePathProperty">模板路径的 SerializedProperty。</param>
        protected void GenerateFromTemplate(SerializedProperty sourceDirPathProperty, SerializedProperty templatePathProperty)
        {
            GenerateTemplateToDirectory(sourceDirPathProperty.stringValue, templatePathProperty.stringValue);
        }

    }
}
