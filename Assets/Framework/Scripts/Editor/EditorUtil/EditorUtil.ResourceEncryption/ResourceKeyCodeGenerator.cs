/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * filename:  ResourceKeyCodeGenerator.cs
 * author:    taoye
 * created:   2026/10/9
 * descrip:   生成项目专属的客户端资源密钥提供器
 ***************************************************************/
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Coord = NovaFramework.Editor.EditorUtil.Config.DimensionProjector.Coord;

namespace NovaFramework.Editor
{
    /// <summary>生成代码与密钥配置共同纳入项目私有仓库，须先编译再构建 Player。</summary>
    public static class ResourceKeyCodeGenerator
    {
        private const string FileName = "ResourceKeys.Generated.cs";
        private const string GeneratedMarker = "// 项目生成的资源密钥：";

        /// <summary>把目录规范为 Assets 相对路径；拒绝越界、目录穿越和 Editor 目录。</summary>
        public static string NormalizeOutputDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("请选择客户端密钥源码导出目录。");
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.GetFullPath(Path.IsPathRooted(directory) ? directory : Path.Combine(projectRoot, directory));
            string relative = Path.GetRelativePath(projectRoot, fullPath).Replace('\\', '/').TrimEnd('/');
            if (relative != "Assets" && !relative.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidOperationException("客户端密钥源码导出目录必须位于 Assets 内。");
            if (relative.Split('/').Any(part => part.Equals("Editor", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("客户端密钥源码不能放在 Editor 目录。");
            return relative;
        }

        /// <summary>获取配置的正式源码路径；不创建或指定程序集。</summary>
        public static string GetAssetPath(ConfigMasterSO master) =>
            NormalizeOutputDirectory(master.ResourceEncryption.ClientCodeOutputDirectory) + "/" + FileName;

        /// <summary>检查保存输入与当前 Player 程序集边界，返回实际归属的程序集。</summary>
        private static UnityEditor.Compilation.Assembly ValidateConfiguration(ConfigMasterSO master, string assetPath, Coord coord)
        {
            ResourceKeyStore.Validate(master.ResourceEncryption);
            string assemblyName = Path.GetFileNameWithoutExtension(UnityEditor.Compilation.CompilationPipeline.GetAssemblyNameFromScriptPath(assetPath));
            var owner = UnityEditor.Compilation.CompilationPipeline.GetAssemblies(UnityEditor.Compilation.AssembliesType.PlayerWithoutTestAssemblies)
                .FirstOrDefault(assembly => assembly.name == assemblyName);
            if (owner == null) throw new InvalidOperationException("所选目录的程序集不参与当前平台客户端编译，请选择客户端代码目录。");
            if (!owner.assemblyReferences.Any(reference => reference.name == "NovaFramework.Runtime"))
                throw new InvalidOperationException("所选目录的程序集须引用 NovaFramework.Runtime。");
            var hotAssemblies = new System.Collections.Generic.HashSet<string>(HybridCLR.Editor.SettingsUtil.HotUpdateAssemblyFilesIncludePreserved
                .Select(Path.GetFileNameWithoutExtension), StringComparer.Ordinal);
            var visited = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            ValidateStartupAssembly(owner, hotAssemblies, visited);
            string configuredNamespace = ResolveNamespace(master, coord);
            using (var provider = new Microsoft.CSharp.CSharpCodeProvider())
                if (string.IsNullOrWhiteSpace(configuredNamespace) || configuredNamespace.Split('.').Any(part => !provider.IsValidIdentifier(part)))
                    throw new InvalidOperationException("请在名字空间配置中填写有效的 C# 命名空间，再导出客户端密钥源码。");
            return owner;
        }

        /// <summary>沿实际依赖闭包检查热更程序集，避免资源启动依赖后加载的业务代码。</summary>
        private static void ValidateStartupAssembly(UnityEditor.Compilation.Assembly assembly,
            System.Collections.Generic.HashSet<string> hotAssemblies, System.Collections.Generic.HashSet<string> visited)
        {
            if (!visited.Add(assembly.name)) return;
            if (hotAssemblies.Contains(assembly.name))
                throw new InvalidOperationException("客户端密钥源码所属程序集或其依赖属于 HybridCLR 热更代码，请选择随客户端编译的目录。");
            foreach (var reference in assembly.assemblyReferences) ValidateStartupAssembly(reference, hotAssemblies, visited);
        }

        /// <summary>按调用方明确的导出坐标解析 Config 命名空间，不由目录或程序集名推断。</summary>
        private static string ResolveNamespace(ConfigMasterSO master, Coord coord) => EditorUtil.Config.DimensionalResolver.ResolveNamespace(
            master, coord.Platform, coord.Channel, coord.Mode);

        /// <summary>校验配置并导出客户端源码；路径改变时只清理本工具的旧产物，不生成 asmdef。</summary>
        public static void Generate(ConfigMasterSO master) => Generate(master,
            new Coord(master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode));

        /// <summary>按明确导出坐标生成源码；只更新生成路径状态，不改写当前渠道、模式或密钥配置。</summary>
        public static void Generate(ConfigMasterSO master, Coord coord)
        {
            string legacyPath = master.ResourceEncryption.GeneratedCodeAssetPath == "Assets/NovaGenerated/ResourceKeys/ResourceKeys.Generated.cs"
                ? "Assets/NovaGenerated/ResourceKeys/NovaProject.ResourceKeys.asmdef" : null;
            byte[] legacy = !string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath) ? File.ReadAllBytes(legacyPath) : null;
            byte[] meta = legacy != null && File.Exists(legacyPath + ".meta") ? File.ReadAllBytes(legacyPath + ".meta") : null;
            try { GenerateCore(master, coord); }
            catch
            {
                // 旧程序集也属于迁移事务；后续校验或写盘失败必须恢复原来的启动归属。
                if (legacy != null && !File.Exists(legacyPath))
                {
                    File.WriteAllBytes(legacyPath, legacy);
                    if (meta != null) File.WriteAllBytes(legacyPath + ".meta", meta);
                    AssetDatabase.Refresh();
                }
                throw;
            }
        }

        /// <summary>执行已验证生成和旧产物迁移；失败由外层恢复旧程序集，文件写入单独回滚。</summary>
        private static void GenerateCore(ConfigMasterSO master, Coord coord)
        {
            var data = master.ResourceEncryption;
            ResourceKeyStore.Validate(data);
            if (!data.Packages.Any(package => package.Keys.Count > 0) && string.IsNullOrEmpty(data.GeneratedCodeAssetPath)) return;
            string assetPath = GetAssetPath(master);
            var owner = ValidateConfiguration(master, assetPath, coord);
            string previousPath = data.GeneratedCodeAssetPath;
            ValidateSingleProvider(assetPath, previousPath);
            // 目标冲突或旧来源非生成文件时明确失败，避免覆盖项目组手写代码。
            if (File.Exists(assetPath) && !File.ReadAllText(assetPath).StartsWith(GeneratedMarker, StringComparison.Ordinal))
                throw new InvalidOperationException("导出目录已有同名手写文件，请选择其他目录。");
            if (!string.IsNullOrEmpty(previousPath) && previousPath != assetPath && File.Exists(previousPath) &&
                !File.ReadAllText(previousPath).StartsWith(GeneratedMarker, StringComparison.Ordinal))
                throw new InvalidOperationException("旧生成文件已被手工改写，请先保留该文件并处理路径冲突。");
            // 旧 asmdef 删除后路径归属会改变；先移除精确旧产物再查询最终程序集。
            bool removedLegacyAssembly = RemoveLegacyAssembly(previousPath);
            if (removedLegacyAssembly) owner = ValidateConfiguration(master, assetPath, coord);
            string code = GetCode(data, ResolveNamespace(master, coord));
            string linkerPath = Path.GetDirectoryName(assetPath) + "/link.xml";
            string linker = GetLinkerXml(owner.name, ResolveNamespace(master, coord));
            if (File.Exists(linkerPath) && !File.ReadAllText(linkerPath).StartsWith("<!-- Nova ResourceKeys -->", StringComparison.Ordinal))
                throw new InvalidOperationException("导出目录已有项目的 link.xml，请选择独立子目录以保留原文件。");
            bool changed = !File.Exists(assetPath) || File.ReadAllText(assetPath) != code ||
                !File.Exists(linkerPath) || File.ReadAllText(linkerPath) != linker;
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            // 先成功写入两个新产物，再移除旧产物；任一文件写入或删除失败均恢复之前的文件。
            string previousLinker = string.IsNullOrEmpty(previousPath) ? null : Path.GetDirectoryName(previousPath) + "/link.xml";
            var snapshots = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (string path in new[] { assetPath, linkerPath, previousPath, previousLinker })
            {
                if (string.IsNullOrEmpty(path) || snapshots.ContainsKey(path)) continue;
                snapshots.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
                string meta = path + ".meta";
                snapshots.Add(meta, File.Exists(meta) ? File.ReadAllBytes(meta) : null);
            }
            try
            {
                if (changed)
                {
                    File.WriteAllText(assetPath, code, new UTF8Encoding(false));
                    File.WriteAllText(linkerPath, linker, new UTF8Encoding(false));
                }
                if (!string.IsNullOrEmpty(previousPath) && previousPath != assetPath && File.Exists(previousPath) &&
                    !AssetDatabase.DeleteAsset(previousPath)) throw new IOException("旧客户端密钥源码删除失败，导出路径尚未切换。");
                if (!string.IsNullOrEmpty(previousLinker) && previousLinker != linkerPath && File.Exists(previousLinker) &&
                    File.ReadAllText(previousLinker).StartsWith("<!-- Nova ResourceKeys -->", StringComparison.Ordinal) &&
                    !AssetDatabase.DeleteAsset(previousLinker)) throw new IOException("旧客户端密钥裁剪文件删除失败。");
            }
            catch
            {
                foreach (var snapshot in snapshots)
                {
                    if (snapshot.Value != null) File.WriteAllBytes(snapshot.Key, snapshot.Value);
                    else if (File.Exists(snapshot.Key)) File.Delete(snapshot.Key);
                }
                AssetDatabase.Refresh();
                throw;
            }
            data.ClientCodeOutputDirectory = NormalizeOutputDirectory(data.ClientCodeOutputDirectory);
            data.GeneratedCodeAssetPath = assetPath;
            if (changed || removedLegacyAssembly || previousPath != assetPath) AssetDatabase.Refresh();
        }

        /// <summary>一次性移除旧工具生成的精确 asmdef，重新导入后以最终路径归属继续验证。</summary>
        private static bool RemoveLegacyAssembly(string previousPath)
        {
            if (previousPath != "Assets/NovaGenerated/ResourceKeys/ResourceKeys.Generated.cs") return false;
            string legacyPath = Path.GetDirectoryName(previousPath) + "/NovaProject.ResourceKeys.asmdef";
            const string legacyAssembly = "{\"name\":\"NovaProject.ResourceKeys\",\"references\":[\"NovaFramework.Runtime\"],\"autoReferenced\":true}";
            if (!File.Exists(legacyPath) || File.ReadAllText(legacyPath).Trim() != legacyAssembly) return false;
            if (!AssetDatabase.DeleteAsset(legacyPath)) throw new IOException("旧密钥程序集定义删除失败。");
            AssetDatabase.Refresh();
            return true;
        }

        /// <summary>一个项目只保留一个生成提供器，避免多个 ConfigMaster 生成代码的启动注册互相覆盖。</summary>
        private static void ValidateSingleProvider(string assetPath, string previousPath)
        {
            foreach (string candidate in AssetDatabase.GetAllAssetPaths())
            {
                if (Path.GetFileName(candidate) != FileName || SamePath(candidate, assetPath) || SamePath(candidate, previousPath)) continue;
                if (File.Exists(candidate) && File.ReadAllText(candidate).StartsWith(GeneratedMarker, StringComparison.Ordinal))
                    throw new InvalidOperationException("项目已有另一份客户端密钥生成源码，请共用同一导出目录，避免多个密钥提供器重复注册。");
            }
        }

        /// <summary>显式保留所选程序集中的提供器和启动注册方法；不注入可能重复的程序集属性。</summary>
        private static string GetLinkerXml(string assemblyName, string configuredNamespace) =>
            "<!-- Nova ResourceKeys -->\n<linker>\n  <assembly fullname=\"" + System.Security.SecurityElement.Escape(assemblyName) +
            "\">\n    <type fullname=\"" + System.Security.SecurityElement.Escape(configuredNamespace + ".ResourceKeys") +
            "\" preserve=\"all\" />\n  </assembly>\n</linker>\n";

        /// <summary>比较 Unity 编译列表的绝对路径和配置相对路径，空路径不匹配。</summary>
        private static bool SamePath(string first, string second) => !string.IsNullOrEmpty(first) &&
            !string.IsNullOrEmpty(second) && Path.GetFullPath(first) == Path.GetFullPath(second);

        /// <summary>检查激活且已保存的 Master 与当前源码一致，不在 Player 构建中触发编译。</summary>
        public static void ValidateGenerated()
        {
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null) return;
            var data = ResourceKeyStore.Load();
            Coord coord = new Coord(master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
            if (!data.Packages.Any(package => package.Keys.Count > 0) && string.IsNullOrEmpty(data.GeneratedCodeAssetPath)) return;
            string assetPath = GetAssetPath(master);
            var owner = ValidateConfiguration(master, assetPath, coord);
            ValidateSingleProvider(assetPath, null);
            if (!owner.sourceFiles.Any(path => SamePath(path, assetPath)) || !File.Exists(assetPath) || File.ReadAllText(assetPath) !=
                GetCode(data, ResolveNamespace(master, coord)) || !File.Exists(Path.GetDirectoryName(assetPath) + "/link.xml") ||
                File.ReadAllText(Path.GetDirectoryName(assetPath) + "/link.xml") != GetLinkerXml(owner.name, ResolveNamespace(master, coord)))
                throw new InvalidOperationException("客户端资源密钥源码尚未同步，请先保存 Config，再点击顶部“导出”，等待编译完成后再构建。");
        }

        /// <summary>兼容旧生成调用，要求调用方使用激活 Master 的已保存数据。</summary>
        [Obsolete("请使用 Generate(ConfigMasterSO)。")]
        public static void Generate(ResourceKeyStoreData data)
        {
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null || !ReferenceEquals(data, master.ResourceEncryption))
                throw new InvalidOperationException("请使用 ConfigMaster 生成客户端密钥源码。");
            Generate(master);
        }

        /// <summary>CI 从激活且已保存的 ConfigMaster 生成源码，后续构建前必须等待编译完成。</summary>
        public static void GenerateForCI()
        {
            ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
            if (master == null) throw new InvalidOperationException("请先激活 ConfigMaster，再生成客户端密钥源码。");
            Generate(master);
            EditorUtility.SetDirty(master);
            AssetDatabase.SaveAssets();
        }
        /// <summary>产生稳定排序的源码；标识和密钥采用 Base64，避免字符串注入和非 ASCII 编译差异。</summary>
        private static string GetCode(ResourceKeyStoreData data, string configuredNamespace)
        {
            ResourceKeyStore.Validate(data);
            var text = new StringBuilder("// 项目生成的资源密钥：与密钥配置一起纳入项目私有仓库，不得加入热更程序集列表。\nusing System;\nusing UnityEngine;\nusing NovaFramework.Runtime;\nnamespace " + configuredNamespace + " {\n[UnityEngine.Scripting.Preserve]\ninternal sealed class ResourceKeys : IResourceKeyProvider {\n/// <summary>资源启动前注册项目专属密钥。</summary>\n[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]\nprivate static void Register() { ResourceKeyRegistry.Provider = new ResourceKeys(); }\n/// <summary>按包裹和编号获取密钥副本，不存在时明确失败。</summary>\npublic byte[] GetKey(string packageName, string keyId) {\n");
            foreach (var package in data.Packages.OrderBy(p => p.PackageName, StringComparer.Ordinal))
                foreach (var key in package.Keys.OrderBy(k => k.KeyId, StringComparer.Ordinal))
                    text.Append("if (packageName == Decode(\"").Append(Encode(package.PackageName)).Append("\") && keyId == Decode(\"").Append(Encode(key.KeyId)).Append("\")) return Convert.FromBase64String(\"").Append(key.KeyBase64).Append("\");\n");
            return text.Append("throw new InvalidOperationException(\"客户端缺少该包裹的资源密钥编号。\");\n}\n/// <summary>还原配置标识，不承担密钥保护。</summary>\nprivate static string Decode(string value) => System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value));\n}\n}\n").ToString();
        }
        /// <summary>以 Base64 编码配置标识，避免生成源码注入。</summary>
        private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
    }
}
