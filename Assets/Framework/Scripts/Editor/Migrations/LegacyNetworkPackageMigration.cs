/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LegacyNetworkPackageMigration.cs
 * author:    taoye
 * created:   2026/9/2
 * descrip:   已下架 BestHTTP 消费端包迁移
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NovaFramework.Editor.Migrations
{
    /// <summary>
    /// 从消费项目 manifest 中移除 Nova 已下架的 BestHTTP 相关包。
    /// </summary>
    internal static class LegacyNetworkPackageMigration
    {
        internal const string FrameworkPackageName = "com.solotopia.nova.framework";
        internal const string AdapterPackageName = "com.solotopia.nova.framework.besthttp";
        internal const string BestHttpPackageName = "com.tivadar.best.http";
        internal const string BestTlsPackageName = "com.tivadar.best.tlssecurity";
        internal const string LegacyBestHttpPackageName = "com.solotopia.best.http";
        internal const string LegacyBestTlsPackageName = "com.solotopia.best.tlssecurity";

        private static readonly string[] s_LegacyPackageNames =
        {
            AdapterPackageName,
            BestHttpPackageName,
            BestTlsPackageName,
            LegacyBestHttpPackageName,
            LegacyBestTlsPackageName,
        };

        private static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static string ManifestPath => Path.Combine(ProjectRoot, "Packages", "manifest.json");

        internal static bool ApplyManifestCleanup(
            JObject manifest,
            out IReadOnlyList<string> removedItems)
        {
            var removed = new List<string>();
            if (manifest == null)
            {
                removedItems = removed;
                return false;
            }

            if (manifest["dependencies"] is JObject dependencies)
            {
                foreach (string packageName in s_LegacyPackageNames)
                {
                    if (dependencies.Remove(packageName))
                    {
                        removed.Add("dependency:" + packageName);
                    }
                }
            }

            if (manifest["testables"] is JArray testables)
            {
                for (int index = testables.Count - 1; index >= 0; index--)
                {
                    string packageName = testables[index]?.Type == JTokenType.String
                        ? testables[index].Value<string>()
                        : null;
                    if (Array.IndexOf(s_LegacyPackageNames, packageName) < 0)
                    {
                        continue;
                    }

                    testables.RemoveAt(index);
                    removed.Add("testable:" + packageName);
                }
            }

            removedItems = removed;
            return removed.Count > 0;
        }

        internal static bool Run(out string summary)
        {
            JObject manifest;
            try
            {
                manifest = JObject.Parse(File.ReadAllText(ManifestPath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                summary = "读取 Packages/manifest.json 失败：" + exception.Message;
                throw new InvalidOperationException(summary, exception);
            }

            if (!ApplyManifestCleanup(manifest, out IReadOnlyList<string> removedItems))
            {
                summary = string.Empty;
                return false;
            }

            WriteJsonAtomically(ManifestPath, manifest);
            summary = string.Join(", ", removedItems);
            return true;
        }

        private static void WriteJsonAtomically(string path, JObject value)
        {
            string temporaryPath = path + ".nova-network-migration.tmp";
            File.WriteAllText(
                temporaryPath,
                value.ToString(Newtonsoft.Json.Formatting.Indented) + "\n",
                new UTF8Encoding(false));
            try
            {
                if (File.Exists(path))
                {
                    File.Replace(temporaryPath, path, null);
                }
                else
                {
                    File.Move(temporaryPath, path);
                }
            }
            catch (PlatformNotSupportedException)
            {
                File.Copy(temporaryPath, path, true);
                File.Delete(temporaryPath);
            }
        }
    }
}
