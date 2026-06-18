/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DebugComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/27
 * descrip:   Debug 组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using System;
using Newtonsoft.Json.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class DebugComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 调试器工具配置在 Library 目录下的存储路径。
        /// </summary>
        private static string ConfigFilePath => Util.SysIO.Path.GetFullPath(
            Util.SysIO.Path.Combine(Application.dataPath, c_ConfigFileRelPath));

        /// <summary>
        /// 默认 AAB 输出目录。
        /// </summary>
        private static string DefaultAABFolder => Util.SysIO.Path.GetFullPath(
            Util.SysIO.Path.Combine(Application.dataPath, c_AABOutputRelDir));

        /// <summary>
        /// 默认 APK 输出目录。
        /// </summary>
        private static string DefaultAPKFolder => Util.SysIO.Path.GetFullPath(
            Util.SysIO.Path.Combine(Application.dataPath, c_APKOutputRelDir));

        /// <summary>
        /// AAB 安装脚本路径。
        /// </summary>
        private static string InstallAABScriptPath => Util.SysIO.Path.GetFullPath(c_InstallAABScriptPackagePath);

        /// <summary>
        /// APK 安装脚本路径。
        /// </summary>
        private static string InstallAPKScriptPath => Util.SysIO.Path.GetFullPath(c_InstallAPKScriptPackagePath);

        /// <summary>
        /// 从 Library 缓存文件中读取 Android 构建工具配置。
        /// </summary>
        private void ReadConfig()
        {
            if (!Util.SysIO.File.Exists(ConfigFilePath))
            {
                WriteConfig();
                return;
            }

            try
            {
                string content = Util.SysIO.File.ReadAllTextSync(ConfigFilePath);
                JObject json = JObject.Parse(content);
                m_BundleToolPath = json.ContainsKey("BundleToolPath") ? json["BundleToolPath"].ToString() : "None";
                m_ADBPath = json.ContainsKey("ADBPath") ? json["ADBPath"].ToString() : "None";
                m_HistoryAABPath = json.ContainsKey("HistoryAABPath") ? json["HistoryAABPath"].ToString() : DefaultAABFolder;
                m_HistoryAPKPath = json.ContainsKey("HistoryAPKPath") ? json["HistoryAPKPath"].ToString() : DefaultAPKFolder;
                m_AndroidSignaturePath = json.ContainsKey("SignaturePath") ? json["SignaturePath"].ToString() : "None";
                m_AndroidSignaturePass = json.ContainsKey("SignaturePass") ? json["SignaturePass"].ToString() : string.Empty;
                m_AndroidSignatureAlias = json.ContainsKey("SignatureAlias") ? json["SignatureAlias"].ToString() : string.Empty;
                m_AndroidSignatureAliasPass = json.ContainsKey("SignatureAliasPass") ? json["SignatureAliasPass"].ToString() : string.Empty;
                m_BuildClearCache = json.ContainsKey("ClearCache") && (bool)json["ClearCache"];
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Editor, $"读取调试器配置失败：{e.Message}，已重置为默认值。");
                ResetConfigToDefault();
                WriteConfig();
            }
        }

        /// <summary>
        /// 将当前 Android 构建工具配置写入 Library 缓存文件。
        /// </summary>
        private void WriteConfig()
        {
            try
            {
                Util.SysIO.Directory.CreateIfNotExist(Util.SysIO.Path.GetDirectoryName(ConfigFilePath));

                JObject json = new JObject
                {
                    ["BundleToolPath"] = m_BundleToolPath ?? "None",
                    ["ADBPath"] = m_ADBPath ?? "None",
                    ["HistoryAABPath"] = m_HistoryAABPath ?? DefaultAABFolder,
                    ["HistoryAPKPath"] = m_HistoryAPKPath ?? DefaultAPKFolder,
                    ["SignaturePath"] = m_AndroidSignaturePath ?? "None",
                    ["SignaturePass"] = m_AndroidSignaturePass ?? string.Empty,
                    ["SignatureAlias"] = m_AndroidSignatureAlias ?? string.Empty,
                    ["SignatureAliasPass"] = m_AndroidSignatureAliasPass ?? string.Empty,
                    ["ClearCache"] = m_BuildClearCache
                };
                Util.SysIO.File.WriteAllTextSync(ConfigFilePath, json.ToString());
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Editor, $"写入调试器配置失败：{e.Message}");
            }
        }

        /// <summary>
        /// 重置 Android 构建工具配置为默认值。
        /// </summary>
        private void ResetConfigToDefault()
        {
            m_BundleToolPath = "None";
            m_ADBPath = "None";
            m_HistoryAABPath = DefaultAABFolder;
            m_HistoryAPKPath = DefaultAPKFolder;
            m_AndroidSignaturePath = "None";
            m_AndroidSignaturePass = string.Empty;
            m_AndroidSignatureAlias = string.Empty;
            m_AndroidSignatureAliasPass = string.Empty;
            m_BuildClearCache = false;
        }

        /// <summary>
        /// 构建 AAB 包体，返回生成的 AAB 文件绝对路径；失败时返回 null。
        /// </summary>
        /// <returns>AAB 文件绝对路径，失败时返回 null。</returns>
        private string BuildAAB()
        {
            try
            {
                string folder = DefaultAABFolder;
                Util.SysIO.Directory.CreateIfNotExist(folder);

                string fileName = $"{Application.productName}_{Application.version}.aab";
                string outputPath = Util.SysIO.Path.Combine(folder, fileName);

                EditorUserBuildSettings.buildAppBundle = true;
                PlayerSettings.Android.keystoreName = m_AndroidSignaturePath != "None" ? m_AndroidSignaturePath : string.Empty;
                PlayerSettings.Android.keystorePass = m_AndroidSignaturePass;
                PlayerSettings.Android.keyaliasName = m_AndroidSignatureAlias;
                PlayerSettings.Android.keyaliasPass = m_AndroidSignatureAliasPass;

                var buildOptions = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    options = m_BuildClearCache ? BuildOptions.CleanBuildCache : BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(buildOptions);
                if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    Log.Debug(LogTag.Editor, $"构建 AAB 成功：{outputPath}");
                    return outputPath;
                }

                Log.Error(LogTag.Editor, $"构建 AAB 失败：{report.summary.totalErrors} 个错误。");
                return null;
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Editor, $"构建 AAB 异常：{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 构建 APK 包体，返回生成的 APK 文件绝对路径；失败时返回 null。
        /// </summary>
        /// <returns>APK 文件绝对路径，失败时返回 null。</returns>
        private string BuildAPK()
        {
            try
            {
                string folder = DefaultAPKFolder;
                Util.SysIO.Directory.CreateIfNotExist(folder);

                string fileName = $"{Application.productName}_{Application.version}.apk";
                string outputPath = Util.SysIO.Path.Combine(folder, fileName);

                EditorUserBuildSettings.buildAppBundle = false;
                PlayerSettings.Android.keystoreName = m_AndroidSignaturePath != "None" ? m_AndroidSignaturePath : string.Empty;
                PlayerSettings.Android.keystorePass = m_AndroidSignaturePass;
                PlayerSettings.Android.keyaliasName = m_AndroidSignatureAlias;
                PlayerSettings.Android.keyaliasPass = m_AndroidSignatureAliasPass;

                var buildOptions = new BuildPlayerOptions
                {
                    scenes = EditorBuildSettingsScene.GetActiveSceneList(EditorBuildSettings.scenes),
                    locationPathName = outputPath,
                    target = BuildTarget.Android,
                    options = m_BuildClearCache ? BuildOptions.CleanBuildCache : BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(buildOptions);
                if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                {
                    Log.Debug(LogTag.Editor, $"构建 APK 成功：{outputPath}");
                    EditorUtil.FileSystem.OpenFolder(folder);
                    return outputPath;
                }

                Log.Error(LogTag.Editor, $"构建 APK 失败：{report.summary.totalErrors} 个错误。");
                return null;
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Editor, $"构建 APK 异常：{e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 调用 Python 脚本将 AAB 转换为 APKS 并安装到已连接的 Android 设备。
        /// </summary>
        /// <param name="aabPath">AAB 文件绝对路径。</param>
        /// <param name="apksOutputPath">转换后 APKS 文件的输出路径。</param>
        /// <returns>启动安装进程是否成功。</returns>
        private bool InstallAABToDevice(string aabPath, string apksOutputPath)
        {
            Log.Debug(LogTag.Editor, $"安装 AAB：{aabPath}，输出 APKS：{apksOutputPath}");
            return RunPython(InstallAABScriptPath, $"\"{aabPath}\" \"{apksOutputPath}\" \"{m_BundleToolPath}\" \"{m_ADBPath}\" \"{m_AndroidSignaturePath}\" \"{m_AndroidSignatureAlias}\" \"{m_AndroidSignaturePass}\" \"{m_AndroidSignatureAliasPass}\"");
        }

        /// <summary>
        /// 调用 Python 脚本通过 adb 安装 APK 到已连接的 Android 设备。
        /// </summary>
        /// <param name="apkPath">APK 文件绝对路径。</param>
        /// <returns>启动安装进程是否成功。</returns>
        private bool InstallAPKToDevice(string apkPath)
        {
            Log.Debug(LogTag.Editor, $"安装 APK：{apkPath}");
            return RunPython(InstallAPKScriptPath, $"\"{apkPath}\" \"{m_ADBPath}\"");
        }

        /// <summary>
        /// 启动 Python 脚本进程（非阻塞，脚本输出重定向到 Unity Console）。
        /// </summary>
        /// <param name="scriptPath">Python 脚本绝对路径。</param>
        /// <param name="arguments">传入脚本的命令行参数字符串。</param>
        /// <returns>进程启动成功返回 true。</returns>
        private bool RunPython(string scriptPath, string arguments)
        {
            if (!Util.SysIO.File.Exists(scriptPath))
            {
                Log.Error(LogTag.Editor, "Python 脚本不存在：{0}", scriptPath);
                return false;
            }

            return EditorUtil.ProcessRunner.RunAsync(
                "python",
                Txt.Format("\"{0}\" {1}", scriptPath, arguments),
                onStdout: line => { if (!string.IsNullOrEmpty(line)) Log.Debug(LogTag.Editor, line); },
                onStderr: line => { if (!string.IsNullOrEmpty(line)) Log.Warning(LogTag.Editor, line); });
        }

        /// <summary>
        /// 绘制 Debug 管理器选择器、Debugger 激活类型与 Console 最大日志条数配置。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("Debug 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, UnityEngine.GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IDebugManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();

            EditorUtil.Draw.EnumSelector<DebuggerActiveType>("Debugger 激活类型", m_DebuggerActiveType, true, null, UnityEngine.GUILayout.Width(180f));
            EditorUtil.Draw.IntSlider("Console 最大日志条数", m_MaximumConsoleEntries, 0, 20000, true, null, null, UnityEngine.GUILayout.Width(180f));
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制磁盘监控折叠面板。
        /// </summary>
        private void DrawDiskMonitoring()
        {
            if (!EditorUtil.Draw.Foldout("磁盘监控"))
            {
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1)请按 Persistent 路径所在磁盘的剩余空间从小到大顺序配置阈值",
                    "(2)运行时触及阈值时框架抛出事件",
                    "(3)业务侧需在事件回调中处理后续逻辑"
                });

                for (int index = 0; index < m_DiskCheckingConfigs.arraySize; index++)
                {
                    SerializedProperty element = m_DiskCheckingConfigs.GetArrayElementAtIndex(index);
                    string platformName = element.FindPropertyRelative("PlatformName").stringValue;
                    SerializedProperty enabledProp = element.FindPropertyRelative("Enabled");
                    SerializedProperty availableSpaces = element.FindPropertyRelative("AvailableSpaces");
                    SerializedProperty availableSpacesIntervals = element.FindPropertyRelative("AvailableSpacesIntervals");

                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.DisabledGroup(Application.isPlaying, () =>
                        {
                            GUI.color = Color.cyan;
                            enabledProp.boolValue = EditorUtil.Draw.ToggleLeft($"[{platformName} 磁盘监控节点]", enabledProp.boolValue, false, GUILayout.Width(160));
                            GUI.color = Color.white;
                        });

                        if (enabledProp.boolValue)
                        {
                            int arraySize = Mathf.Max(EditorUtil.Draw.IntField(availableSpaces.arraySize, false, GUILayout.Width(60)), 0);
                            availableSpaces.arraySize = arraySize;
                            availableSpacesIntervals.arraySize = arraySize;
                        }
                    });

                    if (enabledProp.boolValue)
                    {
                        for (int i = 0; i < availableSpaces.arraySize; i++)
                        {
                            int rowIndex = i;
                            EditorUtil.Draw.Layout.Horizontal(() =>
                            {
                                if (rowIndex < availableSpaces.arraySize - 1)
                                {
                                    EditorUtil.Draw.Label($"[{rowIndex + 1}] 当剩余磁盘空间 <=", false, GUILayout.Width(140));
                                    availableSpaces.GetArrayElementAtIndex(rowIndex).intValue = EditorUtil.Draw.IntField(availableSpaces.GetArrayElementAtIndex(rowIndex).intValue, false, GUILayout.Width(60));
                                    EditorUtil.Draw.Label("MB 时，检测时间间隔为", false, GUILayout.Width(140));
                                }
                                else
                                {
                                    string rowLabel = rowIndex == 0 ? $"[{rowIndex + 1}] 检测时间间隔为" : $"[{rowIndex + 1}] 其他情况时，检测时间间隔为";
                                    EditorUtil.Draw.Label(rowLabel, false, GUILayout.Width(180));
                                }
                                availableSpacesIntervals.GetArrayElementAtIndex(rowIndex).floatValue = EditorUtil.Draw.FloatField(availableSpacesIntervals.GetArrayElementAtIndex(rowIndex).floatValue, false, GUILayout.Width(60));
                                EditorUtil.Draw.Label("秒。", false);
                            });
                        }
                    }
                    EditorUtil.Draw.Space(5);
                }
            });
            EditorUtil.Draw.DecreaseIndentLevel();
        }

        /// <summary>
        /// 绘制构建安装（Android）折叠面板。
        /// </summary>
        private void DrawAndroidBuild()
        {
            if (!EditorUtil.Draw.Foldout("构建安装 (Android)"))
            {
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)bundletool 工具位于 Unity 安装目录 Editor/Data/PlaybackEngines/AndroidPlayer/Tools 下",
                "(2)adb 工具位于 Android SDK/platform-tools 目录下"
            });

            bool tmpClearCache = EditorUtil.Draw.Toggle("编译时清空缓存", m_BuildClearCache, false);
            if (tmpClearCache != m_BuildClearCache)
            {
                m_BuildClearCache = tmpClearCache;
                WriteConfig();
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label($"签名文件路径：{m_AndroidSignaturePath}", false);
                    EditorUtil.Draw.Button("选择文件", false, () =>
                    {
                        string selected = EditorUtility.OpenFilePanel("选择签名文件", Application.dataPath, "keystore");
                        if (!string.IsNullOrEmpty(selected))
                        {
                            m_AndroidSignaturePath = selected;
                            WriteConfig();
                        }
                    }, GUILayout.MaxWidth(60));
                    EditorUtil.Draw.Button("打开目录", false, () => EditorUtil.FileSystem.OpenFolder(m_AndroidSignaturePath), GUILayout.MaxWidth(60));
                });

                if (!Util.SysIO.File.Exists(m_AndroidSignaturePath))
                {
                    m_AndroidSignaturePath = "None";
                }

                DrawTextFieldWithWrite("签名 Pass", ref m_AndroidSignaturePass);
                DrawTextFieldWithWrite("签名 Alias", ref m_AndroidSignatureAlias);
                DrawTextFieldWithWrite("签名 AliasPass", ref m_AndroidSignatureAliasPass);
            });

            bool bundletoolExists = false;
            bool adbExists = false;

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                bundletoolExists = DrawToolPathRow("bundletool 路径：", ref m_BundleToolPath, "jar");
                adbExists = DrawToolPathRow("adb 路径：", ref m_ADBPath, Application.platform == RuntimePlatform.WindowsEditor ? "exe" : string.Empty);

                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label($"aab 目录：{DefaultAABFolder}", false);
                    EditorUtil.Draw.Button("打开目录", false, () => EditorUtil.FileSystem.OpenFolder(DefaultAABFolder), GUILayout.Width(60));
                });

                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label($"apk 目录：{DefaultAPKFolder}", false);
                    EditorUtil.Draw.Button("打开目录", false, () => EditorUtil.FileSystem.OpenFolder(DefaultAPKFolder), GUILayout.Width(60));
                });
            });

            bool isSignatureReady = m_AndroidSignaturePath != "None"
                && !string.IsNullOrEmpty(m_AndroidSignaturePass)
                && !string.IsNullOrEmpty(m_AndroidSignatureAlias)
                && !string.IsNullOrEmpty(m_AndroidSignatureAliasPass);

            EditorUtil.Draw.DisabledGroup(!isSignatureReady || !bundletoolExists || !adbExists, () =>
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Button("构建 AAB", true, () => BuildAAB());
                    EditorUtil.Draw.Button("构建 APK", true, () => BuildAPK());
                });

                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Button("安装 AAB 到设备", true, () =>
                    {
                        string selected = EditorUtility.OpenFilePanel("选择 AAB 安装包", m_HistoryAABPath, "aab");
                        if (!string.IsNullOrEmpty(selected))
                        {
                            string curFolder = Util.SysIO.Directory.GetPath(selected);
                            if (curFolder != m_HistoryAABPath)
                            {
                                m_HistoryAABPath = curFolder;
                                WriteConfig();
                            }
                            InstallAABToDevice(selected, selected.Replace(".aab", ".apks"));
                        }
                    });
                    EditorUtil.Draw.Button("安装 APK 到设备", true, () =>
                    {
                        string selected = EditorUtility.OpenFilePanel("选择 APK 安装包", m_HistoryAPKPath, "apk");
                        if (!string.IsNullOrEmpty(selected))
                        {
                            string curFolder = Util.SysIO.Directory.GetPath(selected);
                            if (curFolder != m_HistoryAPKPath)
                            {
                                m_HistoryAPKPath = curFolder;
                                WriteConfig();
                            }
                            InstallAPKToDevice(selected);
                        }
                    });
                });

                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Button("构建 AAB 并安装到设备", true, () =>
                    {
                        string aabPath = BuildAAB();
                        if (!string.IsNullOrEmpty(aabPath))
                        {
                            InstallAABToDevice(aabPath, aabPath.Replace(".aab", ".apks"));
                        }
                    });
                    EditorUtil.Draw.Button("构建 APK 并安装到设备", true, () =>
                    {
                        string apkPath = BuildAPK();
                        if (!string.IsNullOrEmpty(apkPath))
                        {
                            InstallAPKToDevice(apkPath);
                        }
                    });
                });
            });
            EditorUtil.Draw.DecreaseIndentLevel();
        }

        /// <summary>
        /// 绘制带即时写入 config 的文本输入行。
        /// </summary>
        /// <param name="label">显示标签。</param>
        /// <param name="value">绑定的字符串引用，输入变化时自动写入 config。</param>
        private void DrawTextFieldWithWrite(string label, ref string value)
        {
            string newVal = EditorUtil.Draw.TextField(label, value, false);
            if (newVal != value)
            {
                value = newVal;
                WriteConfig();
            }
        }

        /// <summary>
        /// 绘制工具路径行（显示路径 + 选择工具按钮 + 打开目录按钮），返回工具文件是否存在。
        /// 注意：此方法使用 ref 参数接收路径，Lambda 无法捕获 ref 参数，
        /// 因此此处保留直接使用 GUILayout.Button 而非 EditorUtil.Draw.Button。
        /// </summary>
        /// <param name="label">路径显示标签。</param>
        /// <param name="path">绑定的路径引用，选择文件后自动更新并写入 config。</param>
        /// <param name="extension">文件选择面板的扩展名过滤。</param>
        /// <returns>工具文件存在返回 true。</returns>
        private bool DrawToolPathRow(string label, ref string path, string extension)
        {
            // ref 参数无法被 Lambda 捕获，使用局部变量中转。
            // 注意：GUIUtility.ExitGUI() 会抛出 ExitGUIException 穿透 Horizontal()，
            // 导致 Horizontal() 之后的 path = localPath 无法执行。
            // 因此使用 needsExitGUI 标记，将 path 回写和 ExitGUI 移出 lambda，在 Horizontal() 之后执行。
            string localPath = path;
            bool needsExitGUI = false;
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label($"{label}{localPath}", false);
                if (GUILayout.Button("选择工具", GUILayout.MaxWidth(60)))
                {
                    string selected = EditorUtility.OpenFilePanel($"选择 {label}", Application.dataPath, extension);
                    if (!string.IsNullOrEmpty(selected))
                    {
                        localPath = selected;
                    }
                    needsExitGUI = true;
                }
                if (GUILayout.Button("打开目录", GUILayout.MaxWidth(60)))
                {
                    EditorUtil.FileSystem.OpenFolder(localPath);
                    needsExitGUI = true;
                }
            });
            path = localPath;
            if (needsExitGUI)
            {
                WriteConfig();
                serializedObject.ApplyModifiedProperties();
                GUIUtility.ExitGUI();
            }

            bool exists = path != "None" && Util.SysIO.File.Exists(path);
            if (!exists)
            {
                path = "None";
            }
            return exists;
        }
    }
}
