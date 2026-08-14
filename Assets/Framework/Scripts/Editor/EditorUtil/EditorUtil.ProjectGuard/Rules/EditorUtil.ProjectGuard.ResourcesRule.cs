/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ProjectGuard.ResourcesRule.cs
 * author:    taoye
 * created:   2026/7/15
 * descrip:   Nova 项目规范守卫资源规则
 ***************************************************************/

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

            /// <summary>
            /// 检查受管目录内需确认归属的 Resources 资源，不修改资源或目录。
            /// </summary>
            /// <param name="managedRoots">当前检查范围的目录根节点。</param>
            /// <param name="report">问题收集报告。</param>
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
                        "发现一个需要确认归属的 Resources 资源。\n" +
                        "处理方式：业务资源建议迁移到 Bundle；第三方插件资源可以保留。\n" +
                        "技术信息：当前范围发现归属待确认的非 Resources/BuiltIn 资源。",
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
