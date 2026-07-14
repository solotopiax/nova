/***************************************************************
 * filename:  NovaSpark2.4.cs
 * descrip:   Nova 框架一次性引导器（火种）。
 *            把 Nova 框架所需的"火种"——UPM registry、必备依赖与
 *            PlugPals 配置——带入一个全新的 Unity 工程，点燃新征程。
 *            使命完成后自动删除脚本，功成身退。
 *
 *            设计要点：
 *            - Unity 版本门槛：低于最低要求（6000.4.2f1）时不执行任何补全，
 *              仅弹窗提示升级，且不自删（便于升级后重试）。
 *            - 自包含、零第三方依赖（内嵌保序 mini-JSON），可在尚未
 *              安装任何 Nova 包的空工程上直接运行。
 *            - 幂等：对已安装/框架源工程自动跳过；缺什么补什么。
 *            - 不属于任何自定义程序集，放在 Assets/Editor 下即可。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace NovaBootstrap
{
    /// <summary>
    /// Nova 框架一次性引导器。编辑器加载时自动检测当前工程是否已具备
    /// 引入 Nova 框架所需的环境，缺失则在用户确认后补全，补全成功即自动自删。
    /// </summary>
    [InitializeOnLoad]
    public static class NovaSpark
    {
        /// <summary>引导器对外展示名称，用于日志与弹窗文案。</summary>
        private const string c_Name = "NovaSpark";

        /// <summary>本会话内"用户已拒绝安装"标记键，避免同一会话每次域重载反复弹窗。</summary>
        private const string c_SessionDeclinedKey = "NovaSpark.Declined";

        /// <summary>Nova 框架要求的最低 Unity 版本（含）。当前工程低于此版本时引导器不执行任何补全，仅提示升级。</summary>
        private const string c_MinUnityVersion = "6000.4.2f1";

        /// <summary>本会话内"已提示 Unity 版本过低"标记键，避免同一会话每次域重载反复弹窗。</summary>
        private const string c_SessionVersionWarnedKey = "NovaSpark.VersionWarned";

        /// <summary>PlugPals 窗口菜单路径。用 ExecuteMenuItem 字符串调用，避免对框架类型的编译期依赖。</summary>
        private const string c_PlugPalsMenuPath = "Nova/Open PlugPals";

        /// <summary>
        /// 单轮域重载内"尝试打开 PlugPals 窗口"的最大轮询次数上限。
        /// 静态字段在每次域重载后重置为 0，故每轮重载都获得一份全新预算，
        /// 既能在本轮内等待菜单注册，又不会无限空转。
        /// </summary>
        private const int c_MaxOpenAttempts = 50;

        /// <summary>当前域重载内累计的打开尝试次数（域重载即归零）。</summary>
        private static int s_openAttempts;

        // ---- manifest 待补全的依赖项 ----

        /// <summary>Unity MCP 包键。</summary>
        private const string c_McpKey = "com.coplaydev.unity-mcp";

        /// <summary>Unity MCP 包来源（git url，锁定 tag）。</summary>
        private const string c_McpValue = "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#v10.1.0";

        /// <summary>Nova BestHTTP 封装包键。</summary>
        private const string c_BestHttpKey = "com.solotopia.nova.framework.besthttp";

        /// <summary>Nova BestHTTP 封装包版本。</summary>
        private const string c_BestHttpValue = "0.0.10";

        /// <summary>External Dependency Manager(EDM) 包键。Firebase/AppsFlyer/MAX 等的公共依赖，按策略由工程 manifest 显式固定，不靠各包传递。</summary>
        private const string c_EdmKey = "com.google.external-dependency-manager";

        /// <summary>EDM 版本（经 openupm registry 解析）。</summary>
        private const string c_EdmValue = "1.2.188";

        /// <summary>
        /// Nova 框架主包键。双重用途：
        /// (1) 安装目标——新工程经 Solotopia registry 拉取主包及其传递的 com.solotopia.* 子包；
        /// (2) "已安装 / 框架源工程"判据——manifest 已含该键时引导器静默退场，杜绝误改本仓或已装工程。
        /// </summary>
        private const string c_FrameworkKey = "com.solotopia.nova.framework";

        /// <summary>Nova 框架主包版本（经 Solotopia registry 解析，会传递安装一众 com.solotopia.* 核心子包）。</summary>
        private const string c_FrameworkValue = "0.5.38";

        /// <summary>PlugPals 配置文件相对工程根的路径。</summary>
        private const string c_PlugPalsRelativePath = "ProjectSettings/Nova/PlugPalsRegistries.json";

        // PlugPals 配置规格单一事实源：Detect 比对与 Apply 写入共用，避免两处不同步。
        private const string s_PlugPalsExternalUrl = "https://upm.solotopiax.com";
        private const string s_PlugPalsExternalName = "Solotopia";
        private const string s_PlugPalsInternalUrl = "http://172.16.22.175:4874";
        private const string s_PlugPalsInternalName = "Solotopia Internal";

        /// <summary>PlugPals 配置文件内容（按既定规格原样写入）。</summary>
        private const string c_PlugPalsContent =
            "{\n" +
            "    \"externalUrl\": \"" + s_PlugPalsExternalUrl + "\",\n" +
            "    \"externalName\": \"" + s_PlugPalsExternalName + "\",\n" +
            "    \"internalUrl\": \"" + s_PlugPalsInternalUrl + "\",\n" +
            "    \"internalName\": \"" + s_PlugPalsInternalName + "\"\n" +
            "}";

        /// <summary>需要补全的 scoped registry 定义（按 url 判重，缺则补、scope 取并集）。</summary>
        private static readonly RegistrySpec[] s_Registries =
        {
            new RegistrySpec("Solotopia", "https://upm.solotopiax.com", new[] { "com.solotopia" }),
            new RegistrySpec("Solotopia Internal", "http://172.16.22.175:4874", new[] { "com.tivadar", "com.onevcat" }),
            new RegistrySpec("package.openupm.com", "https://package.openupm.com", new[] { "com.google.external-dependency-manager" }),
        };

        /// <summary>
        /// Android 发布设置开关规格（ProjectSettings.asset 字段名, 展示名, 目标值）。
        /// 仅对齐开关状态；模板文件内容由 EDM Resolve 与框架构建处理器生成。
        /// </summary>
        private static readonly (string Field, string Label, bool Value)[] s_AndroidToggles =
        {
            ("useCustomMainManifest", "Custom Main Manifest", true),
            ("useCustomLauncherManifest", "Custom Launcher Manifest", true),
            ("useCustomMainGradleTemplate", "Custom Main Gradle Template", true),
            ("useCustomLauncherGradleManifest", "Custom Launcher Gradle Template", true),
            ("useCustomBaseGradleTemplate", "Custom Base Gradle Template", false),
            ("useCustomGradlePropertiesTemplate", "Custom Gradle Properties Template", true),
            ("useCustomGradleSettingsTemplate", "Custom Gradle Settings Template", true),
            ("useCustomProguardFile", "Custom Proguard File", true),
        };

        /// <summary>静态构造：编辑器加载即排入延迟执行，避免在域初始化阶段做 IO 与弹窗。</summary>
        static NovaSpark()
        {
            EditorApplication.delayCall += Run;
        }

        /// <summary>引导主流程：检测 → 询问 → 补全 → 询问自删。</summary>
        private static void Run()
        {
            try
            {
                // Unity 版本门槛：低于 Nova 要求的最低版本则不执行任何检测/补全，弹窗提示升级后退出。
                // 不自删（便于用户升级 Unity 后重新触发引导）；每会话仅提示一次，避免域重载反复弹窗。
                if (!IsUnityVersionSupported())
                {
                    if (!SessionState.GetBool(c_SessionVersionWarnedKey, false))
                    {
                        SessionState.SetBool(c_SessionVersionWarnedKey, true);
                        EditorUtility.DisplayDialog(
                            $"{c_Name} · Unity 版本过低",
                            $"Nova 框架要求 Unity 最低版本 {c_MinUnityVersion}，当前工程为 {Application.unityVersion}。\n\n" +
                            "请升级 Unity 到不低于该版本后重试；在此之前引导器不会执行任何操作。",
                            "我知道了");
                        Debug.LogWarning($"[{c_Name}] Unity 版本过低（当前 {Application.unityVersion}，要求 ≥ {c_MinUnityVersion}），引导已跳过。");
                    }
                    return;
                }

                // 接力：上一轮已补全并落下"待打开 PlugPals"标记。此处先于其它判断处理，
                // 等框架编译完成、菜单可用后打开窗口，再功成身退；菜单暂不可用则下个 tick 续试。
                if (EditorPrefs.GetBool(PendingOpenKey, false))
                {
                    if (EditorApplication.ExecuteMenuItem(c_PlugPalsMenuPath))
                    {
                        EditorPrefs.DeleteKey(PendingOpenKey);
                        DeleteSelf();
                    }
                    else if (s_openAttempts++ < c_MaxOpenAttempts)
                    {
                        // 框架尚在解析/编译，菜单还未注册，等下个编辑器 tick 再试。
                        EditorApplication.delayCall += Run;
                    }
                    return;
                }

                string manifestPath = Path.Combine(ProjectRoot, "Packages/manifest.json");
                if (!File.Exists(manifestPath))
                    return;

                var manifest = Json.Parse(File.ReadAllText(manifestPath)) as JObj;
                if (manifest == null)
                    return;

                // 已安装 / 框架源工程（含本主仓）：标记已装，继续向下检测配置是否过时（旧版 url 矫正）。
                // 源工程（file: 引用 framework）仍跳过，避免改动主仓 manifest。
                var deps = manifest.Get("dependencies") as JObj;
                bool alreadyInstalled = deps != null && deps.ContainsKey(c_FrameworkKey);
                bool isSourceProject = alreadyInstalled &&
                    (deps.Get(c_FrameworkKey) as string)?.StartsWith("file:") == true;
                if (isSourceProject)
                    return;

                var missing = Detect(manifest, alreadyInstalled);
                if (missing.Count == 0)
                {
                    // 环境已就绪：若是已装工程（用户重复拖入到已完成工程），火种使命已完成，直接自删走人；
                    // 否则（首装工程理论上不该出现 missing 为空，兜底）静默退出。
                    if (alreadyInstalled)
                    {
                        Debug.Log($"[{c_Name}] 配置已是本版最新状态，无需矫正；火种已传递，新征程启程。");
                        EditorApplication.delayCall += DeleteSelf;
                    }
                    return;
                }

                if (SessionState.GetBool(c_SessionDeclinedKey, false))
                    return; // 本会话用户已拒绝，不再打扰。

                string title, body, confirm;
                if (alreadyInstalled)
                {
                    title = $"{c_Name} · 配置矫正";
                    body = "检测到本工程 Nova 配置与当前版本不符（如下），将按本版规格矫正：\n\n" +
                           "  · " + string.Join("\n  · ", missing) + "\n\n" +
                           "点「矫正配置」即可一键对齐。";
                    confirm = "矫正配置";
                }
                else
                {
                    title = $"{c_Name} · 引入 Nova 框架";
                    body = "为确保 Nova 框架的正常引入，即将为您部署以下内容：\n\n" +
                           "  · " + string.Join("\n  · ", missing) + "\n\n" +
                           "点「引入框架」即可一键完成，开启新征程 ✨";
                    confirm = "引入框架";
                }

                bool agree = EditorUtility.DisplayDialog(title, body, confirm, "暂不");

                if (!agree)
                {
                    SessionState.SetBool(c_SessionDeclinedKey, true);
                    return;
                }

                Apply(manifest, manifestPath);

                if (alreadyInstalled)
                {
                    // 矫正模式：manifest 已改动，必须把 packages-lock.json 一并清掉——否则 Unity 仍按旧 lock 拉包，
                    // 矫正后的版本号不会生效。删完调 Client.Resolve() 触发 UPM 全量重新解析。
                    try
                    {
                        string lockPath = Path.Combine(ProjectRoot, "Packages/packages-lock.json");
                        if (File.Exists(lockPath))
                            File.Delete(lockPath);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[{c_Name}] 清理 packages-lock.json 失败（不影响后续 Resolve）：{ex.Message}");
                    }
                    Debug.Log($"[{c_Name}] 配置矫正完成：依赖版本/registry/PlugPals 已对齐本版规格；UPM 正在重新解析（请稍候）。");
                    Client.Resolve();
                    AssetDatabase.Refresh();
                    // 火种使命完成（矫正模式不打开 PlugPals 窗口，直接自删）；
                    // 延迟一帧再删，避免域重载与 Client.Resolve() 撞车。
                    EditorApplication.delayCall += DeleteSelf;
                    return;
                }

                Debug.Log($"[{c_Name}] Nova 框架引导完成，已补全 manifest 与 PlugPals 配置，UPM 正在重新解析。");

                // 此刻框架刚落盘、尚未编译完成，PlugPals 窗口还打不开。
                // 落下接力标记，待框架编译完成后的下一轮 Run 打开窗口并自删；
                // 同时排一次延迟回调兜底，防止个别情形下不触发域重载。
                EditorPrefs.SetBool(PendingOpenKey, true);
                EditorApplication.delayCall += Run;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{c_Name}] 引导过程出错：{e}");
            }
        }

        /// <summary>检测当前 manifest 缺失了哪些必备项，返回可读的缺失清单。</summary>
        /// <param name="alreadyInstalled">已装工程（framework 已在 dependencies）时，仅检测配置过时，跳过依赖缺失检测。</param>
        private static List<string> Detect(JObj manifest, bool alreadyInstalled)
        {
            var missing = new List<string>();

            var deps = manifest.Get("dependencies") as JObj;
            if (!alreadyInstalled)
            {
                if (deps == null || !deps.ContainsKey(c_McpKey))
                    missing.Add($"依赖 {c_McpKey}");
                if (deps == null || !deps.ContainsKey(c_FrameworkKey))
                    missing.Add($"依赖 {c_FrameworkKey}（Nova 框架主包）");
                if (deps == null || !deps.ContainsKey(c_BestHttpKey))
                    missing.Add($"依赖 {c_BestHttpKey}");
                if (deps == null || !deps.ContainsKey(c_EdmKey))
                    missing.Add($"依赖 {c_EdmKey}");
            }
            else
            {
                // 已装工程：检测 semver 依赖版本号是否过时（如 framework 0.5.31 公网已下架→需矫正为 0.5.32）。
                if (deps != null)
                {
                    if (deps.ContainsKey(c_FrameworkKey) && (deps.Get(c_FrameworkKey) as string) != c_FrameworkValue)
                        missing.Add($"依赖 {c_FrameworkKey} 版本过时，需矫正为 {c_FrameworkValue}");
                    if (deps.ContainsKey(c_BestHttpKey) && (deps.Get(c_BestHttpKey) as string) != c_BestHttpValue)
                        missing.Add($"依赖 {c_BestHttpKey} 版本过时，需矫正为 {c_BestHttpValue}");
                    if (deps.ContainsKey(c_EdmKey) && (deps.Get(c_EdmKey) as string) != c_EdmValue)
                        missing.Add($"依赖 {c_EdmKey} 版本过时，需矫正为 {c_EdmValue}");
                }
            }

            var registries = manifest.Get("scopedRegistries") as JArr;
            foreach (var spec in s_Registries)
            {
                // 按 name 找（旧版同 name 域 url 可能过时，需矫正）；找不到视为缺失。
                var existing = FindRegistryByName(registries, spec.Name);
                if (existing == null)
                {
                    missing.Add($"registry {spec.Name} ({spec.Url})");
                    continue;
                }

                if ((existing.Get("url") as string) != spec.Url)
                    missing.Add($"registry {spec.Name} url 过时，需矫正为 {spec.Url}");

                var scopes = existing.Get("scopes") as JArr;
                foreach (var scope in spec.Scopes)
                {
                    if (!ContainsString(scopes, scope))
                    {
                        missing.Add($"registry {spec.Name} 缺 scope {scope}");
                        break;
                    }
                }
            }

            string plugPalsPath = Path.Combine(ProjectRoot, c_PlugPalsRelativePath);
            if (IsPlugPalsContentOutdated(plugPalsPath))
                missing.Add($"{c_PlugPalsRelativePath}（内容过时或缺失）");

            // Android 发布设置开关（按图对齐）。
            var ps = LoadProjectSettings();
            if (ps != null)
            {
                foreach (var t in s_AndroidToggles)
                {
                    var p = ps.FindProperty(t.Field);
                    if (p == null)
                        continue;
                    if (p.boolValue != t.Value)
                        missing.Add($"Android 设置 {t.Label} 应为 {(t.Value ? "开" : "关")}");
                }
            }

            return missing;
        }

        /// <summary>执行补全：合并 manifest 依赖与 registry，写入 PlugPals 配置，触发 UPM 解析。</summary>
        private static void Apply(JObj manifest, string manifestPath)
        {
            // 1) dependencies：缺键才补，不覆盖既有值。
            var deps = manifest.Get("dependencies") as JObj;
            if (deps == null)
            {
                deps = new JObj();
                manifest.Set("dependencies", deps);
            }
            // MCP 是 git url（用户可能改过 fork），保持"缺键才补"，不覆盖。
            if (!deps.ContainsKey(c_McpKey))
                deps.Set(c_McpKey, c_McpValue);
            // framework/besthttp/edm 是 semver 版本号，已装工程的旧版本可能公网已下架（如 framework 0.5.31）→
            // 强制对齐本版规格，避免 UPM 解析"cannot be found"。
            UpsertDepVersion(deps, c_FrameworkKey, c_FrameworkValue);
            UpsertDepVersion(deps, c_BestHttpKey, c_BestHttpValue);
            UpsertDepVersion(deps, c_EdmKey, c_EdmValue);

            // 2) scopedRegistries：按 name 找，命中则强制覆盖 url（矫正旧版过时地址）+ 补齐 scope；
            //    找不到才整条追加。
            var registries = manifest.Get("scopedRegistries") as JArr;
            if (registries == null)
            {
                registries = new JArr();
                manifest.Set("scopedRegistries", registries);
            }
            foreach (var spec in s_Registries)
            {
                var existing = FindRegistryByName(registries, spec.Name);
                if (existing == null)
                {
                    var entry = new JObj();
                    entry.Set("name", spec.Name);
                    entry.Set("url", spec.Url);
                    var scopeArr = new JArr();
                    foreach (var scope in spec.Scopes)
                        scopeArr.Add(scope);
                    entry.Set("scopes", scopeArr);
                    registries.Add(entry);
                }
                else
                {
                    // 同名域：强制把 url 刷成本版规格（矫正旧版 4873 等过时地址）。
                    existing.Set("url", spec.Url);
                    var scopes = existing.Get("scopes") as JArr;
                    if (scopes == null)
                    {
                        scopes = new JArr();
                        existing.Set("scopes", scopes);
                    }
                    foreach (var scope in spec.Scopes)
                    {
                        if (!ContainsString(scopes, scope))
                            scopes.Add(scope);
                    }
                }
            }

            File.WriteAllText(manifestPath, Json.Write(manifest), new UTF8Encoding(false));

            // 3) PlugPals 配置：内容过时或缺失则（覆盖）写入本版规格，确保 url/name 随版本矫正。
            string plugPalsPath = Path.Combine(ProjectRoot, c_PlugPalsRelativePath);
            if (IsPlugPalsContentOutdated(plugPalsPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(plugPalsPath));
                File.WriteAllText(plugPalsPath, c_PlugPalsContent, new UTF8Encoding(false));
            }

            // 4) Android 发布设置开关：按规格对齐（仅设开关，不创建模板文件，
            //    其内容由 EDM Resolve / 框架构建处理器自动生成）。
            var ps = LoadProjectSettings();
            if (ps != null)
            {
                bool changed = false;
                foreach (var t in s_AndroidToggles)
                {
                    var p = ps.FindProperty(t.Field);
                    if (p == null)
                        continue;
                    if (p.boolValue != t.Value)
                    {
                        p.boolValue = t.Value;
                        changed = true;
                    }
                }
                if (changed)
                {
                    ps.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                }
            }

            AssetDatabase.Refresh();
            Client.Resolve();
        }

        /// <summary>删除引导器自身（脚本与其 .meta）。</summary>
        private static void DeleteSelf()
        {
            string assetPath = ToAssetPath(SelfFilePath());
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.DeleteAsset(assetPath))
            {
                AssetDatabase.Refresh();
                Debug.Log($"[{c_Name}] 火种已传递，新征程启程。");
                return;
            }

            // 兜底：AssetDatabase 删除失败时直接删文件。
            string abs = SelfFilePath();
            if (File.Exists(abs))
            {
                File.Delete(abs);
                if (File.Exists(abs + ".meta"))
                    File.Delete(abs + ".meta");
                AssetDatabase.Refresh();
                Debug.Log($"[{c_Name}] 火种已传递，新征程启程。");
            }
        }

        // ---------- 辅助方法 ----------

        /// <summary>工程根目录（Assets 的父级）。</summary>
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        /// <summary>当前 Unity 版本是否达到 Nova 要求的最低版本 c_MinUnityVersion（按 major.minor.patch 比较，含等于）。</summary>
        private static bool IsUnityVersionSupported()
        {
            return CompareUnityVersion(Application.unityVersion, c_MinUnityVersion) >= 0;
        }

        /// <summary>
        /// 比较两个 Unity 版本字符串（形如 "6000.4.2f1"），按 major.minor.patch 三段数值比较。
        /// 返回负数表示 a 低于 b，0 表示相等，正数表示 a 高于 b；无法解析的段按 0 计。
        /// </summary>
        private static int CompareUnityVersion(string a, string b)
        {
            int[] va = ParseUnityVersion(a);
            int[] vb = ParseUnityVersion(b);
            for (int i = 0; i < 3; i++)
            {
                if (va[i] != vb[i])
                    return va[i] < vb[i] ? -1 : 1;
            }
            return 0;
        }

        /// <summary>把 Unity 版本字符串解析为 [major, minor, patch] 三段整数；patch 段自动去除 f/b/a 等发布后缀。</summary>
        private static int[] ParseUnityVersion(string version)
        {
            int[] result = new int[3];
            if (string.IsNullOrEmpty(version))
                return result;
            string[] parts = version.Split('.');
            for (int i = 0; i < 3 && i < parts.Length; i++)
            {
                int end = 0;
                while (end < parts[i].Length && char.IsDigit(parts[i][end]))
                    end++;
                if (end > 0)
                    int.TryParse(parts[i].Substring(0, end), out result[i]);
            }
            return result;
        }

        /// <summary>"待打开 PlugPals"接力标记键。EditorPrefs 为全用户级，故按工程路径区分，避免跨工程串扰。</summary>
        private static string PendingOpenKey => "NovaSpark.PendingOpenPlugPals." + ProjectRoot;

        /// <summary>加载 ProjectSettings.asset 的 SerializedObject（PlayerSettings 序列化体），用于读写发布开关。</summary>
        private static SerializedObject LoadProjectSettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0 || assets[0] == null)
                return null;
            return new SerializedObject(assets[0]);
        }

        /// <summary>编译期捕获的本脚本绝对路径，用于精准定位与自删。</summary>
        private static string SelfFilePath([CallerFilePath] string path = "") => path;

        /// <summary>把绝对路径转换为以 Assets 开头的工程相对资产路径。</summary>
        private static string ToAssetPath(string absolute)
        {
            if (string.IsNullOrEmpty(absolute))
                return null;
            absolute = absolute.Replace('\\', '/');
            int idx = absolute.LastIndexOf("/Assets/", StringComparison.Ordinal);
            if (idx < 0)
                return absolute.StartsWith("Assets/", StringComparison.Ordinal) ? absolute : null;
            return absolute.Substring(idx + 1);
        }

        /// <summary>缺键则补、键存在但值不一致则覆盖（用于 semver 依赖矫正，避免 UPM 拉到下架版本）。</summary>
        private static void UpsertDepVersion(JObj deps, string key, string value)
        {
            if (!deps.ContainsKey(key) || (deps.Get(key) as string) != value)
                deps.Set(key, value);
        }

        /// <summary>在 registry 数组中按 url 查找已存在的条目。</summary>
        private static JObj FindRegistry(JArr registries, string url)
        {
            if (registries == null)
                return null;
            foreach (var item in registries.Items)
            {
                if (item is JObj obj && (obj.Get("url") as string) == url)
                    return obj;
            }
            return null;
        }

        /// <summary>在 registry 数组中按 name 查找已存在的条目（矫正用：同名域直接覆盖 url）。</summary>
        private static JObj FindRegistryByName(JArr registries, string name)
        {
            if (registries == null)
                return null;
            foreach (var item in registries.Items)
            {
                if (item is JObj obj && (obj.Get("name") as string) == name)
                    return obj;
            }
            return null;
        }

        /// <summary>PlugPals 配置内容是否与本版规格一致（externalUrl/internalUrl/name 任一不符即过时）。</summary>
        private static bool IsPlugPalsContentOutdated(string path)
        {
            if (!File.Exists(path))
                return true;
            try
            {
                var cfg = Json.Parse(File.ReadAllText(path)) as JObj;
                if (cfg == null)
                    return true;
                return (cfg.Get("externalUrl") as string) != s_PlugPalsExternalUrl ||
                       (cfg.Get("externalName") as string) != s_PlugPalsExternalName ||
                       (cfg.Get("internalUrl") as string) != s_PlugPalsInternalUrl ||
                       (cfg.Get("internalName") as string) != s_PlugPalsInternalName;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>判断字符串数组是否已包含指定值。</summary>
        private static bool ContainsString(JArr arr, string value)
        {
            if (arr == null)
                return false;
            foreach (var item in arr.Items)
            {
                if ((item as string) == value)
                    return true;
            }
            return false;
        }

        /// <summary>scoped registry 规格（名称、地址、scope 列表）。</summary>
        private readonly struct RegistrySpec
        {
            public readonly string Name;
            public readonly string Url;
            public readonly string[] Scopes;

            public RegistrySpec(string name, string url, string[] scopes)
            {
                Name = name;
                Url = url;
                Scopes = scopes;
            }
        }

        // ============================================================
        // 内嵌保序 mini-JSON：仅支持 object / array / string / number /
        // bool / null，足以安全读写 manifest.json，且不依赖任何第三方包。
        // 解析与序列化均保持键序，仅追加缺失项，最大限度减少 diff。
        // ============================================================

        /// <summary>保序 JSON 对象。</summary>
        private sealed class JObj
        {
            private readonly List<string> _keys = new List<string>();
            private readonly Dictionary<string, object> _map = new Dictionary<string, object>();

            public IReadOnlyList<string> Keys => _keys;
            public bool ContainsKey(string key) => _map.ContainsKey(key);
            public object Get(string key) => _map.TryGetValue(key, out var v) ? v : null;

            public void Set(string key, object value)
            {
                if (!_map.ContainsKey(key))
                    _keys.Add(key);
                _map[key] = value;
            }
        }

        /// <summary>JSON 数组。</summary>
        private sealed class JArr
        {
            public readonly List<object> Items = new List<object>();
            public void Add(object value) => Items.Add(value);
        }

        /// <summary>JSON 解析与序列化。</summary>
        private static class Json
        {
            public static object Parse(string text)
            {
                int pos = 0;
                var value = ParseValue(text, ref pos);
                return value;
            }

            private static object ParseValue(string s, ref int pos)
            {
                SkipWhitespace(s, ref pos);
                char c = s[pos];
                switch (c)
                {
                    case '{': return ParseObject(s, ref pos);
                    case '[': return ParseArray(s, ref pos);
                    case '"': return ParseString(s, ref pos);
                    case 't': pos += 4; return true;
                    case 'f': pos += 5; return false;
                    case 'n': pos += 4; return null;
                    default: return ParseNumber(s, ref pos);
                }
            }

            private static JObj ParseObject(string s, ref int pos)
            {
                var obj = new JObj();
                pos++; // 跳过 '{'
                SkipWhitespace(s, ref pos);
                if (s[pos] == '}') { pos++; return obj; }
                while (true)
                {
                    SkipWhitespace(s, ref pos);
                    string key = ParseString(s, ref pos);
                    SkipWhitespace(s, ref pos);
                    pos++; // 跳过 ':'
                    object value = ParseValue(s, ref pos);
                    obj.Set(key, value);
                    SkipWhitespace(s, ref pos);
                    if (s[pos] == ',') { pos++; continue; }
                    if (s[pos] == '}') { pos++; break; }
                }
                return obj;
            }

            private static JArr ParseArray(string s, ref int pos)
            {
                var arr = new JArr();
                pos++; // 跳过 '['
                SkipWhitespace(s, ref pos);
                if (s[pos] == ']') { pos++; return arr; }
                while (true)
                {
                    object value = ParseValue(s, ref pos);
                    arr.Add(value);
                    SkipWhitespace(s, ref pos);
                    if (s[pos] == ',') { pos++; continue; }
                    if (s[pos] == ']') { pos++; break; }
                }
                return arr;
            }

            private static string ParseString(string s, ref int pos)
            {
                var sb = new StringBuilder();
                pos++; // 跳过起始引号
                while (true)
                {
                    char c = s[pos++];
                    if (c == '"')
                        break;
                    if (c == '\\')
                    {
                        char e = s[pos++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                sb.Append((char)Convert.ToInt32(s.Substring(pos, 4), 16));
                                pos += 4;
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }
                return sb.ToString();
            }

            private static object ParseNumber(string s, ref int pos)
            {
                int start = pos;
                while (pos < s.Length && "+-0123456789.eE".IndexOf(s[pos]) >= 0)
                    pos++;
                string num = s.Substring(start, pos - start);
                return double.Parse(num, System.Globalization.CultureInfo.InvariantCulture);
            }

            private static void SkipWhitespace(string s, ref int pos)
            {
                while (pos < s.Length && char.IsWhiteSpace(s[pos]))
                    pos++;
            }

            public static string Write(object value)
            {
                var sb = new StringBuilder();
                WriteValue(sb, value, 0);
                sb.Append('\n');
                return sb.ToString();
            }

            private static void WriteValue(StringBuilder sb, object value, int indent)
            {
                switch (value)
                {
                    case JObj obj: WriteObject(sb, obj, indent); break;
                    case JArr arr: WriteArray(sb, arr, indent); break;
                    case string str: WriteString(sb, str); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case null: sb.Append("null"); break;
                    case double d: sb.Append(d.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
                    default: sb.Append(value); break;
                }
            }

            private static void WriteObject(StringBuilder sb, JObj obj, int indent)
            {
                if (obj.Keys.Count == 0) { sb.Append("{}"); return; }
                sb.Append("{\n");
                string pad = new string(' ', (indent + 1) * 2);
                for (int i = 0; i < obj.Keys.Count; i++)
                {
                    string key = obj.Keys[i];
                    sb.Append(pad);
                    WriteString(sb, key);
                    sb.Append(": ");
                    WriteValue(sb, obj.Get(key), indent + 1);
                    if (i < obj.Keys.Count - 1)
                        sb.Append(',');
                    sb.Append('\n');
                }
                sb.Append(new string(' ', indent * 2));
                sb.Append('}');
            }

            private static void WriteArray(StringBuilder sb, JArr arr, int indent)
            {
                if (arr.Items.Count == 0) { sb.Append("[]"); return; }
                sb.Append("[\n");
                string pad = new string(' ', (indent + 1) * 2);
                for (int i = 0; i < arr.Items.Count; i++)
                {
                    sb.Append(pad);
                    WriteValue(sb, arr.Items[i], indent + 1);
                    if (i < arr.Items.Count - 1)
                        sb.Append(',');
                    sb.Append('\n');
                }
                sb.Append(new string(' ', indent * 2));
                sb.Append(']');
            }

            private static void WriteString(StringBuilder sb, string str)
            {
                sb.Append('"');
                foreach (char c in str)
                {
                    switch (c)
                    {
                        case '"': sb.Append("\\\""); break;
                        case '\\': sb.Append("\\\\"); break;
                        case '\b': sb.Append("\\b"); break;
                        case '\f': sb.Append("\\f"); break;
                        case '\n': sb.Append("\\n"); break;
                        case '\r': sb.Append("\\r"); break;
                        case '\t': sb.Append("\\t"); break;
                        default: sb.Append(c); break;
                    }
                }
                sb.Append('"');
            }
        }
    }
}
