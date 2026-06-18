/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Draw.Panel.cs
 * author:    taoye
 * created:   2026/1/27
 * descrip:   编辑器绘制工具-面板相关
 ***************************************************************/

using NovaFramework.Runtime;
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
            /// 面板工具。
            /// </summary>
            public static class Panel
            {
                /// <summary>
                /// 选择文件时上次使用的目录（工程相对路径），下次打开选择面板时优先使用。
                /// </summary>
                private static string s_LastSelectFileDirectory;

                /// <summary>
                /// 选择文件夹时上次使用的路径（工程相对路径），下次打开选择面板时优先使用。
                /// </summary>
                private static string s_LastSelectFolderDirectory;

                /// <summary>
                /// 文件选择面板（异步，通过 delayCall 延迟弹出）。若 property 当前值有效则优先使用；否则使用历史目录（若有效）；否则使用工程根目录（Assets 上一级）。选择后自动更新历史记录。
                /// </summary>
                /// <param name="title">标题。</param>
                /// <param name="directory">默认目录（传空则自动计算）。</param>
                /// <param name="extension">扩展名。</param>
                /// <param name="property">绑定的 SerializedProperty，可选。</param>
                /// <param name="onComplete">完成回调，可选。</param>
                public static void SelectFileDelay(string title, string directory, string extension, SerializedProperty property = null, System.Action onComplete = null)
                {
                    string initialDir = directory;
                    if (string.IsNullOrEmpty(initialDir))
                    {
                        initialDir = GetInitialDirectoryForSelectFile(property?.stringValue);
                    }

                    EditorApplication.delayCall += () =>
                    {
                        string path = EditorUtility.OpenFilePanel(title, initialDir, extension);
                        if (!string.IsNullOrEmpty(path))
                        {
                            string relativePath = EditorUtil.FileSystem.GetProjectRelativePath(path);
                            if (!string.IsNullOrEmpty(relativePath))
                            {
                                s_LastSelectFileDirectory = Util.SysIO.Path.GetDirectoryName(relativePath);
                                if (property != null)
                                {
                                    property.stringValue = relativePath;
                                    property.serializedObject.ApplyModifiedProperties();
                                }
                            }
                        }
                        onComplete?.Invoke();
                    };
                }

                /// <summary>
                /// 文件夹选择面板（异步，通过 delayCall 延迟弹出）。若 property 当前值有效则优先使用；否则使用历史路径（若有效）；否则使用工程根目录（Assets 上一级）。选择后自动更新历史记录。
                /// </summary>
                /// <param name="title">标题。</param>
                /// <param name="folder">默认文件夹（传空则自动计算）。</param>
                /// <param name="defaultName">默认名称。</param>
                /// <param name="property">绑定的 SerializedProperty，可选。</param>
                /// <param name="onComplete">完成回调，可选。</param>
                public static void SelectFolderDelay(string title, string folder, string defaultName, SerializedProperty property = null, System.Action onComplete = null)
                {
                    string initialFolder = folder;
                    if (string.IsNullOrEmpty(initialFolder))
                    {
                        initialFolder = GetInitialDirectoryForSelectFolder(property?.stringValue);
                    }

                    EditorApplication.delayCall += () =>
                    {
                        string path = EditorUtility.OpenFolderPanel(title, initialFolder, defaultName);
                        if (!string.IsNullOrEmpty(path))
                        {
                            string relativePath = EditorUtil.FileSystem.GetProjectRelativePath(path);
                            if (!string.IsNullOrEmpty(relativePath))
                            {
                                s_LastSelectFolderDirectory = relativePath;
                                if (property != null)
                                {
                                    property.stringValue = relativePath;
                                    property.serializedObject.ApplyModifiedProperties();
                                }
                            }
                        }
                        onComplete?.Invoke();
                    };
                }

                /// <summary>
                /// 选择文件夹后将「文件夹路径 + 原文件名」组合写回 property（异步，通过 delayCall 延迟弹出）。
                /// 用于「字段语义为文件路径，但只允许选择目录」的场景：用户先选目录，组件保留原文件名，未指定文件名时写为「目录/」交由用户在文本框补全。
                /// </summary>
                /// <param name="title">标题。</param>
                /// <param name="folder">默认文件夹（传空则自动计算）。</param>
                /// <param name="property">绑定的 SerializedProperty（字符串类型，期望为文件路径）。</param>
                /// <param name="onComplete">完成回调，可选。</param>
                public static void SelectFolderForFileDelay(string title, string folder, SerializedProperty property = null, System.Action onComplete = null)
                {
                    string current = property?.stringValue;
                    string initialFolder = folder;
                    if (string.IsNullOrEmpty(initialFolder))
                    {
                        if (!string.IsNullOrEmpty(current))
                        {
                            string dir = Util.SysIO.Path.GetDirectoryName(current);
                            if (!string.IsNullOrEmpty(dir) && Util.SysIO.Directory.Exists(dir))
                            {
                                initialFolder = dir;
                            }
                        }
                        if (string.IsNullOrEmpty(initialFolder))
                        {
                            initialFolder = GetInitialDirectoryForSelectFolder(null);
                        }
                    }

                    EditorApplication.delayCall += () =>
                    {
                        string path = EditorUtility.OpenFolderPanel(title, initialFolder, "");
                        if (!string.IsNullOrEmpty(path))
                        {
                            string relativePath = EditorUtil.FileSystem.GetProjectRelativePath(path);
                            if (!string.IsNullOrEmpty(relativePath))
                            {
                                s_LastSelectFolderDirectory = relativePath;
                                if (property != null)
                                {
                                    string fileName = !string.IsNullOrEmpty(current) ? Util.SysIO.Path.GetFileName(current) : string.Empty;
                                    string folderNorm = relativePath.TrimEnd('/', '\\');
                                    string newValue = string.IsNullOrEmpty(fileName) || fileName == "/" || fileName == "\\"
                                        ? folderNorm + "/"
                                        : folderNorm + "/" + fileName;
                                    property.stringValue = newValue;
                                    property.serializedObject.ApplyModifiedProperties();
                                }
                            }
                        }
                        onComplete?.Invoke();
                    };
                }

                /// <summary>
                /// 文件夹选择面板（同步）。直接返回用户选择的工程相对路径；用户取消时返回空字符串。
                /// 适用于需要同步拿到路径后立即处理的场景（如逐文件代码生成）。
                /// 与 SelectFolderDelay 的区别：不使用 delayCall，在当前调用栈内同步弹出并等待用户操作。
                /// </summary>
                /// <param name="title">标题。</param>
                /// <param name="folder">默认文件夹（传空则自动计算）。</param>
                /// <param name="defaultName">默认名称。</param>
                /// <returns>用户选择的工程相对路径；取消时返回空字符串。</returns>
                public static string SelectFolder(string title, string folder = "", string defaultName = "")
                {
                    string initialFolder = string.IsNullOrEmpty(folder)
                        ? GetInitialDirectoryForSelectFolder(null)
                        : folder;

                    string path = EditorUtility.OpenFolderPanel(title, initialFolder, defaultName);
                    if (string.IsNullOrEmpty(path))
                        return string.Empty;

                    string relativePath = EditorUtil.FileSystem.GetProjectRelativePath(path);
                    if (!string.IsNullOrEmpty(relativePath))
                        s_LastSelectFolderDirectory = relativePath;

                    return relativePath;
                }

                /// <summary>
                /// 保存文件面板（异步，通过 delayCall 延迟弹出）。弹出系统文件保存对话框，让用户选择保存位置。
                /// 选择后返回完整路径（非工程相对路径），通过 onComplete 回调传出。
                /// </summary>
                /// <param name="title">标题。</param>
                /// <param name="directory">默认目录。</param>
                /// <param name="defaultName">默认文件名。</param>
                /// <param name="extension">扩展名（不含点号，如 "xlsx"）。</param>
                /// <param name="onComplete">完成回调，参数为用户选择的完整路径；用户取消时为空字符串。</param>
                public static void SaveFileDelay(string title, string directory, string defaultName, string extension, System.Action<string> onComplete = null)
                {
                    string initialDir = directory;
                    if (string.IsNullOrEmpty(initialDir))
                    {
                        initialDir = Util.SysIO.Path.GetDirectoryName(Application.dataPath);
                    }

                    EditorApplication.delayCall += () =>
                    {
                        string path = EditorUtility.SaveFilePanel(title, initialDir, defaultName, extension);
                        onComplete?.Invoke(path ?? string.Empty);
                    };
                }

                /// <summary>
                /// 获取选择文件面板的初始目录：历史目录（有效）→ 当前路径所在目录 → 工程根目录（Assets 上一级）。
                /// </summary>
                /// <param name="currentRelativePath">当前已配置的路径（工程相对，可为文件或目录）。</param>
                /// <returns>用于打开文件选择面板的初始目录。</returns>
                private static string GetInitialDirectoryForSelectFile(string currentRelativePath)
                {
                    if (!string.IsNullOrEmpty(s_LastSelectFileDirectory) && Util.SysIO.Directory.Exists(s_LastSelectFileDirectory))
                    {
                        return s_LastSelectFileDirectory;
                    }
                    if (!string.IsNullOrEmpty(currentRelativePath))
                    {
                        string dir = Util.SysIO.Path.GetDirectoryName(currentRelativePath);
                        if (!string.IsNullOrEmpty(dir) && Util.SysIO.Directory.Exists(dir))
                        {
                            return dir;
                        }
                    }
                    return Util.SysIO.Path.GetDirectoryName(Application.dataPath);
                }

                /// <summary>
                /// 弹出确认对话框，返回用户是否点击确认。
                /// </summary>
                /// <param name="title">对话框标题。</param>
                /// <param name="message">提示内容。</param>
                /// <param name="ok">确认按钮文本。</param>
                /// <param name="cancel">取消按钮文本。</param>
                /// <returns>用户是否点击确认。</returns>
                public static bool Confirm(string title, string message, string ok = "确认", string cancel = "取消")
                {
                    return EditorUtility.DisplayDialog(title, message, ok, cancel);
                }

                /// <summary>
                /// 获取选择文件夹面板的初始路径：历史路径（有效）→ 当前路径 → 工程根目录（Assets 上一级）。
                /// </summary>
                /// <param name="currentRelativePath">当前已配置的文件夹路径（工程相对）。</param>
                /// <returns>用于打开文件夹选择面板的初始路径。</returns>
                private static string GetInitialDirectoryForSelectFolder(string currentRelativePath)
                {
                    if (!string.IsNullOrEmpty(s_LastSelectFolderDirectory) && Util.SysIO.Directory.Exists(s_LastSelectFolderDirectory))
                    {
                        return s_LastSelectFolderDirectory;
                    }
                    if (!string.IsNullOrEmpty(currentRelativePath) && Util.SysIO.Directory.Exists(currentRelativePath))
                    {
                        return currentRelativePath;
                    }
                    return Util.SysIO.Path.GetDirectoryName(Application.dataPath);
                }
            }
        }   
    }
}
