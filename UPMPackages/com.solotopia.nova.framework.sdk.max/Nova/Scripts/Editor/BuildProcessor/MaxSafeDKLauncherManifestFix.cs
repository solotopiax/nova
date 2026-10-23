/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  MaxSafeDKLauncherManifestFix.cs
 * author:    yingzheng
 * created:   2026/10/22
 * descrip:   [MaxSafeDK-LauncherManifest-Fix] Unity 6000.4.4f1+ 且开启 AppLovin Quality Service 时，
 *            向生成的 launcher AndroidManifest.xml 的 application 节点补写 SafeDKApplication。
 *            MAX 修复该版本问题后可整文件删除。
 ***************************************************************/

#if UNITY_ANDROID && NOVA_APPLOVIN_MAX
using System;
using System.IO;
using System.Xml;
using NovaFramework.Runtime;
using UnityEditor.Android;
using IOPath = System.IO.Path;

namespace NovaFramework.SDK.MaxAdPlugin.Editor
{
    /// <summary>
    /// MAX Quality Service（SafeDK）在 Unity 6000.4.4f1+ 下 launcher 缺失 Application 入口的临时修复。
    /// 仅修改本次构建生成的 Gradle 工程 launcher/src/main/AndroidManifest.xml，不论是否勾选 Custom Launcher Manifest 均生效，
    /// 也不改动 Assets/Plugins/Android/LauncherManifest.xml。
    /// </summary>
    public sealed class MaxSafeDKLauncherManifestFix : IPostGenerateGradleAndroidProject
    {
        /// <summary>
        /// 需要写入 application 节点的 SafeDK Application 类名。
        /// </summary>
        private const string c_SafeDKApplicationName = "com.safedk.android.SafeDKApplication";

        /// <summary>
        /// Android 命名空间。
        /// </summary>
        private const string c_AndroidNamespace = "http://schemas.android.com/apk/res/android";

        /// <summary>
        /// 需要修复的最低 Unity 版本（含）。
        /// </summary>
        private static readonly Version s_MinUnityVersion = new Version(6000, 4, 4);

        /// <summary>
        /// 晚于 AppLovin 自身 Gradle 后处理执行，避免被其改写覆盖。
        /// </summary>
        public int callbackOrder => 1000;

        /// <summary>
        /// Gradle 工程生成后按条件补写 SafeDKApplication。
        /// </summary>
        /// <param name="path">Unity 传入的 unityLibrary 模块路径。</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string unityVersion = UnityEngine.Application.unityVersion;
            if (!IsUnityVersionAffected(unityVersion))
            {
                Log.Debug(LogTag.Editor, $"[MaxSafeDKLauncherManifestFix] 当前 Unity 版本 {unityVersion} 低于 6000.4.4f1，跳过 SafeDKApplication 补写。");
                return;
            }

            if (!AppLovinSettings.Instance.QualityServiceEnabled)
            {
                Log.Debug(LogTag.Editor, "[MaxSafeDKLauncherManifestFix] AppLovinSettings.QualityServiceEnabled 未开启，跳过 SafeDKApplication 补写。");
                return;
            }

            string manifestPath = IOPath.GetFullPath(IOPath.Combine(path, "..", "launcher", "src", "main", "AndroidManifest.xml"));
            if (!File.Exists(manifestPath))
            {
                Log.Warning(LogTag.Editor, $"[MaxSafeDKLauncherManifestFix] 未找到 launcher AndroidManifest.xml，跳过 SafeDKApplication 补写：{manifestPath}");
                return;
            }

            PatchLauncherManifest(manifestPath);
        }

        /// <summary>
        /// 判断 Unity 版本是否为 6000.4.4f1 及以上。
        /// </summary>
        /// <param name="unityVersion">Application.unityVersion，如 6000.4.4f1。</param>
        /// <returns>版本号不低于 6000.4.4 时返回 true。</returns>
        private static bool IsUnityVersionAffected(string unityVersion)
        {
            if (string.IsNullOrEmpty(unityVersion))
            {
                return false;
            }

            string[] parts = unityVersion.Split('.');
            if (parts.Length < 3 || !int.TryParse(parts[0], out int major) || !int.TryParse(parts[1], out int minor))
            {
                return false;
            }

            int patchLength = 0;
            while (patchLength < parts[2].Length && char.IsDigit(parts[2][patchLength]))
            {
                patchLength++;
            }

            if (patchLength == 0 || !int.TryParse(parts[2].Substring(0, patchLength), out int patch))
            {
                return false;
            }

            return new Version(major, minor, patch) >= s_MinUnityVersion;
        }

        /// <summary>
        /// 向 application 节点写入 android:name；已存在其他自定义 Application 时只告警不覆盖。
        /// </summary>
        /// <param name="manifestPath">生成的 launcher AndroidManifest.xml 路径。</param>
        private static void PatchLauncherManifest(string manifestPath)
        {
            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(manifestPath);
            if (!(document.SelectSingleNode("/manifest/application") is XmlElement application))
            {
                Log.Warning(LogTag.Editor, $"[MaxSafeDKLauncherManifestFix] launcher AndroidManifest.xml 缺少 application 节点，跳过 SafeDKApplication 补写：{manifestPath}");
                return;
            }

            string current = application.GetAttribute("name", c_AndroidNamespace);
            if (string.Equals(current, c_SafeDKApplicationName, StringComparison.Ordinal))
            {
                Log.Warning(LogTag.Editor, $"[MaxSafeDKLauncherManifestFix] launcher application 已是 android:name={c_SafeDKApplicationName}，无需重复写入。");
                return;
            }

            if (!string.IsNullOrEmpty(current))
            {
                Log.Warning(LogTag.Editor, $"[MaxSafeDKLauncherManifestFix] launcher application 已声明 android:name={current}，保留现有值，未写入 {c_SafeDKApplicationName}。");
                return;
            }

            application.SetAttribute("name", c_AndroidNamespace, c_SafeDKApplicationName);
            document.Save(manifestPath);
            Log.Debug(LogTag.Editor, $"[MaxSafeDKLauncherManifestFix] 已向 launcher AndroidManifest.xml 写入 android:name={c_SafeDKApplicationName}。");
        }
    }
}
#endif
