/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.DesignTimeEditing.cs
 * author:    taoye
 * created:   2026/10/12
 * descrip:   ConfigMaster 工作副本统一归一、保存与 UI 适配器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>窗口与受控 Action 共用的设计态保存服务；不导出运行配置、不承担 UI。</summary>
            internal static class DesignTimeEditing
            {
                private static readonly List<Func<ConfigMasterSO, bool>> s_DraftGuards = new();
                private static readonly List<Action<ConfigMasterSO>> s_SavedListeners = new();

                /// <summary>显式选择保存范围和旧资源密钥迁移收尾；默认只保存目标且不清理旧文件。</summary>
                internal sealed class SaveOptions
                {
                    public bool SaveAllAssets;
                    public bool CompleteLegacyMigration;
                }

                /// <summary>正式配置已保存；迁移清理失败不撤销已经完成的保存。</summary>
                internal sealed class SaveResult
                {
                    public bool Committed;
                    public string MigrationWarning;
                }

                /// <summary>携带真实资产写入是否已开始；写入开始后不得向调用者声称无变化。</summary>
                internal sealed class DesignTimeSaveException : Exception
                {
                    public bool WriteStarted { get; }
                    public string FailureStage { get; }

                    /// <summary>保存稳定失败阶段和写入边界；原异常仅供本地窗口诊断，不用于公开 Action 输出。</summary>
                    internal DesignTimeSaveException(bool writeStarted, string failureStage, Exception inner)
                        : base("设计态配置保存失败。", inner)
                    {
                        WriteStarted = writeStarted;
                        FailureStage = failureStage;
                    }
                }

                /// <summary>注册 UI 草稿检测适配器；同一委托不会重复注册。</summary>
                internal static void RegisterDraftGuard(Func<ConfigMasterSO, bool> guard)
                {
                    if (guard == null) throw new ArgumentNullException(nameof(guard));
                    if (!s_DraftGuards.Contains(guard)) s_DraftGuards.Add(guard);
                }

                /// <summary>注册保存后的展示刷新适配器；工具层不引用任何 EditorWindow。</summary>
                internal static void RegisterSavedListener(Action<ConfigMasterSO> listener)
                {
                    if (listener == null) throw new ArgumentNullException(nameof(listener));
                    if (!s_SavedListeners.Contains(listener)) s_SavedListeners.Add(listener);
                }

                /// <summary>只读询问全部适配器；检测失败也拒绝覆盖，以免丢失未知草稿。</summary>
                internal static bool HasUnsavedDraft(ConfigMasterSO master)
                {
                    foreach (Func<ConfigMasterSO, bool> guard in s_DraftGuards)
                    {
                        try { if (guard(master)) return true; }
                        catch { return true; }
                    }
                    return false;
                }

                /// <summary>保存后通知 UI 和既有 Inspector；监听失败作为警告，不重做保存。</summary>
                internal static string[] NotifyMasterSaved(ConfigMasterSO master)
                {
                    var warnings = new List<string>();
                    foreach (Action<ConfigMasterSO> listener in s_SavedListeners.ToArray())
                    {
                        try { listener(master); }
                        catch { warnings.Add("配置已保存，但一个编辑界面刷新失败，请重新打开相关界面。"); }
                    }
                    try { Events.NotifyActiveConfigMasterSaved(master); }
                    catch { warnings.Add("配置已保存，但一个配置监听器刷新失败。"); }
                    return warnings.ToArray();
                }

                /// <summary>仅在工作副本上复用统一归一与资源密钥校验；不保存、不生成源码。</summary>
                internal static void NormalizeWorkingCopy(
                    ConfigMasterSO workingCopy, SerializedObject workingCopySO, DimensionProjector.Coord coordinate)
                {
                    if (workingCopy == null) throw new ArgumentNullException(nameof(workingCopy));
                    if (EditorUtility.IsPersistent(workingCopy))
                        throw new InvalidOperationException("归一必须使用未持久化的 ConfigMaster 工作副本。");
                    if (workingCopySO != null && (workingCopySO.targetObjects.Length != 1 ||
                        workingCopySO.targetObject != workingCopy))
                        throw new InvalidOperationException("SerializedObject 必须只绑定当前 ConfigMaster 工作副本。");
                    try
                    {
                        workingCopySO?.ApplyModifiedProperties();
                        DimensionProjector.NormalizeForSave(workingCopy, workingCopySO, coordinate);
                    }
                    catch (Exception exception) { throw new DesignTimeSaveException(false, "normalize", exception); }
                    try { ResourceKeyStore.Validate(workingCopy.ResourceEncryption); }
                    catch (Exception exception) { throw new DesignTimeSaveException(false, "resource-key-validation", exception); }
                }

                /// <summary>统一保存链；归一及 beforeCommit 在 Copy 前完成，正式写入后的失败携带 WriteStarted。</summary>
                internal static SaveResult SaveWorkingCopy(
                    ConfigMasterSO master, ConfigMasterSO workingCopy, SerializedObject workingCopySO,
                    DimensionProjector.Coord coordinate, SaveOptions options,
                    Action<ConfigMasterSO> beforeCommit = null)
                {
                    bool writeStarted = false;
                    string stage = "prepare";
                    try
                    {
                        if (master == null || !EditorUtility.IsPersistent(master) || ReferenceEquals(master, workingCopy))
                            throw new InvalidOperationException("保存需要真实 ConfigMaster 资产和独立工作副本。");
                        if (options == null) throw new ArgumentNullException(nameof(options));
                        NormalizeWorkingCopy(workingCopy, workingCopySO, coordinate);
                        stage = "before-commit";
                        beforeCommit?.Invoke(workingCopy);
                        string assetPath = AssetDatabase.GetAssetPath(master);
                        if (string.IsNullOrEmpty(assetPath)) throw new InvalidOperationException("ConfigMaster 资产路径为空。");
                        UnityEngine.HideFlags originalHideFlags = master.hideFlags;
                        writeStarted = true;
                        stage = "commit";
                        EditorUtility.CopySerialized(workingCopy, master);
                        master.hideFlags = originalHideFlags;
                        master.name = Path.GetFileNameWithoutExtension(assetPath);
                        EditorUtility.SetDirty(master);
                        if (options.SaveAllAssets) AssetDatabase.SaveAssets();
                        else AssetDatabase.SaveAssetIfDirty(master);
                        if (EditorUtility.IsDirty(master)) throw new InvalidOperationException("保存后 ConfigMaster 仍有未落盘改动。");
                        var result = new SaveResult { Committed = true };
                        if (options.CompleteLegacyMigration)
                        {
                            try { ResourceKeyStore.CompleteLegacyMigration(master); }
                            catch (Exception exception) { result.MigrationWarning = exception.Message; }
                        }
                        return result;
                    }
                    catch (DesignTimeSaveException) { throw; }
                    catch (Exception exception)
                    {
                        throw new DesignTimeSaveException(writeStarted, stage, exception);
                    }
                }
            }
        }
    }
}
