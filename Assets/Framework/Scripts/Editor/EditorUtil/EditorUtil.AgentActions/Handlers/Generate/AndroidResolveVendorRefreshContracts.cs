/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveVendorRefreshContracts.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   Android 精确模板域的既有厂商资源回调自然分支
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>只观察完整 DLL 绑定后的九个厂商回调；不调用回调、私有 getter、更新、取消或注册设置。</summary>
    internal static class AndroidResolveVendorRefreshContracts
    {
        private const BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        /// <summary>冻结回调输入的角色，避免把允许导入的目录错误当成本次必定删除的目录。</summary>
        [Serializable]
        internal sealed class Inputs
        {
            public string[] allPaths;
            public string[] deletedPaths;
            public string[] sourcePaths;
        }

        /// <summary>由正规计划复制来源和实际删除集合生成角色；条件目录仍是精确单路径。</summary>
        /// <param name="plan">已冻结的正规本地模板解析计划。</param>
        /// <param name="allowed">本轮精确允许路径集合；不是目录递归通配。</param>
        /// <returns>按角色冻结的完整潜在刷新路径输入。</returns>
        internal static Inputs CreateInputs(AndroidResolveActionCommon.TemplateResolutionPlan plan, string[] allowed)
        {
            return new Inputs { allPaths = allowed, deletedPaths = plan.deletionSet.Select(CleanPath).ToArray(),
                sourcePaths = plan.copies.Select(item => item.source).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray() };
        }

        /// <summary>只去掉既有计划的条件说明，不接受调用者给目录通配权限。</summary>
        /// <param name="path">当前检查的精确路径，不授予目录通配权限。</param>
        /// <returns>仅去除既有条件说明的精确路径，不产生通配范围。</returns>
        private static string CleanPath(string path)
        {
            return path.Replace("/（仅删除后为空时移除目录）", "").Replace("（仅移除空目录时删除）", "");
        }

        /// <summary>核对固定 Manifest、现存 importer 标签、实际正则及已冻结生效设置；摘要只保存元数据和 SHA。</summary>
        /// <param name="inputs">完整且按角色冻结的本次潜在输入，不含递归写通配。</param>
        /// <param name="token">取消只读遍历，不取消厂商工作。</param>
        /// <param name="digest">成功时的稳定自然分支摘要。</param>
        /// <param name="error">不能证明的精确分支。</param>
        /// <returns>这些已绑定厂商实现不进入额外解析、Manifest 或 UPM 修改分支时返回 true。</returns>
        internal static bool TryCapture(Inputs inputs, CancellationToken token, out string digest, out string error)
        {
            digest = null;
            error = null;
            try
            {
                if (inputs?.allPaths == null || inputs.deletedPaths == null || inputs.sourcePaths == null ||
                    inputs.allPaths.Length == 0 || inputs.allPaths.Concat(inputs.deletedPaths).Concat(inputs.sourcePaths).Any(string.IsNullOrEmpty))
                    throw new InvalidOperationException("厂商刷新合同缺完整路径角色。");
                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android) throw new InvalidOperationException("厂商自然分支只覆盖当前 Android。");
                var rows = new List<string>();
                foreach (string path in inputs.allPaths)
                {
                    token.ThrowIfCancellationRequested();
                    string name = Path.GetFileName(path);
                    if (name == "google-services.json" || name == "GoogleService-Info.plist" ||
                        path == "Assets/StreamingAssets/google-services-desktop.json" || path.StartsWith("Assets/FacebookSDK/", StringComparison.Ordinal))
                        throw new InvalidOperationException("Firebase/Facebook 正规回调的写分支命中：" + path);
                }
                const string manifest = "Assets/Plugins/Android/AndroidManifest.xml";
                if (!Directory.Exists(Path.Combine(GenerateActionCommon.ProjectRoot, "Assets/Plugins/Android")) ||
                    !File.Exists(Path.Combine(GenerateActionCommon.ProjectRoot, manifest)) || inputs.allPaths.Contains(manifest, StringComparer.Ordinal))
                    throw new InvalidOperationException("Firebase Manifest 必须已经存在且不属于本轮写集；缺失会触发正规生成。");
                rows.Add("manifest|" + GenerateActionCommon.ComputeFileHash(Path.Combine(GenerateActionCommon.ProjectRoot, manifest), token));
                Type registry = Find("Google.XmlPackageManagerRegistries");
                // 该精确 cctor 只建立 Regex/常量/字符串数组，读取不运行 resolver 初始化或外部操作。
                if (!(Read(registry, "fileRegularExpressions") is IEnumerable registryPatterns) || !Equals(Read(registry, "REGISTRIES_LABEL"), "gumpr_registries"))
                    throw new InvalidOperationException("UPM registry 正则或标签 backing 不符合已绑定实现。");
                Regex[] registryRegex = CapturePatterns(registryPatterns, "UPM registry", rows);
                foreach (string path in inputs.allPaths.Concat(inputs.sourcePaths).Distinct(StringComparer.Ordinal))
                {
                    token.ThrowIfCancellationRequested();
                    if (registryRegex.Any(pattern => pattern.IsMatch(path))) throw new InvalidOperationException("UPM registry 正则命中本次路径：" + path);
                    AssetImporter importer = AssetImporter.GetAtPath(path);
                    if (importer != null && (!string.IsNullOrEmpty(importer.assetBundleName) || !string.IsNullOrEmpty(importer.assetBundleVariant)))
                        throw new InvalidOperationException("本轮路径或复制源存在 AssetBundle 名称/variant，可能触发额外名称变更回调：" + path);
                    if (importer != null && AssetDatabase.GetLabels(importer).Contains("gumpr_registries", StringComparer.Ordinal))
                        throw new InvalidOperationException("本轮路径或复制源具有 gumpr_registries 标签：" + path);
                }
                rows.Add("upm-label|no-match");
                Type resolver = Find("GooglePlayServices.PlayServicesResolver");
                if (Read(resolver, "gradleResolverInstance") == null) throw new InvalidOperationException("EDM GradleResolver 尚未自然存在；回调 getter 会构造新实例。");
                Type settings = resolver.Assembly.GetType("GooglePlayServices.SettingsDialog", true);
                if (!(Read(settings, "ConfigurablePackageDir") is bool configurable) || configurable ||
                    !Equals(Read(settings, "DefaultPackageDir"), "Assets/Plugins/Android"))
                    throw new InvalidOperationException("EDM Manifest/包目录不是已审固定目录。");
                if (!AndroidResolveActionCommon.TryReadGoogleSettings(Read(settings, "projectSettings"), new[]
                    { new AndroidResolveActionCommon.GoogleSettingRequest { key = "GooglePlayServices.AutoResolverEnabled", defaultBoolean = true } },
                    token, out AndroidResolveActionCommon.SettingEntry[] preferences, out string persistence, out error)) return false;
                bool automatic = preferences.Single().value == bool.TrueString && !UnityEngine.Application.isBatchMode;
                rows.Add("auto|" + automatic + "|" + persistence);
                if (inputs.allPaths.Contains(manifest, StringComparer.Ordinal)) throw new InvalidOperationException("本轮导入会触发 EDM Manifest patch。");
                if (!(Read(resolver, "autoResolveFilePatterns") is IEnumerable resolverPatterns)) throw new InvalidOperationException("EDM 自动解析正则集合不存在。");
                Regex[] automaticPatterns = CapturePatterns(resolverPatterns, "EDM auto", rows);
                if (automatic)
                {
                    if (inputs.deletedPaths.Any(path => path.StartsWith("Assets/Plugins/Android", StringComparison.Ordinal)))
                        throw new InvalidOperationException("本轮删除命中启用的 EDM Reresolve 包目录支路，不能重入解析。");
                    if (inputs.allPaths.Any(path => automaticPatterns.Any(pattern => pattern.IsMatch(path))))
                        throw new InvalidOperationException("本轮路径命中启用的 EDM 自动解析文件正则，不能安排第二次解析。");
                }
                // CocoapodsIntegrationEnabled 在 Android 目标于读取偏好前直接 false；不调用 iOS 设置 getter。
                rows.Add("ios|Android-target-false");
                rows.Add("firebase-settings-cache|assetsChanged=true");
                rows.Add("facebook-cache|currentAsset=last-import-only");
                digest = GenerateActionCommon.ComputeTextHash(string.Join("\n", rows));
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = exception is InvalidOperationException ? exception.Message : "厂商刷新自然分支只读检查失败：" + exception.GetType().Name; return false; }
        }

        /// <summary>只接纳 BCL 原生 Regex，冻结 pattern/options/timeout，禁止派生类虚拟匹配器执行任意代码。</summary>
        /// <param name="patterns">已存在的实际正则集合，只接纳精确 BCL Regex。</param>
        /// <param name="role">当前正则集合在合同中的固定角色名称。</param>
        /// <param name="rows">当前捕获生成的规范化元数据行。</param>
        /// <returns>已核数量、真实类型与容量的现有原生 Regex 数组。</returns>
        private static Regex[] CapturePatterns(IEnumerable patterns, string role, List<string> rows)
        {
            var result = new List<Regex>();
            foreach (object value in patterns)
            {
                if (result.Count >= 64 || value?.GetType() != typeof(Regex)) throw new InvalidOperationException(role + " 正则数量或真实类型未知。");
                var pattern = (Regex)value;
                if (pattern.ToString().Length > 2048) throw new InvalidOperationException(role + " 正则超出已审容量。");
                result.Add(pattern);
            }
            rows.AddRange(result.Select(pattern => role + "|" + pattern + "|" + pattern.Options + "|" + pattern.MatchTimeout.Ticks).OrderBy(value => value, StringComparer.Ordinal));
            return result.ToArray();
        }

        /// <summary>仅从已加载模块找元数据，不加载程序集、不初始化 owner。</summary>
        /// <param name="name">固定完整类型名或已审字段名，不接受业务输入作为反射指令。</param>
        /// <returns>唯一已加载的实际类型；缺失或冲突由调用者阻断。</returns>
        private static Type Find(string name)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).Where(type => type != null).Distinct().Single();
        }

        /// <summary>调用者仅在正常 InitializeOnLoad 已完成或精读为纯内存 cctor 的类型上读取固定 backing。</summary>
        /// <param name="type">已加载且需要核对的实际类型元数据。</param>
        /// <param name="name">固定完整类型名或已审字段名，不接受业务输入作为反射指令。</param>
        /// <returns>固定已审静态字段的现值。</returns>
        private static object Read(Type type, string name)
        {
            FieldInfo field = type?.GetField(name, Fields);
            if (field == null || !field.IsStatic) throw new MissingFieldException(type?.FullName, name);
            return field.GetValue(null);
        }
    }
}
