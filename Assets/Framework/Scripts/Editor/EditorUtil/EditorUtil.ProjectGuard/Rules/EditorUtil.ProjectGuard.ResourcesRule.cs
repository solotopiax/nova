using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class ProjectGuard
        {
            private enum ResourceClassification
            {
                Ignored,
                AllowedBuiltIn,
                Violation,
            }

            private static void ValidateResources(string[] managedRoots, NovaGuardReport report)
            {
                if (managedRoots == null || managedRoots.Length == 0)
                    return;

                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string guid in AssetDatabase.FindAssets(string.Empty, managedRoots))
                {
                    string path = NormalizePath(AssetDatabase.GUIDToAssetPath(guid));
                    if (string.IsNullOrEmpty(path) || !seen.Add(path) || AssetDatabase.IsValidFolder(path))
                        continue;

                    bool supplierOwned = IsSupplierOwnedAsset(path);
                    if (ClassifyResourcePath(path, managedRoots, supplierOwned) != ResourceClassification.Violation)
                        continue;

                    report.Add(new NovaGuardIssue(
                        "NOVA-RES-001",
                        NovaGuardSeverity.Warning,
                        "当前范围发现归属待确认的非 BuiltIn Resources；若属于业务资源请评估迁移 Bundle，第三方插件资源合法。",
                        path));
                }
            }

            private static ResourceClassification ClassifyResourcePath(
                string rawAssetPath, IEnumerable<string> managedRoots, bool supplierOwned)
            {
                string path = NormalizePath(rawAssetPath);
                string fileName = Path.GetFileName(path);
                if (string.IsNullOrEmpty(path) || path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                    supplierOwned || fileName.Equals(".DS_Store", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || !IsUnderRoot(path, managedRoots))
                    return ResourceClassification.Ignored;

                int resourcesIndex = path.IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase);
                if (resourcesIndex < 0)
                    return ResourceClassification.Ignored;

                string relative = path.Substring(resourcesIndex + "/Resources/".Length);
                return relative.StartsWith("BuiltIn/", StringComparison.OrdinalIgnoreCase)
                    ? ResourceClassification.AllowedBuiltIn
                    : ResourceClassification.Violation;
            }

            private static string ClassifyResourcePathForDiagnostics(
                string assetPath, string[] managedRoots, bool supplierOwned)
            {
                return ClassifyResourcePath(assetPath, managedRoots, supplierOwned).ToString();
            }

            private static bool IsSupplierOwnedAsset(string path)
            {
                if (path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                    path.IndexOf("/Plugins/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/ThirdParty/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("/Third Party/", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (type == null)
                    return false;

                string assemblyName = type.Assembly.GetName().Name;
                if (assemblyName.StartsWith("NovaFramework", StringComparison.Ordinal) ||
                    assemblyName.StartsWith("Unity", StringComparison.Ordinal) ||
                    assemblyName == "Assembly-CSharp")
                    return false;

                return typeof(ScriptableObject).IsAssignableFrom(type);
            }
        }
    }
}
