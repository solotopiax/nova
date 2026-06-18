/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureComponentInspector.Methods.cs
 * author:    taoye
 * created:   2026/3/9
 * descrip:   Procedure组件编辑器面板定制 —— 私有方法
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    internal sealed partial class ProcedureComponentInspector : BaseComponentInspector
    {
        /// <summary>
        /// 绘制配置信息。
        /// </summary>
        private void DrawConfigs()
        {
            EditorUtil.Draw.TypesSelector("Procedure 管理器", m_ManagerTypeNames, m_CurManagerTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.HelpBox(MessageType.Info, new[] { "支持自定义类型，实现框架层 IProcedureManager 接口后，该类型将自动出现在此列表中。" });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 刷新流程类型名称列表，并验证入口流程有效性。
        /// </summary>
        private void RefreshProcedureTypeNames()
        {
            m_ProcedureTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(ProcedureBase)));

            string entrance = m_EntranceProcedureTypeName.stringValue;
            if (!string.IsNullOrEmpty(entrance) && !m_ProcedureTypeNames.Contains(entrance))
            {
                m_EntranceProcedureTypeName.stringValue = string.Empty;
            }

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>
        /// 绘制入口流程选择。
        /// </summary>
        private void DrawProcedureSettings()
        {
            if (m_ProcedureTypeNames.Count == 0)
            {
                EditorUtil.Draw.HelpBox(MessageType.Warning, new string[] { "未找到任何 ProcedureBase 的子类。" });
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.TypesSelector("入口流程", m_ProcedureTypeNames, m_EntranceProcedureTypeName, true, null, GUILayout.Width(180f));
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制启动阶段配置。
        /// </summary>
        private void DrawLauncherSettings()
        {
            if (!EditorUtil.Draw.Foldout("启动阶段配置", null, true))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Property("闪屏展示时间 (秒)", m_LauncherSettings.FindPropertyRelative("m_SplashDuration"), true, GUILayout.Width(180f));
            });
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Property("闪屏面板 Prefab", m_LauncherSettings.FindPropertyRelative("m_SplashPanelPrefab"), true, GUILayout.Width(180f));
            });
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Property("进度面板 Prefab", m_LauncherSettings.FindPropertyRelative("m_ProgressPanelPrefab"), true, GUILayout.Width(180f));
            });
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Property("弹窗面板 Prefab", m_LauncherSettings.FindPropertyRelative("m_DialogPanelPrefab"), true, GUILayout.Width(180f));
            });
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1)路径以 Resources/ 为根，不带文件扩展名（如 BuiltIn/Prefabs/LauncherSplashPanel）",
                    "(2)用于启动阶段（资源系统与热更流程就绪前）的最小 UI：闪屏、加载进度、错误弹窗",
                    "(3)Prefab 必须打入主包随版本发布，该阶段无法访问热更资源，热更失败时仍需依赖这些面板向用户呈现提示",
                }, false, GUILayout.ExpandWidth(true));
            });
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.Property("多语言 JSON 路径模板", m_LauncherSettings.FindPropertyRelative("m_LocalizationJsonPathTemplate"), true, GUILayout.Width(180f));
            });
            EditorUtil.Draw.Layout.Horizontal(() =>
            {
                EditorUtil.Draw.Space(16f);
                EditorUtil.Draw.HelpBox(MessageType.Info, new[]
                {
                    "(1)路径以 Resources/ 为根，不带文件扩展名，{0} 占位 Language 枚举名（如 BuiltIn/Jsons/LocalizationTexts_{0}）",
                    "(2)启动阶段独立解析器只走 Resources 通道，与 LocalizationManager 完全解耦，可在资源系统就绪前安全使用",
                    "(3)JSON 必须随主包发版；缺失时回退 English，English 也缺失则文本走 key 自身回退",
                }, false, GUILayout.ExpandWidth(true));
            });
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制运行时信息：当前流程名称 + LoadException + LoadComplete/EntranceType（仅 Play Mode 可见）。
        /// </summary>
        private void DrawRuntimeInfo()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            ProcedureComponent component = (ProcedureComponent)target;
            string currentName = component.CurrentProcedure != null ? component.CurrentProcedure.GetType().FullName : "无";
            EditorUtil.Draw.Label("当前流程", currentName, false);

            // 通过反射获取当前 ProcedureLoadDll 实例的运行时状态
            ProcedureBase currentProc = component.CurrentProcedure;
            Exception loadException = TryGetLoadException(currentProc);
            if (loadException != null)
            {
                EditorUtil.Draw.HelpBox(MessageType.Error, new[] { loadException.Message }, false);
            }

            bool? loadComplete = TryGetLoadComplete(currentProc);
            if (loadComplete.HasValue)
            {
                EditorUtil.Draw.Label("DLL 加载完成", loadComplete.Value ? "是" : "否", false);
            }

            Type entranceType = TryGetEntranceType(currentProc);
            if (entranceType != null)
            {
                EditorUtil.Draw.Label("业务入口类型", entranceType.FullName, false);
            }

            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 绘制 Procedure 跳转历史 Foldout（仅 Play Mode 可见）。
        /// 历史数据由 ProcedureComponent.RunHistory 提供，Inspector 只做展示。
        /// </summary>
        private void DrawProcedureHistory()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (!EditorUtil.Draw.Foldout(ref m_RunHistoryFoldout, "Procedure History"))
            {
                EditorUtil.Draw.Line();
                return;
            }

            EditorUtil.Draw.IncreaseIndentLevel();

            IReadOnlyList<ProcedureRunInfo> history = ((ProcedureComponent)target).RunHistory;
            if (history == null || history.Count == 0)
            {
                EditorUtil.Draw.Label("（等待第一个 Procedure 进入）", false);
            }
            else
            {
                for (int i = 0; i < history.Count; i++)
                {
                    ProcedureRunInfo info = history[i];
                    string elapsed = TimeSpan.FromSeconds(info.Elapsed).ToString(@"hh\:mm\:ss");
                    string status = info.Finished ? "  " : " *";
                    // TODO: 待 EditorUtil.Draw 提供 Rect 版行内布局后替换此裸 EditorGUILayout 调用。
                    // 第一列 TypeFullName 走 ExpandWidth 避免被默认 labelWidth 截断；第二列 elapsed 固定宽度靠右对齐。
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"#{i + 1}{status} {info.TypeFullName}", GUILayout.ExpandWidth(true));
                    EditorGUILayout.LabelField(elapsed, GUILayout.Width(70f));
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorUtil.Draw.DecreaseIndentLevel();
            EditorUtil.Draw.Line();
        }

        /// <summary>
        /// 通过反射从 ProcedureLoadDll 实例获取 LoadException，非 ProcedureLoadDll 类型时返回 null。
        /// </summary>
        /// <param name="procedure">当前 Procedure 实例。</param>
        /// <returns>LoadException 值，无法获取时为 null。</returns>
        private Exception TryGetLoadException(ProcedureBase procedure)
        {
            if (procedure == null || m_LoadExceptionPropInfo == null)
            {
                return null;
            }

            if (procedure.GetType().FullName != "NovaFramework.Runtime.ProcedureLoadDll")
            {
                return null;
            }

            try
            {
                return m_LoadExceptionPropInfo.GetValue(procedure) as Exception;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 通过反射从 ProcedureLoadDll 实例获取 LoadComplete，非 ProcedureLoadDll 类型时返回 null。
        /// </summary>
        /// <param name="procedure">当前 Procedure 实例。</param>
        /// <returns>LoadComplete 值，无法获取时为 null。</returns>
        private bool? TryGetLoadComplete(ProcedureBase procedure)
        {
            if (procedure == null || m_LoadCompletePropInfo == null)
            {
                return null;
            }

            if (procedure.GetType().FullName != "NovaFramework.Runtime.ProcedureLoadDll")
            {
                return null;
            }

            try
            {
                object val = m_LoadCompletePropInfo.GetValue(procedure);
                return val is bool b ? b : (bool?)null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 通过反射从 ProcedureLoadDll 实例获取 EntranceType，非 ProcedureLoadDll 类型时返回 null。
        /// </summary>
        /// <param name="procedure">当前 Procedure 实例。</param>
        /// <returns>EntranceType 值，无法获取时为 null。</returns>
        private Type TryGetEntranceType(ProcedureBase procedure)
        {
            if (procedure == null || m_EntranceTypePropInfo == null)
            {
                return null;
            }

            if (procedure.GetType().FullName != "NovaFramework.Runtime.ProcedureLoadDll")
            {
                return null;
            }

            try
            {
                return m_EntranceTypePropInfo.GetValue(procedure) as Type;
            }
            catch
            {
                return null;
            }
        }
    }
}
