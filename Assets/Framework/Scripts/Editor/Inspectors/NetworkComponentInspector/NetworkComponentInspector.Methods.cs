/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NetworkComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Network组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class NetworkComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制四个管理器实现类类型选择器，及说明 HelpBox 与分隔线。
        /// </summary>
        private void DrawManagerSelectors()
        {
            EditorUtil.Draw.TypesSelector("Network 管理器", m_NetworkManagerTypeNames, m_CurNetworkManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.TypesSelector("HTTP 管理器", m_HttpManagerTypeNames, m_CurHttpManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.TypesSelector("DoH 管理器", m_DoHManagerTypeNames, m_CurDoHManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.TypesSelector("WebSocket 管理器", m_WebSocketManagerTypeNames, m_CurWebSocketManagerTypeName, true, null, GUILayout.Width(175));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[]
            {
                "(1)实现 INetworkManager 接口的自定义类型将出现在 Network 管理器列表",
                "(2)实现 IHttpManager 接口的自定义类型将出现在 HTTP 管理器列表",
                "(3)实现 IDoHManager 接口的自定义类型将出现在 DoH 管理器列表",
                "(4)实现 IWebSocketManager 接口的自定义类型将出现在 WebSocket 管理器列表"
            });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制域名表（HostKey）导出区域：模板提示、表格目录、文件树、全局导出按钮。
        /// </summary>
        private void DrawHostKeyExport()
        {
            if (m_HostKeySourceDirPath == null)
            {
                return;
            }

            if (m_HostKeyUnitsSettings == null)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout("域名表格导出", "NetworkHostKeyExport", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            string dirPath = m_HostKeySourceDirPath.stringValue;
            DrawTemplatePathHintReadOnly(c_HostKeyTemplateFileName, "模板文件位置：", dirPath, 102);

            DrawSourceDirRow(m_HostKeySourceDirPath, "表格目录位置：", "选择域名表目录位置");
            if (!string.IsNullOrEmpty(dirPath) && Util.SysIO.Directory.Exists(dirPath))
            {
                if (!m_IsHostKeyLubanConfigExists)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "Luban 配置目录 (_configs/) 尚未初始化，首次导出时将自动创建。" });
                }

                DrawNetworkSourceFilesListWithFolders(dirPath, m_HostKeyUnitsSettings, m_HostKeyFolderFoldoutState, "HostKeySettings", "network-hostkey", "HostKeyTables");
                EditorUtil.Draw.Button("导出所有域名表数据和类型", true, () =>
                {
                    EditorUtil.Luban.DataTypeNameHelper.DoRefreshAllDataTypeNames(dirPath, m_HostKeyUnitsSettings, serializedObject);
                    DoExportAllDataAndTypes(m_HostKeyUnitsSettings, "HostKeySettings");
                });
            }

            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制指令表（NetCmd）导出区域：模板提示、表格目录、文件树、全局导出按钮。
        /// </summary>
        private void DrawNetCmdExport()
        {
            if (m_NetCmdSourceDirPath == null)
            {
                return;
            }

            if (m_NetCmdUnitsSettings == null)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout("指令表格导出", "NetworkNetCmdExport", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            string dirPath = m_NetCmdSourceDirPath.stringValue;
            DrawTemplatePathHintReadOnly(c_NetCmdTemplateFileName, "模板文件位置：", dirPath, 102);

            DrawSourceDirRow(m_NetCmdSourceDirPath, "表格目录位置：", "选择指令表目录位置");
            if (!string.IsNullOrEmpty(dirPath) && Util.SysIO.Directory.Exists(dirPath))
            {
                if (!m_IsNetCmdLubanConfigExists)
                {
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "Luban 配置目录 (_configs/) 尚未初始化，首次导出时将自动创建。" });
                }

                DrawNetworkSourceFilesListWithFolders(dirPath, m_NetCmdUnitsSettings, m_NetCmdFolderFoldoutState, "NetCmdSettings", "network-cmd", "NetworkTables");
                EditorUtil.Draw.Button("导出所有指令表数据和类型", true, () =>
                {
                    EditorUtil.Luban.DataTypeNameHelper.DoRefreshAllDataTypeNames(dirPath, m_NetCmdUnitsSettings, serializedObject);
                    DoExportAllDataAndTypes(m_NetCmdUnitsSettings, "NetCmdSettings");
                });
            }

            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制表格目录位置行（可编辑 + 选择 / 打开文件夹按钮）。
        /// </summary>
        /// <param name="sourceDirPath">数据源目录路径属性。</param>
        /// <param name="label">行首标签文本。</param>
        /// <param name="dialogTitle">选择对话框标题。</param>
        private void DrawSourceDirRow(SerializedProperty sourceDirPath, string label, string dialogTitle)
        {
            if (sourceDirPath == null) return;

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label(label, false, GUILayout.Width(c_LabelWidth));
                EditorUtil.Draw.TextField(sourceDirPath, true);
                EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay(dialogTitle, "", "", sourceDirPath, onComplete: () =>
                {
                    EditorUtil.FileSystem.RefreshDelayed();
                }), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(sourceDirPath.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthLarge));
            });
        }

        /// <summary>
        /// Network 专用的文件树绘制：过滤 _configs/ 和 _temp/ 子目录，通过闭包捕获 settingsPropertyName / targetName / managerName 上下文。
        /// </summary>
        /// <param name="directoryPath">数据源目录完整路径。</param>
        /// <param name="unitsSettingsProp">单元设置列表属性。</param>
        /// <param name="foldoutState">目录树折叠状态字典。</param>
        /// <param name="settingsPropertyName">Settings 内属性名称。</param>
        /// <param name="targetName">Luban target 名称。</param>
        /// <param name="managerName">Luban manager 类名。</param>
        private void DrawNetworkSourceFilesListWithFolders(string directoryPath, SerializedProperty unitsSettingsProp, Dictionary<string, bool> foldoutState, string settingsPropertyName, string targetName, string managerName)
        {
            string rootPathNorm = directoryPath.TrimEnd('/', '\\');
            string configsDirPrefix = (rootPathNorm + "/_configs/").Replace('\\', '/');
            string tempDirPrefix = (rootPathNorm + "/_temp/").Replace('\\', '/');

            EditorUtil.Draw.SourceFileTree.DrawSourceFilesListWithFolders(directoryPath, unitsSettingsProp, foldoutState, 5, files =>
            {
                return files.Where(f =>
                {
                    string norm = f.Replace('\\', '/');
                    return !norm.StartsWith(configsDirPrefix) && !norm.StartsWith(tempDirPrefix);
                }).ToArray();
            }, (filePath, capturedRelativePath, seq, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty) =>
            {
                DrawNetworkSourceFileRow(filePath, capturedRelativePath, seq, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, rootPathNorm, settingsPropertyName, targetName, managerName);
            });
        }

        /// <summary>
        /// 绘制单个数据源文件的所有行：文件名行、数据导出行、类型导出行、Asset 地址行。
        /// 通过闭包捕获 Network 多套导出上下文（rootPathNorm / settingsPropertyName / targetName / managerName）。
        /// </summary>
        /// <param name="filePath">文件完整路径。</param>
        /// <param name="capturedRelativePath">文件相对路径。</param>
        /// <param name="seq">序号。</param>
        /// <param name="indentSpace">缩进像素。</param>
        /// <param name="savedIndent">保存的缩进级别。</param>
        /// <param name="detailProp">当前文件的单元设置属性。</param>
        /// <param name="sourceUnitsSettingsProperty">全部单元设置列表属性。</param>
        /// <param name="rootPathNorm">数据源根目录规范化路径。</param>
        /// <param name="settingsPropertyName">Settings 内属性名称。</param>
        /// <param name="targetName">Luban target 名称。</param>
        /// <param name="managerName">Luban manager 类名。</param>
        private void DrawNetworkSourceFileRow(string filePath, string capturedRelativePath, int seq, float indentSpace, int savedIndent, SerializedProperty detailProp, SerializedProperty sourceUnitsSettingsProperty, string rootPathNorm, string settingsPropertyName, string targetName, string managerName)
        {
            EditorUtil.Draw.SourceFileTree.DrawDefaultFileNameRow(filePath, seq, indentSpace, savedIndent);
            EditorUtil.Draw.SourceFileTree.DrawDataExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, (fp, dep, dp) => DoExportDataForFile(fp, dep, rootPathNorm, settingsPropertyName, targetName, managerName), (fp, dtp) => EditorUtil.Luban.DataTypeNameHelper.DoRefreshDataTypeNames(fp, dtp, serializedObject));
            EditorUtil.Draw.SourceFileTree.DrawClassExportRow(filePath, capturedRelativePath, indentSpace, savedIndent, detailProp, sourceUnitsSettingsProperty, (fp, cep, dp) => DoExportClassForFile(fp, cep, rootPathNorm, settingsPropertyName, targetName, managerName), (fp, dtp) => EditorUtil.Luban.DataTypeNameHelper.DoRefreshDataTypeNames(fp, dtp, serializedObject));
            EditorUtil.Draw.SourceFileTree.DrawAssetLocationRow(detailProp, indentSpace, savedIndent);
        }

        /// <summary>
        /// 对单个数据源文件执行数据导出：预过滤、通过 Pipeline 同步配置、调用 Luban CLI 导出数据、合并 JSON。
        /// </summary>
        /// <param name="filePath">数据源文件的完整路径。</param>
        /// <param name="dataExportPath">导出数据的目标路径。</param>
        /// <param name="regionDirPath">数据源目录路径。</param>
        /// <param name="settingsPropertyName">Settings 内属性名称。</param>
        /// <param name="targetName">Luban target 名称。</param>
        /// <param name="managerName">Luban manager 类名。</param>
        private void DoExportDataForFile(string filePath, string dataExportPath, string regionDirPath, string settingsPropertyName, string targetName, string managerName)
        {
            if (string.IsNullOrEmpty(dataExportPath))
            {
                return;
            }

            IDataTableSettings settings = GetDataTableSettings(settingsPropertyName);
            if (settings == null)
            {
                return;
            }

            string tempDir = EditorUtil.Luban.ExportHelper.GetPreFilterTempDirPath(regionDirPath);
            EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);

            try
            {
                NetworkExcelPreFilter.FilterFile(filePath, tempDir);

                string relativePath = Util.SysIO.Path.GetRelativePath(regionDirPath.TrimEnd('/', '\\'), filePath);
                IReadOnlyList<IDataTableUnitSetting> regionUnits = GetCurrentRegionUnitSettings(settingsPropertyName);
                IDataTableUnitSetting unitSetting = EditorUtil.Luban.ExportHelper.FindUnitSetting(regionUnits, relativePath);
                if (unitSetting == null)
                {
                    Log.Error(LogTag.Editor, "未找到文件 {0} 对应的 UnitSetting。", relativePath);
                    return;
                }

                var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(regionDirPath, settings, targetName, managerName);
                ctx.RegionUnits = regionUnits;
                ctx.TargetUnit = unitSetting;
                EditorUtil.Luban.Pipeline.ExportData(ctx);
            }
            finally
            {
                EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);
            }
        }

        /// <summary>
        /// 对单个数据源文件执行类型导出：预过滤、通过 Pipeline 同步配置、调用 Luban CLI 生成代码。
        /// </summary>
        /// <param name="filePath">数据源文件的完整路径。</param>
        /// <param name="classExportPath">类型导出目标目录。</param>
        /// <param name="regionDirPath">数据源目录路径。</param>
        /// <param name="settingsPropertyName">Settings 内属性名称。</param>
        /// <param name="targetName">Luban target 名称。</param>
        /// <param name="managerName">Luban manager 类名。</param>
        private void DoExportClassForFile(string filePath, string classExportPath, string regionDirPath, string settingsPropertyName, string targetName, string managerName)
        {
            if (string.IsNullOrEmpty(classExportPath))
            {
                return;
            }

            IDataTableSettings settings = GetDataTableSettings(settingsPropertyName);
            if (settings == null)
            {
                return;
            }

            string tempDir = EditorUtil.Luban.ExportHelper.GetPreFilterTempDirPath(regionDirPath);
            EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);

            try
            {
                NetworkExcelPreFilter.FilterAll(regionDirPath, tempDir);

                string relativePath = Util.SysIO.Path.GetRelativePath(regionDirPath.TrimEnd('/', '\\'), filePath);
                IReadOnlyList<IDataTableUnitSetting> regionUnits = GetCurrentRegionUnitSettings(settingsPropertyName);
                IDataTableUnitSetting unitSetting = EditorUtil.Luban.ExportHelper.FindUnitSetting(regionUnits, relativePath);

                var ctx = EditorUtil.Luban.ExportHelper.BuildExportContext(regionDirPath, settings, targetName, managerName);
                ctx.RegionUnits = regionUnits;
                ctx.OutputCodeDir = classExportPath;
                ctx.RelevantFileNames = EditorUtil.Luban.ExportHelper.BuildRelevantFileNames(filePath, regionDirPath, regionUnits, managerName);
                ctx.TargetUnit = unitSetting;
                EditorUtil.Luban.Pipeline.ExportCode(ctx);
            }
            finally
            {
                EditorUtil.Luban.ConfigSyncer.CleanTempDir(tempDir);
            }
        }

        /// <summary>
        /// 导出所有已配置的数据源文件：清理旧导出目录、委托 EditorUtil.Network 执行 Luban 全量导出、更新 Inspector 配置存在状态。
        /// </summary>
        /// <param name="unitsSettingsProp">单元设置列表属性（用于 null 守卫和 ApplyModifiedProperties 前检查）。</param>
        /// <param name="settingsPropertyName">Settings 内属性名称（"HostKeySettings" 或 "NetCmdSettings"）。</param>
        private void DoExportAllDataAndTypes(SerializedProperty unitsSettingsProp, string settingsPropertyName)
        {
            if (unitsSettingsProp == null)
            {
                return;
            }

            serializedObject.ApplyModifiedProperties();

            NetworkComponent networkComponent = (NetworkComponent)target;
            NetworkSettings ns = networkComponent.NetworkSettings;
            if (ns == null)
            {
                return;
            }

            IReadOnlyList<IDataTableUnitSetting> regionUnits = GetCurrentRegionUnitSettings(settingsPropertyName);
            if (regionUnits == null)
            {
                return;
            }

            var dataPathsToClear = new HashSet<string>();
            foreach (IDataTableUnitSetting unit in regionUnits)
            {
                if (!string.IsNullOrEmpty(unit.DatasExportPath))
                {
                    dataPathsToClear.Add(unit.DatasExportPath);
                }
            }
            foreach (string path in dataPathsToClear)
            {
                EditorUtil.FileSystem.DeletePath(path);
            }

            bool success = false;
            if (settingsPropertyName == "HostKeySettings")
            {
                success = EditorUtil.Network.HostKeyExporter.ExportHostKeyAll(ns.HostKeySettings);
                if (success)
                {
                    m_IsHostKeyLubanConfigExists = true;
                }
            }
            else if (settingsPropertyName == "NetCmdSettings")
            {
                success = EditorUtil.Network.NetCmdExporter.ExportNetCmdAll(ns.NetCmdSettings);
                if (success)
                {
                    m_IsNetCmdLubanConfigExists = true;
                }
            }
        }

        /// <summary>
        /// 通过反射获取 IDataTableSettings 实例。
        /// </summary>
        /// <param name="settingsPropertyName">Settings 内属性名称（"HostKeySettings" 或 "NetCmdSettings"）。</param>
        /// <returns>IDataTableSettings 实例，无法获取时返回 null。</returns>
        private IDataTableSettings GetDataTableSettings(string settingsPropertyName)
        {
            NetworkComponent networkComponent = (NetworkComponent)target;
            NetworkSettings ns = networkComponent.NetworkSettings;
            if (ns == null)
            {
                return null;
            }

            if (settingsPropertyName == "HostKeySettings") return ns.HostKeySettings;
            if (settingsPropertyName == "NetCmdSettings") return ns.NetCmdSettings;
            return null;
        }

        /// <summary>
        /// 获取指定 Settings 的单元设置列表。
        /// </summary>
        /// <param name="settingsPropertyName">Settings 内属性名称（"HostKeySettings" 或 "NetCmdSettings"）。</param>
        /// <returns>单元设置列表，无法获取时返回 null。</returns>
        private IReadOnlyList<IDataTableUnitSetting> GetCurrentRegionUnitSettings(string settingsPropertyName)
        {
            NetworkComponent networkComponent = (NetworkComponent)target;
            NetworkSettings ns = networkComponent.NetworkSettings;
            if (ns == null) return null;
            if (settingsPropertyName == "HostKeySettings") return ns.HostKeySettings?.HostKeyUnits;
            if (settingsPropertyName == "NetCmdSettings") return ns.NetCmdSettings?.NetCmdUnits;
            return null;
        }

        /// <summary>
        /// HostKey Luban _configs/ 文件变更回调。
        /// </summary>
        private void OnHostKeyLubanConfigChanged()
        {
            m_IsHostKeyLubanConfigExists = true;
            Repaint();
        }

        /// <summary>
        /// NetCmd Luban _configs/ 文件变更回调。
        /// </summary>
        private void OnNetCmdLubanConfigChanged()
        {
            m_IsNetCmdLubanConfigExists = true;
            Repaint();
        }

        /// <summary>
        /// 绘制 DoH 管理折叠区（UseDoH 开关 / DNS 超时 / 运行时域名 IP 列表），并加分隔线。
        /// </summary>
        private void DrawDoHSettings()
        {
            if (!EditorUtil.Draw.Foldout("DoH 管理", "NetworkDoHSettings", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            EditorUtil.Draw.Toggle("启用 DoH (DNS-over-HTTPS)", m_DoHSettings.FindPropertyRelative("UseDoH"), true, null, null, GUILayout.Width(185));
            EditorUtil.Draw.Property("DNS 查询超时时间 (秒)", m_DoHSettings.FindPropertyRelative("DnsTimeoutSeconds"), true, GUILayout.Width(175));
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "时间 0 表示不限制超时。" }, false, GUILayout.ExpandWidth(true));
            });
            DrawRuntimeDoHAddresses((NetworkComponent)target);
            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制 Http 管理折叠区（连接 / 请求超时 + 默认值提示），并加分隔线。
        /// </summary>
        private void DrawHttpSettings()
        {
            if (!EditorUtil.Draw.Foldout("Http 管理", "NetworkHttpSettings", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            EditorUtil.Draw.Property("HTTP 连接超时时间 (秒)", m_HttpSettings.FindPropertyRelative("ConnectTimeout"), true, GUILayout.Width(175));
            EditorUtil.Draw.Property("HTTP 请求超时时间 (秒)", m_HttpSettings.FindPropertyRelative("RequestTimeout"), true, GUILayout.Width(175));
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1)ConnectTimeout 默认 20 秒",
                    "(2)RequestTimeout 默认 60 秒",
                    "(3)各 API 的 timeout 参数传 -1 时使用此处的默认值"
                }, false, GUILayout.ExpandWidth(true));
            });
            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制 WebSocket 管理折叠区（超时 / 心跳 / 重连 / 运行时通道列表），并加分隔线。
        /// </summary>
        private void DrawWebSocketSettings()
        {
            if (!EditorUtil.Draw.Foldout("WebSocket 管理", "NetworkWebSocketSettings", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            EditorUtil.Draw.Property("WS 连接超时时间 (秒)", m_WebSocketSettings.FindPropertyRelative("ConnectTimeout"), true, GUILayout.Width(175));
            EditorUtil.Draw.Property("WS 身份认证超时时间 (秒)", m_WebSocketSettings.FindPropertyRelative("AuthenticateTimeout"), true, GUILayout.Width(175));
            EditorUtil.Draw.Property("WS 心跳发送间隔 (秒)", m_WebSocketSettings.FindPropertyRelative("HeartBeatTimeInterval"), true, GUILayout.Width(175));
            EditorUtil.Draw.Property("WS 心跳响应超时时间 (秒)", m_WebSocketSettings.FindPropertyRelative("HeartBeatTimeout"), true, GUILayout.Width(175));
            EditorUtil.Draw.Toggle("WS 启用自动重连", m_WebSocketSettings.FindPropertyRelative("EnableAutoReconnect"), true, null, null, GUILayout.Width(175));
            EditorUtil.Draw.DisabledGroup(!m_WebSocketSettings.FindPropertyRelative("EnableAutoReconnect").boolValue, () =>
            {
                EditorUtil.Draw.Property("WS 自动重连最大次数", m_WebSocketSettings.FindPropertyRelative("AutoReconnectMaxCounter"), true, GUILayout.Width(175));
                EditorUtil.Draw.Property("WS 自动重连间隔时间 (秒)", m_WebSocketSettings.FindPropertyRelative("AutoReconnectTimeInterval"), true, GUILayout.Width(175));
                if (EditorUtil.Draw.Foldout("WS 自动重连失败 UI", "WSAutoReconnectFailedUI", true))
                {
                    EditorUtil.Draw.IncreaseIndentLevel();
                    EditorUtil.Draw.Property("Asset 地址", m_WebSocketSettings.FindPropertyRelative("AutoReconnectFailedUIAssetLocation"), true, GUILayout.Width(175));
                    EditorUtil.Draw.DecreaseIndentLevel();
                }
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                    {
                        "(1)断线重连计数器达到最大次数后自动弹出此 UI",
                        "(2)用户手动操作后清空计数器并重新开始计数"
                    }, false, GUILayout.ExpandWidth(true));
                });
            });
            DrawRuntimeWebSocketChannels((NetworkComponent)target);
            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制 WebSocket 通道列表运行时状态，以颜色区分连接状态。
        /// </summary>
        /// <param name="t">目标 NetworkComponent。</param>
        private void DrawRuntimeWebSocketChannels(NetworkComponent t)
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (t.WebSocketManager == null)
            {
                EditorUtil.Draw.HelpBox(MessageType.Warning, new[] { "WebSocketManager 未初始化。" });
                return;
            }

            var channels = t.WebSocketManager.NetChannels;
            if (!EditorUtil.Draw.Foldout($"WebSocket 通道列表 ({channels?.Count ?? 0})", "NetworkWSList"))
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                if (channels == null || channels.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < channels.Count; i++)
                {
                    WebSocketScope.NetChannelBase channel = channels[i];

                    Color prevColor = GUI.color;
                    if (channel.IsConnected) GUI.color = Color.green;
                    else if (channel.IsDisconnectedSubjectively) GUI.color = Color.red;
                    else GUI.color = Color.yellow;

                    bool expanded = EditorUtil.Draw.Foldout($"[{i}] {channel}", $"NetworkWSChannel_{i}");
                    GUI.color = prevColor;

                    if (expanded)
                    {
                        EditorUtil.Draw.Layout.Vertical("box", () =>
                        {
                            EditorUtil.Draw.Label("通道类型", channel.GetNetChannelType().ToString(), false);
                            EditorUtil.Draw.Label("服务器地址", channel.ServerAddress, false);
                            EditorUtil.Draw.Label("已连接", channel.IsConnected.ToString(), false);
                            EditorUtil.Draw.Label("主动断开", channel.IsDisconnectedSubjectively.ToString(), false);
                        });
                    }
                }
            });
        }

        /// <summary>
        /// 绘制 DoH 域名 → IP 列表运行时状态（仅 UseDoH 开启时显示）。
        /// </summary>
        /// <param name="t">目标 NetworkComponent。</param>
        private void DrawRuntimeDoHAddresses(NetworkComponent t)
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (!m_DoHSettings.FindPropertyRelative("UseDoH").boolValue)
            {
                return;
            }

            if (t.DoHManager == null)
            {
                return;
            }

            var domainIPs = t.DoHManager.AllDomainIPAddresses;
            if (!EditorUtil.Draw.Foldout($"DoH 域名 IP 列表 ({domainIPs?.Count ?? 0})", "NetworkDoHList"))
            {
                return;
            }

            EditorUtil.Draw.Layout.Vertical("box", () =>
            {
                if (domainIPs == null || domainIPs.Count == 0)
                {
                    return;
                }

                int idx = 0;
                foreach (var kvp in domainIPs)
                {
                    bool expanded = EditorUtil.Draw.Foldout($"https://{kvp.Key}", $"NetworkDoHHost_{idx}");
                    if (expanded)
                    {
                        EditorUtil.Draw.Layout.Vertical("box", () =>
                        {
                            foreach (var ip in kvp.Value)
                            {
                                EditorUtil.Draw.Label($"https://{ip}", false);
                            }
                        });
                    }

                    idx++;
                }
            });
        }

        /// <summary>
        /// 绘制 Proto 协议管理区域：协议目录 + .proto 文件树 + 类型导出位置 + protoc 编译按钮。
        /// </summary>
        private void DrawProtoManagement()
        {
            if (m_ProtoSettings == null)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout("Proto 协议导出", "NetworkProtoManagement", true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();
            DrawSourceDirRow(m_ProtoSourceDirPath, "协议目录位置：", "选择 Proto 协议目录位置");

            string protoDir = m_ProtoSourceDirPath?.stringValue;
            if (!string.IsNullOrEmpty(protoDir) && Util.SysIO.Directory.Exists(protoDir))
            {
                DrawProtoFilesTree(protoDir);
            }

            DrawProtoExportButton();
            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 按目录层级绘制可折叠的 .proto 文件树：Layout 事件时刷新缓存并预建设置条目，再构建目录树递归绘制。
        /// </summary>
        /// <param name="protoDir">Proto 协议目录完整路径。</param>
        private void DrawProtoFilesTree(string protoDir)
        {
            if (m_ProtoUnitsSettings == null)
            {
                return;
            }

            SerializedProperty currentProtoUnits = m_ProtoUnitsSettings;

            if (Event.current.type == EventType.Layout)
            {
                m_CachedProtoFiles = EditorUtil.Proto.CliRunner.GetProtoFiles(protoDir);

                if (m_CachedProtoFiles != null)
                {
                    string rootNorm = protoDir.TrimEnd('/', '\\');
                    bool modified = false;
                    foreach (string filePath in m_CachedProtoFiles)
                    {
                        string relativePath = Util.SysIO.Path.GetRelativePath(rootNorm, filePath);
                        bool found = false;
                        for (int i = 0; i < currentProtoUnits.arraySize; i++)
                        {
                            SerializedProperty pathProp = currentProtoUnits.GetArrayElementAtIndex(i).FindPropertyRelative("SourcePath");
                            if (pathProp != null && pathProp.stringValue == relativePath)
                            {
                                found = true;
                                break;
                            }
                        }
                        if (!found)
                        {
                            currentProtoUnits.arraySize++;
                            SerializedProperty newElem = currentProtoUnits.GetArrayElementAtIndex(currentProtoUnits.arraySize - 1);
                            SerializedProperty newPathProp = newElem.FindPropertyRelative("SourcePath");
                            if (newPathProp != null)
                            {
                                newPathProp.stringValue = relativePath;
                            }
                            modified = true;
                        }
                    }
                    if (modified)
                    {
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            }

            if (m_CachedProtoFiles == null || m_CachedProtoFiles.Length == 0)
            {
                return;
            }

            string rootPathNorm = protoDir.TrimEnd('/', '\\');
            FileFolderTree.TreeNode root = FileFolderTree.BuildTree(rootPathNorm, m_CachedProtoFiles);
            if (root == null)
            {
                return;
            }

            EnsureProtoFileStylesInitialized();
            DrawProtoFolderNode(root, rootPathNorm, currentProtoUnits);
        }

        /// <summary>
        /// 确保 Proto 文件名样式已初始化（延迟创建，避免 OnEnable 时 EditorStyles 未就绪）。
        /// </summary>
        private static void EnsureProtoFileStylesInitialized()
        {
            if (s_ProtoFileNameStyle != null)
            {
                return;
            }

            s_ProtoFileNameStyle = new GUIStyle(EditorStyles.label);
            s_ProtoFileNameStyle.normal.textColor = new Color(0xB8 / 255f, 0xF2 / 255f, 0xF2 / 255f);
            s_ProtoFileNameStyle.padding = new RectOffset(0, 0, 0, 0);
        }

        /// <summary>
        /// 递归绘制 Proto 目录树节点：若为根节点则绘制根目录 Foldout 并维护缩进；再绘制子文件夹；最后绘制当前节点下的 .proto 文件行。
        /// </summary>
        /// <param name="node">当前目录树节点。</param>
        /// <param name="rootPathNorm">Proto 根目录规范化路径（无末尾斜杠）。</param>
        /// <param name="currentProtoUnits">Proto 单元设置列表属性。</param>
        private void DrawProtoFolderNode(FileFolderTree.TreeNode node, string rootPathNorm, SerializedProperty currentProtoUnits)
        {
            bool isRoot = string.IsNullOrEmpty(node.SegmentName);
            if (isRoot)
            {
                if (!m_ProtoFileFolderFoldoutState.TryGetValue(rootPathNorm, out bool rootExpanded))
                {
                    m_ProtoFileFolderFoldoutState[rootPathNorm] = rootExpanded = true;
                }
                string rootDisplayName = Util.SysIO.Path.GetFileName(rootPathNorm);
                if (string.IsNullOrEmpty(rootDisplayName))
                {
                    rootDisplayName = rootPathNorm;
                }
                int rootFileCount = node.TotalFileCount();
                rootExpanded = EditorUtil.Draw.Foldout(ref rootExpanded, $"{rootDisplayName} ({rootFileCount})", true);
                m_ProtoFileFolderFoldoutState[rootPathNorm] = rootExpanded;
                if (!rootExpanded)
                {
                    return;
                }
                EditorUtil.Draw.IncreaseIndentLevel();
            }

            foreach (var child in node.Children.OrderBy(c => c.SegmentName))
            {
                if (!m_ProtoFileFolderFoldoutState.TryGetValue(child.FullPath, out bool expanded))
                {
                    m_ProtoFileFolderFoldoutState[child.FullPath] = expanded = true;
                }
                int childFileCount = child.TotalFileCount();
                expanded = EditorUtil.Draw.Foldout(ref expanded, $"{child.SegmentName} ({childFileCount})", true);
                m_ProtoFileFolderFoldoutState[child.FullPath] = expanded;
                if (expanded)
                {
                    EditorUtil.Draw.IncreaseIndentLevel();
                    DrawProtoFolderNode(child, rootPathNorm, currentProtoUnits);
                    EditorUtil.Draw.DecreaseIndentLevel();
                }
            }

            int savedIndent = EditorUtil.Draw.SaveIndentLevel();
            float indentSpace = savedIndent * EditorUtil.Draw.SourceFileTree.c_IndentPixelsPerLevel;

            var orderedFiles = node.FileFullPaths.OrderBy(f => f).ToList();
            for (int index = 0; index < orderedFiles.Count; index++)
            {
                string filePath = orderedFiles[index];
                DrawProtoFileRow(filePath, rootPathNorm, index + 1, indentSpace, savedIndent, currentProtoUnits);
            }

            if (isRoot)
            {
                EditorUtil.Draw.DecreaseIndentLevel();
            }
        }

        /// <summary>
        /// 绘制单个 .proto 文件行：文件名 + 打开 / 打开文件夹按钮，以及类型导出位置配置行。
        /// </summary>
        /// <param name="filePath">.proto 文件完整路径。</param>
        /// <param name="rootPathNorm">Proto 根目录规范化路径。</param>
        /// <param name="seq">当前节点下的序号。</param>
        /// <param name="indentSpace">缩进像素偏移。</param>
        /// <param name="savedIndent">保存的缩进级别，绘制完毕后恢复。</param>
        /// <param name="currentProtoUnits">Proto 单元设置列表属性。</param>
        private void DrawProtoFileRow(string filePath, string rootPathNorm, int seq, float indentSpace, int savedIndent, SerializedProperty currentProtoUnits)
        {
            string fileName = Util.SysIO.Path.GetFileName(filePath);

            EditorUtil.Draw.SetIndentLevel(0);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(indentSpace);
                EditorUtil.Draw.Label($"[{seq}] {fileName}", s_ProtoFileNameStyle, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Button("打开", false, () => EditorUtil.FileSystem.OpenFile(filePath), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(filePath), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthMedium));
            });
            EditorUtil.Draw.RestoreIndentLevel(savedIndent);

            string capturedRelativePath = Util.SysIO.Path.GetRelativePath(rootPathNorm, filePath);
            SerializedProperty detailProp = GetOrCreateProtoUnitSetting(capturedRelativePath, currentProtoUnits);
            SerializedProperty csharpExportProp = detailProp?.FindPropertyRelative("CSharpExportPath");

            if (csharpExportProp != null)
            {
                EditorUtil.Draw.SourceFileTree.EnsureStylesInitialized();
                EditorUtil.Draw.SetIndentLevel(0);
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    bool isValid = EditorUtil.Draw.SourceFileTree.IsValidDirectoryPath(csharpExportProp.stringValue);
                    EditorUtil.Draw.Space(indentSpace);
                    EditorUtil.Draw.Label("类型导出位置：", EditorUtil.Draw.SourceFileTree.ContentStyle, false, GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ExportLabelWidth));
                    EditorUtil.Draw.TextField(csharpExportProp, EditorUtil.Draw.SourceFileTree.ContentFieldStyle, true, null, GUILayout.ExpandWidth(true));
                    if (!isValid)
                    {
                        EditorUtil.Draw.SourceFileTree.DrawInvalidBorderForLastRect();
                    }
                    EditorUtil.Draw.Button("选择", true, () => EditorUtil.Draw.Panel.SelectFolderDelay("选择 C# 类型输出目录", "", "", csharpExportProp), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                    EditorUtil.Draw.Button("导出", true, () =>
                    {
                        string protoDir = m_ProtoSourceDirPath?.stringValue;
                        if (!string.IsNullOrEmpty(protoDir))
                        {
                            string exportPath = csharpExportProp.stringValue;
                            if (!string.IsNullOrEmpty(exportPath))
                            {
                                EditorUtil.Proto.CliRunner.CompileSingle(filePath, protoDir.TrimEnd('/', '\\'), exportPath);
                                AssetDatabase.Refresh();
                            }
                        }
                    }, GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthSmall));
                    EditorUtil.Draw.Button("打开文件夹", false, () => EditorUtil.FileSystem.OpenFolder(csharpExportProp.stringValue), GUILayout.Width(EditorUtil.Draw.SourceFileTree.c_ButtonWidthMedium));
                });
                EditorUtil.Draw.RestoreIndentLevel(savedIndent);
            }
        }

        /// <summary>
        /// 在 ProtoUnits 中查找或创建指定 proto 文件的单元设置。
        /// </summary>
        /// <param name="sourceRelativePath">相对于 Proto 根目录的文件路径。</param>
        /// <param name="currentProtoUnits">Proto 单元设置列表属性。</param>
        /// <returns>对应的 SerializedProperty，无法操作时返回 null。</returns>
        private SerializedProperty GetOrCreateProtoUnitSetting(string sourceRelativePath, SerializedProperty currentProtoUnits)
        {
            if (currentProtoUnits == null)
            {
                return null;
            }

            for (int i = 0; i < currentProtoUnits.arraySize; i++)
            {
                SerializedProperty elem = currentProtoUnits.GetArrayElementAtIndex(i);
                SerializedProperty pathProp = elem.FindPropertyRelative("SourcePath");
                if (pathProp != null && pathProp.stringValue == sourceRelativePath)
                {
                    return elem;
                }
            }

            currentProtoUnits.arraySize++;
            SerializedProperty newElem = currentProtoUnits.GetArrayElementAtIndex(currentProtoUnits.arraySize - 1);
            SerializedProperty newPathProp = newElem.FindPropertyRelative("SourcePath");
            if (newPathProp != null)
            {
                newPathProp.stringValue = sourceRelativePath;
            }
            return newElem;
        }

        /// <summary>
        /// 绘制 protoc 编译按钮：委托 EditorUtil.Network.ProtoExporter 遍历所有已配置的 .proto 文件，逐个编译到各自的类型导出位置。
        /// </summary>
        private void DrawProtoExportButton()
        {
            string protoDir = m_ProtoSourceDirPath?.stringValue;
            bool hasProtoDir = !string.IsNullOrEmpty(protoDir) && Util.SysIO.Directory.Exists(protoDir);

            EditorUtil.Draw.DisabledGroup(!hasProtoDir || m_ProtoUnitsSettings == null, () =>
            {
                EditorUtil.Draw.Button("导出所有协议类型", true, () =>
                {
                    NetworkComponent networkComponent = (NetworkComponent)target;
                    EditorUtil.Network.ProtoExporter.ExportAllProtos(networkComponent.ProtoSettings);
                });
            });
        }
    }
}
