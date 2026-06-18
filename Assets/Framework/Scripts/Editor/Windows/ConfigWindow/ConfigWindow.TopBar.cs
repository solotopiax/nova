/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.TopBar.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   ConfigWindow 顶部工具栏（SO 选择 + Platform/Channel/DevelopMode + 导出）
 ***************************************************************/

using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 绘制顶部工具栏：第一行 SO 选择与文件操作（含保存），第二行 Platform/Channel/DevelopMode 与导出区域。
        /// </summary>
        private void DrawTopBar()
        {
            EditorUtil.Draw.Layout.Vertical("helpBox", () =>
            {
                // 第一行：编辑器存档文件选择 + 保存 + 打开文件夹
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Label("编辑器存档文件：", false, GUILayout.Width(100));
                    ConfigMasterSO newMaster = EditorUtil.Draw.ObjectField<ConfigMasterSO>(string.Empty, m_Master, false, false);
                    if (newMaster != m_Master) RebindMaster(newMaster);
                    EditorUtil.Draw.Button("创建", false, () => CreateMasterInteractive(), GUILayout.Width(64f));
                    EditorUtil.Draw.Button("选择", false, () => PickMasterInteractive(), GUILayout.Width(64f));
                    // 保存按钮：ConfigMasterSO 有改动时可用，无改动时禁用
                    EditorUtil.Draw.DisabledGroup(!m_IsDirty, () =>
                    {
                        EditorUtil.Draw.SuccessButton("保存", false, () => OnClickSave(), GUILayout.Width(64f));
                    });
                    EditorUtil.Draw.Button("打开文件夹", false, () => RevealMasterInFinder(), GUILayout.Width(90f));
                });

                if (m_Master == null) return;

                // 第二行：平台/渠道/模式选择 + 导出区域整体靠右
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    // 三组控件压缩 labelWidth，组间插 24f 间距
                    float prevLabelWidth = EditorGUIUtility.labelWidth;
                    try
                    {
                        EditorGUIUtility.labelWidth = 80f;

                        EditorUtil.Draw.Label("平台类型：", false, GUILayout.Width(64f));
                        PlatformType platform = EditorUtil.Draw.EnumPopup<PlatformType>(string.Empty, m_WorkingCopy != null ? m_WorkingCopy.CurrentPlatform : m_Master.CurrentPlatform, false, GUILayout.Width(120));
                        EditorUtil.Draw.Space(24f);

                        EditorUtil.Draw.Label("渠道类型：", false, GUILayout.Width(64f));
                        ChannelType channel = EditorUtil.Draw.EnumPopup<ChannelType>(string.Empty, m_WorkingCopy != null ? m_WorkingCopy.CurrentChannel : m_Master.CurrentChannel, false, GUILayout.Width(120));
                        TryApplyPlatformChannel(platform, channel);
                        EditorUtil.Draw.Space(24f);

                        EditorUtil.Draw.Label("开发模式：", false, GUILayout.Width(64f));
                        DevelopMode developMode = EditorUtil.Draw.EnumPopup<DevelopMode>(string.Empty, m_WorkingCopy != null ? m_WorkingCopy.CurrentDevelopMode : m_Master.CurrentDevelopMode, false, GUILayout.Width(120));
                        TryApplyDevelopMode(developMode);
                    }
                    finally
                    {
                        EditorGUIUtility.labelWidth = prevLabelWidth;
                    }

                    // FlexibleSpace 把导出区域推到最右
                    EditorUtil.Draw.FlexibleSpace();

                    EditorUtil.Draw.Label("导出文件：", false, GUILayout.Width(60));
                    ConfigRuntimeSO currentTarget = m_WorkingCopy != null ? m_WorkingCopy.ExportTarget : m_Master?.ExportTarget;
                    ConfigRuntimeSO newTarget = EditorUtil.Draw.ObjectField<ConfigRuntimeSO>(string.Empty, currentTarget, false, false, GUILayout.Width(200));
                    if (newTarget != currentTarget && m_WorkingCopy != null)
                    {
                        m_WorkingCopy.ExportTarget = newTarget;
                        m_IsDirty = true;
                    }

                    EditorUtil.Draw.Button("选择", false, () => OnClickSelectExportAsset(), GUILayout.Width(64f));
                    EditorUtil.Draw.SuccessButton("导出", false, () => OnClickExport(), GUILayout.Width(64f));
                    ConfigRuntimeSO exportTarget = m_WorkingCopy != null ? m_WorkingCopy.ExportTarget : null;
                    EditorUtil.Draw.DisabledGroup(exportTarget == null, () =>
                    {
                        EditorUtil.Draw.Button("打开文件夹", false, () =>
                        {
                            if (exportTarget == null) return;
                            EditorUtility.RevealInFinder(AssetDatabase.GetAssetPath(exportTarget));
                        }, GUILayout.Width(90f));
                    });
                });

                // 说明上一行三项选择的语义：它们是导出时的过滤条件，而非运行时环境的自动识别依据
                EditorUtil.Draw.HintLabel("平台类型 / 渠道类型 / 开发模式 三项用于设定导出时筛选配置的目标组合，点击「导出」仅将与该组合匹配的那一份配置写入导出文件。此三项不用于运行时当前环境的自动识别，应用实际所处的平台与渠道由运行设备及打包参数决定，与此处的选择无关。");
            });
        }

        /// <summary>
        /// "选择"按钮回调：打开文件选择面板，校验类型后将 ConfigRuntimeSO 写入 WorkingCopy.ExportTarget 并置脏。
        /// </summary>
        private void OnClickSelectExportAsset()
        {
            if (m_WorkingCopy == null) return;
            string abs = EditorUtility.OpenFilePanelWithFilters("选择导出文件", "Assets", new[] { "Asset Files", "asset" });
            if (string.IsNullOrEmpty(abs)) return;
            string rel = FileUtil.GetProjectRelativePath(abs);
            if (string.IsNullOrEmpty(rel)) return;
            ConfigRuntimeSO loaded = AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(rel);
            if (loaded == null)
            {
                EditorUtility.DisplayDialog("类型错误", "请选择 ConfigRuntimeSO 类型资产。", "知道了");
                return;
            }
            m_WorkingCopy.ExportTarget = loaded;
            m_IsDirty = true;
            Repaint();
        }

        /// <summary>
        /// 切换 ConfigMaster（ADR-047 三入口收口）：若有未保存脏数据弹框确认，
        /// 通过后销毁旧 WorkingCopy、重建新 WorkingCopy 并刷新 Plugin 缓存。
        /// </summary>
        /// <param name="newMaster">要切换到的目标 ConfigMasterSO；传 null 时清空绑定。</param>
        private void RebindMaster(ConfigMasterSO newMaster)
        {
            if (!ConfirmDiscardDirty()) return;
            m_Master = newMaster;
            DestroyWorkingCopy();
            if (newMaster != null)
            {
                // 持久化激活 master 到 Globals.json，使顶部 ObjectField 拖拽/创建/选择与 BindGuide 按钮行为一致（ADR-047 写入入口）
                EditorUtil.Config.WorkspaceActive.Set(newMaster);
                RebuildWorkingCopy();
                EditorUtil.Config.StructureGuard.SyncEnumGrid(newMaster);
                RefreshPluginCache();
                m_LastKnownChannel = newMaster.CurrentChannel;
                PromptMissingRefsIfAny();
            }
        }

        /// <summary>
        /// 弹 SaveFilePanel 让用户选择位置，创建新的 ConfigMaster.asset 并立即绑定。
        /// </summary>
        private void CreateMasterInteractive()
        {
            string path = EditorUtility.SaveFilePanelInProject("创建 ConfigMaster", "ConfigMaster", "asset", "选择创建位置");
            if (string.IsNullOrEmpty(path)) return;
            RebindMaster(EditorUtil.Asset.Operator.CreateAt<ConfigMasterSO>(path));
        }

        /// <summary>
        /// 弹 OpenFilePanel 让用户选择已存在的 ConfigMaster.asset 并绑定。
        /// </summary>
        private void PickMasterInteractive()
        {
            string abs = EditorUtility.OpenFilePanel("选择 ConfigMaster", "Assets", "asset");
            if (string.IsNullOrEmpty(abs)) return;
            string rel = FileUtil.GetProjectRelativePath(abs);
            if (string.IsNullOrEmpty(rel))
            {
                EditorUtility.DisplayDialog("路径错误", "所选文件不在当前 Unity 项目目录中，请重新选择。", "知道了");
                return;
            }
            RebindMaster(EditorUtil.Asset.Operator.LoadAt<ConfigMasterSO>(rel));
        }

        /// <summary>
        /// 在 OS 文件管理器中定位当前 ConfigMaster.asset；未绑定时静默返回。
        /// </summary>
        private void RevealMasterInFinder()
        {
            if (m_Master == null) return;
            string path = AssetDatabase.GetAssetPath(m_Master);
            if (!string.IsNullOrEmpty(path)) EditorUtility.RevealInFinder(path);
        }

        /// <summary>
        /// 尝试应用 Platform/Channel 变更：值未改变时直接返回；
        /// 否则释放焦点后记录 pending 坐标，延迟一帧由 ApplyPendingCoordSwitch 写入 WorkingCopy，
        /// 确保当前编辑字段在旧坐标格子下完成失焦提交后再切坐标（PAT-22 升级）。
        /// </summary>
        /// <param name="platform">目标平台类型。</param>
        /// <param name="channel">目标渠道类型。</param>
        private void TryApplyPlatformChannel(PlatformType platform, ChannelType channel)
        {
            if (m_WorkingCopy == null) return;
            if (platform == m_WorkingCopy.CurrentPlatform && channel == m_WorkingCopy.CurrentChannel) return;
            m_MasterSO?.ApplyModifiedProperties();
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            // 延迟一帧切坐标：本帧不改坐标，待 DrawRightPanel 让当前编辑字段在旧坐标格子下完成失焦提交后，由 ApplyPendingCoordSwitch 应用（PAT-22 升级）
            m_PendingPlatform = platform;
            m_PendingChannel = channel;
            m_PendingDevelopMode = m_WorkingCopy.CurrentDevelopMode;
            m_HasPendingCoordSwitch = true;
            m_IsDirty = true;
            m_LastKnownChannel = channel;
            Repaint();
        }

        /// <summary>
        /// 尝试应用 DevelopMode 变更：值未改变时直接返回；
        /// 否则释放焦点后记录 pending 坐标，延迟一帧由 ApplyPendingCoordSwitch 写入 WorkingCopy，
        /// 确保当前编辑字段在旧坐标格子下完成失焦提交后再切坐标（PAT-22 升级）。
        /// </summary>
        /// <param name="developMode">目标开发模式。</param>
        private void TryApplyDevelopMode(DevelopMode developMode)
        {
            if (m_WorkingCopy == null) return;
            if (developMode == m_WorkingCopy.CurrentDevelopMode) return;
            m_MasterSO?.ApplyModifiedProperties();
            GUI.FocusControl(null);
            EditorGUIUtility.editingTextField = false;
            // 延迟一帧切坐标：本帧不改坐标，待 DrawRightPanel 让当前编辑字段在旧坐标格子下完成失焦提交后，由 ApplyPendingCoordSwitch 应用（PAT-22 升级）
            m_PendingPlatform = m_WorkingCopy.CurrentPlatform;
            m_PendingChannel = m_WorkingCopy.CurrentChannel;
            m_PendingDevelopMode = developMode;
            m_HasPendingCoordSwitch = true;
            m_IsDirty = true;
            Repaint();
        }

        /// <summary>
        /// 保存按钮回调：将 WorkingCopy 通过 CopySerialized 写回真实资产并落盘，重置脏标志。
        /// </summary>
        private void OnClickSave()
        {
            if (m_Master == null) return;
            CommitWorkingCopyToAsset();
        }

        /// <summary>
        /// 导出入口：有未保存改动时提示先保存再导出（导出源为已落盘的配置资产）；
        /// 先执行校验，存在 Error 级问题时弹校验对话框；
        /// 未设置导出目标 SO 时弹 SaveFilePanel 引导用户选择位置创建 ConfigRuntimeSO.asset；
        /// 通过后将数据写入目标 ConfigRuntimeSO，并将结果回写 m_Master.ExportTarget 持久化。
        /// </summary>
        private void OnClickExport()
        {
            if (m_IsDirty)
            {
                EditorUtility.DisplayDialog("请先保存", "检测到未保存的修改，导出前请先点击保存。导出源为已落盘的配置资产。", "知道了");
                return;
            }
            IReadOnlyList<EditorUtil.Config.Validator.ValidationIssue> issues =
                EditorUtil.Config.Validator.Validate(m_Master, m_Master.CurrentPlatform, m_Master.CurrentChannel, m_Master.CurrentDevelopMode);
            if (HasAnyError(issues))
            {
                ShowValidationDialog(issues);
                return;
            }

            string assetPath;
            if (m_Master.ExportTarget == null)
            {
                assetPath = EditorUtility.SaveFilePanelInProject("导出 ConfigRuntime", "ConfigRuntime", "asset", "选择导出位置");
                if (string.IsNullOrEmpty(assetPath)) return;
            }
            else
            {
                assetPath = AssetDatabase.GetAssetPath(m_Master.ExportTarget);
            }

            ConfigRuntimeSO result = EditorUtil.Config.Exporter.Export(m_Master, m_Master.CurrentPlatform, m_Master.CurrentChannel, m_Master.CurrentDevelopMode, assetPath);
            if (result == null)
            {
                EditorUtility.DisplayDialog("导出失败", $"未找到 Platform={m_Master.CurrentPlatform} × Channel={m_Master.CurrentChannel} 的配置行，请检查 ConfigMasterSO。", "知道了");
                return;
            }

            if (m_Master.ExportTarget == null)
            {
                ConfigRuntimeSO newExport = AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(assetPath);
                m_Master.ExportTarget = newExport;
                // 同步到 WorkingCopy，保持两者一致
                if (m_WorkingCopy != null) m_WorkingCopy.ExportTarget = newExport;
                Repaint();
            }

            EditorUtility.SetDirty(m_Master);
            AssetDatabase.SaveAssetIfDirty(m_Master);
            EditorUtil.Config.SceneDevelopModeWriter.WriteActiveScene(m_Master.CurrentDevelopMode);
            EditorUtility.DisplayDialog("导出成功", $"已成功导出到：\n{assetPath}", "知道了");
        }
    }
}
