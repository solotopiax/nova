/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigWindow.RightPanel.Luban.cs
 * author:    taoye
 * created:   2026/4/27
 * descrip:   ConfigWindow 右侧面板 Luban 检测分片
 ***************************************************************/

using UnityEditor;
using UnityEngine;
using static NovaFramework.Editor.EditorUtil.Environment.LubanChecker;

namespace NovaFramework.Editor
{
    internal sealed partial class ConfigWindow : EditorWindow
    {
        /// <summary>
        /// 绘制 Luban 环境检测面板（无折叠，状态行+按钮同行展示）。
        /// </summary>
        private void DrawLubanSection()
        {
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Label("Luban 环境检测", m_SectionTitleStyle, false);
            });

            EditorUtil.Draw.Space(8f);
            DrawLubanStatusAndButtons();
            DrawLubanWindowsExportWarning();
            DrawLubanInstallGuide();
        }

        /// <summary>
        /// 绘制 Luban 状态区域：
        /// 第一行：.NET SDK 状态块 + "|" 分隔 + Luban.dll 状态块；
        /// 第二行：重新检测、打开官网按钮靠右对齐。
        /// </summary>
        private void DrawLubanStatusAndButtons()
        {
            // 第一行：dotnet + luban 双状态块（左缩进 16f）
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);

                bool dotnetOk = IsDotnetReady();
                GUIStyle dotnetStyle = dotnetOk ? m_StatusReadyStyle : m_StatusErrorStyle;
                string dotnetIcon = dotnetOk ? "✓" : "✗";
                EditorUtil.Draw.Label(dotnetIcon, dotnetStyle, false, GUILayout.Width(16f));
                EditorUtil.Draw.Label(".NET SDK", false, GUILayout.Width(72f));
                EditorUtil.Draw.Label(ResolveDotnetStatusText(), m_DescStyle, false, GUILayout.Width(160f));

                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.Label("|", false, GUILayout.Width(8f));
                EditorUtil.Draw.Space(8f);

                bool lubanOk = m_LubanCheckResult.Issue == EnvironmentIssue.None;
                GUIStyle lubanStyle = lubanOk ? m_StatusReadyStyle : m_StatusErrorStyle;
                string lubanIcon = lubanOk ? "✓" : "✗";
                EditorUtil.Draw.Label(lubanIcon, lubanStyle, false, GUILayout.Width(16f));
                EditorUtil.Draw.Label("Luban.dll", false, GUILayout.Width(72f));
                EditorUtil.Draw.Label(ResolveLubanDllStatusText(), m_DescStyle, false, GUILayout.Width(160f));
            });

            // 操作按钮行：右对齐
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.FlexibleSpace();

                EditorUtil.Draw.Button("重新检测", false, () =>
                {
                    m_LubanCheckResult = EditorUtil.Environment.LubanChecker.Recheck();
                    Repaint();
                }, GUILayout.Width(80f));

                EditorUtil.Draw.Space(2f);

                EditorUtil.Draw.Button("打开官网", false, () =>
                {
                    Application.OpenURL(c_DotnetDownloadUrl);
                }, GUILayout.Width(80f));
            });
            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 绘制 Windows 平台下的 Luban 导出安全限制提示。
        /// </summary>
        private void DrawLubanWindowsExportWarning()
        {
            string warningText = GetLubanWindowsExportWarningText(Application.platform);
            if (string.IsNullOrEmpty(warningText))
            {
                return;
            }

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Layout.Vertical("HelpBox", () =>
                {
                    EditorUtil.Draw.Space(4f);
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Label(warningText, m_DescStyle, false, GUILayout.ExpandWidth(true));
                        EditorUtil.Draw.Space(4f);
                    });
                    EditorUtil.Draw.Space(4f);
                });
                EditorUtil.Draw.Space(12f);
            });

            EditorUtil.Draw.Space(4f);
        }

        /// <summary>
        /// 绘制 Luban 安装指南区域（平台对应命令 + 复制按钮）。
        /// </summary>
        private void DrawLubanInstallGuide()
        {
            bool dotnetMissing = m_LubanCheckResult.Issue == EnvironmentIssue.DotnetNotFound || m_LubanCheckResult.Issue == EnvironmentIssue.DotnetVersionTooLow || m_LubanCheckResult.Issue == EnvironmentIssue.DotnetVersionTooHigh;
            if (!dotnetMissing)
            {
                return;
            }

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("安装指南", m_SectionTitleStyle, false);
            });

            EditorUtil.Draw.Space(4f);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label(Runtime.Txt.Format("建议版本：{0} ~ {1}", EditorUtil.Environment.LubanChecker.c_MinDotnetVersion, EditorUtil.Environment.LubanChecker.c_MaxDotnetVersion), m_DescStyle, false);
            });

            EditorUtil.Draw.Space(4f);

            bool isMac = Application.platform == RuntimePlatform.OSXEditor;
            bool isWindows = Application.platform == RuntimePlatform.WindowsEditor;
            string platformName = isMac ? "macOS (dotnet-install.sh)" : (isWindows ? "Windows (winget)" : "Linux (dotnet-install.sh)");
            string installCmd = isMac ? c_InstallCmdMac : (isWindows ? c_InstallCmdWin : c_InstallCmdLinux);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label(platformName, false, GUILayout.Width(200f));
            });

            EditorUtil.Draw.Space(2f);

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Layout.Vertical("HelpBox", () =>
                {
                    EditorUtil.Draw.Space(4f);
                    EditorUtil.Draw.Layout.Horizontal(() =>
                    {
                        EditorUtil.Draw.Label(installCmd, m_CodeStyle, false, GUILayout.ExpandWidth(true));
                        EditorUtil.Draw.Space(4f);
                        EditorUtil.Draw.Button("复制", false, false, () =>
                        {
                            EditorGUIUtility.systemCopyBuffer = installCmd;
                        }, GUILayout.Width(50f));
                        EditorUtil.Draw.Space(4f);
                    });
                    EditorUtil.Draw.Space(4f);
                });
                EditorUtil.Draw.Space(12f);
            });

            EditorUtil.Draw.Space(2f);
        }

        /// <summary>
        /// 获取 dotnet 状态描述文本。
        /// </summary>
        /// <returns>状态描述。</returns>
        private string ResolveDotnetStatusText()
        {
            switch (m_LubanCheckResult.Issue)
            {
                case EnvironmentIssue.DotnetNotFound:
                    return Runtime.Txt.Format("未找到 dotnet，请安装 {0} ~ {1}", EditorUtil.Environment.LubanChecker.c_MinDotnetVersion, EditorUtil.Environment.LubanChecker.c_MaxDotnetVersion);
                case EnvironmentIssue.DotnetVersionTooLow:
                    return Runtime.Txt.Format("版本过低（当前 {0}，需要 {1} ~ {2}）", m_LubanCheckResult.DotnetVersion, EditorUtil.Environment.LubanChecker.c_MinDotnetVersion, EditorUtil.Environment.LubanChecker.c_MaxDotnetVersion);
                case EnvironmentIssue.DotnetVersionTooHigh:
                    return Runtime.Txt.Format("版本过高（当前 {0}，需要 {1} ~ {2}）", m_LubanCheckResult.DotnetVersion, EditorUtil.Environment.LubanChecker.c_MinDotnetVersion, EditorUtil.Environment.LubanChecker.c_MaxDotnetVersion);
                case EnvironmentIssue.DotnetNotExecutable:
                    return "dotnet 执行失败，请检查安装是否完整";
                default:
                    if (string.IsNullOrEmpty(m_LubanCheckResult.DotnetVersion))
                    {
                        return "就绪";
                    }
                    return Runtime.Txt.Format("就绪（{0}）", m_LubanCheckResult.DotnetVersion);
            }
        }

        /// <summary>
        /// 获取 Luban.dll 状态描述文本。
        /// </summary>
        /// <returns>状态描述。</returns>
        private string ResolveLubanDllStatusText()
        {
            if (m_LubanCheckResult.Issue == EnvironmentIssue.LubanDllNotFound)
            {
                return "未找到，请确认 com.solotopia.luban 包已安装";
            }

            if (m_LubanCheckResult.Issue == EnvironmentIssue.DotnetNotFound || m_LubanCheckResult.Issue == EnvironmentIssue.DotnetVersionTooLow || m_LubanCheckResult.Issue == EnvironmentIssue.DotnetVersionTooHigh || m_LubanCheckResult.Issue == EnvironmentIssue.DotnetNotExecutable)
            {
                return "（待检测）";
            }

            return "就绪";
        }

        /// <summary>
        /// 判断 dotnet 检测是否通过（路径存在且版本在兼容区间内）。
        /// </summary>
        /// <returns>dotnet 是否就绪。</returns>
        private bool IsDotnetReady()
        {
            return m_LubanCheckResult.Issue != EnvironmentIssue.DotnetNotFound && m_LubanCheckResult.Issue != EnvironmentIssue.DotnetVersionTooLow && m_LubanCheckResult.Issue != EnvironmentIssue.DotnetNotExecutable && m_LubanCheckResult.Issue != EnvironmentIssue.DotnetVersionTooHigh;
        }

        /// <summary>
        /// 获取 Windows 平台下的 Luban 导出安全限制提示文本。
        /// </summary>
        /// <param name="platform">当前编辑器平台。</param>
        /// <returns>Windows 返回提示文本，其它平台返回空字符串。</returns>
        private static string GetLubanWindowsExportWarningText(RuntimePlatform platform)
        {
            if (platform != RuntimePlatform.WindowsEditor)
            {
                return string.Empty;
            }

            return "Win11系统在使用Luban导表功能时可能会提示：‘应用程序控制策略已阻止此文件’ 类似的错误，这是操作系统本身的安全限制问题，具体解决方案：设置->隐私和安全性->Windows安全中心->应用和浏览器控制->智能应用控制设置->关闭 即可。";
        }
    }
}
