/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ImportantUpdates.Initializer.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   重要升级声明的启动补查与 UPM 注册事件调度
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        internal static partial class ImportantUpdates
        {
            private const string c_StartedSessionKey = "Nova.ImportantUpdates.Started";
            private const string c_ScopedSessionKey = "Nova.ImportantUpdates.ScopedOperation";
            private static bool s_Queued;
            private static bool s_CheckAll;
            private static bool s_Showing;
            private static double s_NextCheck;
            private static ListRequest s_ListRequest;
            private static string s_LastError;

            /// <summary>重载后重新注册包事件；真正启动时全量补查，重载时优先恢复本地显式批次。</summary>
            [InitializeOnLoad]
            private static class Initializer
            {
                /// <summary>只在交互式 Editor 安排检查，batchmode 不弹窗、不写已读记录。</summary>
                static Initializer()
                {
                    if (Application.isBatchMode) return;
                    Events.registeredPackages -= OnRegisteredPackages;
                    Events.registeredPackages += OnRegisteredPackages;
                    bool startup = !SessionState.GetBool(c_StartedSessionKey, false);
                    SessionState.SetBool(c_StartedSessionKey, true);
                    QueueCheck(startup || !SessionState.GetBool(c_ScopedSessionKey, false));
                }
            }

            /// <summary>包注册通知只合并检查；显式升级会话不混入自动变更依赖和其他包的旧声明。</summary>
            private static void OnRegisteredPackages(PackageRegistrationEventArgs args)
            {
                QueueCheck(!SessionState.GetBool(c_ScopedSessionKey, false));
            }

            /// <summary>把检查排到 delayCall 后的 Editor update，保证现有 Resolve 先提交且不在 OnGUI 布局栈弹模态。</summary>
            private static void QueueCheck(bool checkAll)
            {
                if (Application.isBatchMode) return;
                s_CheckAll |= checkAll;
                s_Queued = true;
                EditorApplication.delayCall -= AttachCheck;
                EditorApplication.delayCall += AttachCheck;
            }

            /// <summary>以 update 持续等待编译和包变更结束，避免只依靠单帧 delayCall 丢失补查。</summary>
            private static void AttachCheck()
            {
                EditorApplication.update -= TryCheck;
                EditorApplication.update += TryCheck;
            }

            /// <summary>在稳定 Editor 中执行离线包查询；查询完成后才核验本轮目标，不以提交成功或等待时长判定完成。</summary>
            private static void TryCheck()
            {
                if (!s_Queued || Application.isBatchMode)
                {
                    EditorApplication.update -= TryCheck;
                    return;
                }
                if (s_Showing || s_ActiveBatchId != null || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.timeSinceStartup < s_NextCheck) return;

                try
                {
                    LocalState state = LoadState(GetStatePath());
                    if (state.Pending.Count == 0 && !s_CheckAll)
                    {
                        FinishCheck();
                        return;
                    }

                    if (s_ListRequest == null)
                    {
                        // 离线查询由 UPM 排队处理，既不联网也不重新执行 Resolve；重载后可以安全重新查询。
                        s_ListRequest = Client.List(true, true);
                        return;
                    }
                    if (!s_ListRequest.IsCompleted) return;
                    if (s_ListRequest.Status != StatusCode.Success)
                    {
                        string message = s_ListRequest.Error?.message ?? "Unity 离线包查询未成功。";
                        s_ListRequest = null;
                        throw new InvalidOperationException(message);
                    }
                    s_ListRequest = null;

                    Dictionary<string, InstalledPackage> installed = ReadInstalledPackages();
                    bool scoped = state.Pending.Count > 0;
                    UpgradeBatch batch = scoped ? state.Pending[0] : null;
                    if (scoped)
                    {
                        // 包查询已终结后仍未达到目标的包不计入成功；其他成功目标可以合并展示。
                        string[] failed = batch.Targets.Where(target => !installed.TryGetValue(target.PackageName, out InstalledPackage package) ||
                            package.Version != target.TargetVersion).Select(target => target.PackageName + "@" + target.TargetVersion).ToArray();
                        if (failed.Length > 0)
                            Debug.LogWarning("[Nova Important Updates] 以下目标未实际安装成功，不写入声明确认记录：" + string.Join(", ", failed));
                        // 本次只处理显式范围，其他包和自动升级的依赖留给下次真正启动的全量补查。
                        s_CheckAll = false;
                    }

                    Presentation presentation = BuildPresentation(state, installed, batch);
                    UpdateObservations(state, installed);
                    if (presentation.Notices.Count == 0)
                    {
                        if (batch != null) state.Pending.RemoveAll(item => item.Id == batch.Id);
                        SaveState(GetStatePath(), state);
                        if (state.Pending.Count == 0) FinishCheck();
                        return;
                    }

                    SaveState(GetStatePath(), state);
                    s_LastError = null;
                    s_Showing = true;
                    try
                    {
                        // 原生关闭按钮或异常退出不代表确认；窗口只在明确点击后调用 TryAcknowledge。
                        ImportantUpdatesWindow.Show(presentation);
                    }
                    finally
                    {
                        s_Showing = false;
                        // 未确认时在下一次安全检查恢复；这只是重试节流，不参与安装成功判定。
                        s_NextCheck = EditorApplication.timeSinceStartup + 2;
                    }
                    if (LoadState(GetStatePath()).Pending.Count == 0 && scoped) FinishCheck();
                    else if (!scoped)
                    {
                        Presentation remaining = BuildPresentation(LoadState(GetStatePath()), installed, null);
                        if (remaining.Notices.Count == 0) FinishCheck();
                    }
                }
                catch (Exception exception)
                {
                    s_ListRequest = null;
                    string message = exception.Message;
                    if (s_LastError != message)
                    {
                        Debug.LogError("[Nova Important Updates] 检查未完成，未修改已读状态；修复后会继续检查。\n" + exception);
                        s_LastError = message;
                    }
                    s_NextCheck = EditorApplication.timeSinceStartup + 30;
                }
            }

            /// <summary>记录已核验的版本用于下次窗口说明，观察到新版本不等于用户已确认。</summary>
            private static void UpdateObservations(LocalState state, IDictionary<string, InstalledPackage> installed)
            {
                state.Observed = installed.Values.Select(package => new Observation
                {
                    PackageName = package.Name,
                    Version = package.Version
                }).ToList();
            }

            /// <summary>检查结束后注销心跳，后续仅由启动或包变更重新安排。</summary>
            private static void FinishCheck()
            {
                s_Queued = false;
                s_CheckAll = false;
                s_ListRequest = null;
                s_LastError = null;
                EditorApplication.update -= TryCheck;
            }
        }
    }
}
