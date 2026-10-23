/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.Dialogs.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   ConfigWindow 弹框相关私有方法（脏数据确认/校验/缺失引用）
 ***************************************************************/

using System.Collections.Generic;
using System.Text;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 仅在自动整理无法安全完成时延迟提示；正常保存不弹整理确认，也不在 GUI 绘制栈打开模态窗口。
        /// </summary>
        /// <param name="writeStarted">真实资产写入是否已经开始；开始后不能承诺资产未修改。</param>
        private void ScheduleSaveFailedDialog(bool writeStarted)
        {
            ConfigMasterSO expectedMaster = m_Master;
            EditorApplication.delayCall += () =>
            {
                if (!ReferenceEquals(expectedMaster, m_Master)) return;
                EditorUtility.DisplayDialog(
                    writeStarted ? "配置保存未完成" : "配置未保存",
                    writeStarted
                        ? "配置保存未完成，资产写入已经开始，窗口中的修改仍保留。请查看 Console 中的详细原因，并核对配置资产后再继续。"
                        : "配置中存在无法自动处理的问题，本次没有保存，修改仍保留。请查看 Console 中的详细原因。",
                    "知道了");
            };
        }

        /// <summary>
        /// 窗口关闭时提醒保存或导出的统一入口。
        /// 未保存时提供“取消 / 仅保存 / 保存并导出”；取消恢复窗口及草稿，不写配置或导出，
        /// 选择“仅保存”后本次关闭不再追问导出。
        /// 已手动保存但尚未导出时，仅询问是否立即导出。
        /// </summary>
        private void HandleCloseReminder()
        {
            if (m_Master == null) return;
            if (m_IsDirty)
            {
                int choice = EditorUtility.DisplayDialogComplex(
                    "配置修改尚未完成",
                    "你修改了配置，但还没有保存。\n\n" +
                    "仅保存：保存本次修改，暂不更新游戏运行配置。\n" +
                    "保存并导出：保存修改，并立即更新游戏运行配置。\n" +
                    "取消：不保存、不导出，返回配置页面继续编辑。",
                    "保存并导出",
                    "取消",
                    "仅保存");
                if (choice == 1)
                {
                    ReopenAfterFailedCloseExport(true);
                    return;
                }
                bool saveAndExport = choice == 0;
                if (!CommitWorkingCopyToAsset(false))
                {
                    ReopenAfterFailedCloseExport(true);
                    return;
                }
                if (saveAndExport && !TryExport(false)) ReopenAfterFailedCloseExport();
                return;
            }

            if (!m_HasSavedChangesPendingExport) return;
            bool export = EditorUtility.DisplayDialog(
                "配置尚未导出",
                "修改已经保存，但还没有更新到游戏运行配置。是否现在导出？",
                "导出",
                "暂不导出");
            if (export && !TryExport(false)) ReopenAfterFailedCloseExport();
        }

        /// <summary>
        /// 关窗取消或联动保存、导出未完成时，在销毁流程结束后的下一次 Editor 更新恢复配置窗口。
        /// preserveWorkingCopy 为 true 时保留未保存草稿，不写入真实配置资产。
        /// </summary>
        private void ReopenAfterFailedCloseExport(bool preserveWorkingCopy = false)
        {
            ConfigMasterSO master = m_Master;
            PlatformType platform = m_EditingPlatform;
            ChannelType channel = m_Master.CurrentChannel;
            DevelopMode developMode = m_Master.CurrentDevelopMode;
            LeftTreeItem selectedItem = m_SelectedItem;
            System.Type selectedPluginType = m_SelectedPluginType;
            bool hadPendingExport = m_HasSavedChangesPendingExport;
            ConfigMasterSO workingCopySnapshot = preserveWorkingCopy && m_WorkingCopy != null
                ? Instantiate(m_WorkingCopy)
                : null;
            EditorApplication.delayCall += () =>
            {
                ConfigWindow window = OpenConfigSection(
                    master, platform, channel, developMode, selectedItem, selectedPluginType);
                if (workingCopySnapshot != null)
                {
                    if (window.m_WorkingCopy != null)
                    {
                        EditorUtility.CopySerialized(workingCopySnapshot, window.m_WorkingCopy);
                        window.m_MasterSO?.Update();
                        window.m_IsDirty = true;
                        window.m_HasSavedChangesPendingExport = hadPendingExport;
                    }
                    DestroyImmediate(workingCopySnapshot);
                }
                else
                {
                    window.m_HasSavedChangesPendingExport = true;
                }
            };
        }

        /// <summary>
        /// Master 切换 / 场景切换换 Master 前的脏数据确认：有未保存改动时弹三选一对话框（保存/取消/丢弃）。
        /// 统一使用 m_IsDirty 作为脏判定口径，不再依赖 m_MasterSO.hasModifiedProperties。
        /// </summary>
        /// <returns>true 表示可以继续切换，false 表示用户选择取消。</returns>
        private bool ConfirmDiscardDirty()
        {
            if (!m_IsDirty) return true;
            int choice = EditorUtility.DisplayDialogComplex(
                "未保存的改动",
                "当前有未保存的编辑，切换前是否保存？",
                "保存", "取消", "丢弃");
            if (choice == 0)
            {
                return CommitWorkingCopyToAsset();
            }
            if (choice == 2)
            {
                // 丢弃：销毁 WorkingCopy 并从真实资产重建，清除所有未保存改动
                DestroyWorkingCopy();
                if (m_Master != null) RebuildWorkingCopy();
                else m_IsDirty = false;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Scene 已完成切换后收敛旧 Master 的脏 WorkingCopy。
        /// 此时不能继续保留旧绑定，因此只提供“保存旧配置”或“丢弃旧配置”两种确定性结果。
        /// </summary>
        private void ResolveDirtyForSceneChange()
        {
            if (!m_IsDirty) return;
            bool save = EditorUtility.DisplayDialog(
                "场景已切换",
                "当前 ConfigMaster 有未保存改动。工作区必须跟随新场景，请选择保存旧配置后切换，或丢弃旧改动后切换。",
                "保存旧配置并切换",
                "丢弃旧改动并切换");
            if (save)
            {
                // 场景已经切换，随后会销毁旧 WorkingCopy；必须在此同步完成修复/保存，
                // 不能把旧问题留给 delayCall 后误作用到新场景绑定的 ConfigMaster。
                if (!CommitWorkingCopyToAsset(false))
                {
                    EditorUtility.DisplayDialog(
                        "旧配置未保存",
                        "场景已经切换，旧 ConfigMaster 的修改未能通过保存校验，因此不会把工作区重新切回旧配置。请返回原场景后重新打开 Config 窗口处理。",
                        "知道了");
                }
                return;
            }

            DestroyWorkingCopy();
            m_IsDirty = false;
        }

        /// <summary>
        /// 判断问题列表中是否存在 Error 级别。
        /// </summary>
        /// <param name="issues">校验问题列表。</param>
        /// <returns>存在 Error 级别时返回 true。</returns>
        private static bool HasAnyError(IReadOnlyList<EditorUtil.Config.Validator.ValidationIssue> issues)
        {
            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Level == EditorUtil.Config.Validator.Severity.Error) return true;
            }
            return false;
        }

        /// <summary>
        /// 展示校验问题对话框，列出每条 Level/Path/Message。
        /// </summary>
        /// <param name="issues">校验问题列表。</param>
        private void ShowValidationDialog(IReadOnlyList<EditorUtil.Config.Validator.ValidationIssue> issues)
        {
            EditorUtility.DisplayDialog("导出校验失败", BuildValidationMessage(issues), "知道了");
        }

        /// <summary>
        /// 展示不阻断导出的 Warning，并要求用户显式确认是否继续，避免静默忽略失效配置残留。
        /// </summary>
        /// <param name="issues">仅包含 Warning 或至少不含 Error 的校验问题列表。</param>
        /// <returns>用户选择继续导出时返回 true，选择取消时返回 false。</returns>
        private bool ConfirmValidationWarnings(IReadOnlyList<EditorUtil.Config.Validator.ValidationIssue> issues)
        {
            return EditorUtility.DisplayDialog(
                "导出校验警告",
                BuildValidationMessage(issues),
                "继续导出",
                "取消");
        }

        /// <summary>
        /// 构建带当前导出坐标、问题级别和失效配置处理指引的统一校验文本。
        /// </summary>
        /// <param name="issues">待展示的校验问题列表。</param>
        /// <returns>用于 Error 提示或 Warning 确认框的完整文本。</returns>
        private string BuildValidationMessage(IReadOnlyList<EditorUtil.Config.Validator.ValidationIssue> issues)
        {
            StringBuilder sb = new();
            sb.AppendLine($"发现以下问题（校验范围：平台 {m_EditingPlatform} / 渠道 {m_Master.CurrentChannel} / 模式 {m_Master.CurrentDevelopMode}），[Error] 项必须修复后再导出，[Warning] 项不阻断本次导出：");
            bool hasMissingRef = false;
            for (int i = 0; i < issues.Count; i++)
            {
                sb.AppendLine($"- [{issues[i].Level}] {issues[i].Path}: {issues[i].Message}");
                if (issues[i].Path.Contains("SDKConfigs[") || issues[i].Path.Contains("KitConfigs[")) hasMissingRef = true;
            }

            if (hasMissingRef)
            {
                sb.AppendLine();
                sb.AppendLine("上述「失效配置」的处理方式：重新打开 Config 窗口，在自动弹出的失效配置对话框中选择「清理（推荐）」移除空项；若某项需要启用，请先安装对应插件包，再在左树勾选后重新导出。");
            }
            return sb.ToString();
        }

        /// <summary>
        /// OnEnable / RebindMaster 调：在 WorkingCopy 中扫出失效的 SDK / Kit 配置（SerializeReference 丢失）则弹清理确认框。
        /// 用户确认后只清理内存副本并标记待保存，不绕过统一保存入口直接修改真实资产。
        /// </summary>
        private void PromptMissingRefsIfAny()
        {
            if (m_WorkingCopy == null) return;
            IReadOnlyList<EditorUtil.Config.StructureGuard.MissingRef> missing = EditorUtil.Config.StructureGuard.DetectMissingPluginRefs(m_WorkingCopy);
            if (missing.Count == 0) return;
            int total = 0;
            for (int i = 0; i < missing.Count; i++) total += missing[i].MissingCount;
            bool clean = EditorUtility.DisplayDialog(
                $"检测到 {total} 项失效的 SDK / Kit 配置",
                $"有 {total} 项 SDK / Kit 配置指向的类型已不存在（通常是对应插件被移除或重命名后留下的空配置），" +
                "它们不会生效，也无法在面板里正常编辑。\n\n" +
                "建议清理这些失效项，让配置保持干净；若选「暂不处理」，它们会保留，下次打开仍会再次提示。",
                "清理（推荐）", "暂不处理");
            if (clean)
            {
                EditorUtil.Config.StructureGuard.CleanMissingPluginRefs(m_WorkingCopy);
                m_MasterSO?.Update();
                m_IsDirty = true;
            }
        }
    }
}
