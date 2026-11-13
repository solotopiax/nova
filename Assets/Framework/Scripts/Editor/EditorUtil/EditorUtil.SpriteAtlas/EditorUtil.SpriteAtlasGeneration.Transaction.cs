/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.SpriteAtlasGeneration.Transaction.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   图集输出的精确备份、失败回滚与显式恢复
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class SpriteAtlasGeneration
        {
            /// <summary>仅含本次已声明输出文件的恢复事务；未知版本不执行。</summary>
            [Serializable]
            private sealed class RecoveryTransaction
            {
                public int Version = 1;
                public string OwnerGuid;
                public string OutputFolder;
                public string State = "Pending";
                public List<FileSnapshot> Files = new List<FileSnapshot>();
            }

            /// <summary>一个精确输出文件的原字节备份和最后已知磁盘状态。</summary>
            [Serializable]
            private sealed class FileSnapshot
            {
                public string AssetPath;
                public string BackupName;
                public string BeforeHash;
                public string ExpectedHash;
            }

            /// <summary>
            /// 显式恢复本配置遗留的未完成事务；无事务时无动作。
            /// 仅从已保存配置 GUID 定位备份，不接受任意恢复路径；输出发生外部变更或备份损坏时拒绝覆盖并保留现场。
            /// 已提交或已恢复的终态事务只核验并继续清理，不重新回滚，也不要求已删除的备份仍存在。
            /// </summary>
            /// <param name="settingsAssetPath">拥有该事务的已保存项目配置路径。</param>
            public static void Recover(string settingsAssetPath)
            {
                Recover(settingsAssetPath, null, null);
            }

            /// <summary>共用实际恢复入口；内部观察点只在真实导入或删除完成后执行，不注册全局回调。</summary>
            /// <param name="settingsAssetPath">事务所属配置路径。</param>
            /// <param name="afterRecoveryImported">真实恢复导入返回后的观察点。</param>
            /// <param name="afterBackupDeleted">真实删除单个备份后的观察点。</param>
            internal static void Recover(string settingsAssetPath, Action<string> afterRecoveryImported,
                Action<string> afterBackupDeleted)
            {
                EnsureIdle();
                string path = ValidateAssetPath(settingsAssetPath, false);
                LoadSettings(path, out string ownerGuid);
                string directory = RecoveryDirectory(ownerGuid);
                if (!Directory.Exists(directory)) return;
                s_IsApplying = true;
                try
                {
                    RecoveryTransaction transaction = ReadRecovery(directory, ownerGuid);
                    if (transaction.State == "Pending") Restore(transaction, directory, afterRecoveryImported);
                    CleanupTerminal(ownerGuid, directory, afterBackupDeleted);
                }
                finally { s_IsApplying = false; }
            }

            /// <summary>生成前拒绝遗留恢复状态；不自动重放未知结果或删除旧备份。</summary>
            /// <param name="ownerGuid">当前配置 GUID。</param>
            private static void RequireNoRecovery(string ownerGuid)
            {
                string directory = RecoveryDirectory(ownerGuid);
                if (Directory.Exists(directory))
                    throw new InvalidOperationException("存在未完成图集生成事务，请先显式恢复并核对：" + directory);
            }

            /// <summary>从当前工程与精确配置 GUID 定位唯一事务目录，不使用用户提供的备份路径。</summary>
            /// <param name="ownerGuid">当前配置 GUID。</param>
            /// <returns>配置专属的 Library 绝对路径。</returns>
            private static string RecoveryDirectory(string ownerGuid)
            {
                if (string.IsNullOrEmpty(ownerGuid) || ownerGuid.Length != 32 || ownerGuid.Any(character => !Uri.IsHexDigit(character)))
                    throw new InvalidOperationException("图集配置 GUID 不合法，不能定位恢复事务。");
                string current = ProjectRoot();
                foreach (string segment in new[] { "Library", "Nova", "SpriteAtlasGeneration", ownerGuid })
                {
                    current = Path.Combine(current, segment);
                    if ((File.Exists(current) || Directory.Exists(current)) &&
                        (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("图集恢复目录不能经过软链：" + current);
                }
                return current;
            }

            /// <summary>为所有受影响输出建立原字节备份，逐图集提交，失败时恢复且不吞掉原错误。</summary>
            /// <param name="plan">刚刚完成漂移复核的真实计划。</param>
            /// <param name="afterOutputWritten">内部提交观察点。</param>
            /// <param name="afterRecoveryImported">真实恢复导入返回后的观察点。</param>
            /// <param name="afterBackupDeleted">真实删除单个备份后的观察点。</param>
            private static void Commit(ResolvedPlan plan, Action<string> afterOutputWritten,
                Action<string> afterRecoveryImported, Action<string> afterBackupDeleted)
            {
                string directory = RecoveryDirectory(plan.PublicPlan.SettingsGuid);
                var paths = plan.Rules.Where(rule => rule.Entry.Change != SpriteAtlasGenerationChange.Unchanged)
                    .Select(rule => rule.Entry.OutputPath).Concat(new[] { plan.ManifestPath }).ToArray();
                RecoveryTransaction transaction = CreateRecovery(plan, paths, directory);
                string activeWrite = null;
                try
                {
                    foreach (ResolvedRule resolved in plan.Rules.Where(rule => rule.Entry.Change != SpriteAtlasGenerationChange.Unchanged))
                    {
                        RequireStableInputs(plan);
                        RequireExpectedOutputs(transaction);
                        activeWrite = resolved.Entry.OutputPath;
                        WriteAtlas(plan.Settings, resolved);
                        CaptureExpected(transaction, directory, activeWrite);
                        activeWrite = null;
                        afterOutputWritten?.Invoke(resolved.Entry.OutputPath);
                        if (resolved.Previous == null)
                        {
                            resolved.Previous = new ManifestRecord
                            {
                                RuleId = resolved.Rule.Id,
                                OutputPath = resolved.Entry.OutputPath,
                                Guid = AssetDatabase.AssetPathToGUID(resolved.Entry.OutputPath),
                            };
                            plan.Manifest.Outputs.Add(resolved.Previous);
                        }
                        resolved.Previous.SourceFingerprint = resolved.SourceFingerprint;
                        resolved.Previous.ContentFingerprint = resolved.ContentFingerprint;
                        resolved.Previous.FileHash = HashFile(resolved.Entry.OutputPath);
                        resolved.Previous.MetaHash = HashFile(resolved.Entry.OutputPath + ".meta");
                    }
                    RequireStableInputs(plan);
                    RequireExpectedOutputs(transaction);
                    plan.Manifest.Outputs = plan.Manifest.Outputs.OrderBy(record => record.OutputPath, StringComparer.Ordinal).ToList();
                    activeWrite = plan.ManifestPath;
                    File.WriteAllText(Absolute(plan.ManifestPath), JsonUtility.ToJson(plan.Manifest, true) + "\n");
                    AssetDatabase.ImportAsset(plan.ManifestPath, ImportAssetOptions.ForceSynchronousImport);
                    CaptureExpected(transaction, directory, activeWrite);
                    activeWrite = null;
                    RequireStableInputs(plan);
                    RequireExpectedOutputs(transaction);
                }
                catch (Exception original)
                {
                    try
                    {
                        // 原生保存抛错也可能已经写出部分文件，先冻结此刻实际状态再精确回滚。
                        if (activeWrite != null) CaptureExpected(transaction, directory, activeWrite);
                        Restore(transaction, directory, afterRecoveryImported);
                    }
                    catch (Exception recovery)
                    {
                        throw new InvalidOperationException("图集生成失败且恢复尚未完成；保留备份，禁止继续生成：" + directory,
                            new AggregateException(original, recovery));
                    }
                    try { CleanupTerminal(transaction.OwnerGuid, directory, afterBackupDeleted); }
                    catch (Exception cleanup)
                    {
                        throw new InvalidOperationException("图集生成失败，输出已恢复；备份清理未完成，请显式恢复继续清理：" + directory,
                            new AggregateException(original, cleanup));
                    }
                    throw new InvalidOperationException("图集生成失败，本次输出已恢复原状。", original);
                }

                // 终态持久化之后只允许核验和清理；清理异常绝不能重新进入上面的生成回滚块。
                transaction.State = "Committed";
                try { SaveRecovery(transaction, directory); }
                catch (Exception marker)
                {
                    throw new InvalidOperationException("图集输出已写入，但提交状态尚未确认；保留事务，请显式恢复核对：" + directory, marker);
                }
                try { CleanupTerminal(transaction.OwnerGuid, directory, afterBackupDeleted); }
                catch (Exception cleanup)
                {
                    throw new InvalidOperationException("图集输出已提交，备份清理未完成；不会回滚输出，请显式恢复继续清理：" + directory, cleanup);
                }
            }

            /// <summary>同步导入回调返回后再次核对配置、全局模式和全部源输入，避免把执行中变化登记成成功。</summary>
            /// <param name="plan">提交前冻结的实际输入。</param>
            private static void RequireStableInputs(ResolvedPlan plan)
            {
                string settingsPath = plan.PublicPlan.SettingsAssetPath;
                if (EditorUtility.IsDirty(plan.Settings) || HashFile(settingsPath) != plan.SettingsFileHash ||
                    HashFile(settingsPath + ".meta") != plan.SettingsMetaHash ||
                    HashText(JsonUtility.ToJson(plan.Settings)) != plan.SettingsModelHash || EditorSettings.spritePackerMode != plan.PackerMode)
                    throw new InvalidOperationException("图集生成期间配置或 Sprite Packer 模式发生变化，不能登记成功。");
                foreach (ResolvedRule rule in plan.Rules)
                {
                    var sources = new List<SpriteAtlasGenerationSource>();
                    string fingerprint = ReadSources(rule.Roots, rule.Exclusions, sources, new List<UnityEngine.Object>());
                    if (fingerprint != rule.SourceFingerprint ||
                        BuildContentFingerprint(rule.Rule, plan.Settings.Format, sources) != rule.ContentFingerprint)
                        throw new InvalidOperationException("图集生成期间源图片或规则发生变化：" + rule.Entry.OutputPath);
                }
            }

            /// <summary>写下一个输出前核对整个事务目标，未由当前写入阶段改动的文件不能被回调偷偷改写。</summary>
            /// <param name="transaction">本次事务冻结的输出状态。</param>
            private static void RequireExpectedOutputs(RecoveryTransaction transaction)
            {
                foreach (FileSnapshot snapshot in transaction.Files)
                {
                    if (Directory.Exists(Absolute(snapshot.AssetPath)) || HashFile(snapshot.AssetPath) != snapshot.ExpectedHash)
                        throw new InvalidOperationException("图集生成期间目标发生外部变化：" + snapshot.AssetPath);
                }
            }

            /// <summary>提交前冻结现有输出和 meta 的精确字节；创建失败不会改动任何正式输出。</summary>
            /// <param name="plan">本次已核对计划。</param>
            /// <param name="assetPaths">受影响图集与清单路径。</param>
            /// <param name="directory">当前配置专属恢复目录。</param>
            /// <returns>已经落盘的备份事务。</returns>
            private static RecoveryTransaction CreateRecovery(ResolvedPlan plan, IEnumerable<string> assetPaths, string directory)
            {
                if (Directory.Exists(directory)) throw new InvalidOperationException("图集恢复目录已存在：" + directory);
                var transaction = new RecoveryTransaction
                {
                    OwnerGuid = plan.PublicPlan.SettingsGuid,
                    OutputFolder = plan.PublicPlan.OutputFolder,
                };
                Directory.CreateDirectory(directory);
                try
                {
                    foreach (string path in assetPaths.SelectMany(path => new[] { path, path + ".meta" })
                                 .Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
                    {
                        string beforeHash = HashFile(path);
                        string backupName = transaction.Files.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin";
                        if (!string.IsNullOrEmpty(beforeHash)) File.Copy(Absolute(path), Path.Combine(directory, backupName));
                        transaction.Files.Add(new FileSnapshot
                        {
                            AssetPath = path, BackupName = backupName, BeforeHash = beforeHash, ExpectedHash = beforeHash,
                        });
                    }
                    SaveRecovery(transaction, directory);
                    return transaction;
                }
                catch
                {
                    Directory.Delete(directory, true);
                    throw;
                }
            }

            /// <summary>每个写入阶段之后保存实际输出 Hash；后续恢复据此拒绝覆盖外部新改动。</summary>
            /// <param name="transaction">当前事务。</param>
            /// <param name="directory">固定恢复目录。</param>
            /// <param name="writtenAsset">只更新生成阶段当前明确写入的资产与 meta；恢复阶段不得调用此入口。</param>
            private static void CaptureExpected(RecoveryTransaction transaction, string directory, string writtenAsset)
            {
                if (string.IsNullOrEmpty(writtenAsset)) throw new ArgumentException("必须明确当前写入的图集或清单。", nameof(writtenAsset));
                foreach (FileSnapshot snapshot in transaction.Files.Where(snapshot =>
                             snapshot.AssetPath == writtenAsset || snapshot.AssetPath == writtenAsset + ".meta"))
                    snapshot.ExpectedHash = HashFile(snapshot.AssetPath);
                SaveRecovery(transaction, directory);
            }

            /// <summary>以临时文件原子替换恢复描述；备份文件仍保持原字节。</summary>
            /// <param name="transaction">当前恢复描述。</param>
            /// <param name="directory">已核对固定目录。</param>
            private static void SaveRecovery(RecoveryTransaction transaction, string directory)
            {
                string target = Path.Combine(directory, "transaction.json");
                string temporary = target + ".tmp";
                File.WriteAllText(temporary, JsonUtility.ToJson(transaction, true) + "\n");
                if (File.Exists(target)) File.Replace(temporary, target, null);
                else File.Move(temporary, target);
            }

            /// <summary>读取配置自身事务，严格核对 owner、输出根、文件白名单与备份名，防止篡改造成越界恢复。</summary>
            /// <param name="directory">按配置 GUID 推导的固定目录。</param>
            /// <param name="ownerGuid">当前配置 GUID。</param>
            /// <returns>经过路径与版本检查的事务。</returns>
            private static RecoveryTransaction ReadRecovery(string directory, string ownerGuid)
            {
                string file = Path.Combine(directory, "transaction.json");
                if (!File.Exists(file)) throw new InvalidOperationException("恢复描述缺失，请保留现场并核对备份：" + directory);
                RecoveryTransaction transaction = JsonUtility.FromJson<RecoveryTransaction>(File.ReadAllText(file));
                if (transaction == null || transaction.Version != 1 || transaction.OwnerGuid != ownerGuid || transaction.Files == null)
                    throw new InvalidOperationException("恢复描述版本或配置 GUID 不一致：" + directory);
                // 本轮早期未带状态字段的同版本事务只能按未提交处理，不能推断成成功终态。
                if (string.IsNullOrEmpty(transaction.State)) transaction.State = "Pending";
                if (transaction.State != "Pending" && transaction.State != "Committed" && transaction.State != "Restored")
                    throw new InvalidOperationException("图集恢复描述包含未知状态：" + directory);
                string root = ValidateAssetPath(transaction.OutputFolder, true);
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int index = 0; index < transaction.Files.Count; index++)
                {
                    FileSnapshot snapshot = transaction.Files[index];
                    if (snapshot == null) throw new InvalidOperationException("恢复文件记录为空：" + directory);
                    string path = ValidateAssetPath(snapshot.AssetPath, false);
                    string basePath = path.EndsWith(".meta", StringComparison.Ordinal) ? path.Substring(0, path.Length - 5) : path;
                    if (!string.Equals(Path.GetDirectoryName(basePath)?.Replace('\\', '/'), root, StringComparison.Ordinal) ||
                        !(basePath.EndsWith(".spriteatlas", StringComparison.Ordinal) || basePath.EndsWith(".spriteatlasv2", StringComparison.Ordinal) ||
                          string.Equals(basePath, root + "/" + c_ManifestName, StringComparison.Ordinal)) || !seen.Add(path) ||
                        snapshot.BackupName != index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".bin" ||
                        snapshot.BeforeHash == null || snapshot.ExpectedHash == null)
                        throw new InvalidOperationException("恢复描述包含未声明、重复或越界文件：" + path);
                    string backup = Path.Combine(directory, snapshot.BackupName);
                    if ((File.Exists(backup) || Directory.Exists(backup)) &&
                        (File.GetAttributes(backup) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("图集恢复备份不能是软链：" + path);
                }
                return transaction;
            }

            /// <summary>先一次性验证所有原备份和最后已知输出，再恢复原字节并重新导入；不保存其他脏资产。</summary>
            /// <param name="transaction">已经核对归属与路径的恢复描述。</param>
            /// <param name="directory">当前配置的固定恢复目录。</param>
            /// <param name="afterRecoveryImported">真实导入或新资产注销返回后的内部观察点；跨目标变化保留现场。</param>
            private static void Restore(RecoveryTransaction transaction, string directory, Action<string> afterRecoveryImported)
            {
                // 自动回滚也走同一描述校验，不能靠内存对象绕过恢复路径白名单。
                transaction = ReadRecovery(directory, transaction.OwnerGuid);
                if (transaction.State != "Pending") throw new InvalidOperationException("终态图集事务只允许核验和清理，不能重新回滚。");
                foreach (FileSnapshot snapshot in transaction.Files)
                {
                    if (Directory.Exists(Absolute(snapshot.AssetPath)))
                        throw new InvalidOperationException("待恢复文件被目录占用，拒绝删除目录：" + snapshot.AssetPath);
                    string currentHash = HashFile(snapshot.AssetPath);
                    if (!string.Equals(currentHash, snapshot.ExpectedHash, StringComparison.Ordinal) &&
                        !string.Equals(currentHash, snapshot.BeforeHash, StringComparison.Ordinal))
                        throw new InvalidOperationException("待恢复输出已发生外部变更，拒绝覆盖：" + snapshot.AssetPath);
                    if (!string.IsNullOrEmpty(snapshot.BeforeHash) && !string.Equals(
                            HashAbsoluteFile(Path.Combine(directory, snapshot.BackupName)), snapshot.BeforeHash, StringComparison.Ordinal))
                        throw new InvalidOperationException("图集原字节备份缺失或损坏：" + snapshot.AssetPath);
                    if (currentHash == snapshot.BeforeHash) snapshot.ExpectedHash = snapshot.BeforeHash;
                }
                // 仅将已经独立证实为所需原字节的文件登记为已恢复；未知摘要在上面的全量预检中直接失败。
                SaveRecovery(transaction, directory);
                var removedNewFiles = new HashSet<string>(StringComparer.Ordinal);
                foreach (FileSnapshot snapshot in transaction.Files.Where(snapshot =>
                             !snapshot.AssetPath.EndsWith(".meta", StringComparison.Ordinal) && snapshot.BeforeHash.Length == 0))
                {
                    FileSnapshot meta = transaction.Files.SingleOrDefault(candidate => candidate.AssetPath == snapshot.AssetPath + ".meta");
                    if (meta == null || meta.BeforeHash.Length != 0)
                        throw new InvalidOperationException("新资产的原始文件与 meta 状态不成对，拒绝删除：" + snapshot.AssetPath);
                    RequireExpectedOutputs(transaction);
                    // 必须先从 AssetDatabase 注销仍在磁盘上的新资产；先删实物再通知会留下已加载的导入缓存。
                    if (File.Exists(Absolute(snapshot.AssetPath)) || File.Exists(Absolute(meta.AssetPath)) ||
                        AssetDatabase.LoadMainAssetAtPath(snapshot.AssetPath) != null)
                    {
                        if (!AssetDatabase.DeleteAsset(snapshot.AssetPath))
                            throw new InvalidOperationException("无法精确注销本事务新建资产，保留恢复状态：" + snapshot.AssetPath);
                    }
                    if (File.Exists(Absolute(snapshot.AssetPath)) || File.Exists(Absolute(meta.AssetPath)) ||
                        Directory.Exists(Absolute(snapshot.AssetPath)) || Directory.Exists(Absolute(meta.AssetPath)))
                        throw new InvalidOperationException("新资产注销后仍有文件或 meta，不能登记恢复成功：" + snapshot.AssetPath);
                    // 仅登记这一对已证实不存在的路径；删除回调改动其他目标时，不吸收其新摘要。
                    snapshot.ExpectedHash = snapshot.BeforeHash;
                    meta.ExpectedHash = meta.BeforeHash;
                    SaveRecovery(transaction, directory);
                    if (AssetDatabase.LoadMainAssetAtPath(snapshot.AssetPath) != null)
                        throw new InvalidOperationException("新资产导入缓存尚未注销，保留恢复状态：" + snapshot.AssetPath);
                    afterRecoveryImported?.Invoke(snapshot.AssetPath);
                    RequireExpectedOutputs(transaction);
                    removedNewFiles.Add(snapshot.AssetPath);
                    removedNewFiles.Add(meta.AssetPath);
                }
                foreach (FileSnapshot snapshot in transaction.Files.Where(snapshot => !removedNewFiles.Contains(snapshot.AssetPath)))
                {
                    // 当前已是所需原字节时无需重写；这也允许崩溃发生在复制完成、登记之前的安全续接。
                    if (HashFile(snapshot.AssetPath) != snapshot.BeforeHash)
                    {
                        RequireExpectedOutputs(transaction);
                        string target = Absolute(snapshot.AssetPath);
                        if (string.IsNullOrEmpty(snapshot.BeforeHash))
                        {
                            if (File.Exists(target)) File.Delete(target);
                        }
                        else File.Copy(Path.Combine(directory, snapshot.BackupName), target, true);
                    }
                    if (HashFile(snapshot.AssetPath) != snapshot.BeforeHash)
                        throw new InvalidOperationException("图集恢复写入未达到已知原字节，保留现场：" + snapshot.AssetPath);
                    // 只登记本次单文件已经达到的预先已知原字节，绝不从其他目标的当前内容吸收摘要。
                    snapshot.ExpectedHash = snapshot.BeforeHash;
                    SaveRecovery(transaction, directory);
                }
                foreach (string path in transaction.Files.Select(snapshot => snapshot.AssetPath)
                             .Where(path => !path.EndsWith(".meta", StringComparison.Ordinal) && !removedNewFiles.Contains(path)))
                {
                    RequireExpectedOutputs(transaction);
                    if (File.Exists(Absolute(path)))
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    else AssetDatabase.DeleteAsset(path);
                    afterRecoveryImported?.Invoke(path);
                    RequireExpectedOutputs(transaction);
                }
                RequireExpectedOutputs(transaction);
                transaction.State = "Restored";
                SaveRecovery(transaction, directory);
            }

            /// <summary>终态事务只核对成功输出并清理已知备份；部分删除失败不触发回滚，也不要求已删备份仍存在。</summary>
            /// <param name="ownerGuid">事务所属配置 GUID。</param>
            /// <param name="directory">当前配置固定事务目录。</param>
            /// <param name="afterBackupDeleted">单个真实备份删除后的内部观察点。</param>
            private static void CleanupTerminal(string ownerGuid, string directory, Action<string> afterBackupDeleted)
            {
                RecoveryTransaction transaction = ReadRecovery(directory, ownerGuid);
                if (transaction.State != "Committed" && transaction.State != "Restored")
                    throw new InvalidOperationException("只有已提交或已恢复的事务才允许删除备份。");
                RequireExpectedOutputs(transaction);
                var allowedNames = new HashSet<string>(transaction.Files.Select(snapshot => snapshot.BackupName), StringComparer.Ordinal)
                    { "transaction.json", "transaction.json.tmp" };
                foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (!File.Exists(entry) || !allowedNames.Contains(Path.GetFileName(entry)) ||
                        (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("图集终态目录含未知材料，保留现场：" + entry);
                }
                foreach (FileSnapshot snapshot in transaction.Files)
                {
                    string backup = Path.Combine(directory, snapshot.BackupName);
                    if (!File.Exists(backup)) continue;
                    RequireExpectedOutputs(transaction);
                    File.Delete(backup);
                    afterBackupDeleted?.Invoke(backup);
                }
                RequireExpectedOutputs(transaction);
                string temporary = Path.Combine(directory, "transaction.json.tmp");
                if (File.Exists(temporary)) File.Delete(temporary);
                File.Delete(Path.Combine(directory, "transaction.json"));
                try { Directory.Delete(directory, false); }
                catch (Exception deletion)
                {
                    // 最后删空目录失败时尽力保留终态描述；即使描述无法补回，也绝不自动回滚或重放生成。
                    try { if (Directory.Exists(directory)) SaveRecovery(transaction, directory); }
                    catch (Exception marker) { throw new AggregateException(deletion, marker); }
                    throw;
                }
            }
        }
    }
}
