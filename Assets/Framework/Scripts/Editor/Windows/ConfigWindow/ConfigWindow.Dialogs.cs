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
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
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
                CommitWorkingCopyToAsset();
                return true;
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
            StringBuilder sb = new();
            sb.AppendLine("发现以下问题，请修复后再导出：");
            for (int i = 0; i < issues.Count; i++)
            {
                sb.AppendLine($"- [{issues[i].Level}] {issues[i].Path}: {issues[i].Message}");
            }
            EditorUtility.DisplayDialog("导出校验失败", sb.ToString(), "知道了");
        }

        /// <summary>
        /// OnEnable / RebindMaster 调：扫出失效的 SDK / Kit 配置（SerializeReference 丢失）则弹清理确认框（推荐清理）。
        /// </summary>
        private void PromptMissingRefsIfAny()
        {
            if (m_Master == null) return;
            IReadOnlyList<EditorUtil.Config.StructureGuard.MissingRef> missing = EditorUtil.Config.StructureGuard.DetectMissingPluginRefs(m_Master);
            if (missing.Count == 0) return;
            int total = 0;
            for (int i = 0; i < missing.Count; i++) total += missing[i].MissingCount;
            bool clean = EditorUtility.DisplayDialog(
                $"检测到 {total} 项失效的 SDK / Kit 配置",
                $"有 {total} 项 SDK / Kit 配置指向的类型已不存在（通常是对应插件被移除或重命名后留下的空配置），" +
                "它们不会生效，也无法在面板里正常编辑。\n\n" +
                "建议清理这些失效项，让配置保持干净；若选「暂不处理」，它们会保留，下次打开仍会再次提示。",
                "清理（推荐）", "暂不处理");
            if (clean) EditorUtil.Config.StructureGuard.CleanMissingPluginRefs(m_Master);
        }
    }
}
