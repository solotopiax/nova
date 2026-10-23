/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MemoryProtectionPrivacyPanel.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   独立内存防护包在隐私工作副本中的配置与商业插件引导
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>自动注册隐私扩展，仅写工作副本的通用 JSON，不依赖主窗口内部成员。</summary>
    [InitializeOnLoad]
    internal static class MemoryProtectionPrivacyPanel
    {
        private static GUIStyle s_SectionTitleStyle;

        /// <summary>脚本域加载时注册扩展，安装独立包后自动出现在 AES 与 iOS 区域之间。</summary>
        static MemoryProtectionPrivacyPanel()
        {
            PrivacyPanelExtensions.Register(MemoryProtectionConfigs.ExtensionKey, 100, Draw);
        }

        /// <summary>绘制两项内存防护开关；字段修改由宿主统一应用、广播及保存。</summary>
        /// <param name="privacy">当前坐标的 Privacy 工作副本。</param>
        /// <param name="repaint">宿主窗口的重绘回调。</param>
        private static void Draw(SerializedProperty privacy, Action repaint)
        {
            SerializedProperty extensions = privacy.FindPropertyRelative("ExtensionConfigsJson");
            if (extensions == null) return;
            DrawSectionSeparator();
            s_SectionTitleStyle ??= new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.7f, 0.85f, 1f) },
            };
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("内存数据防护", s_SectionTitleStyle, false);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(4f);
            if (!MemoryProtectionConfigs.TryRead(new PrivacyConfigs
            {
                ExtensionConfigsJson = extensions.stringValue,
            }, out MemoryProtectionConfigs config))
            {
                EditorUtil.Draw.HelpBox(MessageType.Warning, new[]
                {
                    "内存防护的隐私扩展配置格式无效；请修复配置，现有内容不会被覆盖。",
                }, false);
                return;
            }

            EditorGUI.BeginChangeCheck();
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Label("检测内存篡改", false, GUILayout.Width(140f));
                config.EnableMemoryTamperDetection = EditorUtil.Draw.Toggle(config.EnableMemoryTamperDetection);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                using (new EditorGUI.DisabledScope(!config.EnableMemoryTamperDetection))
                {
                    EditorUtil.Draw.Label("定期更换内存密钥", false, GUILayout.Width(140f));
                    config.EnableMemoryKeyRotation = EditorUtil.Draw.Toggle(config.EnableMemoryKeyRotation);
                }
                EditorUtil.Draw.Space(16f);
            });
            if (EditorGUI.EndChangeCheck())
                extensions.stringValue = config.WriteToPrivacyJson(extensions.stringValue);
            DrawInstallationGuide(repaint);
        }

        /// <summary>绘制插件重新检查与识别状态，按钮和状态位于简短说明上方。</summary>
        /// <param name="repaint">插件识别结果变化后的窗口重绘回调。</param>
        private static void DrawInstallationGuide(Action repaint)
        {
            bool available = MemoryProtectionInstallation.IsAvailable;
            EditorUtil.PlugPals.RequiredLibraryGuide guide = MemoryProtectionInstallation.Guide;
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Button("重新检查插件", false, () =>
                {
                    MemoryProtectionInstallation.Refresh();
                    repaint?.Invoke();
                }, GUILayout.Width(120f), GUILayout.Height(24f));
                EditorUtil.Draw.Space(8f);
                EditorUtil.Draw.StatusLabel(available
                    ? "当前状态：已检测到 Anti-Cheat Toolkit"
                    : "当前状态：未检测到可用的 Anti-Cheat Toolkit", available ? Color.green : Color.yellow);
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(8f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(available ? MessageType.Info : MessageType.Warning,
                    available
                        ? new[]
                        {
                            "(1) 使用自动生成的内存密钥保护已接入的关键数值。",
                            "(2) 异常检测与轮换结果通过框架埋点上报。",
                        }
                        : new[]
                        {
                            "(1) 插件缺失时防护不生效，配置保存、导出及游戏运行不受影响。",
                            "(2) 外网用户购买后从 My Assets 导入；内网授权用户从内部云仓库安装。",
                        }, false, GUILayout.ExpandWidth(true));
                EditorUtil.Draw.Space(16f);
            });
            if (available) return;
            EditorUtil.Draw.Space(4f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Button("商店购买 Anti-Cheat Toolkit", false,
                    () => Application.OpenURL(guide.purchaseUrl), GUILayout.Width(210f), GUILayout.Height(24f));
                EditorUtil.Draw.Button("打开 Package Manager", false,
                    () => UnityEditor.PackageManager.UI.Window.Open(string.Empty), GUILayout.Width(170f), GUILayout.Height(24f));
                EditorUtil.Draw.Button("打开内部云仓库", false,
                    PlugPalsWindow.OpenInternalRegistry, GUILayout.Width(140f), GUILayout.Height(24f));
                EditorUtil.Draw.Space(16f);
            });
        }

        /// <summary>按宿主隐私页的公共内容边界分隔独立扩展区域。</summary>
        private static void DrawSectionSeparator()
        {
            EditorUtil.Draw.Space(12f);
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Line();
                EditorUtil.Draw.Space(16f);
            });
            EditorUtil.Draw.Space(12f);
        }
    }
}
