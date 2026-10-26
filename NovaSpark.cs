/***************************************************************
 * filename:  NovaSpark.cs
 * descrip:   Nova 框架安装脚本。
 *
 *            作用：在 Unity 项目中安装 Nova Framework。
 *
 *            使用方法：
 *            1. 仅使用 Unity 6000.4.x（最低 6000.4.2f1），将本文件放入项目的 Assets/Editor/。
 *               更高的 Unity 次版本存在 HybridCLR 兼容问题，不在支持范围内。
 *            2. 等待 Unity 编译完成，在弹窗中点击“引入框架”。
 *            3. 等待包解析、编译和 AI 工具链自动安装、绑定完成；脚本会打开 PlugPals。
 *               缺失工具自动补齐，失败自动重试；验证通过后引导脚本才自动删除。
 *
 *            已安装 Nova 的项目会先列出需调整的配置，点击“矫正配置”后才修改。
 *            使用 file: 引用 Framework 的源码工程会自动跳过。
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
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

        /// <summary>Nova 框架支持的最低 Unity 版本（含）；只接受 6000.4.x 版本线。</summary>
        private const string c_MinUnityVersion = "6000.4.2f1";

        /// <summary>Nova 框架支持范围的上界（不含）；更高的 Unity 次版本存在 HybridCLR 兼容问题。</summary>
        private const string c_MaxUnityVersionExclusive = "6000.5.0a0";

        /// <summary>本会话内"已提示 Unity 版本不受支持"标记键，避免同一会话每次域重载反复弹窗。</summary>
        private const string c_SessionVersionWarnedKey = "NovaSpark.VersionWarned";

        /// <summary>PlugPals 窗口菜单路径。用 ExecuteMenuItem 字符串调用，避免对框架类型的编译期依赖。</summary>
        private const string c_PlugPalsMenuPath = "Nova/Open PlugPals";

        /// <summary>安装完成后请求 Framework 检查工具链；固定字符串避免首装前引用 Framework 程序集。</summary>
        private const string c_ToolchainMenuPath = "Nova/Check and Setup AI Toolchain";

        /// <summary>
        /// 单轮域重载内"尝试打开 PlugPals 窗口"的最大轮询次数上限。
        /// 静态字段在每次域重载后重置为 0，故每轮重载都获得一份全新预算，
        /// 既能在本轮内等待菜单注册，又不会无限空转。
        /// </summary>
        private const int c_MaxOpenAttempts = 50;

        /// <summary>当前域重载内累计的打开尝试次数（域重载即归零）。</summary>
        private static int s_openAttempts;

        /// <summary>同一域中只请求一次自动安装；之后轮询实际结果，不反复重置重试间隔。</summary>
        private static bool s_ToolchainRequested;
        private static bool s_WaitWarningShown;

        /// <summary>按秒等待，避免同一帧耗尽包注册和菜单就绪预算。</summary>
        private static double s_NextRunTime;

        /// <summary>域重载后的实际安装包查询；Resolve 不返回请求对象，必须另行核对安装包图。</summary>
        private static UnityEditor.PackageManager.Requests.ListRequest s_InstalledPackagesRequest;

        // ---- manifest 待补全的依赖项 ----

        /// <summary>External Dependency Manager(EDM) 包键。Firebase/AppsFlyer/MAX 等的公共依赖，按策略由工程 manifest 显式固定，不靠各包传递。</summary>
        private const string c_EdmKey = "com.google.external-dependency-manager";

        /// <summary>EDM 版本（经 openupm registry 解析）。</summary>
        private const string c_EdmValue = "1.2.190";

        /// <summary>
        /// Nova 框架主包键。双重用途：
        /// (1) 安装目标——新工程经 Solotopia registry 拉取主包及其传递的 com.solotopia.* 子包；
        /// (2) "已安装 / 框架源工程"判据——manifest 已含该键且值为 file: 引用时静默退场；其他已装工程仍检测并经确认矫正。
        /// </summary>
        private const string c_FrameworkKey = "com.solotopia.nova.framework";

        /// <summary>Nova 框架主包版本（经 Solotopia registry 解析，会传递安装一众 com.solotopia.* 核心子包）。</summary>
        private const string c_FrameworkValue = "0.7.5";

        /// <summary>默认显式安装的 Nova 内存防护扩展包键；不随之安装付费 Anti-Cheat Toolkit。</summary>
        private const string c_MemoryProtectionKey = "com.solotopia.nova.framework.memory.protection";

        /// <summary>Nova 内存防护扩展包的固定目标版本，经 Solotopia registry 解析。</summary>
        private const string c_MemoryProtectionValue = "0.1.2";

        /// <summary>默认显式安装的 Nova MCP 中立契约包键，与 Unity MCP Provider 包分开管理。</summary>
        private const string c_NovaMcpKey = "com.solotopia.nova.framework.mcp";

        /// <summary>Nova MCP 中立契约包的固定目标版本；其 Unity MCP Provider 依赖经 OpenUPM 解析。</summary>
        private const string c_NovaMcpValue = "0.1.7";

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
            new RegistrySpec("Solotopia Internal", "http://172.16.22.175:4874", new[] { "com.onevcat", "cn.efunstudio" }),
            new RegistrySpec("package.openupm.com", "https://package.openupm.com", new[]
            {
                "com.google.external-dependency-manager",
                "com.coplaydev.unity-mcp",
            }),
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

        /// <summary>引导主流程：先拒绝支持范围外的 Unity 版本，再检测、确认、矫正，并通过域重载标记接力打开窗口和自动自删。</summary>
        /// <remarks>跨文件写入不提供事务回滚；发起 Client.Resolve 不等待成功，重试预算耗尽也可能保留标记。</remarks>
        private static void Run()
        {
            try
            {
                // Unity 版本门槛：仅允许 6000.4.x 且不低于最低版本，其余版本不执行任何检测或补全。
                // 不自删（便于用户切换到支持版本后重新触发引导）；每会话仅提示一次，避免域重载反复弹窗。
                if (!IsUnityVersionSupported())
                {
                    if (!SessionState.GetBool(c_SessionVersionWarnedKey, false))
                    {
                        SessionState.SetBool(c_SessionVersionWarnedKey, true);
                        EditorUtility.DisplayDialog(
                            $"{c_Name} · Unity 版本不受支持",
                            $"Nova 框架仅支持 Unity 6000.4.x（最低 {c_MinUnityVersion}），当前工程为 {Application.unityVersion}。\n\n" +
                            "更高的 Unity 次版本存在 HybridCLR 兼容问题。请使用支持范围内的版本后重试；在此之前引导器不会执行任何操作。",
                            "我知道了");
                        Debug.LogWarning($"[{c_Name}] Unity 版本不受支持（当前 {Application.unityVersion}，仅支持 6000.4.x 且最低 {c_MinUnityVersion}；更高次版本存在 HybridCLR 兼容问题），引导已跳过。");
                    }
                    return;
                }

                // 接力：上一轮已补全并落下"待打开 PlugPals"标记。此处先于其它判断处理，
                // 等框架编译完成、菜单可用后打开窗口，再功成身退；菜单暂不可用则下个 tick 续试。
                if (EditorPrefs.GetBool(PendingOpenKey, false)
                    || EditorPrefs.GetBool(PendingToolchainKey, false))
                {
                    if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                    {
                        ScheduleRun();
                        return;
                    }

                    if (!AreBootstrapPackagesReady())
                    {
                        return;
                    }

                    bool openReady = !EditorPrefs.GetBool(PendingOpenKey, false)
                        || EditorApplication.ExecuteMenuItem(c_PlugPalsMenuPath);
                    if (openReady)
                    {
                        // 窗口只打开一次，后续等待工具链自动安装绑定完成。
                        EditorPrefs.SetBool(PendingToolchainKey, true);
                        EditorPrefs.DeleteKey(PendingOpenKey);
                    }
                    if (openReady && !s_ToolchainRequested)
                    {
                        s_ToolchainRequested = EditorApplication.ExecuteMenuItem(c_ToolchainMenuPath);
                    }
                    if (s_ToolchainRequested)
                    {
                        string state = SessionState.GetString("Nova.AgentSkills.Toolchain.State.v1", string.Empty);
                        string identity = SessionState.GetString("Nova.AgentSkills.Toolchain.CheckedIdentity.v1", string.Empty);
                        if (state == "success" && identity.EndsWith("|" + c_FrameworkValue, StringComparison.Ordinal))
                        {
                            EditorPrefs.DeleteKey(PendingOpenKey);
                            EditorPrefs.DeleteKey(PendingToolchainKey);
                            Debug.Log($"[{c_Name}] Nova Framework 和 AI 工具链已自动安装、绑定并通过本地复核，引导脚本自动删除。");
                            DeleteSelf();
                        }
                        else ScheduleRun();
                    }
                    else if (s_openAttempts++ < c_MaxOpenAttempts)
                    {
                        // 框架尚在解析/编译，菜单还未注册，等下个编辑器 tick 再试。
                        ScheduleRun();
                    }
                    else
                    {
                        if (!s_WaitWarningShown)
                        {
                            s_WaitWarningShown = true;
                            Debug.LogWarning($"[{c_Name}] 正在等待工具链安装入口，保留引导脚本并继续自动重试。");
                        }
                        ScheduleRun(30);
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

                var missing = Detect(manifest);
                if (missing.Count == 0)
                {
                    // 环境已就绪：若是已装工程（用户重复拖入到已完成工程），火种使命已完成，直接自删走人；
                    // 否则（首装工程理论上不该出现 missing 为空，兜底）静默退出。
                    if (alreadyInstalled)
                    {
                        Debug.Log($"[{c_Name}] 配置无需矫正，编译稳定后仍会自动补齐、绑定并检查 AI 工具链。");
                        EditorPrefs.SetBool(PendingToolchainKey, true);
                        EditorApplication.delayCall += Run;
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
                    EditorPrefs.SetBool(PendingToolchainKey, true);
                    Client.Resolve();
                    AssetDatabase.Refresh();
                    // 与首装共用重载接力，工具链安装绑定通过后再自删；矫正模式不额外打开 PlugPals。
                    EditorApplication.delayCall += Run;
                    return;
                }

                Debug.Log($"[{c_Name}] Nova 框架引导完成，已补全 manifest 与 PlugPals 配置，UPM 正在重新解析。");

                // 此刻框架刚落盘、尚未编译完成，PlugPals 窗口还打不开。
                // 落下接力标记，待框架编译完成后的下一轮 Run 打开窗口并自删；
                // 同时排一次延迟回调兜底，防止个别情形下不触发域重载。
                EditorPrefs.SetBool(PendingOpenKey, true);
                Client.Resolve();
                EditorApplication.delayCall += Run;
            }
            catch (Exception e)
            {
                Debug.LogError($"[{c_Name}] 引导过程出错：{e}");
            }
        }

        /// <summary>查询 UPM 实际安装版本；Resolve 无返回结果，必须以目标包图而非旧菜单判断接力条件。</summary>
        /// <returns>实际 Framework 和配套直接包已按本轮目标注册时返回 true。</returns>
        private static bool AreBootstrapPackagesReady()
        {
            if (s_InstalledPackagesRequest == null)
            {
                s_InstalledPackagesRequest = Client.List(true, true);
            }
            if (!s_InstalledPackagesRequest.IsCompleted)
            {
                ScheduleRun();
                return false;
            }
            if (s_InstalledPackagesRequest.Status == StatusCode.Failure)
            {
                if (!s_WaitWarningShown)
                {
                    s_WaitWarningShown = true;
                    Debug.LogWarning($"[{c_Name}] UPM 实际安装结果尚未就绪，保留引导脚本，稍后自动重新解析和核对。");
                }
                s_InstalledPackagesRequest = null;
                Client.Resolve();
                ScheduleRun(30);
                return false;
            }

            bool framework = false, novaMcp = false, memory = false, edm = false;
            foreach (UnityEditor.PackageManager.PackageInfo package in s_InstalledPackagesRequest.Result)
            {
                if (package.errors != null && package.errors.Length > 0) continue;
                if (package.name == c_FrameworkKey && package.version == c_FrameworkValue) framework = true;
                if (package.name == c_NovaMcpKey && package.version == c_NovaMcpValue) novaMcp = true;
                if (package.name == c_MemoryProtectionKey && package.version == c_MemoryProtectionValue) memory = true;
                if (package.name == c_EdmKey && package.version == c_EdmValue) edm = true;
            }
            if (framework && novaMcp && memory && edm)
            {
                // UPM 列表与 Editor 已注册包都达到目标，避免列表已更新而加载中的程序集仍属于旧包。
                UnityEditor.PackageManager.PackageInfo[] registered = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
                if (registered != null && Array.Exists(registered,
                    package => package.name == c_FrameworkKey && package.version == c_FrameworkValue
                        && (package.errors == null || package.errors.Length == 0)))
                {
                    return true;
                }
            }

            // 包图可能仍处于重载切换窗口；有限次重查，耗尽后保留真实未完成状态。
            s_InstalledPackagesRequest = null;
            if (s_openAttempts++ < c_MaxOpenAttempts)
            {
                ScheduleRun();
            }
            else
            {
                if (!s_WaitWarningShown)
                {
                    s_WaitWarningShown = true;
                    Debug.LogWarning($"[{c_Name}] 实际安装包尚未达到目标，保留引导脚本并自动重新解析；不会仅凭旧菜单判断完成。");
                }
                s_openAttempts = 0;
                Client.Resolve();
                ScheduleRun(30);
            }
            return false;
        }

        /// <summary>有等待项时按秒继续推进；失败状态由 Framework 自动重试，不要求项目组补装。</summary>
        private static void ScheduleRun(double delaySeconds = 1)
        {
            s_NextRunTime = EditorApplication.timeSinceStartup + delaySeconds;
            EditorApplication.update -= TryRunScheduled;
            EditorApplication.update += TryRunScheduled;
        }

        /// <summary>时间到达后只执行一次，域重载由静态入口自动接力。</summary>
        private static void TryRunScheduled()
        {
            if (EditorApplication.timeSinceStartup < s_NextRunTime) return;
            EditorApplication.update -= TryRunScheduled;
            Run();
        }

        /// <summary>检测新工程与已装工程的默认直接依赖、registry 及配置缺项，统一核对固定目标版本。</summary>
        /// <param name="manifest">当前工程 manifest；Framework 的 file: 源工程已由调用方提前跳过。</param>
        /// <returns>缺失项及与本版目标不一致的可读说明。</returns>
        private static List<string> Detect(JObj manifest)
        {
            var missing = new List<string>();

            var deps = manifest.Get("dependencies") as JObj;
            // 四项都作为工程直接依赖检测；已有传递安装结果不能代替 manifest 中的明确声明。
            DetectDepVersion(deps, missing, c_FrameworkKey, c_FrameworkValue, "Nova Framework");
            DetectDepVersion(deps, missing, c_MemoryProtectionKey, c_MemoryProtectionValue, "Nova Framework - MemoryProtection");
            DetectDepVersion(deps, missing, c_NovaMcpKey, c_NovaMcpValue, "Nova Framework - MCP");
            DetectDepVersion(deps, missing, c_EdmKey, c_EdmValue, "EDM");

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

        /// <summary>执行补全：显式写入四项默认依赖，合并 registry 与 PlugPals 配置并触发 UPM 解析。</summary>
        /// <param name="manifest">已获得用户确认的当前工程 manifest。</param>
        /// <param name="manifestPath">当前工程 manifest 的实际写入路径。</param>
        private static void Apply(JObj manifest, string manifestPath)
        {
            // 1) dependencies：缺键补齐，已有值与固定目标不一致时覆盖，不比较版本高低。
            var deps = manifest.Get("dependencies") as JObj;
            if (deps == null)
            {
                deps = new JObj();
                manifest.Set("dependencies", deps);
            }
            // 固定工程的直接安装目标；Nova MCP 的 Provider 继续由其依赖解析，ACTk 仍由获授权项目另行安装。
            UpsertDepVersion(deps, c_FrameworkKey, c_FrameworkValue);
            UpsertDepVersion(deps, c_MemoryProtectionKey, c_MemoryProtectionValue);
            UpsertDepVersion(deps, c_NovaMcpKey, c_NovaMcpValue);
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

        /// <summary>当前 Unity 版本是否处于 6000.4.x 支持范围，含最低版本、不含下一次版本线的首个预发行版。</summary>
        /// <returns>仅当前版本不低于最低版本且低于支持范围上界时返回 true；非法版本返回 false。</returns>
        private static bool IsUnityVersionSupported()
        {
            return CompareUnityVersion(Application.unityVersion, c_MinUnityVersion) >= 0 &&
                   CompareUnityVersion(Application.unityVersion, c_MaxUnityVersionExclusive) < 0;
        }

        /// <summary>
        /// 比较两个 Unity 版本字符串（形如 "6000.4.2f1"），先比较三段数值，再按 a、b、f、p 阶段及修订号比较。
        /// 同一 patch 的预发行版不能满足正式版门槛；无法解析的版本视为零版本。
        /// </summary>
        /// <param name="a">待比较的 Unity 完整版本。</param>
        /// <param name="b">作为门槛的 Unity 完整版本。</param>
        /// <returns>负数表示 a 较低，零表示相等，正数表示 a 较高。</returns>
        private static int CompareUnityVersion(string a, string b)
        {
            int[] va = ParseUnityVersion(a);
            int[] vb = ParseUnityVersion(b);
            for (int i = 0; i < va.Length; i++)
            {
                if (va[i] != vb[i])
                    return va[i] < vb[i] ? -1 : 1;
            }
            return 0;
        }

        /// <summary>解析 Unity 的主、次、patch、发布阶段及修订号；允许中国版 c 后缀，不改变基础版本顺序。</summary>
        /// <param name="version">完整 Unity 版本；不完整、未知发布阶段或整数溢出均视为零版本。</param>
        /// <returns>五段排序值；非法输入返回全零数组。</returns>
        private static int[] ParseUnityVersion(string version)
        {
            int[] result = new int[5];
            Match match = Regex.Match(version ?? string.Empty, @"^(\d+)\.(\d+)\.(\d+)([abfp])(\d+)(?:c\d+)?$");
            if (!match.Success)
                return result;
            for (int i = 0; i < 3; i++)
            {
                if (!int.TryParse(match.Groups[i + 1].Value, out result[i]))
                    return new int[5];
            }
            result[3] = "abfp".IndexOf(match.Groups[4].Value[0]);
            if (!int.TryParse(match.Groups[5].Value, out result[4]))
                return new int[5];
            return result;
        }

        /// <summary>"待打开 PlugPals"接力标记键。EditorPrefs 为全用户级，故按工程路径区分，避免跨工程串扰。</summary>
        private static string PendingOpenKey => "NovaSpark.PendingOpenPlugPals." + ProjectRoot;

        /// <summary>已有工程矫正或重复导入后的工具链检查接力标记，按项目隔离并在本地安装绑定验证通过后删除。</summary>
        private static string PendingToolchainKey => "NovaSpark.PendingToolchainValidation." + ProjectRoot;

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

        /// <summary>检查默认直接依赖是否缺失或与固定目标不一致，供新工程和已装工程共用。</summary>
        /// <param name="deps">当前直接依赖对象；缺少整个对象时按缺失处理。</param>
        /// <param name="missing">收集待用户确认的配置调整说明。</param>
        /// <param name="key">准确的 UPM 包名。</param>
        /// <param name="value">本版固定目标版本，不比较版本高低。</param>
        /// <param name="label">包的可读名称。</param>
        private static void DetectDepVersion(JObj deps, List<string> missing, string key, string value, string label)
        {
            if (deps == null || !deps.ContainsKey(key))
                missing.Add($"依赖 {key}（{label}，目标版本 {value}）");
            else if ((deps.Get(key) as string) != value)
                missing.Add($"依赖 {key}（{label}）版本与目标不一致，需对齐为 {value}");
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

        /// <summary>保序 JSON 对象；重复键覆盖旧值，首次插入位置保持不变。</summary>
        /// <remarks>Keys 返回内部列表的只读接口而非快照；Get 不区分缺键与显式 null。本类型不验证 JSON schema。</remarks>
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

        /// <summary>启动引导内部使用的保序 JSON 解析与序列化，不是严格或通用 JSON 实现。</summary>
        /// <remarks>
        /// 数字以 double 表示，可能丢失原文本精度；解析不完整验证字面量、冒号和全部转义，写入也不转义所有控制字符。
        /// 不限制递归深度或循环引用，调用方仅应使用已知清单模型，不用于不可信输入、通用校验或安全路径解析。
        /// </remarks>
        private static class Json
        {
            /// <summary>解析启动脚本内部使用的 JSON 值，不作为通用严格 JSON 校验器。</summary>
            /// <param name="text">待解析文本；调用方负责提供预期的清单结构。</param>
            /// <returns>内部保序对象、数组或标量；只读取第一个值，不要求消耗全部尾部文本。</returns>
            /// <remarks>无递归深度限制，畸形或截断输入可能直接抛出解析或索引异常。</remarks>
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

            /// <summary>将内部 JSON 模型写成保序文本并追加换行，不写入文件。</summary>
            /// <param name="value">由内部解析器或调用方按支持类型构造的值。</param>
            /// <returns>格式化文本；不保证任意业务对象都能生成合法 JSON。</returns>
            /// <remarks>不做 schema 或递归深度校验，不替代项目通用序列化器。</remarks>
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
