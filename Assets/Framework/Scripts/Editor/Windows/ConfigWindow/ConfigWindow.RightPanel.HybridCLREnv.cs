/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.HybridCLREnv.cs
 * author:    taoye
 * created:   2026/5/9
 * descrip:   ConfigWindow HybridCLR 环境检测面板
 ***************************************************************/

using UnityEditor;
using UnityEngine;
using static NovaFramework.Editor.EditorUtil.Environment.HybridCLRChecker;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 绘制 HybridCLR 环境检测面板（状态行 + 重新检测按钮 + 未就绪时操作指引，与 Luban/Python3 面板风格一致）。
        /// </summary>
        private void DrawHybridCLREnvSection()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("HybridCLR 环境检测", m_SectionTitleStyle, false);
            });

            EditorUtil.Draw.Space(8f);
            DrawHybridCLREnvStatusAndButtons();
        }

        /// <summary>
        /// 绘制 HybridCLR 状态区域：
        /// 第一行：图标 + "HybridCLR" + 状态文本（左缩进 16f）；
        /// 最后一行：重新检测按钮靠右对齐；
        /// 未就绪时追加"操作指引"HelpBox（左缩进 16f）。
        /// </summary>
        private void DrawHybridCLREnvStatusAndButtons()
        {
            // 第一行：状态图标 + 名称 + 状态文本（左缩进 16f）
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);

                bool ready = m_HybridCLRCheckResult.IsReady;
                GUIStyle iconStyle = ready ? m_StatusReadyStyle : m_StatusErrorStyle;
                string icon = ready ? "✓" : "✗";
                EditorUtil.Draw.Label(icon, iconStyle, false, GUILayout.Width(16f));
                EditorUtil.Draw.Label("HybridCLR", false, GUILayout.Width(80f));
                EditorUtil.Draw.Label(ResolveHybridCLREnvStatusText(), m_DescStyle, false, GUILayout.Width(260f));
            });

            // 操作按钮行：右对齐
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.FlexibleSpace();
                EditorUtil.Draw.Button("重新检测", false, () =>
                {
                    m_HybridCLRCheckResult = EditorUtil.Environment.HybridCLRChecker.Recheck();
                    Repaint();
                }, GUILayout.Width(80f));
            });

            EditorUtil.Draw.Space(2f);

            // 未就绪时：操作指引 HelpBox（左缩进 16f）
            if (!m_HybridCLRCheckResult.IsReady)
            {
                EditorUtil.Draw.Layout.Horizontal(() =>
                {
                    EditorUtil.Draw.Space(16f);
                    string guideMessage = ResolveHybridCLRGuideMessage(m_HybridCLRCheckResult.Issue);
                    EditorUtil.Draw.HelpBox(MessageType.Info, new[] { guideMessage }, false, GUILayout.ExpandWidth(true));
                });
                EditorUtil.Draw.Space(2f);
            }
        }

        /// <summary>
        /// 获取 HybridCLR 环境状态描述文本。
        /// </summary>
        /// <returns>状态描述字符串。</returns>
        private string ResolveHybridCLREnvStatusText()
        {
            if (!m_HybridCLRCheckResult.IsReady)
            {
                switch (m_HybridCLRCheckResult.Issue)
                {
                    case HybridCLRIssue.PackageNotFound:
                        return "未安装包";
                    case HybridCLRIssue.InstallerNotRun:
                        return "Installer 未运行";
                    default:
                        return "未就绪";
                }
            }

            if (string.IsNullOrEmpty(m_HybridCLRCheckResult.PackageVersion))
            {
                return "就绪";
            }

            return $"就绪（{m_HybridCLRCheckResult.PackageVersion}）";
        }

        /// <summary>
        /// 根据问题类型返回对应的操作指引文案。
        /// </summary>
        /// <param name="issue">HybridCLR 问题类型。</param>
        /// <returns>操作指引文案字符串。</returns>
        private string ResolveHybridCLRGuideMessage(HybridCLRIssue issue)
        {
            switch (issue)
            {
                case HybridCLRIssue.PackageNotFound:
                    return "未安装 HybridCLR 包。请确认 Packages/manifest.json 中引用了 com.solotopia.hybridclr。";
                case HybridCLRIssue.InstallerNotRun:
                    return "HybridCLR 包已安装，但尚未运行 Installer。请点击菜单 HybridCLR > Installer... 按引导完成安装。";
                default:
                    return "HybridCLR 环境未就绪，请检查包安装状态。";
            }
        }
    }
}
