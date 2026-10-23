/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ImportantUpdates.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   重要升级声明的本地状态、安装核验和批次恢复
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 从实际安装包的 CHANGELOG 读取重要声明；确认记录按工程和本机用户隔离，不依赖网络或 EditorPrefs。
        /// </summary>
        internal static partial class ImportantUpdates
        {
            private static string s_ActiveBatchId;

            /// <summary>本地可恢复状态；观察版本只用于展示，绝不代替逐版本的人工确认记录。</summary>
            [Serializable]
            internal sealed class LocalState
            {
                public int SchemaVersion = 1;
                public List<string> Acknowledged = new List<string>();
                public List<Observation> Observed = new List<Observation>();
                public List<UpgradeBatch> Pending = new List<UpgradeBatch>();
            }

            /// <summary>上次检查时已核验的包版本。</summary>
            [Serializable]
            internal sealed class Observation
            {
                public string PackageName;
                public string Version;
            }

            /// <summary>跨重载保存的显式升级批次；本地记录不具备自动重新执行升级的权限。</summary>
            [Serializable]
            internal sealed class UpgradeBatch
            {
                public string Id;
                public List<UpgradeTarget> Targets = new List<UpgradeTarget>();
            }

            /// <summary>用户明确操作的包及升级前后版本，不包含 UPM 自动带动的依赖。</summary>
            [Serializable]
            internal sealed class UpgradeTarget
            {
                public string PackageName;
                public string FromVersion;
                public string TargetVersion;
            }

            /// <summary>由真实目录和 package.json 校验得到的安装快照。</summary>
            internal sealed class InstalledPackage
            {
                internal string Name;
                internal string DisplayName;
                internal string Version;
                internal string Root;
            }

            /// <summary>窗口展示的单个正式版本声明；正文快照用于避免确认展示后被外部修改的内容。</summary>
            internal sealed class Notice
            {
                internal string PackageName;
                internal string DisplayName;
                internal string FromVersion;
                internal string InstalledVersion;
                internal string Version;
                internal string Body;
                internal string ReleaseFingerprint;
            }

            /// <summary>一次窗口的固定写集，仅包含该窗口真正展示的包和版本。</summary>
            internal sealed class Presentation
            {
                internal string BatchId;
                internal List<Notice> Notices = new List<Notice>();
            }

            /// <summary>管理批次调用范围；释放后只安排核验，不触发新的安装行为。</summary>
            private sealed class BatchScope : IDisposable
            {
                private readonly string m_Previous;

                /// <summary>保存进入批次前的上下文，使嵌套调用能够恢复外层范围。</summary>
                internal BatchScope(string previous) => m_Previous = previous;

                /// <summary>结束同步提交范围并安排一次安装结果检查。</summary>
                public void Dispose()
                {
                    s_ActiveBatchId = m_Previous;
                    QueueCheck(false);
                }
            }

            /// <summary>获取被 Library 忽略规则覆盖的项目内用户专属状态位置。</summary>
            internal static string GetStatePath()
            {
                using (SHA256 hash = SHA256.Create())
                {
                    string identity = System.Environment.UserDomainName + "\\" + System.Environment.UserName;
                    string userKey = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity)))
                        .Replace("-", string.Empty).Substring(0, 24);
                    return Path.GetFullPath(Path.Combine(Application.dataPath, "../Library/Nova/ImportantUpdates", userKey, "State.json"));
                }
            }

            /// <summary>开始合并多个同步安装提交；每个目标仍由 InstallPackage 的既有预检决定是否接受。</summary>
            internal static IDisposable BeginBatch()
            {
                string previous = s_ActiveBatchId;
                s_ActiveBatchId = previous ?? Guid.NewGuid().ToString("N");
                return new BatchScope(previous);
            }

            /// <summary>在修改 manifest 前持久化目标；写入失败时抛错，避免失去单包或批量范围。</summary>
            internal static string RecordUpgrade(PlugPals.PackageDisplayEntry entry)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Name) || string.IsNullOrEmpty(entry.LatestVersion))
                {
                    throw new ArgumentException("重要升级记录缺少包名或目标版本。", nameof(entry));
                }

                string id = s_ActiveBatchId ?? Guid.NewGuid().ToString("N");
                string path = GetStatePath();
                LocalState state = LoadState(path);
                RecordTarget(state, id, entry);
                SaveState(path, state);
                // SessionState 仅用于同一 Editor 会话的调度隔离，不承担用户确认持久化。
                SessionState.SetBool(c_ScopedSessionKey, true);
                return id;
            }

            /// <summary>只更新内存中的显式批次，供真实提交和隔离测试复用，不合并其他操作或传递依赖。</summary>
            internal static void RecordTarget(LocalState state, string id, PlugPals.PackageDisplayEntry entry)
            {
                UpgradeBatch batch = state.Pending.Find(item => item.Id == id);
                if (batch == null)
                {
                    batch = new UpgradeBatch { Id = id };
                    state.Pending.Add(batch);
                }

                batch.Targets.RemoveAll(item => item.PackageName == entry.Name);
                batch.Targets.Add(new UpgradeTarget
                {
                    PackageName = entry.Name,
                    FromVersion = entry.LocalVersion,
                    TargetVersion = entry.LatestVersion
                });
            }

            /// <summary>manifest 未成功保存时撤销当前目标，保留同批次内其他已提交的包。</summary>
            internal static void CancelUpgrade(string batchId, string packageName)
            {
                string path = GetStatePath();
                LocalState state = LoadState(path);
                UpgradeBatch batch = state.Pending.Find(item => item.Id == batchId);
                if (batch == null) return;
                batch.Targets.RemoveAll(item => item.PackageName == packageName);
                state.Pending.RemoveAll(item => item.Targets.Count == 0);
                SaveState(path, state);
            }

            /// <summary>现有 Resolve 已排队后请求核验，确保查询不早于本轮安装提交。</summary>
            internal static void UpgradeSubmitted() => QueueCheck(false);

            /// <summary>读取本地状态并严格校验结构；损坏记录不能被静默当作已读或空记录。</summary>
            internal static LocalState LoadState(string path)
            {
                if (!File.Exists(path)) return new LocalState();
                JObject json = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                if ((int?)json[nameof(LocalState.SchemaVersion)] != 1 ||
                    !(json[nameof(LocalState.Acknowledged)] is JArray) ||
                    !(json[nameof(LocalState.Pending)] is JArray) ||
                    !(json[nameof(LocalState.Observed)] is JArray))
                {
                    throw new InvalidDataException("重要升级提示的本地记录格式无效：" + path);
                }

                LocalState state = json.ToObject<LocalState>();
                if (state == null || state.Acknowledged.Any(string.IsNullOrEmpty) ||
                    state.Observed.Any(item => item == null || string.IsNullOrEmpty(item.PackageName) || string.IsNullOrEmpty(item.Version)) ||
                    state.Pending.Any(item => item == null || string.IsNullOrEmpty(item.Id) || item.Targets == null ||
                        item.Targets.Any(target => target == null || string.IsNullOrEmpty(target.PackageName) || string.IsNullOrEmpty(target.TargetVersion))))
                {
                    throw new InvalidDataException("重要升级提示的本地记录包含无效条目：" + path);
                }
                return state;
            }

            /// <summary>同目录原子替换状态，失败时保留原记录，并清除本次写入的临时文件。</summary>
            internal static void SaveState(string path, LocalState state)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    File.WriteAllText(temporary, JsonConvert.SerializeObject(state, Formatting.Indented), new UTF8Encoding(false));
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }

            /// <summary>构造包名和正式版本的确认键；版本正文整体展示，禁止只展示部分后确认整个版本。</summary>
            internal static string GetAcknowledgementKey(string packageName, string version) => packageName + "@" + version;

            /// <summary>读取实际安装目录；registry 包还必须与 lock 解析结果一致，本地 file 包核验真实元数据。</summary>
            internal static Dictionary<string, InstalledPackage> ReadInstalledPackages()
            {
                PackageInfo[] registered = PackageInfo.GetAllRegisteredPackages();
                if (registered == null) throw new InvalidOperationException("Unity 包注册表尚未就绪。");
                var packages = new Dictionary<string, InstalledPackage>(StringComparer.Ordinal);
                var registeredNames = new HashSet<string>(registered.Where(item => item != null).Select(item => item.name), StringComparer.Ordinal);
                string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string lockPath = Path.Combine(projectRoot, "Packages/packages-lock.json");
                JObject locked = File.Exists(lockPath) ? JObject.Parse(File.ReadAllText(lockPath)) : null;
                foreach (PackageInfo package in registered)
                {
                    if (package == null || (package.errors != null && package.errors.Length > 0)) continue;
                    InstalledPackage installed = ReadPackageDirectory(package.name, package.resolvedPath);
                    if (installed == null || installed.Version != package.version) continue;
                    JToken lockEntry = locked?["dependencies"]?[package.name];
                    if (package.source == PackageSource.Registry && (string)lockEntry?["version"] != installed.Version) continue;
                    packages[installed.Name] = installed;
                }

                string manifestPath = Path.Combine(projectRoot, "Packages/manifest.json");
                JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
                if (manifest["dependencies"] is JObject dependencies)
                {
                    foreach (JProperty dependency in dependencies.Properties())
                    {
                        // 仅补 Unity 没返回的 Assets 开发包；不得覆盖注册表已有的错误或版本不一致结果。
                        if (registeredNames.Contains(dependency.Name)) continue;
                        string reference = (string)dependency.Value;
                        if (reference == null || !reference.StartsWith("file:", StringComparison.Ordinal)) continue;
                        string root = Path.GetFullPath(Path.Combine(projectRoot, "Packages", reference.Substring(5)));
                        InstalledPackage installed = ReadPackageDirectory(dependency.Name, root);
                        if (installed != null) packages[installed.Name] = installed;
                    }
                }
                return packages;
            }

            /// <summary>核对目录、包名和实际版本，不以 PackageCache 文件夹名推断已安装身份。</summary>
            private static InstalledPackage ReadPackageDirectory(string expectedName, string root)
            {
                if (string.IsNullOrEmpty(root)) return null;
                string path = Path.Combine(root, "package.json");
                if (!File.Exists(path)) return null;
                JObject package = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                string name = (string)package["name"];
                string version = (string)package["version"];
                if (name != expectedName || string.IsNullOrEmpty(version)) return null;
                return new InstalledPackage
                {
                    Name = name, DisplayName = (string)package["displayName"] ?? name,
                    Version = version, Root = root
                };
            }

            /// <summary>按固定包范围和未确认集合创建展示快照，包内版本从新到旧排列，首次检查也不自动跳过历史声明。</summary>
            internal static Presentation BuildPresentation(LocalState state, IDictionary<string, InstalledPackage> installed, UpgradeBatch batch)
            {
                var presentation = new Presentation { BatchId = batch?.Id };
                var acknowledged = new HashSet<string>(state.Acknowledged, StringComparer.Ordinal);
                IEnumerable<string> names = batch == null ? installed.Keys : batch.Targets.Select(item => item.PackageName);
                foreach (string name in names.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
                {
                    if (!installed.TryGetValue(name, out InstalledPackage package)) continue;
                    UpgradeTarget target = batch?.Targets.Find(item => item.PackageName == name);
                    if (target != null && package.Version != target.TargetVersion) continue;
                    string changelogPath = Path.Combine(package.Root, "CHANGELOG.md");
                    if (!File.Exists(changelogPath)) continue;
                    string markdown = File.ReadAllText(changelogPath, Encoding.UTF8);
                    if (markdown.IndexOf("Important", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    List<ReleaseNotes> releases = ParseChangelog(markdown);
                    string fingerprint = GetReleaseFingerprint(releases, package.Version);
                    foreach (ReleaseNotes release in releases.OrderByDescending(item => item.Version, Comparer<string>.Create(CompareReleaseVersions)))
                    {
                        if (CompareReleaseVersions(release.Version, package.Version) > 0 ||
                            acknowledged.Contains(GetAcknowledgementKey(name, release.Version))) continue;
                        presentation.Notices.Add(new Notice
                        {
                            PackageName = name, DisplayName = package.DisplayName,
                            FromVersion = target?.FromVersion ?? state.Observed.Find(item => item.PackageName == name)?.Version,
                            InstalledVersion = package.Version, Version = release.Version, Body = release.Body,
                            ReleaseFingerprint = fingerprint
                        });
                    }
                }
                return presentation;
            }

            /// <summary>人工确认时重新核对正文快照，原子保存且只确认本窗口展示的包版本。</summary>
            internal static bool TryAcknowledge(Presentation presentation, out string error)
            {
                try
                {
                    Acknowledge(GetStatePath(), presentation, ReadInstalledPackages());
                    error = null;
                    return true;
                }
                catch (Exception exception)
                {
                    error = "确认记录未保存，请重试；本窗口内容仍会继续提醒。";
                    Debug.LogError("[Nova Important Updates] " + exception);
                    return false;
                }
            }

            /// <summary>核验展示写集后写入指定本地存档；独立路径用于隔离回归测试，不接触真实用户记录。</summary>
            internal static void Acknowledge(string path, Presentation presentation, IDictionary<string, InstalledPackage> installed)
            {
                if (presentation == null || presentation.Notices.Count == 0) throw new ArgumentException("没有可确认的重要声明。");
                LocalState state = LoadState(path);
                foreach (Notice notice in presentation.Notices)
                {
                    if (!installed.TryGetValue(notice.PackageName, out InstalledPackage package) || package.Version != notice.InstalledVersion)
                        throw new InvalidOperationException("安装版本已变化，请重新检查重要声明。");
                    List<ReleaseNotes> releases = ParseChangelog(File.ReadAllText(Path.Combine(package.Root, "CHANGELOG.md"), Encoding.UTF8));
                    ReleaseNotes current = releases.Find(item => item.Version == notice.Version);
                    if (current == null || current.Body != notice.Body || CompareReleaseVersions(notice.Version, package.Version) > 0 ||
                        notice.ReleaseFingerprint != GetReleaseFingerprint(releases, package.Version))
                        throw new InvalidOperationException("重要声明正文或适用版本已变化，请重新检查。");
                    string key = GetAcknowledgementKey(notice.PackageName, notice.Version);
                    if (!state.Acknowledged.Contains(key)) state.Acknowledged.Add(key);
                }
                if (presentation.BatchId != null) state.Pending.RemoveAll(item => item.Id == presentation.BatchId);
                SaveState(path, state);
            }

            /// <summary>绑定当前已生效的完整重要版本集合；新增历史声明也会使旧展示失效，但未发布草稿变化不影响确认。</summary>
            private static string GetReleaseFingerprint(IEnumerable<ReleaseNotes> releases, string installedVersion)
            {
                var content = new StringBuilder();
                foreach (ReleaseNotes release in releases.Where(item => CompareReleaseVersions(item.Version, installedVersion) <= 0)
                    .OrderBy(item => item.Version, StringComparer.Ordinal))
                {
                    content.Append(release.Version.Length).Append(':').Append(release.Version)
                        .Append(release.Body.Length).Append(':').Append(release.Body);
                }
                using (SHA256 hash = SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(content.ToString())));
            }
        }
    }
}
