/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Pipify.WindowUpload.cs
 * author:    Codex
 * created:   2026/10/12
 * descrip:   窗口构建完成后、Google Play 上传开始前的重载交接
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Pipify
        {
            // 只跨同一次 Editor 会话保存构建身份和游标，不保存参数正文、签名密码或凭据内容。
            internal const string c_WindowUploadCheckpointKey = "Nova.Pipify.WindowUploadCheckpoint";

            /// <summary>窗口正在执行或等待上传前重载时，禁止另一条 Batch 占用工作区。</summary>
            internal static bool IsBatchRunning => Runner.IsRunning || HasWindowUploadCheckpoint;

            /// <summary>仅代表尚未启动上传的窗口交接，不用于恢复已经开始的上传。</summary>
            internal static bool HasWindowUploadCheckpoint =>
                !string.IsNullOrEmpty(SessionState.GetString(c_WindowUploadCheckpointKey, ""));

            /// <summary>构建后检查点；参数只记录指纹，恢复时重新核对原存档。</summary>
            [Serializable]
            internal sealed class WindowUploadCheckpoint
            {
                public string MasterGuid;
                public string SettingsGuid;
                public int BatchIndex;
                public string BatchFingerprint;
                public int NextStepIndex;
                public string OutputPath;
                public string OutputFingerprint;
                public string UploadPath;
                public string UploadFingerprint;
                public BuildTarget Target;
                public bool DevelopmentBuild;
                public string PackageName;

                /// <summary>恢复成功产物身份，禁止从重载后的 PlayerSettings 猜包名或文件。</summary>
                internal PipifyBuildArtifact RestoreArtifact() => new PipifyBuildArtifact
                {
                    OutputPath = OutputPath, Target = Target,
                    DevelopmentBuild = DevelopmentBuild, PackageName = PackageName
                };
            }

            /// <summary>保存尚未调用的上传位置，主动让 Unity 完成编译与重载；原域的异步链不再启动上传。</summary>
            private static async UniTask HandOffWindowUploadAsync(Batch batch, int nextStepIndex,
                PipifyBuildArtifact artifact, PipifySteps.GooglePlayUploadInput uploadInput, string masterGuid, string settingsGuid,
                WindowReporter reporter, CancellationToken ct)
            {
                PipifySettingsSO settings = FindSettingsContaining(batch);
                var checkpoint = new WindowUploadCheckpoint
                {
                    MasterGuid = masterGuid, SettingsGuid = settingsGuid,
                    BatchIndex = settings.Batches.IndexOf(batch), BatchFingerprint = GetBatchFingerprint(batch),
                    NextStepIndex = nextStepIndex, OutputPath = artifact.OutputPath,
                    OutputFingerprint = GetArtifactFingerprint(artifact.OutputPath), Target = artifact.Target,
                    UploadPath = uploadInput.AabPath, UploadFingerprint = GetArtifactFingerprint(uploadInput.AabPath),
                    DevelopmentBuild = artifact.DevelopmentBuild, PackageName = artifact.PackageName
                };
                SessionState.SetString(c_WindowUploadCheckpointKey, JsonUtility.ToJson(checkpoint));
                Log.Debug(LogTag.Editor, "{0} 构建已完成，等待编译与重载后继续第 {1} 步；尚未启动 Google Play 上传。",
                    c_LogPrefix, nextStepIndex + 1);
                // 从脚本调用 Refresh 不保证即时重载，必须显式请求并把执行权交回 Editor。
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtility.RequestScriptReload();
                var timer = System.Diagnostics.Stopwatch.StartNew();
                while (timer.Elapsed.TotalSeconds < 120)
                {
                    ct.ThrowIfCancellationRequested();
                    if (reporter.ReportStep(nextStepIndex, "等待编译与重载（上传尚未开始）", 0f))
                        throw new OperationCanceledException(ct);
                    await UniTask.Delay(200, ignoreTimeScale: true, cancellationToken: ct);
                }
                throw new TimeoutException("[Pipify] 等待程序集重载超时；上传尚未启动，请检查 Console。已保留 AAB。");
            }

            /// <summary>重载后等编译、导入和其他启动回调收敛，再尝试消费一次窗口上传检查点。</summary>
            [InitializeOnLoadMethod]
            private static void QueueWindowUploadAfterReload()
            {
                EditorApplication.quitting -= ClearWindowUploadCheckpoint;
                EditorApplication.quitting += ClearWindowUploadCheckpoint;
                if (!HasWindowUploadCheckpoint) return;
                EditorApplication.delayCall += () => ResumeWindowUploadAsync().Forget();
            }

            /// <summary>校验工作区、参数指纹和构建身份，消费检查点后从同一 Runner 继续；失败不重放。</summary>
            internal static async UniTask ResumeWindowUploadAsync()
            {
                var timer = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    // 让重载后 sample 同步等 delayCall 先运行；等待不是冻结 Editor。
                    await UniTask.Delay(500, ignoreTimeScale: true);
                    while (EditorApplication.isCompiling || EditorApplication.isUpdating || BuildPipeline.isBuildingPlayer)
                    {
                        if (timer.Elapsed.TotalSeconds >= 120)
                            throw new TimeoutException("[Pipify] 重载后编辑器未就绪；上传尚未启动，已保留 AAB。");
                        await UniTask.Delay(200, ignoreTimeScale: true);
                    }
                    if (EditorUtility.scriptCompilationFailed)
                        throw new InvalidOperationException("[Pipify] 脚本编译失败；上传尚未启动，已保留 AAB，请先修复 Console 编译错误。");
                    string raw = SessionState.GetString(c_WindowUploadCheckpointKey, "");
                    if (string.IsNullOrEmpty(raw)) return;
                    var checkpoint = JsonUtility.FromJson<WindowUploadCheckpoint>(raw);
                    Batch batch = ResolveWindowUploadBatch(checkpoint);
                    // 必须在调用上传之前消费；以后任何重载都只中断，不会重试远端请求。
                    ClearWindowUploadCheckpoint();
                    EditorWindow host = Resources.FindObjectsOfTypeAll<PipifyWindow>().FirstOrDefault();
                    Log.Debug(LogTag.Editor, "{0} 重载完成，从第 {1} 步继续；不重新构建 AAB。", c_LogPrefix, checkpoint.NextStepIndex + 1);
                    await Runner.RunBatchCoreAsync(batch, new WindowReporter(host), null, CancellationToken.None, checkpoint);
                }
                catch (Exception ex)
                {
                    ClearWindowUploadCheckpoint();
                    EditorUtility.ClearProgressBar();
                    Log.Error(LogTag.Editor, "{0} 窗口上传交接失败：{1}", c_LogPrefix, ex);
                }
            }

            /// <summary>恢复前校验原存档和工作区；拒绝变更后的参数、错平台及缺失产物。</summary>
            internal static Batch ResolveWindowUploadBatch(WindowUploadCheckpoint checkpoint)
            {
                if (checkpoint == null ||
                    !Config.WorkspaceActive.TryGetPersistedConfigMaster(out _, out string masterGuid, out _, out _) ||
                    !Config.WorkspaceActive.TryGetPersistedPipifySettings(out PipifySettingsSO settings, out string settingsGuid, out _, out _) ||
                    !string.Equals(checkpoint.MasterGuid, masterGuid, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(checkpoint.SettingsGuid, settingsGuid, StringComparison.OrdinalIgnoreCase) ||
                    checkpoint.BatchIndex < 0 || checkpoint.BatchIndex >= settings.Batches.Count)
                    throw new InvalidOperationException("[Pipify] 重载后工作区或 Batch 已变化；上传尚未启动，已保留 AAB。");
                Batch batch = settings.Batches[checkpoint.BatchIndex];
                if (GetBatchFingerprint(batch) != checkpoint.BatchFingerprint ||
                    checkpoint.NextStepIndex < 0 || checkpoint.NextStepIndex >= batch.Items.Count ||
                    batch.Items[checkpoint.NextStepIndex].StepId != "googleplay.upload_aab" ||
                    checkpoint.Target != EditorUserBuildSettings.activeBuildTarget || !File.Exists(checkpoint.OutputPath) ||
                    GetArtifactFingerprint(checkpoint.OutputPath) != checkpoint.OutputFingerprint ||
                    !File.Exists(checkpoint.UploadPath) || GetArtifactFingerprint(checkpoint.UploadPath) != checkpoint.UploadFingerprint)
                    throw new InvalidOperationException("[Pipify] 重载后参数、平台或构建产物已变化；上传尚未启动，请重新核对配置。");
                return batch;
            }

            /// <summary>计算配置指纹用于漂移检测；检查点不保存可能含密码的参数 JSON。</summary>
            internal static string GetBatchFingerprint(Batch batch)
            {
                using var hash = SHA256.Create();
                return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(batch))));
            }

            /// <summary>流式计算产物指纹，恢复时拒绝路径相同但文件已被替换的 AAB。</summary>
            internal static string GetArtifactFingerprint(string path)
            {
                using var hash = SHA256.Create();
                using var stream = File.OpenRead(path);
                return Convert.ToBase64String(hash.ComputeHash(stream));
            }

            /// <summary>正常结束、取消、失败或退出时移除上传前检查点，避免以后误触发。</summary>
            internal static void ClearWindowUploadCheckpoint() => SessionState.EraseString(c_WindowUploadCheckpointKey);
        }
    }
}
