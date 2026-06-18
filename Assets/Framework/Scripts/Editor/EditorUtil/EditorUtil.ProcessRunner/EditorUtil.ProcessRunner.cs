/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ProcessRunner.cs
 * author:    taoye
 * created:   2026/4/24
 * descrip:   统一外部进程调用器
 ***************************************************************/

using System;
using System.Diagnostics;
using System.Text;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 统一外部进程调用器。
        /// 封装 stdout/stderr 全异步读取、超时 Kill、退出码判断，避免死锁和编辑器卡死。
        /// </summary>
        public static class ProcessRunner
        {
            /// <summary>
            /// 默认超时时间（毫秒）。
            /// </summary>
            private const int c_DefaultTimeoutMs = 120000;

            /// <summary>
            /// 执行外部进程并等待完成。
            /// stdout 和 stderr 均使用异步事件驱动读取，避免缓冲区满导致的死锁。
            /// 超时后自动 Kill 进程。
            /// </summary>
            /// <param name="fileName">可执行文件路径。</param>
            /// <param name="arguments">命令行参数。</param>
            /// <param name="timeoutMs">超时时间（毫秒），默认 120 秒。</param>
            /// <returns>进程执行结果。</returns>
            public static ProcessResult RunSync(string fileName, string arguments, int timeoutMs = c_DefaultTimeoutMs)
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    };

                    using Process process = Process.Start(startInfo);
                    if (process == null)
                    {
                        return new ProcessResult(-1, string.Empty, "进程启动返回 null。", true);
                    }

                    StringBuilder stdoutBuilder = new StringBuilder();
                    StringBuilder stderrBuilder = new StringBuilder();
                    object gate = new object();

                    process.OutputDataReceived += (s, e) => { if (e.Data != null) { lock (gate) { stdoutBuilder.AppendLine(e.Data); } } };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) { lock (gate) { stderrBuilder.AppendLine(e.Data); } } };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    bool exited = process.WaitForExit(timeoutMs);
                    if (!exited)
                    {
                        try
                        {
                            process.Kill();
                            process.WaitForExit();
                        }
                        catch (Exception killEx)
                        {
                            Log.Warning(LogTag.Editor, "进程超时后 Kill 失败：{0}", killEx.Message);
                        }

                        string timeoutStdout;
                        string timeoutStderr;
                        lock (gate)
                        {
                            timeoutStdout = stdoutBuilder.ToString();
                            timeoutStderr = stderrBuilder.ToString();
                        }

                        return new ProcessResult(-1, timeoutStdout, timeoutStderr, true);
                    }

                    // 确保异步输出全部刷新完毕
                    process.WaitForExit();

                    string stdout;
                    string stderr;
                    lock (gate)
                    {
                        stdout = stdoutBuilder.ToString();
                        stderr = stderrBuilder.ToString();
                    }

                    return new ProcessResult(process.ExitCode, stdout, stderr, false);
                }
                catch (Exception e)
                {
                    return new ProcessResult(-1, string.Empty, e.Message, false);
                }
            }

            /// <summary>
            /// 启动外部进程（非阻塞），输出实时流式重定向到指定回调。
            /// 进程退出后自动 Dispose。
            /// </summary>
            /// <param name="fileName">可执行文件路径。</param>
            /// <param name="arguments">命令行参数。</param>
            /// <param name="onStdout">标准输出回调（每行一次，可选）。</param>
            /// <param name="onStderr">标准错误回调（每行一次，可选）。</param>
            /// <param name="workingDirectory">工作目录（可选）。</param>
            /// <returns>启动成功返回 true。</returns>
            public static bool RunAsync(string fileName, string arguments, Action<string> onStdout = null, Action<string> onStderr = null, string workingDirectory = null)
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };

                    if (!string.IsNullOrEmpty(workingDirectory))
                    {
                        startInfo.WorkingDirectory = workingDirectory;
                    }

                    Process process = Process.Start(startInfo);
                    if (process == null)
                    {
                        return false;
                    }

                    process.EnableRaisingEvents = true;
                    process.OutputDataReceived += (_, e) => { if (e.Data != null) { onStdout?.Invoke(e.Data); } };
                    process.ErrorDataReceived += (_, e) => { if (e.Data != null) { onStderr?.Invoke(e.Data); } };
                    process.Exited += (sender, _) =>
                    {
                        // WaitForExit() 确保异步 OutputDataReceived/ErrorDataReceived 回调全部排干后再 Dispose，
                        // 避免丢失最后几行输出或触发 ObjectDisposedException。
                        Process p = (Process)sender;
                        p.WaitForExit();
                        p.Dispose();
                    };
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    return true;
                }
                catch (Exception e)
                {
                    Log.Warning(LogTag.Editor, "ProcessRunner.RunAsync 启动失败: {0}", e.Message);
                    return false;
                }
            }

            /// <summary>
            /// 拼接进程输出诊断文本：合并 stdout 与 stderr，去除多余空白，便于失败时一次性输出可定位的现场。
            /// <para>许多 CLI（如 Luban）将错误信息打到 stdout 而非 stderr，仅看 stderr 会丢掉根因。</para>
            /// </summary>
            /// <param name="result">进程执行结果。</param>
            /// <returns>合并后的诊断文本，全为空时返回 "(no output)"。</returns>
            public static string FormatOutput(ProcessResult result)
            {
                string stdout = (result.Stdout ?? string.Empty).TrimEnd('\r', '\n');
                string stderr = (result.Stderr ?? string.Empty).TrimEnd('\r', '\n');

                bool hasStdout = !string.IsNullOrEmpty(stdout);
                bool hasStderr = !string.IsNullOrEmpty(stderr);
                if (!hasStdout && !hasStderr)
                {
                    return "(no output)";
                }

                StringBuilder sb = new StringBuilder();
                if (hasStdout)
                {
                    sb.Append("[stdout]").Append('\n').Append(stdout);
                }
                if (hasStderr)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append('\n');
                    }
                    sb.Append("[stderr]").Append('\n').Append(stderr);
                }

                return sb.ToString();
            }

            /// <summary>
            /// 外部进程执行结果。
            /// </summary>
            public readonly struct ProcessResult
            {
                /// <summary>
                /// 进程退出码。
                /// </summary>
                public int ExitCode { get; }

                /// <summary>
                /// 标准输出内容。
                /// </summary>
                public string Stdout { get; }

                /// <summary>
                /// 标准错误内容。
                /// </summary>
                public string Stderr { get; }

                /// <summary>
                /// 是否因超时而终止。
                /// </summary>
                public bool TimedOut { get; }

                /// <summary>
                /// 是否执行成功（退出码为 0 且未超时）。
                /// </summary>
                public bool Success => ExitCode == 0 && !TimedOut;

                /// <summary>
                /// 构造进程执行结果。
                /// </summary>
                /// <param name="exitCode">退出码。</param>
                /// <param name="stdout">标准输出。</param>
                /// <param name="stderr">标准错误。</param>
                /// <param name="timedOut">是否超时。</param>
                public ProcessResult(int exitCode, string stdout, string stderr, bool timedOut)
                {
                    ExitCode = exitCode;
                    Stdout = stdout ?? string.Empty;
                    Stderr = stderr ?? string.Empty;
                    TimedOut = timedOut;
                }
            }
        }
    }
}
