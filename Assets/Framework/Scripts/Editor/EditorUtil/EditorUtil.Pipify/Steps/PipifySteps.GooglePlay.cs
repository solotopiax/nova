/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.GooglePlay.cs
 * author:    Codex
 * created:   2026/10/10
 * descrip:   Pipify Google Play AAB 上传与发布步骤
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>Google Play 上传入口；使用隔离的 fastlane 工作目录，不复用项目内 Fastfile 或元数据。</summary>
    internal static partial class PipifySteps
    {
        /// <summary>上传同批次或显式指定的 AAB；验证、取消、超时及非零退出码均中断流水线。</summary>
        /// <param name="ctx">当前 Batch 上下文及取消令牌。</param>
        /// <param name="p">上传参数，默认内部测试草稿。</param>
        /// <returns>fastlane 成功结束后的 UniTask。</returns>
        [PipifyStep("googleplay.upload_aab", "Google Play 上传 AAB", "发布", ParamsType = typeof(GooglePlayUploadParams))]
        internal static async UniTask RunGooglePlayUpload(PipifyContext ctx, GooglePlayUploadParams p)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            if (p == null) throw new ArgumentNullException(nameof(p));
            ctx.CancellationToken.ThrowIfCancellationRequested();
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            var input = ResolveGooglePlayInput(ctx, p, projectRoot);
            string executable = ResolveFastlaneExecutable(p.FastlanePath, projectRoot);
            string workingDirectory = Path.Combine(Path.GetTempPath(), "nova-googleplay-" + Guid.NewGuid().ToString("N"));
            try
            {
                string metadata = Path.Combine(workingDirectory, "metadata");
                Directory.CreateDirectory(metadata);
                if (!string.IsNullOrWhiteSpace(p.ReleaseNotes))
                {
                    string changelogs = Path.Combine(metadata, p.ReleaseNotesLanguage, "changelogs");
                    Directory.CreateDirectory(changelogs);
                    File.WriteAllText(Path.Combine(changelogs, "default.txt"), p.ReleaseNotes, new UTF8Encoding(false));
                }
                ProcessStartInfo startInfo = CreateGooglePlayStartInfo(executable, workingDirectory, metadata, input, p);
                Log.Debug(LogTag.Editor, "[Pipify] Google Play：package={0}, track={1}, status={2}, validateOnly={3}",
                    input.PackageName, p.Track, p.ReleaseStatus, p.ValidateOnly);
                await RunFastlaneUploadAsync(ctx, startInfo, p.TimeoutSeconds);
                Log.Debug(LogTag.Editor, p.ValidateOnly
                    ? "[Pipify] Google Play AAB 上传验证通过，未提交 edit。"
                    : "[Pipify] Google Play edit 提交成功，实际分发取决于轨道、发布状态及 Google 审核。 ");
            }
            finally
            {
                // 只创建隔离元数据，不拷贝凭据；成功、失败与取消都清理，正式 AAB 不删除。
                if (Directory.Exists(workingDirectory)) Directory.Delete(workingDirectory, true);
            }
        }

        /// <summary>解析产物身份、检查 AAB 结构和发布参数，将必填的项目根相对凭据路径转为绝对路径；不读取凭据正文。</summary>
        /// <param name="ctx">当前 Batch 最新成功构建。</param>
        /// <param name="p">上传参数。</param>
        /// <param name="projectRoot">项目绝对根路径。</param>
        /// <returns>校验后的上传输入。</returns>
        internal static GooglePlayUploadInput ResolveGooglePlayInput(PipifyContext ctx, GooglePlayUploadParams p, string projectRoot)
        {
            string pairError = GetGooglePlayInputPairError(p);
            if (!string.IsNullOrEmpty(pairError)) throw new InvalidOperationException("[Pipify] " + pairError);
            PipifyBuildArtifact artifact = ctx.BuildState.LatestArtifact;
            bool fromBuild = string.IsNullOrWhiteSpace(p.AabPath);
            if (fromBuild && (artifact == null || artifact.Target != BuildTarget.Android || artifact.DevelopmentBuild))
                throw new InvalidOperationException("[Pipify] 需要同一 Batch 成功构建的非 Development Android AAB，或显式填写已有 AAB 路径和包名。");
            string aab = ResolveGooglePlayPath(fromBuild ? artifact.OutputPath : p.AabPath, projectRoot);
            if (!string.Equals(Path.GetExtension(aab), ".aab", StringComparison.OrdinalIgnoreCase) || !File.Exists(aab))
                throw new InvalidOperationException("[Pipify] 上传目标必须是存在的 .aab 文件，不能是 APK 或导出的 Android 工程。");
            using (ZipArchive zip = ZipFile.OpenRead(aab))
            {
                if (zip.GetEntry("BundleConfig.pb") == null || zip.GetEntry("base/manifest/AndroidManifest.xml") == null)
                    throw new InvalidOperationException("[Pipify] 文件缺少 Android App Bundle 必需结构，已停止上传。");
            }
            string packageName = string.IsNullOrWhiteSpace(p.PackageName) && fromBuild ? artifact.PackageName : p.PackageName;
            if (string.IsNullOrWhiteSpace(packageName) || !Regex.IsMatch(packageName, @"^[A-Za-z][A-Za-z0-9_]*(\.[A-Za-z][A-Za-z0-9_]*)+$"))
                throw new InvalidOperationException("[Pipify] 请填写有效应用包名；显式 AAB 路径不会从当前 PlayerSettings 推断包名。");
            if (fromBuild && !string.Equals(packageName, artifact.PackageName, StringComparison.Ordinal))
                throw new InvalidOperationException("[Pipify] 上传包名与本次构建时包名不一致。");
            if (string.IsNullOrWhiteSpace(p.Track) || !Regex.IsMatch(p.Track, @"^[A-Za-z0-9_.:-]+$"))
                throw new InvalidOperationException("[Pipify] 目标轨道不能为空，且必须使用 Play Console 轨道标识。");
            if (!Enum.IsDefined(typeof(GooglePlayReleaseStatus), p.ReleaseStatus))
                throw new InvalidOperationException("[Pipify] Google Play 发布状态无效。");
            if (p.ReleaseStatus == GooglePlayReleaseStatus.InProgress &&
                (p.Track != "production" || !(p.Rollout > 0 && p.Rollout < 1)))
                throw new InvalidOperationException("[Pipify] 分阶段发布仅支持 production，比例必须大于 0 且小于 1。");
            if (p.TimeoutSeconds <= 0 || p.TimeoutSeconds > 86400)
                throw new InvalidOperationException("[Pipify] 上传超时必须在 1～86400 秒之间。");
            if (!string.IsNullOrWhiteSpace(p.ReleaseNotes) &&
                (string.IsNullOrWhiteSpace(p.ReleaseNotesLanguage) || !Regex.IsMatch(p.ReleaseNotesLanguage, @"^[A-Za-z]{2,3}(-[A-Za-z0-9]{2,8})*$") || p.ReleaseNotes.Length > 500))
                throw new InvalidOperationException("[Pipify] 更新说明语言需为 BCP-47 标识（如 zh-CN），正文不得超过 500 字符。");
            // 只接受配置中的项目根相对路径，既有环境变量不作为凭据来源。
            if (string.IsNullOrWhiteSpace(p.CredentialsPath))
                throw new InvalidOperationException("[Pipify] 请填写凭据 JSON 路径（相对 Unity 项目根目录），例如 ../GooglePlayKeys/credentials.json。");
            if (Path.IsPathRooted(p.CredentialsPath))
                throw new InvalidOperationException("[Pipify] 凭据 JSON 路径必须相对于 Unity 项目根目录，请将绝对路径改为相对路径。");
            string credentialPath = ResolveGooglePlayPath(p.CredentialsPath, projectRoot);
            if (!File.Exists(credentialPath))
                throw new InvalidOperationException("[Pipify] 凭据 JSON 文件不存在，请检查凭据路径；相对路径以 Unity 项目根目录为基准。");
            if (!string.Equals(Path.GetExtension(credentialPath), ".json", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("[Pipify] Google Play 凭据文件必须是 .json 文件。");
            return new GooglePlayUploadInput { AabPath = aab, PackageName = packageName, CredentialsPath = credentialPath };
        }

        /// <summary>
        /// 校验 AAB 路径与应用包名必须同时填写或同时留空，供界面和执行入口共用；空白视为未填写。
        /// </summary>
        /// <param name="p">Google Play 上传参数。</param>
        /// <returns>缺失字段的补填提示；参数为空或成对配置时返回 null。</returns>
        internal static string GetGooglePlayInputPairError(GooglePlayUploadParams p)
        {
            if (p == null) return null;
            bool hasAab = !string.IsNullOrWhiteSpace(p.AabPath);
            bool hasPackage = !string.IsNullOrWhiteSpace(p.PackageName);
            if (hasAab == hasPackage) return null;
            return hasAab
                ? "已填写 AAB 路径，请同时填写应用包名；如需使用本次构建，请将两项都留空。"
                : "已填写应用包名，请同时填写 AAB 路径；如需使用本次构建，请将两项都留空。";
        }

        /// <summary>构造完整显式的 supply 参数和隔离环境，防止 fastlane 默认 production/completed 或已有配置影响本次上传。</summary>
        /// <param name="executable">可直接启动的 fastlane 文件。</param>
        /// <param name="workingDirectory">本次专属工作目录。</param>
        /// <param name="metadata">本次专属元数据目录。</param>
        /// <param name="input">已经核验的产物和凭据路径。</param>
        /// <param name="p">上传参数。</param>
        /// <returns>无需 shell 的进程启动配置。</returns>
        internal static ProcessStartInfo CreateGooglePlayStartInfo(string executable, string workingDirectory, string metadata,
            GooglePlayUploadInput input, GooglePlayUploadParams p)
        {
            var args = new List<string>
            {
                "supply", "--package_name", input.PackageName, "--aab", input.AabPath,
                "--track", p.Track, "--release_status", p.ReleaseStatus == GooglePlayReleaseStatus.Draft ? "draft" :
                    p.ReleaseStatus == GooglePlayReleaseStatus.Completed ? "completed" : "inProgress",
                "--metadata_path", metadata, "--skip_upload_apk", "true", "--skip_upload_aab", "false",
                "--skip_upload_metadata", "true", "--skip_upload_images", "true", "--skip_upload_screenshots", "true",
                "--skip_upload_changelogs", string.IsNullOrWhiteSpace(p.ReleaseNotes) ? "true" : "false",
                "--validate_only", p.ValidateOnly ? "true" : "false",
                "--timeout", p.TimeoutSeconds.ToString(CultureInfo.InvariantCulture)
            };
            // 草稿按工具使用限制不请求延后审核；只在开放下载用途下采用保存的手动审核选择。
            // 验证模式不提交 edit；有效勾选手动审核时禁止 supply 擅自改成自动审核。
            // fastlane 2.240.1 的 false 仍会传查询参数；取消勾选时允许它按 Google 错误建议兼容同一 edit 的提交。
            if (!p.ValidateOnly)
            {
                bool manualReview = IsGooglePlayManualReviewAvailable(p) && p.ChangesNotSentForReview;
                args.AddRange(new[] { "--changes_not_sent_for_review", manualReview ? "true" : "false",
                    "--rescue_changes_not_sent_for_review", manualReview ? "false" : "true" });
            }
            if (p.ReleaseStatus == GooglePlayReleaseStatus.InProgress)
            {
                args.Add("--rollout");
                args.Add(p.Rollout.ToString(CultureInfo.InvariantCulture));
            }
            var start = new ProcessStartInfo
            {
                FileName = executable, Arguments = string.Join(" ", args.Select(QuoteFastlaneArgument)),
                WorkingDirectory = workingDirectory, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            // 清除父进程所有 supply 隐式选项和原始凭据，认证只传已经核验的文件路径。
            foreach (string key in start.EnvironmentVariables.Keys.Cast<string>().ToArray())
                if (key.StartsWith("SUPPLY_", StringComparison.OrdinalIgnoreCase)) start.EnvironmentVariables.Remove(key);
            start.EnvironmentVariables["SUPPLY_JSON_KEY"] = input.CredentialsPath;
            start.EnvironmentVariables["SUPPLY_UPLOAD_MAX_RETRIES"] = "0";
            start.EnvironmentVariables["CI"] = "true";
            start.EnvironmentVariables["FASTLANE_SKIP_UPDATE_CHECK"] = "true";
            start.EnvironmentVariables["FASTLANE_HIDE_CHANGELOG"] = "true";
            start.EnvironmentVariables["FASTLANE_DISABLE_COLORS"] = "true";
            // GUI 启动的 Unity 可能缺失 UTF-8 locale；子进程独立配置以保留中文路径和说明。
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                string locale = Application.platform == RuntimePlatform.OSXEditor ? "en_US.UTF-8" : "C.UTF-8";
                start.EnvironmentVariables["LANG"] = locale;
                start.EnvironmentVariables["LC_ALL"] = locale;
            }
            start.EnvironmentVariables.Remove("DEBUG");
            start.EnvironmentVariables.Remove("FASTLANE_VERBOSE");
            string binaryDirectory = Path.GetDirectoryName(executable);
            start.EnvironmentVariables["PATH"] = binaryDirectory + Path.PathSeparator + start.EnvironmentVariables["PATH"];
            return start;
        }

        /// <summary>判断工具是否允许请求后台手动审核；仅验证或保存草稿时不可用，UI 与 CLI 共用此限制。</summary>
        /// <param name="p">当前上传参数；为空时不可用。</param>
        /// <returns>非验证且选择开放下载用途时返回 true；不修改原保存值。</returns>
        internal static bool IsGooglePlayManualReviewAvailable(GooglePlayUploadParams p)
        {
            return p != null && !p.ValidateOnly && p.ReleaseStatus != GooglePlayReleaseStatus.Draft;
        }

        /// <summary>按直接进程参数规则转义引号与反斜杠；禁止换行和 NUL，完全不调用 shell。</summary>
        /// <param name="value">单个参数原文。</param>
        /// <returns>包含外层引号的单个参数。</returns>
        internal static string QuoteFastlaneArgument(string value)
        {
            if (value == null || value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
                throw new ArgumentException("[Pipify] 上传进程参数包含非法控制字符。");
            // Mono 在 Unix 按 g_shell_parse_argv 解析，双引号内每个反斜杠都必须转义。
            if (Application.platform != RuntimePlatform.WindowsEditor)
                return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var quoted = new StringBuilder("\"");
            int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                quoted.Append('\\', c == '"' ? slashes * 2 + 1 : slashes);
                quoted.Append(c);
                slashes = 0;
            }
            quoted.Append('\\', slashes * 2).Append('"');
            return quoted.ToString();
        }

        /// <summary>解析项目相对路径，不展开 shell 变量、波浪号或命令替换。</summary>
        /// <param name="path">配置或构建报告中的路径。</param>
        /// <param name="root">项目绝对根路径。</param>
        /// <returns>规范化绝对路径。</returns>
        private static string ResolveGooglePlayPath(string path, string root)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("[Pipify] AAB 路径为空。");
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
        }

        /// <summary>显式路径优先，其次 PATH 和常见 macOS/Linux 安装路径；不安装工具，不启动批处理 shell。</summary>
        /// <param name="configured">用户配置的路径或命令名。</param>
        /// <param name="root">项目根目录。</param>
        /// <returns>存在的 fastlane 可执行文件绝对路径。</returns>
        private static string ResolveFastlaneExecutable(string configured, string root)
        {
            string name = string.IsNullOrWhiteSpace(configured) ? "fastlane" : configured;
            var candidates = new List<string>();
            if (Path.IsPathRooted(name) || name.IndexOfAny(new[] { '/', '\\' }) >= 0) candidates.Add(ResolveGooglePlayPath(name, root));
            else
            {
                foreach (string directory in (System.Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
                    if (!string.IsNullOrWhiteSpace(directory))
                        candidates.Add(Path.Combine(directory, Application.platform == RuntimePlatform.WindowsEditor && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? name + ".exe" : name));
                if (name == "fastlane") candidates.AddRange(new[] { "/opt/homebrew/bin/fastlane", "/usr/local/bin/fastlane", "/usr/bin/fastlane" });
            }
            string resolved = candidates.FirstOrDefault(File.Exists);
            if (resolved == null) throw new InvalidOperationException("[Pipify] 未找到 fastlane。请先安装 fastlane，或填写可执行文件绝对路径。");
            if (Application.platform == RuntimePlatform.WindowsEditor && !resolved.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("[Pipify] Windows 需使用可直接启动的 fastlane .exe；不通过 cmd 执行 .bat/.cmd。可在 macOS/Linux 构建机上传。");
            return Path.GetFullPath(resolved);
        }

        /// <summary>等待进程并持续响应 Editor/CLI 取消；取消、重载或超时终止进程，失败不自动重试提交。</summary>
        /// <param name="ctx">取消与进度宿主。</param>
        /// <param name="start">隔离的启动配置。</param>
        /// <param name="timeoutSeconds">整个上传的最大等待时长。</param>
        /// <returns>退出码为零时完成。</returns>
        private static async UniTask RunFastlaneUploadAsync(PipifyContext ctx, ProcessStartInfo start, int timeoutSeconds)
        {
            using var process = new Process { StartInfo = start };
            var output = new StringBuilder();
            object gate = new object();
            int streamsClosed = 0;
            bool inPrivateKey = false;
            DataReceivedEventHandler receive = (_, e) =>
            {
                if (e.Data == null) { Interlocked.Increment(ref streamsClosed); return; }
                lock (gate)
                {
                    if (e.Data.Contains("-----BEGIN")) inPrivateKey = true;
                    output.AppendLine(inPrivateKey ? "[私钥已隐藏]" : SanitizeFastlaneDiagnostic(e.Data));
                    if (e.Data.Contains("-----END")) inPrivateKey = false;
                    if (output.Length > 16000) output.Remove(0, output.Length - 16000);
                }
            };
            process.OutputDataReceived += receive;
            process.ErrorDataReceived += receive;
            bool started = false;
            /// <summary>域重载或退出时同步终止本轮进程并清场，明确远端结果需要另行核验。</summary>
            void AbortForReload()
            {
                try
                {
                    if (started) StopFastlaneProcess(process);
                    Log.Error(LogTag.Editor, "[Pipify] Google Play 上传被程序集重载或 Editor 退出中断；远端提交状态可能未知，请核对 Play Console 后再重试。");
                }
                finally
                {
                    try
                    {
                        // 域重载不保证异步 finally 执行；终止上传之后立即清理本轮元数据。
                        if (Directory.Exists(start.WorkingDirectory)) Directory.Delete(start.WorkingDirectory, true);
                    }
                    finally { process.Dispose(); }
                }
            }
            AssemblyReloadEvents.beforeAssemblyReload += AbortForReload;
            EditorApplication.quitting += AbortForReload;
            try
            {
                try { started = process.Start(); }
                catch (Exception) { throw new InvalidOperationException("[Pipify] fastlane 启动失败，请检查可执行权限及 Ruby 环境。"); }
                if (!started) throw new InvalidOperationException("[Pipify] fastlane 未能启动。");
                process.StandardInput.Close();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                var timer = Stopwatch.StartNew();
                double nextCliProgress = 0;
                while (!process.HasExited || Volatile.Read(ref streamsClosed) < 2)
                {
                    ctx.CancellationToken.ThrowIfCancellationRequested();
                    bool report = !(ctx.Reporter is EditorUtil.Pipify.CliReporter) || timer.Elapsed.TotalSeconds >= nextCliProgress;
                    if (report && ctx.Reporter != null && ctx.Reporter.ReportStep(ctx.CurrentStepIndex,
                            $"Google Play 上传处理中（已等待 {timer.Elapsed.TotalSeconds:0} 秒）", 0.5f))
                        throw new OperationCanceledException("[Pipify] Google Play 上传已取消；远端提交状态可能未知，请核对后台后再重试。");
                    if (report) nextCliProgress = timer.Elapsed.TotalSeconds + 5;
                    if (timer.Elapsed.TotalSeconds >= timeoutSeconds)
                        throw new TimeoutException("[Pipify] Google Play 上传超时；远端提交状态可能未知，请核对后台后再重试。");
                    await UniTask.Delay(200, ignoreTimeScale: true, cancellationToken: ctx.CancellationToken);
                }
                if (timer.Elapsed.TotalSeconds >= timeoutSeconds)
                    throw new TimeoutException("[Pipify] Google Play 上传超过超时限制；请检查远端提交状态。");
                // 退出与两个输出流均完成后再判断结果，不在主线程无限 WaitForExit。
                ctx.CancellationToken.ThrowIfCancellationRequested();
                if (process.ExitCode != 0)
                {
                    string diagnostic;
                    lock (gate) diagnostic = SanitizeFastlaneDiagnostic(output.ToString());
                    if (diagnostic.Contains("The query parameter changesNotSentForReview must not be set"))
                        throw new InvalidOperationException(
                            "[Pipify] Google 拒绝本次手动提交审核参数，变更提交失败。请先核对 Play Console 的版本记录；" +
                            "开放下载用途下若勾选手动审核且允许按后台规则处理，请取消勾选“稍后在后台手动提交审核”后保存配置；草稿用途已禁用此项。" +
                            "“上传后用途”仍决定是否开放下载，保存草稿不会开放下载。不会自动重传 AAB。\n" + diagnostic);
                    throw new InvalidOperationException($"[Pipify] Google Play 上传失败（退出码 {process.ExitCode}）；远端提交状态可能未知，请核对后台后再重试。\n{diagnostic}");
                }
            }
            catch (OperationCanceledException)
            {
                throw new OperationCanceledException("[Pipify] Google Play 上传已取消；远端提交状态可能未知，请核对后台后再重试。", ctx.CancellationToken);
            }
            finally
            {
                AssemblyReloadEvents.beforeAssemblyReload -= AbortForReload;
                EditorApplication.quitting -= AbortForReload;
                if (started && !process.HasExited) StopFastlaneProcess(process);
            }
        }

        /// <summary>终止 fastlane 及现有子进程并等待退出，防止取消后上传继续运行；命令只接收数字 PID。</summary>
        /// <param name="process">本次启动的进程。</param>
        private static void StopFastlaneProcess(Process process)
        {
            if (process.HasExited) return;
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    var killed = EditorUtil.ProcessRunner.RunSync("taskkill.exe", "/PID " + process.Id + " /T /F", 5000);
                    if (!killed.Success && !process.HasExited)
                        throw new InvalidOperationException("[Pipify] 无法终止上传子进程，请检查构建机进程状态。");
                }
                else KillFastlaneChildren(process.Id);
            }
            finally
            {
                // 即使子树探测失败，也必须停止根进程，不能让失败后的上传继续运行。
                if (!process.HasExited) process.Kill();
                if (!process.WaitForExit(5000))
                    throw new InvalidOperationException("[Pipify] fastlane 进程未退出，请在构建机终止该进程并检查 Play Console。");
            }
        }

        /// <summary>按子进程树从叶子到根终止本轮进程；pgrep 直接启动，不经 shell。</summary>
        /// <param name="parentPid">本轮进程树中的父 PID。</param>
        private static void KillFastlaneChildren(int parentPid)
        {
            var children = EditorUtil.ProcessRunner.RunSync("/usr/bin/pgrep", "-P " + parentPid, 2000);
            if (children.TimedOut || (children.ExitCode != 0 && children.ExitCode != 1))
                throw new InvalidOperationException("[Pipify] 无法检查上传子进程，请检查构建机 pgrep 和进程状态。");
            foreach (string line in children.Stdout.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!int.TryParse(line, out int pid)) continue;
                KillFastlaneChildren(pid);
                try { using var child = Process.GetProcessById(pid); child.Kill(); child.WaitForExit(2000); }
                catch (ArgumentException) { /* 子进程已自然退出。 */ }
                catch (InvalidOperationException) { /* 子进程已自然退出。 */ }
            }
        }

        /// <summary>移除 ANSI、凭据正文、私钥及令牌，只返回有限长度的故障诊断。</summary>
        /// <param name="raw">进程输出末尾缓冲。</param>
        /// <returns>用于 Console 的脱敏诊断。</returns>
        internal static string SanitizeFastlaneDiagnostic(string raw)
        {
            string value = Regex.Replace(raw ?? "", @"\x1B\[[0-?]*[ -/]*[@-~]", "");
            value = Regex.Replace(value, @"-----BEGIN [\s\S]*?-----END [^\r\n]*", "[私钥已隐藏]");
            value = Regex.Replace(value, @"(?i)(private_key|access_token|refresh_token|client_secret|json_key_data)(\s*[\""':=]+\s*)([^\r\n]+)", "$1$2[已隐藏]");
            value = Regex.Replace(value, @"(?i)Bearer\s+\S+|eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+", "[令牌已隐藏]");
            return value.Length > 4000 ? value.Substring(value.Length - 4000) : value;
        }

        /// <summary>本次上传的校验结果，凭据仅保留路径。</summary>
        internal sealed class GooglePlayUploadInput
        {
            internal string AabPath;
            internal string PackageName;
            internal string CredentialsPath;
        }
    }
}
