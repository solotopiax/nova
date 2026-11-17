/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AndroidResolver.JavaEnvironment.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   Android 依赖解析使用的 Unity JDK 选择与环境检查
 ***************************************************************/

using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AndroidResolver
        {
            /// <summary>
            /// 按已审 EDM4U 1.2.188/1.2.190 JavaUtilities 的优先级取得 Unity 当前 JDK。
            /// 使用其已核公开兼容属性，不调用厂商私有 Java 工具或改写 External Tools 设置。
            /// </summary>
            internal static string ResolveJavaHome(Type resolver)
            {
                Type compat = resolver.Assembly.GetType("GooglePlayServices.UnityCompat", true);
                string home = compat.GetProperty("AndroidExternalToolsSettingsJdkRootPath",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                string playback = resolver.GetProperty("AndroidPlaybackEngineDirectory",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
                if (string.IsNullOrEmpty(home) || EditorPrefs.GetBool("JdkUseEmbedded"))
                {
                    string platform = Application.platform.ToString().Replace("Editor", "").Replace("OSX", "MacOS");
                    string first = string.IsNullOrEmpty(playback) ? null : Path.Combine(playback, "Tools", "OpenJDK", platform);
                    string second = string.IsNullOrEmpty(playback) ? null : Path.Combine(playback, "OpenJDK");
                    if (first != null && Directory.Exists(first)) home = first;
                    else if (second != null && Directory.Exists(second)) home = second;
                }
                if (string.IsNullOrEmpty(home)) home = EditorPrefs.GetString("JdkPath");
                if (string.IsNullOrEmpty(home)) home = System.Environment.GetEnvironmentVariable("JAVA_HOME");
                if (string.IsNullOrEmpty(home))
                    throw new InvalidOperationException("[AndroidResolver] Unity 未提供明确的 JDK；不能让 Gradle 猜测系统 Java。");
                return Path.GetFullPath(home);
            }
        }
    }

    /// <summary>
    /// EDM4U 1.2.188/1.2.190 下载支路未传 CommandLineDialog.envVars，且未暴露子进程环境钩子。
    /// 此适配只在已审同步 ResolveSync 完整生命周期内改变当前 Unity 进程的 JAVA_HOME，
    /// 让实际 gradlew 子进程继承 Unity 选择的 JDK；不修改用户/系统环境、PATH 或其余变量。
    /// ResolveSync 的完成回调在 CommandLine.Run 已等待子进程结束之后返回；不适用于异步 Resolve。
    /// </summary>
    [InitializeOnLoad]
    internal sealed class AndroidResolverJavaEnvironment : IDisposable
    {
        private const string Prefix = "Nova.AndroidResolver.JavaEnvironment.";
        private static readonly object Gate = new object();
        private static AndroidResolverJavaEnvironment s_Active;
        private bool m_Disposed;
        private readonly bool m_HadOriginal;
        private readonly string m_Original;

        internal static bool IsActive
        {
            get { lock (Gate) return s_Active != null || SessionState.GetBool(Prefix + "pending", false); }
        }

        static AndroidResolverJavaEnvironment()
        {
            RestorePending();
            AssemblyReloadEvents.beforeAssemblyReload += RestoreActive;
            EditorApplication.quitting += RestoreActive;
        }

        /// <summary>独占创建同步作用域；环境原文只留本地恢复状态，不写 Plan、日志或回执。</summary>
        internal static AndroidResolverJavaEnvironment Begin(string javaHome)
        {
            lock (Gate)
            {
                if (s_Active != null || SessionState.GetBool(Prefix + "pending", false))
                    throw new InvalidOperationException("[AndroidResolver] 另一项 Java 环境作用域尚未收尾，禁止重入或重放解析。");
                string java = Path.Combine(javaHome, "bin", Path.DirectorySeparatorChar == '\\' ? "java.exe" : "java");
                if (!Path.IsPathRooted(javaHome) || !File.Exists(java) || !File.Exists(Path.Combine(javaHome, "release")))
                    throw new InvalidOperationException("[AndroidResolver] 当前 JDK 缺少 bin/java 或 release 文件。");
                foreach (string option in new[] { "JAVA_TOOL_OPTIONS", "JDK_JAVA_OPTIONS", "_JAVA_OPTIONS" })
                    if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(option)))
                        throw new InvalidOperationException("[AndroidResolver] Java 含额外启动参数，须先核对：" + option);
                var scope = new AndroidResolverJavaEnvironment();
                SessionState.SetBool(Prefix + "hadOriginal", scope.m_HadOriginal);
                SessionState.SetString(Prefix + "original", scope.m_Original ?? string.Empty);
                SessionState.SetBool(Prefix + "pending", true);
                s_Active = scope;
                try
                {
                    System.Environment.SetEnvironmentVariable("JAVA_HOME", javaHome, EnvironmentVariableTarget.Process);
                    if (System.Environment.GetEnvironmentVariable("JAVA_HOME") != javaHome)
                        throw new InvalidOperationException("[AndroidResolver] 当前进程未接受 JDK 环境，解析尚未执行。");
                    return scope;
                }
                catch { scope.Dispose(); throw; }
            }
        }

        private AndroidResolverJavaEnvironment()
        {
            IDictionary variables = System.Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process);
            m_HadOriginal = variables.Contains("JAVA_HOME");
            m_Original = m_HadOriginal ? (variables["JAVA_HOME"] as string ?? string.Empty) : null;
        }

        /// <summary>finally、域重载与退出只恢复本作用域拥有的原始进程环境；不发起解析。</summary>
        public void Dispose()
        {
            lock (Gate)
            {
                if (m_Disposed) return;
                RestoreOriginal(m_HadOriginal, m_Original);
                ClearRecovery();
                m_Disposed = true;
                if (ReferenceEquals(s_Active, this)) s_Active = null;
            }
        }

        private static void RestoreActive()
        {
            lock (Gate)
            {
                if (s_Active != null) s_Active.Dispose();
                else RestorePending();
            }
        }

        private static void RestorePending()
        {
            if (!SessionState.GetBool(Prefix + "pending", false)) return;
            RestoreOriginal(SessionState.GetBool(Prefix + "hadOriginal", false),
                SessionState.GetString(Prefix + "original", string.Empty));
            ClearRecovery();
        }

        private static void ClearRecovery()
        {
            SessionState.EraseBool(Prefix + "pending");
            SessionState.EraseBool(Prefix + "hadOriginal");
            SessionState.EraseString(Prefix + "original");
        }

        /// <summary>部分 Mono/.NET 实现把空字符串当删除；原变量为空时用宿主原生 API 保留空与未设的区别。</summary>
        internal static void RestoreOriginal(bool hadOriginal, string original)
        {
            // 先走运行时 API，让具有托管环境缓存的宿主也同步更新；只在其把空值删除时补原生恢复。
            System.Environment.SetEnvironmentVariable("JAVA_HOME", hadOriginal ? original : null, EnvironmentVariableTarget.Process);
            if (hadOriginal && original == string.Empty)
            {
                if (System.Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process).Contains("JAVA_HOME")) return;
                bool ok = Path.DirectorySeparatorChar == '\\'
                    ? SetEnvironmentVariableWindows("JAVA_HOME", string.Empty)
                    : SetEnvironmentVariableUnix("JAVA_HOME", string.Empty, 1) == 0;
                if (!ok) throw new InvalidOperationException("[AndroidResolver] 原进程空 JAVA_HOME 恢复失败；恢复状态保留，禁止重放。");
            }
        }

        [DllImport("libc", EntryPoint = "setenv", SetLastError = true)]
        private static extern int SetEnvironmentVariableUnix(string name, string value, int overwrite);

        [DllImport("kernel32.dll", EntryPoint = "SetEnvironmentVariableW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetEnvironmentVariableWindows(string name, string value);
    }
}
