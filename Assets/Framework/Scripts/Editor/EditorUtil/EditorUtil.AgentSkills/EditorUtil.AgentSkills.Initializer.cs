/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AgentSkills.Initializer.cs
 * author:    taoye
 * created:   2026/8/13
 * descrip:   Nova Project Skills Editor 自动 bridge 启动入口
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using PackageRegistrationEventArgs = UnityEditor.PackageManager.PackageRegistrationEventArgs;
using PackageEvents = UnityEditor.PackageManager.Events;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AgentSkills
        {
            /// <summary>
            /// 在交互式 Unity Editor 中合并包注册事件，并在编辑器稳定后自动投影 Project Skills。
            /// </summary>
            [InitializeOnLoad]
            private static class Initializer
            {
                private static bool s_IsQueued;

                static Initializer()
                {
                    PackageEvents.registeredPackages -= OnRegisteredPackages;
                    PackageEvents.registeredPackages += OnRegisteredPackages;
                    QueueAutomaticReconcile();
                }

                /// <summary>
                /// 仅在 Framework 被新增或升级为目标版本时安排自动 bridge，忽略无关包变动。
                /// </summary>
                /// <param name="eventArgs">Unity Package Manager 的本次注册变更。</param>
                private static void OnRegisteredPackages(PackageRegistrationEventArgs eventArgs)
                {
                    if (eventArgs != null
                        && (ContainsFramework(eventArgs.added) || ContainsFramework(eventArgs.changedTo)))
                    {
                        QueueAutomaticReconcile();
                    }
                }

                /// <summary>
                /// 判断一组已注册 PackageInfo 中是否包含当前 Framework 包。
                /// </summary>
                /// <param name="packages">待检查的包集合。</param>
                /// <returns>包含 Framework 包时返回 true。</returns>
                private static bool ContainsFramework(IEnumerable<PackageInfo> packages)
                {
                    if (packages == null)
                    {
                        return false;
                    }

                    foreach (PackageInfo packageInfo in packages)
                    {
                        if (packageInfo != null
                            && string.Equals(packageInfo.name, c_FrameworkPackageName, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }

                    return false;
                }

                /// <summary>
                /// 合并同一轮 Editor 启动或 Package 注册产生的多个 reconcile 请求，并由 update 心跳保证空闲 Editor 也能继续推进。
                /// </summary>
                private static void QueueAutomaticReconcile()
                {
                    if (Application.isBatchMode)
                    {
                        return;
                    }

                    s_IsQueued = true;
                    // 清理旧版可能遗留的 delayCall，避免其依赖 Inspector 刷新而长期不被派发。
                    EditorApplication.delayCall -= TryRunAutomaticReconcile;
                    // 即使 s_IsQueued 已为 true 也重挂 update，用于修复回调丢失后的待执行状态。
                    EditorApplication.update -= TryRunAutomaticReconcile;
                    EditorApplication.update += TryRunAutomaticReconcile;
                }

                /// <summary>
                /// 每个 Editor update 检查脚本编译和包更新状态；稳定后仅执行一次 reconcile。
                /// </summary>
                private static void TryRunAutomaticReconcile()
                {
                    if (!s_IsQueued || Application.isBatchMode)
                    {
                        s_IsQueued = false;
                        EditorApplication.delayCall -= TryRunAutomaticReconcile;
                        EditorApplication.update -= TryRunAutomaticReconcile;
                        return;
                    }

                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    {
                        return;
                    }

                    s_IsQueued = false;
                    EditorApplication.delayCall -= TryRunAutomaticReconcile;
                    EditorApplication.update -= TryRunAutomaticReconcile;
                    try
                    {
                        ReconcileResult result = AgentSkills.Reconcile();
                        if (result.Conflicts.Count > 0)
                        {
                            Debug.LogWarning($"[Nova Project Skills] 自动 bridge 部分完成，保留本地冲突：{BuildConflictSummary(result.Conflicts)}");
                        }
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning($"[Nova Project Skills] 自动 bridge 未完成：{exception.Message}");
                    }
                }

                /// <summary>
                /// 将结构化冲突压缩为 Console 可扫描的 id:reason 文本，不改变对外结果契约。
                /// </summary>
                /// <param name="conflicts">本次 reconcile 保留的冲突集合。</param>
                /// <returns>逗号分隔的稳定冲突摘要。</returns>
                private static string BuildConflictSummary(IReadOnlyList<ReconcileConflict> conflicts)
                {
                    var builder = new StringBuilder();
                    for (int index = 0; index < conflicts.Count; index++)
                    {
                        if (index > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(conflicts[index].Id);
                        builder.Append(':');
                        builder.Append(conflicts[index].Reason);
                    }

                    return builder.ToString();
                }
            }
        }
    }
}
