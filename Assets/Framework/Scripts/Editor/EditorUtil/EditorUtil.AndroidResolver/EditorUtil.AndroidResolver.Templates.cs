/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.AndroidResolver.Templates.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   Android Gradle 模板准备与设置事务
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEngine;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class AndroidResolver
        {
            internal sealed class TemplateEntry
            {
                internal string Field;
                internal string Name;
                internal string Source;
            }

            /// <summary>
            /// 显式准备 Unity 官方 Android 模板并启用对应七项发布开关；不解析依赖、不下载、不构建。
            /// 已有模板与 GUID 保持，停用模板通过 AssetDatabase.MoveAsset 恢复；失败时恢复本轮文件及原开关。
            /// Custom Base Gradle Template 的当前选择不变；由调用方显式准备，后续依赖解析须重新制定计划。
            /// </summary>
            public static void PrepareTemplates()
            {
                if (!AgentActionLockManager.TryAcquire(new[] { "unity-editor", "asset-database", "android-gradle-templates", "android-java-environment" },
                        out IDisposable lease))
                    throw new InvalidOperationException("[AndroidResolver] 编辑器或 Android 模板正被其它操作使用，未修改任何文件。");
                using (lease) PrepareTemplatesWithLock();
            }

            private static void PrepareTemplatesWithLock()
            {
                RequireProjectDirectory();
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    AndroidResolverJavaEnvironment.IsActive)
                    throw new InvalidOperationException("[AndroidResolver] 等待编辑器完成编译、刷新并退出 Play 后再准备模板。");
                string playback = BuildPipeline.GetPlaybackEngineDirectory(BuildTarget.Android, BuildOptions.None, false);
                if (string.IsNullOrEmpty(playback) || !Directory.Exists(playback))
                    throw new InvalidOperationException("[AndroidResolver] 当前 Unity 未安装 Android Build Support，无法读取官方模板。");
                TemplateEntry[] entries = GetTemplateEntries(playback);
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0 || assets[0] == null)
                    throw new InvalidOperationException("[AndroidResolver] 无法读取 Player Settings，未创建模板或修改开关。");
                if (EditorUtility.IsDirty(assets[0]))
                    throw new InvalidOperationException("[AndroidResolver] Player Settings 有未保存编辑，保留现场；完成该编辑后再准备模板。");
                bool changed;
                using (var settings = new SerializedObject(assets[0]))
                {
                    var properties = entries.Select(entry => settings.FindProperty(entry.Field)).ToArray();
                    if (properties.Any(property => property == null || property.propertyType != SerializedPropertyType.Boolean))
                        throw new InvalidOperationException("[AndroidResolver] 当前 Unity 的 Android 发布开关结构未适配，未修改任何文件。");
                    bool[] before = properties.Select(property => property.boolValue).ToArray();
                    AssetDatabase.DisallowAutoRefresh();
                    try
                    {
                        AssetDatabase.StartAssetEditing();
                        try
                        {
                            changed = PrepareTemplateFiles(Path.Combine(Directory.GetCurrentDirectory(), "Assets", "Plugins", "Android"), entries,
                                () =>
                                {
                                    foreach (SerializedProperty property in properties) property.boolValue = true;
                                    settings.ApplyModifiedPropertiesWithoutUndo();
                                    // 只保存 PlayerSettings，不顺带保存项目原有的其它脏资产。
                                    AssetDatabase.SaveAssetIfDirty(assets[0]);
                                },
                                () =>
                                {
                                    for (int i = 0; i < properties.Length; i++) properties[i].boolValue = before[i];
                                    settings.ApplyModifiedPropertiesWithoutUndo();
                                    AssetDatabase.SaveAssetIfDirty(assets[0]);
                                },
                                (source, destination) => AssetDatabase.MoveAsset(ToProjectAssetPath(source), ToProjectAssetPath(destination)));
                            changed |= before.Any(value => !value);
                        }
                        finally { AssetDatabase.StopAssetEditing(); }
                    }
                    finally { AssetDatabase.AllowAutoRefresh(); }
                }
                if (changed) AssetDatabase.Refresh();
                RequireEnabledTemplatesReady();
            }

            /// <summary>
            /// 使用当前 Android module 的真实官方路径，与 Unity6000.4.2f1 Publishing Settings 的来源一致。
            /// Unity 内部 GetBuildToolsDirectory 只是 playback/Tools；无需创建设置窗口或调用私有 UI。
            /// </summary>
            internal static TemplateEntry[] GetTemplateEntries(string playback)
            {
                string gradle = Path.Combine(playback, "Tools", "GradleTemplates");
                return new[]
                {
                    new TemplateEntry { Field = "useCustomMainManifest", Name = "AndroidManifest.xml", Source = Path.Combine(playback, "Apk", "UnityManifest.xml") },
                    new TemplateEntry { Field = "useCustomLauncherManifest", Name = "LauncherManifest.xml", Source = Path.Combine(playback, "Apk", "LauncherManifest.xml") },
                    new TemplateEntry { Field = "useCustomMainGradleTemplate", Name = "mainTemplate.gradle", Source = Path.Combine(gradle, "mainTemplate.gradle") },
                    new TemplateEntry { Field = "useCustomLauncherGradleManifest", Name = "launcherTemplate.gradle", Source = Path.Combine(gradle, "launcherTemplate.gradle") },
                    new TemplateEntry { Field = "useCustomGradlePropertiesTemplate", Name = "gradleTemplate.properties", Source = Path.Combine(gradle, "gradleTemplate.properties") },
                    new TemplateEntry { Field = "useCustomGradleSettingsTemplate", Name = "settingsTemplate.gradle", Source = Path.Combine(gradle, "settingsTemplate.gradle") },
                    // Unity 的官方 Proguard 项没有内置源文件，首次准备合法地创建空文件。
                    new TemplateEntry { Field = "useCustomProguardFile", Name = "proguard-user.txt", Source = null },
                };
            }

            /// <summary>先完整验证既有内容及所有缺失项的官方来源，再创建文件、恢复停用模板并提交开关。</summary>
            internal static bool PrepareTemplateFiles(string targetRoot, TemplateEntry[] entries,
                Action commitFlags, Action restoreFlags, Func<string, string, string> moveAsset)
            {
                var prepared = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                var disabled = new List<string>();
                foreach (TemplateEntry entry in entries)
                {
                    string target = Path.Combine(targetRoot, entry.Name);
                    RequireNoLinks(target);
                    if (File.Exists(target) && File.Exists(target + ".DISABLED"))
                        throw new InvalidOperationException("[AndroidResolver] 模板与停用副本同时存在，请保留并先处理冲突：" + entry.Name);
                    if (File.Exists(target)) { ValidateTemplate(target, entry.Name); continue; }
                    if (File.Exists(target + ".DISABLED"))
                    {
                        RequireNoLinks(target + ".DISABLED");
                        ValidateTemplate(target + ".DISABLED", entry.Name);
                        disabled.Add(target); continue;
                    }
                    if (entry.Source != null && !File.Exists(entry.Source))
                        throw new InvalidOperationException("[AndroidResolver] 官方模板缺失，尚未修改文件或开关：" + entry.Source);
                    if (entry.Source != null) ValidateTemplate(entry.Source, entry.Name);
                    prepared.Add(target, entry.Source == null ? Array.Empty<byte>() : File.ReadAllBytes(entry.Source));
                }
                bool targetExisted = Directory.Exists(targetRoot);
                string parent = Path.GetDirectoryName(targetRoot);
                bool parentExisted = Directory.Exists(parent);
                var created = new List<string>();
                var moved = new List<string>();
                bool flagsAttempted = false;
                try
                {
                    Directory.CreateDirectory(targetRoot);
                    foreach (KeyValuePair<string, byte[]> file in prepared)
                    {
                        using (var stream = new FileStream(file.Key, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            created.Add(file.Key);
                            stream.Write(file.Value, 0, file.Value.Length);
                        }
                    }
                    foreach (string target in disabled)
                    {
                        string error = moveAsset(target + ".DISABLED", target);
                        if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("[AndroidResolver] 停用模板恢复失败：" + error);
                        moved.Add(target);
                    }
                    foreach (TemplateEntry entry in entries) ValidateTemplate(Path.Combine(targetRoot, entry.Name), entry.Name);
                    flagsAttempted = true;
                    commitFlags();
                    return prepared.Count != 0 || disabled.Count != 0;
                }
                catch (Exception failure)
                {
                    var recovery = new List<Exception>();
                    if (flagsAttempted) TryRecover(restoreFlags, recovery);
                    foreach (string target in moved.AsEnumerable().Reverse())
                        TryRecover(() =>
                        {
                            string error = moveAsset(target, target + ".DISABLED");
                            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
                        }, recovery);
                    foreach (string target in created.AsEnumerable().Reverse())
                        TryRecover(() =>
                        {
                            if (!File.Exists(target)) return;
                            if (!File.ReadAllBytes(target).SequenceEqual(prepared[target]))
                                throw new IOException("新模板字节在准备期间变化，保留现场：" + target);
                            File.Delete(target);
                        }, recovery);
                    if (!targetExisted) TryRecover(() => { if (Directory.Exists(targetRoot) && !Directory.EnumerateFileSystemEntries(targetRoot).Any()) Directory.Delete(targetRoot); }, recovery);
                    if (!parentExisted) TryRecover(() => { if (Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any()) Directory.Delete(parent); }, recovery);
                    if (recovery.Count != 0)
                        throw new AggregateException("[AndroidResolver] 模板准备失败且回滚不完整；保留现场，禁止直接重放。", new[] { failure }.Concat(recovery));
                    throw;
                }
            }

            /// <summary>只读检查已启用模板；不让开关与磁盘不一致继续落入 EDM 的下载兜底。</summary>
            internal static void RequireEnabledTemplatesReady()
            {
                RequireProjectDirectory();
                var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
                if (assets == null || assets.Length == 0 || assets[0] == null)
                    throw new InvalidOperationException("[AndroidResolver] 无法读取 Android 模板开关。");
                using (var settings = new SerializedObject(assets[0]))
                {
                    foreach (TemplateEntry entry in GetTemplateEntries(string.Empty))
                    {
                        SerializedProperty field = settings.FindProperty(entry.Field);
                        if (field == null || field.propertyType != SerializedPropertyType.Boolean)
                            throw new InvalidOperationException("[AndroidResolver] Android 模板开关尚未适配：" + entry.Field);
                        if (!field.boolValue) continue;
                        string path = Path.Combine("Assets", "Plugins", "Android", entry.Name);
                        if (!File.Exists(path))
                            throw new InvalidOperationException("[AndroidResolver] 已启用模板但文件缺失：" + path + "；先完成 Android 模板准备，再重新计划解析。");
                        ValidateTemplate(path, entry.Name);
                    }
                }
            }

            /// <summary>Proguard 可为空；Gradle/Properties 必须有正文，Manifest 必须是无外部实体的合法 manifest XML。</summary>
            internal static void ValidateTemplate(string path, string name)
            {
                if (!File.Exists(path)) throw new FileNotFoundException("[AndroidResolver] 模板文件不存在。", path);
                if (name == "proguard-user.txt") return;
                if (string.IsNullOrWhiteSpace(File.ReadAllText(path)))
                    throw new InvalidOperationException("[AndroidResolver] 模板为空，保留用户文件并停止：" + path);
                if (!name.EndsWith("Manifest.xml", StringComparison.Ordinal)) return;
                var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (XmlReader reader = XmlReader.Create(path, options))
                {
                    var xml = new XmlDocument { XmlResolver = null };
                    xml.Load(reader);
                    if (xml.DocumentElement?.LocalName != "manifest")
                        throw new InvalidOperationException("[AndroidResolver] Manifest 根节点无效，保留用户文件并停止：" + path);
                }
            }

            private static void RequireNoLinks(string path)
            {
                for (string current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
                    if ((File.Exists(current) || Directory.Exists(current)) &&
                        (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("[AndroidResolver] 模板路径包含符号链接或重解析点，未修改：" + path);
            }

            private static void RequireProjectDirectory()
            {
                string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                if (Path.GetFullPath(Directory.GetCurrentDirectory()) != project)
                    throw new InvalidOperationException("[AndroidResolver] 当前工作目录不是实际 Unity 工程根，未修改模板或执行解析。");
            }

            private static string ToProjectAssetPath(string path)
            {
                string root = Path.GetFullPath(Directory.GetCurrentDirectory()) + Path.DirectorySeparatorChar;
                string full = Path.GetFullPath(path);
                if (!full.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException("模板路径不在当前项目内。");
                return full.Substring(root.Length).Replace('\\', '/');
            }

            private static void TryRecover(Action recover, ICollection<Exception> errors)
            {
                try { recover(); } catch (Exception exception) { errors.Add(exception); }
            }
        }
    }
}
