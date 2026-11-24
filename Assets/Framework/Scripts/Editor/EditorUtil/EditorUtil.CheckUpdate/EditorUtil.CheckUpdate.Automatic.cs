/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CheckUpdate.Automatic.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   启动更新提示的安全排队与跨重载恢复
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Log = NovaFramework.Runtime.Log;
using LogTag = NovaFramework.Runtime.LogTag;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CheckUpdate
        {
            // 仅保存尚未展示的会话结果，展示后立即删除；不写入用户的跳过配置或长期验收文件。
            private const string c_AutomaticSnapshotSessionKey = "Nova.CheckUpdate.Automatic.PendingSnapshot.v1";
            private const string c_AutomaticPhaseSessionKey = "Nova.CheckUpdate.Automatic.Phase.v1";
            private const string c_AutomaticWaitingReasonSessionKey = "Nova.CheckUpdate.Automatic.WaitingReason.v1";
            private const double c_AutomaticDayPollIntervalSeconds = 60d;
            private static bool s_AutomaticCheckQueued;
            private static bool s_AutomaticQueryRunning;
            private static bool s_AutomaticStopping;
            private static DateTime s_LastAutomaticCheckDay;
            private static double s_NextAutomaticDayPoll;
            private static AutomaticUpdateSnapshot s_AutomaticSnapshot;

            /// <summary>已经查询但尚未展示的两来源列表；来源分开保留，避免改变跳过配置的归属。</summary>
            private sealed class AutomaticUpdateSnapshot
            {
                public List<UpdateInfo> External = new List<UpdateInfo>();
                public List<UpdateInfo> Internal = new List<UpdateInfo>();
            }

            /// <summary>恢复会话中的待展示结果；没有已完成结果时才安排一次只读网络查询。</summary>
            private static void QueueAutomaticStartupCheck()
            {
                if (Application.isBatchMode || s_AutomaticStopping) return;
                string cached = SessionState.GetString(c_AutomaticSnapshotSessionKey, string.Empty);
                if (!string.IsNullOrEmpty(cached))
                {
                    try { s_AutomaticSnapshot = DeserializeAutomaticUpdateSnapshot(cached); }
                    catch (Exception)
                    {
                        // 缓存不是用户跳过记录；无法恢复时可重新只读查询，但不能假称已展示或已确认。
                        SessionState.EraseString(c_AutomaticSnapshotSessionKey);
                        Log.Warning(LogTag.Editor, "自动更新提示的待展示结果无法恢复，将重新查询；未修改已跳过版本。");
                    }
                }
                AssemblyReloadEvents.beforeAssemblyReload -= StopAutomaticStartupCheck;
                AssemblyReloadEvents.beforeAssemblyReload += StopAutomaticStartupCheck;
                EditorApplication.quitting -= StopAutomaticStartupCheck;
                EditorApplication.quitting += StopAutomaticStartupCheck;
                AttachAutomaticStartupCheck();
                SetAutomaticCheckState("queued", s_AutomaticSnapshot == null
                    ? "已安排启动更新检查，等待当前安装和重要声明处理结束。"
                    : "已恢复待展示的更新列表，无需重新查询仓库。");
            }

            /// <summary>合并展示请求，持续使用 update 等待，不依赖单帧 delayCall 恰好空闲。</summary>
            private static void AttachAutomaticStartupCheck()
            {
                s_AutomaticCheckQueued = true;
                EditorApplication.update -= TryRunAutomaticStartupCheck;
                EditorApplication.update += TryRunAutomaticStartupCheck;
            }

            /// <summary>启动或跨日时等待稳定状态再查询；异步结果经过同一门禁展示，等待期间复用查询结果。</summary>
            private static void TryRunAutomaticStartupCheck()
            {
                if (Application.isBatchMode || s_AutomaticStopping)
                {
                    EditorApplication.update -= TryRunAutomaticStartupCheck;
                    return;
                }
                if (s_AutomaticQueryRunning) return;
                if (!s_AutomaticCheckQueued)
                {
                    // 本轮结束后只按分钟查看本地日期，跨日才重新进入检查队列。
                    double now = EditorApplication.timeSinceStartup;
                    if (now < s_NextAutomaticDayPoll) return;
                    s_NextAutomaticDayPoll = now + c_AutomaticDayPollIntervalSeconds;
                    if (s_LastAutomaticCheckDay == DateTime.Today) return;

                    s_AutomaticCheckQueued = true;
                    SetAutomaticCheckState("queued", "已安排新一天的更新检查，等待编辑器就绪。");
                }
                string reason = AutomaticWindowGate.ReadWaitReason(true);
                if (!string.IsNullOrEmpty(reason))
                {
                    SetAutomaticCheckState("waiting", reason);
                    return;
                }
                if (s_AutomaticSnapshot == null)
                {
                    _ = CheckOnStartupAsync();
                    return;
                }

                DateTime presentationDay = DateTime.Today;
                try
                {
                    IReadOnlyDictionary<string, string> installed = PlugPals.ReadInstalledVersions();
                    if (installed == null)
                    {
                        SetAutomaticCheckState("waiting", "等待 Unity 提供当前已安装包状态；已查询的更新列表仍保留。");
                        return;
                    }
                    SkipConfig config = LoadConfigForDay(presentationDay);
                    List<UpdateInfo> external = FilterSkippedUpdates(RefreshAutomaticUpdateInfos(s_AutomaticSnapshot.External, installed), false, config);
                    List<UpdateInfo> internalItems = FilterSkippedUpdates(RefreshAutomaticUpdateInfos(s_AutomaticSnapshot.Internal, installed), true, config);
                    // 安装和人工窗口状态可能在查询、恢复或本地过滤期间改变，真正展示前最后读取一次。
                    reason = AutomaticWindowGate.ReadWaitReason(true);
                    if (!string.IsNullOrEmpty(reason))
                    {
                        SetAutomaticCheckState("waiting", reason);
                        return;
                    }
                    if ((external.Count > 0 || internalItems.Count > 0) && !CheckUpdateWindow.TryOpenAutomatic(external, internalItems))
                    {
                        SetAutomaticCheckState("waiting", "已有更新窗口正在使用；保留查询结果，等待关闭后再展示，未覆盖窗口中的用户选择。");
                        return;
                    }
                    CompleteAutomaticPresentation(presentationDay);
                }
                catch (Exception exception)
                {
                    FinishAutomaticCheckForDay(presentationDay);
                    SetAutomaticCheckState("blocked", "更新结果尚未展示；待修复后可重载恢复或从菜单手动检查，未写入跳过记录。");
                    Log.Warning(LogTag.Editor, "自动更新提示尚未展示：{0}", exception.Message);
                }
            }

            /// <summary>保存已经取得的只读结果，等待安全展示；不提前过滤或写入跳过记录。</summary>
            /// <param name="external">公网仓库查询结果。</param>
            /// <param name="internalItems">内部仓库查询结果。</param>
            private static void QueueAutomaticPresentation(IReadOnlyList<UpdateInfo> external, IReadOnlyList<UpdateInfo> internalItems)
            {
                if (s_AutomaticStopping) return;
                string snapshot = SerializeAutomaticUpdateSnapshot(external, internalItems);
                SessionState.SetString(c_AutomaticSnapshotSessionKey, snapshot);
                s_AutomaticSnapshot = DeserializeAutomaticUpdateSnapshot(snapshot);
                AttachAutomaticStartupCheck();
                SetAutomaticCheckState("queued", "更新查询已结束，等待安全展示；不会因等待而重复请求仓库。");
            }

            /// <summary>只保存包名和版本，按来源分开，避免把地址、凭据或网络原文写入会话。</summary>
            /// <param name="external">公网结果。</param>
            /// <param name="internalItems">内部结果。</param>
            /// <returns>供同一 Editor 会话跨重载恢复的短期 JSON。</returns>
            private static string SerializeAutomaticUpdateSnapshot(IReadOnlyList<UpdateInfo> external, IReadOnlyList<UpdateInfo> internalItems)
            {
                return new JObject
                {
                    ["schemaVersion"] = 1,
                    ["external"] = JArray.FromObject(external ?? Array.Empty<UpdateInfo>()),
                    ["internal"] = JArray.FromObject(internalItems ?? Array.Empty<UpdateInfo>()),
                }.ToString(Newtonsoft.Json.Formatting.None);
            }

            /// <summary>严格恢复本工具写入的待展示列表，错误缓存不被当作已经展示或已经跳过。</summary>
            /// <param name="json">会话中的短期结果。</param>
            /// <returns>保留两来源身份和原查询版本的列表。</returns>
            private static AutomaticUpdateSnapshot DeserializeAutomaticUpdateSnapshot(string json)
            {
                JObject document = JObject.Parse(json);
                if ((int?)document["schemaVersion"] != 1 || !(document["external"] is JArray external) || !(document["internal"] is JArray internalItems))
                    throw new InvalidOperationException("自动更新提示缓存结构无效。");
                var result = new AutomaticUpdateSnapshot
                {
                    External = external.ToObject<List<UpdateInfo>>(),
                    Internal = internalItems.ToObject<List<UpdateInfo>>(),
                };
                if (result.External.Concat(result.Internal).Any(item => item == null || string.IsNullOrEmpty(item.PackageName)
                    || string.IsNullOrEmpty(item.CurrentVersion) || string.IsNullOrEmpty(item.LatestVersion)))
                    throw new InvalidOperationException("自动更新提示缓存缺少包名或版本。");
                return result;
            }

            /// <summary>展示前重新读取的本地版本拥有优先权，等待期间已升级或已卸载的包不再提示旧更新。</summary>
            /// <param name="updates">已经查询的某一来源结果。</param>
            /// <param name="installed">当前实际安装版本。</param>
            /// <returns>仍有升级可用的结果；不访问网络、不改变远端最新版本。</returns>
            private static List<UpdateInfo> RefreshAutomaticUpdateInfos(IReadOnlyList<UpdateInfo> updates, IReadOnlyDictionary<string, string> installed)
            {
                var result = new List<UpdateInfo>();
                foreach (UpdateInfo item in updates)
                {
                    if (!installed.TryGetValue(item.PackageName, out string current) || PlugPals.CompareSemVer(item.LatestVersion, current) <= 0) continue;
                    result.Add(new UpdateInfo(item.PackageName, current, item.LatestVersion));
                }
                return result;
            }

            /// <summary>实际展示或确定无需提示后结束本轮队列，并清除已消费的待展示列表。</summary>
            /// <param name="day">本轮判定是否展示时所用的本地自然日。</param>
            private static void CompleteAutomaticPresentation(DateTime day)
            {
                s_AutomaticSnapshot = null;
                SessionState.EraseString(c_AutomaticSnapshotSessionKey);
                FinishAutomaticCheckForDay(day);
                SetAutomaticCheckState("complete", "本轮自动更新提示处理结束；更新检查不执行包升级。");
            }

            /// <summary>结束当天这轮自动检查，留下轻量日期检查供常开的编辑器跨日再次提醒。</summary>
            /// <param name="day">本轮实际处理的本地自然日。</param>
            private static void FinishAutomaticCheckForDay(DateTime day)
            {
                s_AutomaticCheckQueued = false;
                s_LastAutomaticCheckDay = day;
            }

            /// <summary>域重载或退出时停止当前域回调，保留尚未展示的会话结果供重载恢复。</summary>
            private static void StopAutomaticStartupCheck()
            {
                s_AutomaticStopping = true;
                EditorApplication.update -= TryRunAutomaticStartupCheck;
                SetAutomaticCheckState("waiting-editor", "等待脚本重载；已完成查询的待展示结果会恢复。");
            }

            /// <summary>记录只读阶段和中文等待原因，不把普通查询完成当作包升级成功。</summary>
            /// <param name="phase">queued、waiting、querying、blocked、waiting-editor 或 complete。</param>
            /// <param name="reason">当前阶段的说明。</param>
            private static void SetAutomaticCheckState(string phase, string reason)
            {
                SessionState.SetString(c_AutomaticPhaseSessionKey, phase);
                SessionState.SetString(c_AutomaticWaitingReasonSessionKey, reason);
            }
        }
    }
}
