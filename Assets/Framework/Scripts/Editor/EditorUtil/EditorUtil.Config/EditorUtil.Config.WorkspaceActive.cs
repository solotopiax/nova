/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.WorkspaceActive.cs
 * author:    taoye
 * created:   2026/5/28
 * descrip:   工程级活动工作区锚点；统一持久化 ConfigMaster 与 PipifySettings，
 *            进入 Sample 时备份业务绑定，离开 Sample 时确定性恢复。
 ***************************************************************/

using System;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// Config 编辑侧事件集。
            /// </summary>
            public static class Events
            {
                /// <summary>
                /// 当前激活 ConfigMaster 保存成功后触发，供 Inspector 等编辑器界面刷新派生视图。
                /// </summary>
                public static event Action<ConfigMasterSO> ActiveConfigMasterSaved;

                /// <summary>
                /// 通知当前激活 ConfigMaster 已保存。
                /// </summary>
                /// <param name="master">已保存的 ConfigMaster。</param>
                internal static void NotifyActiveConfigMasterSaved(ConfigMasterSO master)
                {
                    ActiveConfigMasterSaved?.Invoke(master);
                }
            }
            /// <summary>
            /// 工程级活动工作区锚点。
            /// <para>通过 ProjectSettings/Nova/Globals.json 同时持久化当前 ConfigMaster、PipifySettings，</para>
            /// <para>以及进入 Sample 前的业务绑定，使 Editor、构建入口和 CI 读取同一份确定性状态。</para>
            /// </summary>
            public static class WorkspaceActive
            {
                /// <summary>
                /// Globals.json 相对工程根目录的路径。
                /// </summary>
                private const string c_GlobalsRelPath = "ProjectSettings/Nova/Globals.json";

                /// <summary>
                /// Globals.json 的序列化模型。
                /// <para>字段名即 JSON key，刻意保持 camelCase 以便 Globals.json 文件人工读写时形如 {"configMasterGuid": "..."}，不遵循公有字段首字母大写约定。</para>
                /// </summary>
                [Serializable]
                private sealed class GlobalsJson
                {
                    /// <summary>
                    /// 当前工作区 JSON 结构版本。
                    /// </summary>
                    public int schemaVersion;

                    /// <summary>
                    /// 当前激活 ConfigMaster 的 AssetDatabase GUID。
                    /// </summary>
                    public string configMasterGuid;

                    /// <summary>
                    /// 当前激活 ConfigMaster 的 Assets 相对路径，仅用于身份校验与人工排查。
                    /// </summary>
                    public string configMasterPathHint;

                    /// <summary>
                    /// 当前激活 PipifySettings 的 AssetDatabase GUID。
                    /// </summary>
                    public string pipifySettingsGuid;

                    /// <summary>
                    /// 当前激活 PipifySettings 的 Assets 相对路径，仅用于身份校验与人工排查。
                    /// </summary>
                    public string pipifySettingsPathHint;

                    /// <summary>
                    /// 进入 Sample 前最后一次明确的非 Sample ConfigMaster GUID。
                    /// </summary>
                    public string projectConfigMasterGuid;

                    /// <summary>
                    /// 进入 Sample 前最后一次明确的非 Sample ConfigMaster 路径。
                    /// </summary>
                    public string projectConfigMasterPathHint;

                    /// <summary>
                    /// 进入 Sample 前最后一次明确的非 Sample PipifySettings GUID。
                    /// </summary>
                    public string projectPipifySettingsGuid;

                    /// <summary>
                    /// 进入 Sample 前最后一次明确的非 Sample PipifySettings 路径。
                    /// </summary>
                    public string projectPipifySettingsPathHint;

                    /// <summary>
                    /// 当前由场景自动激活的 Sample 根目录；空值表示当前不处于 Sample 会话。
                    /// </summary>
                    public string activeSampleRoot;
                }

                /// <summary>
                /// 当前 Globals 工作区结构版本。
                /// </summary>
                private const int c_CurrentSchemaVersion = 2;

                /// <summary>
                /// 读取 Globals 当前激活的 ConfigMasterSO，失败返回 null。
                /// 该方法不根据 Scene 改写工作区；场景迁移统一由 <see cref="ReconcileScene"/> 完成。
                /// </summary>
                /// <returns>当前激活的 ConfigMasterSO 实例；无法解析时返回 null。</returns>
                public static ConfigMasterSO Get()
                {
                    if (!TryLoadGlobals(out GlobalsJson globals, out string globalsPath)) return null;
                    ConfigMasterSO master = LoadAssetByGuid<ConfigMasterSO>(globals.configMasterGuid, out string assetPath);
                    bool changed = RepairPathHint(ref globals.configMasterPathHint, assetPath);
                    if (changed && !WriteGlobals(globalsPath, globals)) return null;
                    return master;
                }

                /// <summary>
                /// 获取当前活动工作区绑定的 PipifySettingsSO。
                /// 与 <see cref="Get"/> 共用同一场景路由事务，确保 Master 与 Pipify 不会分别恢复。
                /// </summary>
                /// <returns>当前 PipifySettings；未绑定或资产失效时返回 null。</returns>
                internal static PipifySettingsSO GetPipifySettings()
                {
                    if (!TryLoadGlobals(out GlobalsJson globals, out string globalsPath)) return null;
                    PipifySettingsSO settings = LoadAssetByGuid<PipifySettingsSO>(globals.pipifySettingsGuid, out string assetPath);
                    bool changed = RepairPathHint(ref globals.pipifySettingsPathHint, assetPath);
                    if (changed && !WriteGlobals(globalsPath, globals)) return null;
                    return settings;
                }

                /// <summary>
                /// 根据已保存的 Single Scene 路径协调工作区并原子写入 Globals。
                /// 该入口由 SceneRoute 和窗口刷新共同调用；重复调用幂等，空路径不改变状态。
                /// </summary>
                /// <param name="scenePath">已打开 Scene 的项目相对路径。</param>
                /// <returns>协调成功返回 true；Globals 无法解析或写入失败返回 false。</returns>
                internal static bool ReconcileScene(string scenePath)
                {
                    if (!TryLoadGlobals(out GlobalsJson globals, out string globalsPath)) return false;
                    bool changed = RouteForScene(globals, scenePath);
                    return !changed || WriteGlobals(globalsPath, globals);
                }

                /// <summary>
                /// 只读解析 Globals 中当前激活的 ConfigMaster，供构建计划与就绪检查共用。
                /// 不执行场景推断、不修复 pathHint，也不写文件。
                /// </summary>
                /// <param name="master">解析成功的 ConfigMaster。</param>
                /// <param name="guid">Globals 中记录的 GUID。</param>
                /// <param name="assetPath">GUID 当前解析出的资产路径。</param>
                /// <param name="error">失败原因。</param>
                /// <returns>GUID、pathHint 与资产身份完全一致时返回 true。</returns>
                internal static bool TryGetPersistedConfigMaster(
                    out ConfigMasterSO master,
                    out string guid,
                    out string assetPath,
                    out string error)
                {
                    master = null;
                    guid = null;
                    assetPath = null;
                    error = null;
                    if (!TryLoadGlobals(out GlobalsJson globals, out _, false))
                    {
                        error = "ProjectSettings/Nova/Globals.json 不存在或无法解析。";
                        return false;
                    }

                    guid = globals.configMasterGuid;
                    master = LoadAssetByGuid<ConfigMasterSO>(guid, out assetPath);
                    if (master == null)
                    {
                        error = "Globals.json 未绑定可加载的 ConfigMaster。";
                        return false;
                    }
                    if (!string.Equals(NormalizeAssetPath(globals.configMasterPathHint), assetPath, StringComparison.Ordinal))
                    {
                        error = "Globals.json 的 ConfigMaster GUID、pathHint 或资产身份已漂移。";
                        return false;
                    }
                    return true;
                }

                /// <summary>
                /// 只读解析 Globals 中当前激活的 PipifySettings，供 CLI 与构建前校验共用。
                /// 不执行场景推断、不修复 pathHint，也不写文件。
                /// </summary>
                /// <param name="settings">解析成功的 PipifySettings。</param>
                /// <param name="guid">Globals 中记录的 GUID。</param>
                /// <param name="assetPath">GUID 当前解析出的资产路径。</param>
                /// <param name="error">失败原因。</param>
                /// <returns>GUID、pathHint 与资产身份完全一致时返回 true。</returns>
                internal static bool TryGetPersistedPipifySettings(
                    out PipifySettingsSO settings,
                    out string guid,
                    out string assetPath,
                    out string error)
                {
                    settings = null;
                    guid = null;
                    assetPath = null;
                    error = null;
                    if (!TryLoadGlobals(out GlobalsJson globals, out _, false))
                    {
                        error = "ProjectSettings/Nova/Globals.json 不存在或无法解析。";
                        return false;
                    }

                    guid = globals.pipifySettingsGuid;
                    settings = LoadAssetByGuid<PipifySettingsSO>(guid, out assetPath);
                    if (settings == null)
                    {
                        error = "Globals.json 未绑定可加载的 PipifySettings。";
                        return false;
                    }
                    if (!string.Equals(NormalizeAssetPath(globals.pipifySettingsPathHint), assetPath, StringComparison.Ordinal))
                    {
                        error = "Globals.json 的 PipifySettings GUID、pathHint 或资产身份已漂移。";
                        return false;
                    }
                    return true;
                }

                /// <summary>
                /// 获取当前激活 ConfigMasterSO 所配对的 ConfigRuntimeSO。
                /// <para>经 WorkspaceActive.Get() 锚定激活 master（见 ADR-047），按以下顺序定位配对 ConfigRuntimeSO：</para>
                /// <para>① 首选 master.ExportTarget 序列化引用 —— 由 ConfigWindow 导出时记录，GUID 追踪，资产可置于任意位置，不强制布局；</para>
                /// <para>② ExportTarget 为 null 时，回退 ADR-033 布局约定（master 在 DemoRoot/Editor/ConfigMaster.asset，runtime 在 DemoRoot/Configs/ConfigRuntime.asset），从 masterPath 上溯两级拼路径加载，覆盖未配 ExportTarget 的老工程与新 sample。</para>
                /// <para>无激活 master 时 Warning 并返回 null（成因①）；</para>
                /// <para>ExportTarget 为 null 且 masterPath 为空时 Warning 并返回 null（成因②）；</para>
                /// <para>ExportTarget 为 null 且路径上溯层级不足（master 不在预期布局下）时 Warning 并返回 null（成因③）；</para>
                /// <para>ExportTarget 为 null 且布局约定下 ConfigRuntime.asset 不存在（未导出）时 Warning 并返回 null（成因④）。</para>
                /// </summary>
                /// <returns>激活 master 配对的 ConfigRuntimeSO；任一失败条件时返回 null。</returns>
                public static ConfigRuntimeSO GetActiveRuntime()
                {
                    ConfigMasterSO master = Get();
                    if (master == null)
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 无激活 ConfigMaster，无法定位 ConfigRuntime。");
                        return null;
                    }

                    // ① 首选 ExportTarget 序列化引用：放任意位置都成立，GUID 追踪资产移动
                    if (master.ExportTarget != null)
                    {
                        return master.ExportTarget;
                    }

                    // ② 兜底：ADR-033 布局约定（未配 ExportTarget 的老工程 / 新 sample）
                    return TryResolveByLayoutConvention(master);
                }

                /// <summary>
                /// 按 ADR-033 布局约定从 masterPath 上溯 DemoRoot，拼 Configs/ConfigRuntime.asset 加载。
                /// <para>仅作 ExportTarget 未配置时的兜底，不强制用户使用此布局。</para>
                /// </summary>
                /// <param name="master">已锚定的激活 ConfigMasterSO（调用方保证非 null）。</param>
                /// <returns>布局约定下配对的 ConfigRuntimeSO；任一失败条件时 Warning 并返回 null。</returns>
                private static ConfigRuntimeSO TryResolveByLayoutConvention(ConfigMasterSO master)
                {
                    string masterPath = AssetDatabase.GetAssetPath(master);
                    if (string.IsNullOrEmpty(masterPath))
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 激活 ConfigMaster 的 AssetDatabase 路径为空，无法按布局约定定位 ConfigRuntime。");
                        return null;
                    }

                    // master 在 <DemoRoot>/Editor/ConfigMaster.asset
                    // 上溯两级：去掉文件名 → <DemoRoot>/Editor；再去掉目录名 → <DemoRoot>
                    string editorDir = System.IO.Path.GetDirectoryName(masterPath)?.Replace('\\', '/');
                    string demoRoot = System.IO.Path.GetDirectoryName(editorDir)?.Replace('\\', '/');

                    if (string.IsNullOrEmpty(demoRoot))
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 激活 ConfigMaster 路径层级不足，无法上溯至 DemoRoot：{0}", masterPath);
                        return null;
                    }

                    string runtimePath = $"{demoRoot}/Configs/ConfigRuntime.asset";
                    ConfigRuntimeSO runtime = AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(runtimePath);
                    if (runtime == null)
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 激活 master 的 ConfigRuntime 未导出（布局约定兜底未命中）：{0}", runtimePath);
                        return null;
                    }

                    return runtime;
                }

                /// <summary>
                /// 显式设置激活 ConfigMasterSO，以 GUID + pathHint 原子写入 Globals.json。
                /// <para>master 为 null 时静默返回。</para>
                /// </summary>
                /// <param name="master">要设置为激活的 ConfigMasterSO 实例。</param>
                public static void Set(ConfigMasterSO master)
                {
                    TrySet(master);
                }

                /// <summary>
                /// 尝试持久化激活 ConfigMaster；仅在 Globals 原子写入成功时返回 true。
                /// 供 EditorWindow 在更新本地绑定前确认项目级锚点已经提交，避免 UI 与 Globals 分叉。
                /// </summary>
                /// <param name="master">要设置为激活的 ConfigMasterSO 实例。</param>
                /// <returns>资产有效且 Globals 写入成功返回 true；否则返回 false。</returns>
                internal static bool TrySet(ConfigMasterSO master)
                {
                    if (master == null) return false;
                    if (!TryLoadGlobals(out GlobalsJson globals, out string globalsPath, false))
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] Globals.json 无法解析，拒绝覆盖并设置 ConfigMaster。");
                        return false;
                    }

                    if (!SetAssetReference(master, ref globals.configMasterGuid, ref globals.configMasterPathHint))
                        return false;

                    if (!IsSampleAssetPath(globals.configMasterPathHint))
                    {
                        CopyReference(
                            globals.configMasterGuid,
                            globals.configMasterPathHint,
                            ref globals.projectConfigMasterGuid,
                            ref globals.projectConfigMasterPathHint);
                        globals.activeSampleRoot = string.Empty;
                    }

                    globals.schemaVersion = c_CurrentSchemaVersion;
                    if (!WriteGlobals(globalsPath, globals)) return false;
                    Log.Debug(LogTag.Editor, "[WorkspaceActive] 已设置激活 ConfigMaster：{0}（{1}）",
                        globals.configMasterPathHint, globals.configMasterGuid);
                    return true;
                }

                /// <summary>
                /// 显式设置当前工作区的 PipifySettings，并在非 Sample 资产时同步更新业务备份。
                /// </summary>
                /// <param name="settings">要绑定的 PipifySettings；null 表示显式清空当前绑定。</param>
                /// <returns>Globals 原子写入成功返回 true；解析或写入失败返回 false。</returns>
                internal static bool SetPipifySettings(PipifySettingsSO settings)
                {
                    if (!TryLoadGlobals(out GlobalsJson globals, out string globalsPath, false))
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] Globals.json 无法解析，拒绝覆盖并设置 PipifySettings。");
                        return false;
                    }

                    if (settings == null)
                    {
                        ClearReference(ref globals.pipifySettingsGuid, ref globals.pipifySettingsPathHint);
                        if (!IsSampleAssetPath(EditorSceneManager.GetActiveScene().path))
                        {
                            ClearReference(
                                ref globals.projectPipifySettingsGuid,
                                ref globals.projectPipifySettingsPathHint);
                        }
                    }
                    else
                    {
                        if (!SetAssetReference(settings, ref globals.pipifySettingsGuid, ref globals.pipifySettingsPathHint))
                            return false;
                        if (!IsSampleAssetPath(globals.pipifySettingsPathHint))
                        {
                            CopyReference(
                                globals.pipifySettingsGuid,
                                globals.pipifySettingsPathHint,
                                ref globals.projectPipifySettingsGuid,
                                ref globals.projectPipifySettingsPathHint);
                        }
                    }

                    globals.schemaVersion = c_CurrentSchemaVersion;
                    return WriteGlobals(globalsPath, globals);
                }

                /// <summary>
                /// 原子设置一组显式构建工作区，供 CLI/CI 在不依赖当前 Scene 的情况下冻结输入。
                /// Sample 资产必须位于同一个 Editor 目录；业务资产不得与 Sample 资产混配。
                /// </summary>
                /// <param name="master">显式 ConfigMaster。</param>
                /// <param name="settings">显式 PipifySettings。</param>
                /// <param name="error">失败原因。</param>
                /// <returns>验证并原子写入成功返回 true。</returns>
                internal static bool TrySetExplicitWorkspace(
                    ConfigMasterSO master,
                    PipifySettingsSO settings,
                    out string error)
                {
                    error = null;
                    if (master == null || settings == null)
                    {
                        error = "ConfigMaster 与 PipifySettings 都必须提供。";
                        return false;
                    }
                    if (!TryLoadGlobals(out GlobalsJson globals, out string globalsPath, false))
                    {
                        error = "Globals.json 无法解析，已拒绝覆盖。";
                        return false;
                    }

                    string masterPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(master));
                    string settingsPath = NormalizeAssetPath(AssetDatabase.GetAssetPath(settings));
                    string masterGuid = string.IsNullOrEmpty(masterPath) ? null : AssetDatabase.AssetPathToGUID(masterPath);
                    string settingsGuid = string.IsNullOrEmpty(settingsPath) ? null : AssetDatabase.AssetPathToGUID(settingsPath);
                    if (string.IsNullOrEmpty(masterGuid) || string.IsNullOrEmpty(settingsGuid))
                    {
                        error = "ConfigMaster 或 PipifySettings 尚未保存为可寻址资产。";
                        return false;
                    }

                    bool masterIsSample = IsSampleAssetPath(masterPath);
                    bool settingsIsSample = IsSampleAssetPath(settingsPath);
                    if (masterIsSample != settingsIsSample)
                    {
                        error = "ConfigMaster 与 PipifySettings 不能跨 Sample/业务工作区混配。";
                        return false;
                    }
                    if (masterIsSample && !string.Equals(
                            NormalizeAssetPath(System.IO.Path.GetDirectoryName(masterPath)),
                            NormalizeAssetPath(System.IO.Path.GetDirectoryName(settingsPath)),
                            StringComparison.Ordinal))
                    {
                        error = "Sample ConfigMaster 与 PipifySettings 必须位于同一个 Editor 目录。";
                        return false;
                    }

                    ReplaceReference(masterGuid, masterPath, ref globals.configMasterGuid, ref globals.configMasterPathHint);
                    ReplaceReference(settingsGuid, settingsPath, ref globals.pipifySettingsGuid, ref globals.pipifySettingsPathHint);
                    if (!masterIsSample)
                    {
                        CopyReference(masterGuid, masterPath,
                            ref globals.projectConfigMasterGuid, ref globals.projectConfigMasterPathHint);
                        CopyReference(settingsGuid, settingsPath,
                            ref globals.projectPipifySettingsGuid, ref globals.projectPipifySettingsPathHint);
                    }
                    globals.activeSampleRoot = string.Empty;
                    globals.schemaVersion = c_CurrentSchemaVersion;
                    if (WriteGlobals(globalsPath, globals)) return true;
                    error = "Globals.json 原子写入失败。";
                    return false;
                }

                /// <summary>
                /// 根据场景在业务工作区和 Sample 工作区之间切换当前引用。
                /// Sample 路由会先备份有效的非 Sample 引用；返回业务 Scene 时优先恢复备份，
                /// 旧版 Globals 无备份时仅允许工程内唯一的非 Sample 资产参与迁移。
                /// </summary>
                /// <param name="globals">待更新的 Globals 数据。</param>
                /// <param name="scenePath">当前活动 Scene 的项目相对路径。</param>
                /// <returns>任意字段发生变化时返回 true。</returns>
                private static bool RouteForScene(GlobalsJson globals, string scenePath)
                {
                    string normalizedScenePath = NormalizeAssetPath(scenePath);
                    if (string.IsNullOrEmpty(normalizedScenePath)) return false;

                    return IsSampleAssetPath(normalizedScenePath)
                        ? ActivateSampleWorkspace(globals, normalizedScenePath)
                        : RestoreProjectWorkspace(globals);
                }

                /// <summary>
                /// 激活 Sample 工作区，并在覆盖前保存当前有效的业务 Master 与 Pipify 引用。
                /// Sample 缺少任一配置时会清空对应当前引用，使构建显式失败而不是沿用业务配置。
                /// </summary>
                /// <param name="globals">待更新的 Globals 数据。</param>
                /// <param name="scenePath">Sample Scene 路径。</param>
                /// <returns>任意字段发生变化时返回 true。</returns>
                private static bool ActivateSampleWorkspace(GlobalsJson globals, string scenePath)
                {
                    bool changed = false;
                    bool alreadyInSampleSession = !string.IsNullOrEmpty(globals.activeSampleRoot);
                    ConfigMasterSO currentMaster = LoadAssetByGuid<ConfigMasterSO>(
                        globals.configMasterGuid, out string currentMasterPath);
                    if (!alreadyInSampleSession && currentMaster != null && !string.IsNullOrEmpty(currentMasterPath) &&
                        !IsSampleAssetPath(currentMasterPath))
                    {
                        changed |= CopyReference(
                            globals.configMasterGuid,
                            currentMasterPath,
                            ref globals.projectConfigMasterGuid,
                            ref globals.projectConfigMasterPathHint);
                    }
                    else if (!alreadyInSampleSession &&
                             (IsSampleAssetPath(currentMasterPath) || IsSampleAssetPath(globals.configMasterPathHint)) &&
                             string.IsNullOrEmpty(globals.projectConfigMasterGuid))
                    {
                        ConfigMasterSO legacyProjectMaster = FindUniqueNonSampleAsset<ConfigMasterSO>(out string legacyProjectPath);
                        if (legacyProjectMaster != null)
                        {
                            changed |= ReplaceResolvedReference(
                                legacyProjectMaster,
                                legacyProjectPath,
                                ref globals.projectConfigMasterGuid,
                                ref globals.projectConfigMasterPathHint);
                        }
                    }

                    PipifySettingsSO currentPipify = LoadAssetByGuid<PipifySettingsSO>(
                        globals.pipifySettingsGuid, out string currentPipifyPath);
                    if (!alreadyInSampleSession && currentPipify != null && !string.IsNullOrEmpty(currentPipifyPath) &&
                        !IsSampleAssetPath(currentPipifyPath))
                    {
                        changed |= CopyReference(
                            globals.pipifySettingsGuid,
                            currentPipifyPath,
                            ref globals.projectPipifySettingsGuid,
                            ref globals.projectPipifySettingsPathHint);
                    }
                    else if (!alreadyInSampleSession &&
                             (IsSampleAssetPath(currentPipifyPath) || IsSampleAssetPath(globals.pipifySettingsPathHint)) &&
                             string.IsNullOrEmpty(globals.projectPipifySettingsGuid))
                    {
                        PipifySettingsSO legacyProjectPipify = FindUniqueNonSampleAsset<PipifySettingsSO>(out string legacyPipifyPath);
                        if (legacyProjectPipify != null)
                        {
                            changed |= ReplaceResolvedReference(
                                legacyProjectPipify,
                                legacyPipifyPath,
                                ref globals.projectPipifySettingsGuid,
                                ref globals.projectPipifySettingsPathHint);
                        }
                    }

                    FindSampleWorkspace(scenePath, out ConfigMasterSO sampleMaster, out PipifySettingsSO samplePipify,
                        out string sampleRoot);
                    changed |= ReplaceAssetReference(
                        sampleMaster,
                        ref globals.configMasterGuid,
                        ref globals.configMasterPathHint);
                    changed |= ReplaceAssetReference(
                        samplePipify,
                        ref globals.pipifySettingsGuid,
                        ref globals.pipifySettingsPathHint);
                    changed |= ReplaceString(sampleRoot, ref globals.activeSampleRoot);
                    if (globals.schemaVersion != c_CurrentSchemaVersion)
                    {
                        globals.schemaVersion = c_CurrentSchemaVersion;
                        changed = true;
                    }
                    return changed;
                }

                /// <summary>
                /// 恢复非 Sample 工作区。正常路径只使用已保存的业务备份；
                /// 兼容旧版 Sample 覆写时，只有唯一非 Sample 候选才会自动迁移。
                /// </summary>
                /// <param name="globals">待更新的 Globals 数据。</param>
                /// <returns>任意字段发生变化时返回 true。</returns>
                private static bool RestoreProjectWorkspace(GlobalsJson globals)
                {
                    bool changed = false;
                    string currentMasterPath = ResolveReferencePath(globals.configMasterGuid, globals.configMasterPathHint);
                    bool legacySampleBinding = string.IsNullOrEmpty(globals.activeSampleRoot) &&
                                               IsSampleAssetPath(currentMasterPath);
                    bool sampleSession = !string.IsNullOrEmpty(globals.activeSampleRoot);
                    bool masterNeedsRestore = sampleSession || legacySampleBinding;
                    if (masterNeedsRestore)
                    {
                        ConfigMasterSO projectMaster = LoadAssetByGuid<ConfigMasterSO>(
                            globals.projectConfigMasterGuid, out string projectMasterPath);
                        if (IsSampleAssetPath(projectMasterPath))
                        {
                            projectMaster = null;
                            projectMasterPath = null;
                        }
                        if (projectMaster == null && legacySampleBinding)
                        {
                            projectMaster = FindUniqueNonSampleAsset<ConfigMasterSO>(out projectMasterPath);
                        }
                        changed |= ReplaceResolvedReference(
                            projectMaster,
                            projectMasterPath,
                            ref globals.configMasterGuid,
                            ref globals.configMasterPathHint);
                        if (projectMaster != null)
                        {
                            changed |= CopyReference(
                                globals.configMasterGuid,
                                globals.configMasterPathHint,
                                ref globals.projectConfigMasterGuid,
                                ref globals.projectConfigMasterPathHint);
                        }
                    }
                    else if (!string.IsNullOrEmpty(currentMasterPath))
                    {
                        changed |= RepairPathHint(ref globals.configMasterPathHint, currentMasterPath);
                        changed |= CopyReference(
                            globals.configMasterGuid,
                            currentMasterPath,
                            ref globals.projectConfigMasterGuid,
                            ref globals.projectConfigMasterPathHint);
                    }

                    string currentPipifyPath = ResolveReferencePath(globals.pipifySettingsGuid, globals.pipifySettingsPathHint);
                    bool pipifyNeedsRestore = sampleSession ||
                                              IsSampleAssetPath(currentPipifyPath);
                    if (pipifyNeedsRestore)
                    {
                        PipifySettingsSO projectPipify = LoadAssetByGuid<PipifySettingsSO>(
                            globals.projectPipifySettingsGuid, out string projectPipifyPath);
                        if (IsSampleAssetPath(projectPipifyPath))
                        {
                            projectPipify = null;
                            projectPipifyPath = null;
                        }
                        if (projectPipify == null && !sampleSession)
                        {
                            projectPipify = FindUniqueNonSampleAsset<PipifySettingsSO>(out projectPipifyPath);
                        }
                        changed |= ReplaceResolvedReference(
                            projectPipify,
                            projectPipifyPath,
                            ref globals.pipifySettingsGuid,
                            ref globals.pipifySettingsPathHint);
                        if (projectPipify != null)
                        {
                            changed |= CopyReference(
                                globals.pipifySettingsGuid,
                                globals.pipifySettingsPathHint,
                                ref globals.projectPipifySettingsGuid,
                                ref globals.projectPipifySettingsPathHint);
                        }
                    }
                    else if (!string.IsNullOrEmpty(currentPipifyPath))
                    {
                        changed |= RepairPathHint(ref globals.pipifySettingsPathHint, currentPipifyPath);
                        changed |= CopyReference(
                            globals.pipifySettingsGuid,
                            currentPipifyPath,
                            ref globals.projectPipifySettingsGuid,
                            ref globals.projectPipifySettingsPathHint);
                    }
                    else if (!string.IsNullOrEmpty(currentMasterPath))
                    {
                        // 旧 Globals 没有 Pipify 字段时，只接受唯一非 Sample 资产作为一次性兼容迁移。
                        PipifySettingsSO unique = FindUniqueNonSampleAsset<PipifySettingsSO>(out string uniquePath);
                        changed |= ReplaceResolvedReference(
                            unique,
                            uniquePath,
                            ref globals.pipifySettingsGuid,
                            ref globals.pipifySettingsPathHint);
                        if (unique != null)
                        {
                            changed |= CopyReference(
                                globals.pipifySettingsGuid,
                                globals.pipifySettingsPathHint,
                                ref globals.projectPipifySettingsGuid,
                                ref globals.projectPipifySettingsPathHint);
                        }
                    }
                    changed |= ReplaceString(string.Empty, ref globals.activeSampleRoot);
                    if (globals.schemaVersion != c_CurrentSchemaVersion)
                    {
                        globals.schemaVersion = c_CurrentSchemaVersion;
                        changed = true;
                    }
                    return changed;
                }

                /// <summary>
                /// 从 Sample Scene 所在目录逐级向上查找同一 Editor 目录中的工作区资产对。
                /// 命中任一资产后立即固定 owner，另一项缺失时保持 null，禁止跨目录拼出混合工作区。
                /// </summary>
                /// <param name="scenePath">Sample Scene 路径。</param>
                /// <param name="master">匹配目录内的 ConfigMaster。</param>
                /// <param name="settings">匹配目录内的 PipifySettings。</param>
                /// <param name="sampleRoot">实际命中的 owner 目录；完全未命中时为 Scene 所在目录。</param>
                private static void FindSampleWorkspace(
                    string scenePath,
                    out ConfigMasterSO master,
                    out PipifySettingsSO settings,
                    out string sampleRoot)
                {
                    master = null;
                    settings = null;
                    sampleRoot = string.Empty;
                    if (!IsSampleAssetPath(scenePath)) return;
                    string dir = System.IO.Path.GetDirectoryName(scenePath)?.Replace('\\', '/');
                    while (!string.IsNullOrEmpty(dir) && IsSampleAssetPath(dir))
                    {
                        master = AssetDatabase.LoadAssetAtPath<ConfigMasterSO>($"{dir}/Editor/ConfigMaster.asset");
                        settings = AssetDatabase.LoadAssetAtPath<PipifySettingsSO>($"{dir}/Editor/PipifySettings.asset");
                        if (master != null || settings != null)
                        {
                            sampleRoot = dir;
                            return;
                        }
                        dir = System.IO.Path.GetDirectoryName(dir)?.Replace('\\', '/');
                    }
                    sampleRoot = NormalizeAssetPath(System.IO.Path.GetDirectoryName(scenePath));
                }

                /// <summary>
                /// 在 Assets 范围内寻找唯一的非 Sample 资产，供旧 Globals 一次性迁移。
                /// 多候选时返回 null，禁止依赖 GUID 顺序。
                /// </summary>
                /// <typeparam name="T">目标资产类型。</typeparam>
                /// <param name="assetPath">唯一候选路径；未命中或多候选时为空。</param>
                /// <returns>唯一非 Sample 资产；否则返回 null。</returns>
                private static T FindUniqueNonSampleAsset<T>(out string assetPath) where T : UnityEngine.Object
                {
                    assetPath = null;
                    T candidate = null;
                    string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets" });
                    foreach (string guid in guids)
                    {
                        string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                        if (string.IsNullOrEmpty(path) || IsSampleAssetPath(path)) continue;
                        T loaded = AssetDatabase.LoadAssetAtPath<T>(path);
                        if (loaded == null) continue;
                        if (candidate != null)
                        {
                            assetPath = null;
                            return null;
                        }
                        candidate = loaded;
                        assetPath = path;
                    }
                    return candidate;
                }

                /// <summary>
                /// 从磁盘读取 Globals；文件不存在时返回一个空模型，解析失败时拒绝继续自动写入。
                /// </summary>
                /// <param name="globals">读取到的模型。</param>
                /// <param name="globalsPath">Globals 绝对路径。</param>
                /// <param name="logFailure">是否记录解析失败日志。</param>
                /// <returns>文件不存在或解析成功时返回 true；解析失败返回 false。</returns>
                private static bool TryLoadGlobals(
                    out GlobalsJson globals,
                    out string globalsPath,
                    bool logFailure = true)
                {
                    globalsPath = GetGlobalsPath();
                    globals = new GlobalsJson();
                    if (!File.Exists(globalsPath)) return true;
                    try
                    {
                        globals = JsonUtility.FromJson<GlobalsJson>(File.ReadAllText(globalsPath)) ?? new GlobalsJson();
                        if (globals.schemaVersion > c_CurrentSchemaVersion)
                        {
                            if (logFailure)
                            {
                                Log.Warning(LogTag.Editor,
                                    "[WorkspaceActive] Globals.json schemaVersion={0} 高于当前支持版本 {1}，拒绝读取和改写。",
                                    globals.schemaVersion, c_CurrentSchemaVersion);
                            }
                            globals = null;
                            return false;
                        }
                        return true;
                    }
                    catch (Exception e)
                    {
                        globals = null;
                        if (logFailure)
                        {
                            Log.Warning(LogTag.Editor, "[WorkspaceActive] Globals.json 解析失败，拒绝自动改写：{0}", e.Message);
                        }
                        return false;
                    }
                }

                /// <summary>
                /// 按 GUID 加载资产，并返回 AssetDatabase 当前路径。
                /// </summary>
                /// <typeparam name="T">目标资产类型。</typeparam>
                /// <param name="guid">资产 GUID。</param>
                /// <param name="assetPath">GUID 当前解析路径。</param>
                /// <returns>可加载的目标资产；失败返回 null。</returns>
                private static T LoadAssetByGuid<T>(string guid, out string assetPath) where T : UnityEngine.Object
                {
                    assetPath = string.IsNullOrEmpty(guid)
                        ? null
                        : NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                    return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<T>(assetPath);
                }

                /// <summary>
                /// 优先使用 GUID 当前路径，GUID 失效时保留 pathHint 用于判断旧 Sample 来源。
                /// </summary>
                /// <param name="guid">资产 GUID。</param>
                /// <param name="pathHint">历史路径提示。</param>
                /// <returns>可解析路径或规范化后的 pathHint。</returns>
                private static string ResolveReferencePath(string guid, string pathHint)
                {
                    string resolved = string.IsNullOrEmpty(guid) ? null : AssetDatabase.GUIDToAssetPath(guid);
                    return NormalizeAssetPath(string.IsNullOrEmpty(resolved) ? pathHint : resolved);
                }

                /// <summary>
                /// 将有效资产写入 GUID/pathHint 引用字段。
                /// </summary>
                /// <typeparam name="T">资产类型。</typeparam>
                /// <param name="asset">待写入资产。</param>
                /// <param name="guid">GUID 字段。</param>
                /// <param name="pathHint">路径字段。</param>
                /// <returns>写入成功且字段有效时返回 true。</returns>
                private static bool SetAssetReference<T>(T asset, ref string guid, ref string pathHint) where T : UnityEngine.Object
                {
                    string path = NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
                    string assetGuid = string.IsNullOrEmpty(path) ? null : AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(assetGuid)) return false;
                    guid = assetGuid;
                    pathHint = path;
                    return true;
                }

                /// <summary>
                /// 用目标资产替换引用；目标为空时清空引用，避免沿用另一工作区配置。
                /// </summary>
                /// <typeparam name="T">资产类型。</typeparam>
                /// <param name="asset">目标资产。</param>
                /// <param name="guid">GUID 字段。</param>
                /// <param name="pathHint">路径字段。</param>
                /// <returns>字段发生变化时返回 true。</returns>
                private static bool ReplaceAssetReference<T>(T asset, ref string guid, ref string pathHint) where T : UnityEngine.Object
                {
                    string newPath = asset == null ? null : NormalizeAssetPath(AssetDatabase.GetAssetPath(asset));
                    string newGuid = string.IsNullOrEmpty(newPath) ? null : AssetDatabase.AssetPathToGUID(newPath);
                    return ReplaceReference(newGuid, newPath, ref guid, ref pathHint);
                }

                /// <summary>
                /// 用已解析的资产和路径替换引用。
                /// </summary>
                /// <typeparam name="T">资产类型。</typeparam>
                /// <param name="asset">已解析资产。</param>
                /// <param name="assetPath">已解析路径。</param>
                /// <param name="guid">GUID 字段。</param>
                /// <param name="pathHint">路径字段。</param>
                /// <returns>字段发生变化时返回 true。</returns>
                private static bool ReplaceResolvedReference<T>(
                    T asset,
                    string assetPath,
                    ref string guid,
                    ref string pathHint) where T : UnityEngine.Object
                {
                    string newPath = asset == null ? null : NormalizeAssetPath(assetPath);
                    string newGuid = string.IsNullOrEmpty(newPath) ? null : AssetDatabase.AssetPathToGUID(newPath);
                    return ReplaceReference(newGuid, newPath, ref guid, ref pathHint);
                }

                /// <summary>
                /// 覆盖一组 GUID/pathHint 字段。
                /// </summary>
                /// <param name="newGuid">新 GUID。</param>
                /// <param name="newPath">新路径。</param>
                /// <param name="guid">GUID 字段。</param>
                /// <param name="pathHint">路径字段。</param>
                /// <returns>字段发生变化时返回 true。</returns>
                private static bool ReplaceReference(
                    string newGuid,
                    string newPath,
                    ref string guid,
                    ref string pathHint)
                {
                    newGuid ??= string.Empty;
                    newPath ??= string.Empty;
                    if (string.Equals(guid ?? string.Empty, newGuid, StringComparison.Ordinal) &&
                        string.Equals(pathHint ?? string.Empty, newPath, StringComparison.Ordinal))
                    {
                        return false;
                    }
                    guid = newGuid;
                    pathHint = newPath;
                    return true;
                }

                /// <summary>
                /// 规范化后替换字符串字段。
                /// </summary>
                /// <param name="value">新值。</param>
                /// <param name="target">目标字段。</param>
                /// <returns>字段发生变化时返回 true。</returns>
                private static bool ReplaceString(string value, ref string target)
                {
                    value ??= string.Empty;
                    if (string.Equals(target ?? string.Empty, value, StringComparison.Ordinal)) return false;
                    target = value;
                    return true;
                }

                /// <summary>
                /// 将一组引用复制到目标字段。
                /// </summary>
                /// <param name="sourceGuid">来源 GUID。</param>
                /// <param name="sourcePath">来源路径。</param>
                /// <param name="targetGuid">目标 GUID 字段。</param>
                /// <param name="targetPath">目标路径字段。</param>
                /// <returns>目标字段发生变化时返回 true。</returns>
                private static bool CopyReference(
                    string sourceGuid,
                    string sourcePath,
                    ref string targetGuid,
                    ref string targetPath)
                {
                    return ReplaceReference(sourceGuid, sourcePath, ref targetGuid, ref targetPath);
                }

                /// <summary>
                /// 清空一组引用字段。
                /// </summary>
                /// <param name="guid">GUID 字段。</param>
                /// <param name="pathHint">路径字段。</param>
                private static void ClearReference(ref string guid, ref string pathHint)
                {
                    guid = string.Empty;
                    pathHint = string.Empty;
                }

                /// <summary>
                /// 当 GUID 当前路径与 pathHint 不同时修复提示路径。
                /// </summary>
                /// <param name="pathHint">待修复路径字段。</param>
                /// <param name="assetPath">GUID 当前路径。</param>
                /// <returns>字段发生变化时返回 true。</returns>
                private static bool RepairPathHint(ref string pathHint, string assetPath)
                {
                    if (string.IsNullOrEmpty(assetPath) || string.Equals(pathHint, assetPath, StringComparison.Ordinal))
                        return false;
                    pathHint = assetPath;
                    return true;
                }

                /// <summary>
                /// 判断资产路径是否位于 Unity 导入的 Sample 目录。
                /// </summary>
                /// <param name="path">Assets 相对路径。</param>
                /// <returns>位于 Assets/Samples/ 下返回 true。</returns>
                private static bool IsSampleAssetPath(string path)
                {
                    return !string.IsNullOrEmpty(path) &&
                           NormalizeAssetPath(path).StartsWith("Assets/Samples/", StringComparison.Ordinal);
                }

                /// <summary>
                /// 规范化项目相对路径分隔符。
                /// </summary>
                /// <param name="path">待规范化路径。</param>
                /// <returns>使用正斜杠的路径；空输入返回空字符串。</returns>
                private static string NormalizeAssetPath(string path)
                {
                    return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
                }

                /// <summary>
                /// 获取 Globals.json 的绝对路径。
                /// </summary>
                /// <returns>Globals.json 绝对路径。</returns>
                private static string GetGlobalsPath()
                {
                    return System.IO.Path.Combine(GetProjectRoot(), c_GlobalsRelPath);
                }

                /// <summary>
                /// 将 GlobalsJson 序列化为 JSON 并原子写入目标路径（tmp + rename）。
                /// </summary>
                /// <param name="path">目标文件的绝对路径。</param>
                /// <param name="globals">待写入的 GlobalsJson 实例。</param>
                private static bool WriteGlobals(string path, GlobalsJson globals)
                {
                    string tmp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        string json = JsonUtility.ToJson(globals, prettyPrint: true);
                        string dir = System.IO.Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.WriteAllText(tmp, json);
                        // .NET Standard 2.1 无 File.Move(overwrite) 重载，使用 File.Replace 实现原子覆盖
                        if (File.Exists(path))
                            File.Replace(tmp, path, null);
                        else
                            File.Move(tmp, path);
                        return true;
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            if (File.Exists(tmp)) File.Delete(tmp);
                        }
                        catch (Exception cleanupException)
                        {
                            Log.Warning(LogTag.Editor, "[WorkspaceActive] 清理 Globals 临时文件失败：{0}", cleanupException.Message);
                        }
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 写入 Globals.json 失败：{0}", e.Message);
                        return false;
                    }
                }

                /// <summary>
                /// 获取 Unity 工程根目录的绝对路径（Application.dataPath 的父目录）。
                /// </summary>
                /// <returns>工程根目录绝对路径。</returns>
                private static string GetProjectRoot()
                {
                    return System.IO.Path.GetDirectoryName(Application.dataPath);
                }
            }
        }
    }
}
