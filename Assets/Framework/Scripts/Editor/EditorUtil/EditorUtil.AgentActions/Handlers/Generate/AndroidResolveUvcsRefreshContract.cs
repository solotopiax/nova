/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveUvcsRefreshContract.cs
 * author:    taoye
 * created:   2026/11/17
 * descrip:   UVCS禁用刷新分支及固定日志缓存的独立合同
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>不启动UVCS、不创建repository；只在已存在准确默认日志设施中接纳两个固定日志项的有限内存物化。</summary>
    internal static class AndroidResolveUvcsRefreshContract
    {
        private const string CallbackName = "Unity.PlasticSCM.Editor.AssetUtils.Processor.UVCSAssetPostprocessor";
        private const string DefaultRepository = "log4net-default-repository";
        private static readonly string[] Names = { "PlasticApp", "UVCSAssetPostprocessor" };
        private static readonly BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        private static readonly string Domain = Guid.NewGuid().ToString("N");
        private static readonly ConditionalWeakTable<object, Identity> Identities = new ConditionalWeakTable<object, Identity>();

        /// <summary>仅用本域弱身份，不调用厂商Equals/GetHashCode。</summary>
        private sealed class Identity { internal readonly string value = Guid.NewGuid().ToString("N"); }

        /// <summary>两个固定名称的完整日志条目；其它条目全部留在固定行中。</summary>
        [Serializable]
        internal sealed class Entry
        {
            public string name;
            public string logger;
            public string key;
            public string wrapper;
            public string[] loggerRows;
            public string[] wrapperRows;
            public string handler;
        }

        /// <summary>完整相关容器、配置、已有条目和有序订阅；不丢弃Plan首次物化前态。</summary>
        [Serializable]
        internal sealed class LogState
        {
            public string[] fixedRows;
            public Entry[] targets;
            public string[] configurationHandlers;
            public string sha256;
        }

        /// <summary>包缺席也是准确状态；同域前后材料和实际禁用状态均进入不可变摘要。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            public int version = 1;
            public string domain;
            public bool present;
            public string source;
            public string preferenceKey;
            public bool preferenceDefined;
            public bool preferenceValue;
            public LogState before;
            public LogState after;
            public string[] stateRows;
            public string afterSha256;
            public string sha256;
            [NonSerialized] internal object[] references;
        }

        /// <summary>捕获时仅保存已存在对象；方法不调用任何日志、仓库创建或插件初始化API。</summary>
        private sealed class World
        {
            internal Assembly logAssembly, plasticAssembly;
            internal Type callback, app, netLogger, skeleton, loggerBase, logImpl, loggerWrapper;
            internal object root, repository, selector, levelMap;
            internal Hashtable wrappers, loggers, levels;
            internal string source;
        }

        /// <summary>已有正常派发证明时复用原禁用状态检查；仅未初始化时捕获冷态日志合同，不给已有日志配置追加限制。</summary>
        internal static bool TryCaptureForPlan(CancellationToken token, out Snapshot snapshot, out string error)
        {
            token.ThrowIfCancellationRequested();
            snapshot = null;
            error = null;
            Type callback = FindOptional(CallbackName);
            if (callback == null || AndroidRefreshInitializationWitness.TryGetProof(callback, out _, out _)) return true;
            return TryCapture(token, true, out snapshot, out error);
        }

        /// <summary>只有缺少正常派发证明的新Plan可以授予首次固定日志缓存物化；后续重核只接受已有完整目标项。</summary>
        internal static bool TryCapture(CancellationToken token, bool allowMaterialization, out Snapshot snapshot, out string error)
        {
            snapshot = null; error = null;
            try
            {
                token.ThrowIfCancellationRequested();
                Type callback = FindOptional(CallbackName);
                if (callback == null)
                {
                    snapshot = new Snapshot { domain = Domain, present = false, source = "absent", stateRows = Array.Empty<string>(), references = Array.Empty<object>() };
                    Finish(snapshot); Require(IsValid(snapshot), "UVCS缺席快照结构不完整。"); return true;
                }
                using var read = AndroidRefreshSourceIdentityContract.BeginReadScope();
                AndroidResolvePeripheralRefreshContracts.VerifyModule(typeof(EditorPrefs), token);
                AndroidResolvePeripheralRefreshContracts.VerifyModule(typeof(PlayerSettings), token);
                World world = OpenWorld(callback, token);
                string key = string.Format("{0}_AutomaticAdd", PlayerSettings.productGUID);
                bool defined = EditorPrefs.HasKey(key), preference = EditorPrefs.GetBool(key, true);
                // 厂商顺序是Hierarchy/m_ht后WrapperMap；这里持有WrapperMap、selector及m_ht，
                // 已拒绝创建事件，受审调用同线程可重入，避免夹入另一个日志缓存写入。
                lock (world.root) lock (world.selector) lock (world.loggers)
                {
                    LogState before = CaptureLog(world);
                    if (!allowMaterialization && before.targets.Any(item => item.logger == null || item.wrapper == null))
                        throw Failure("UVCS固定日志项尚未存在；写前、写后或Verify不再授予首次物化许可。");
                    Type shim = world.plasticAssembly.GetType("Codice.LogWrapper.LogManager", true);
                    Require(shim.TypeInitializer == null && Read(shim, "mLoggerForTesting", null) == null,
                        "UVCS日志存在测试替身或未知静态初始化。");
                    // NetLogger的已审cctor先尝试CreateRepository，准确默认repo已存在时仅抛纯LogException，
                    // catch按名称取回同实例；本前置已排除selector初始化、仓库创建、别名及配置读取。
                    Require(ReferenceEquals(Read(world.netLogger, "mDefaultLoggerRepository", null), world.repository),
                        "NetLogger默认仓库与已存在的准确repo不一致。");
                    object appLog = Read(world.app, "mLog", null);
                    object callbackLog = Read(callback, "mLog", null);
                    LogState after = CaptureLog(world);
                    Require(Read(callback, "mIsEnabled", null) is bool enabled && !enabled &&
                        Read(callback, "mWkPath", null) == null && Read(callback, "mUVCSAssetsProcessor", null) == null,
                        "UVCS实际启用或存在workspace/processor；不关闭插件、清队列或假设冷态为禁用。");
                    Require(Read(callback, "mIsRepaintNeededAfterAssetDatabaseRefresh", null) is bool &&
                        Read(callback, "<IsAutomaticAddEnabled>k__BackingField", null) is bool,
                        "UVCS禁用状态字段签名变化。");
                    Require(EditorPrefs.HasKey(key) == defined && EditorPrefs.GetBool(key, true) == preference,
                        "UVCS准确自动添加偏好的存在性或有效值变化。");
                    Require(TryValidateLogTransition(before, after, allowMaterialization, out string transitionError), transitionError);
                    RequireNetLogger(world, appLog, after.targets[0]);
                    RequireNetLogger(world, callbackLog, after.targets[1]);
                    var stateRows = new List<string>();
                    stateRows.Add("netLoggerDefaultRepository=" + Id(Read(world.netLogger, "mDefaultLoggerRepository", null)));
                    CaptureFields("uvcs", callback, null, stateRows, false);
                    CaptureFields("plasticApp", world.app, null, stateRows, false);
                    stateRows.Sort(StringComparer.Ordinal);
                    snapshot = new Snapshot { domain = Domain, present = true, source = world.source,
                        preferenceKey = key, preferenceDefined = defined, preferenceValue = preference,
                        before = before, after = after, stateRows = stateRows.ToArray(),
                        references = new[] { callback.Assembly, world.logAssembly, world.plasticAssembly, world.root,
                            world.repository, world.selector, world.wrappers, world.loggers, world.levelMap, appLog, callbackLog } };
                    Finish(snapshot); Require(IsValid(snapshot), "UVCS完整快照结构或有限转换不完整。"); return true;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { error = "UVCS独立刷新合同未闭合：" + exception.Message; return false; }
        }

        /// <summary>case69只返回真实after稳定摘要及引用；首次物化必须已由独立Plan捕获完成。</summary>
        internal static bool TryCaptureCallback(Type callback, CancellationToken token, out AndroidResolveDefaultPackageCallbacks.State state, out string error)
        {
            state = null;
            if (callback == null || callback.FullName != CallbackName) { error = "UVCS回调身份不一致。"; return false; }
            if (!TryCapture(token, false, out Snapshot snapshot, out error) || !snapshot.present) return false;
            state = new AndroidResolveDefaultPackageCallbacks.State { Digest = snapshot.afterSha256, References = snapshot.references };
            return true;
        }

        /// <summary>旧快照为null由调用方保留旧合同；新快照须完整同域，不能自签或删掉首次前态。</summary>
        internal static bool IsValid(Snapshot snapshot)
        {
            if (snapshot == null || snapshot.version != 1 || snapshot.domain != Domain || snapshot.stateRows == null || snapshot.stateRows.Length > 256) return false;
            if (snapshot.present)
            {
                if (string.IsNullOrEmpty(snapshot.preferenceKey) || string.IsNullOrEmpty(snapshot.source) ||
                    !ValidLog(snapshot.before) || !ValidLog(snapshot.after) || !TryValidateLogTransition(snapshot.before, snapshot.after, true, out _)) return false;
            }
            else if (snapshot.source != "absent" || snapshot.before != null || snapshot.after != null || snapshot.preferenceKey != null || snapshot.stateRows.Length != 0) return false;
            return snapshot.afterSha256 == AfterHash(snapshot) && snapshot.sha256 == SnapshotHash(snapshot);
        }

        /// <summary>执行前后均严格绑定Plan已捕获after；不在后续阶段继承首次物化权限。</summary>
        internal static bool TryValidateTransition(Snapshot planned, Snapshot current, out string error)
        {
            error = null;
            if (!IsValid(planned) || !IsValid(current) || planned.afterSha256 != current.afterSha256)
                error = "UVCS来源、偏好、实际禁用状态或Plan已证日志after变化。";
            return error == null;
        }

        /// <summary>Receipt与当前均绑定真实完成态，不重新授予缺失日志缓存创建。</summary>
        internal static bool SameAfter(Snapshot receiptAfter, Snapshot current) => TryValidateTransition(receiptAfter, current, out _);

        /// <summary>只声明新Plan读取时真实发生的固定内存变化，不授予文件、偏好、仓库或网络写入。</summary>
        internal static string[] GetDeclaredEffects(Snapshot snapshot) => snapshot?.present == true
            ? snapshot.before.targets.Where(item => item.logger == null || item.wrapper == null)
                .Select(item => "UVCS固定日志缓存:" + item.name + "；仅已存在默认repo内的LoggerImpl/LogImpl及准确ConfigurationChanged内存订阅，不读配置、不创建repo或写文件/网络/偏好").ToArray()
            : Array.Empty<string>();

        /// <summary>以已审纯WrapperMap为根；没有已存在默认repo时停止，绝不读取LoggerManager冷字段。</summary>
        private static World OpenWorld(Type callback, CancellationToken token)
        {
            Require(AndroidRefreshSourceIdentityContract.TryVerifySourceAssembly(callback, token, out string source, out string sourceError), sourceError);
            PackageInfo package = PackageInfo.FindForAssembly(callback.Assembly);
            Require(package?.name == "com.unity.collab-proxy" && package.version == "2.12.4", "UVCS包版本不属于已审2.12.4。");
            Assembly log = VerifyDll(package.resolvedPath, "log4netPlastic.dll", "log4net", "e1a77b5d-fa0c-4a1f-8ba6-16c017f5e0b8",
                "93caa5fc6a8cb9e2700742e17dea1d0af623566b045e2cc661cdce419eeabc77", token);
            Assembly plastic = VerifyDll(package.resolvedPath, "unityplastic.dll", "unityplastic", "feec3139-f4c0-4de6-871f-4a301f2c1526",
                "254b3351252b64618e589876aa22d6796a2ce17cae5f9f4b5364d15acb14c11a", token);
            Type manager = log.GetType("log4net.LogManager", true);
            Require(BitConverter.ToString(manager.TypeInitializer.GetMethodBody().GetILAsByteArray()).Replace("-", "") ==
                "14FE06550800067338040006733C04000680160300042A", "LogManager纯根cctor身份变化。");
            object root = Read(manager, "s_wrapperMap", null);
            Require(Exact(root, log, "log4net.Core.WrapperMap"), "日志WrapperMap根身份变化。");
            RequireDelegate(Read(root.GetType(), "m_createWrapperHandler", root) as Delegate, manager, "WrapperCreationHandler", null);
            RequireDelegate(Read(root.GetType(), "m_shutdownHandler", root) as Delegate, root.GetType(), "ILoggerRepository_Shutdown", root);
            Hashtable repositories = Table(Read(root.GetType(), "m_repositories", root), false, "WrapperMap.m_repositories");
            Require(repositories.Count == 1, "UVCS要求已存在唯一默认日志repo；缺失时不触发LoggerManager初始化，额外repo当前未扩审。");
            DictionaryEntry existing = repositories.Cast<DictionaryEntry>().Single();
            Type skeleton = log.GetType("log4net.Repository.LoggerRepositorySkeleton", true);
            Require(Exact(existing.Key, log, "log4net.Repository.Hierarchy.Hierarchy") &&
                (string)Read(skeleton, "m_name", existing.Key) == DefaultRepository, "纯日志根中没有准确默认Hierarchy repo。");
            // 已审整个固定DLL的s_wrapperMap访问链：只有LogManager.cctor写根，唯一读取在private WrapLogger，
            // 其所有调用均先经过LoggerManager API。因此已有repo不是把冷LoggerManager字段读热的猜测。
            Type managerCore = log.GetType("log4net.Core.LoggerManager", true);
            object selector = Read(managerCore, "s_repositorySelector", null);
            Require(Exact(selector, log, "log4net.Core.DefaultRepositorySelector"), "日志使用自定义repository selector。");
            Require(Read(selector.GetType(), "m_defaultRepositoryType", selector) as Type == existing.Key.GetType() &&
                Read(selector.GetType(), "m_loggerRepositoryCreatedEvent", selector) == null, "默认repository类型或创建事件未知。");
            Hashtable named = Table(Read(selector.GetType(), "m_name2repositoryMap", selector), false, "DefaultRepositorySelector.m_name2repositoryMap");
            Require(named.Count == 1 && named.Cast<DictionaryEntry>().Any(item => item.Key as string == DefaultRepository && ReferenceEquals(item.Value, existing.Key)),
                "默认selector的准确repository映射不一致。");
            Require(Table(Read(selector.GetType(), "m_alias2repositoryMap", selector), false, "DefaultRepositorySelector.m_alias2repositoryMap").Count == 0, "日志repository存在未审别名。");
            object repository = existing.Key;
            Require(Exact(Read(repository.GetType(), "m_defaultFactory", repository), log, "log4net.Repository.Hierarchy.DefaultLoggerFactory") &&
                Read(repository.GetType(), "m_loggerCreatedEvent", repository) == null, "日志factory或logger创建事件未知。");
            Require(Read(skeleton, "m_configured", repository) is bool configured && !configured &&
                Read(skeleton, "m_configurationResetEvent", repository) == null, "默认repository已有未审配置或Reset订阅。");
            object levelMap = Read(skeleton, "m_levelMap", repository);
            Require(Exact(levelMap, log, "log4net.Core.LevelMap"), "日志级别表身份变化。");
            var world = new World { logAssembly = log, plasticAssembly = plastic, callback = callback,
                app = callback.Assembly.GetType("Unity.PlasticSCM.Editor.PlasticApp", true),
                netLogger = plastic.GetType("Codice.LogWrapper.NetLogger", true), skeleton = skeleton,
                loggerBase = log.GetType("log4net.Repository.Hierarchy.Logger", true), logImpl = log.GetType("log4net.Core.LogImpl", true),
                loggerWrapper = log.GetType("log4net.Core.LoggerWrapperImpl", true), root = root, repository = repository, selector = selector,
                wrappers = Table(existing.Value, false, "WrapperMap.defaultWrappers"),
                loggers = Table(Read(repository.GetType(), "m_ht", repository), false, "Hierarchy.m_ht"),
                levelMap = levelMap, levels = Table(Read(levelMap.GetType(), "m_mapName2Level", levelMap), true, "LevelMap.m_mapName2Level"),
                source = source + "|log4net:e1a77b5d-fa0c-4a1f-8ba6-16c017f5e0b8|unityplastic:feec3139-f4c0-4de6-871f-4a301f2c1526" };
            foreach (string level in new[] { "DEBUG", "INFO", "WARN", "ERROR", "FATAL" })
                Require(world.levels.Cast<DictionaryEntry>().Any(item => item.Key as string == level && Exact(item.Value, log, "log4net.Core.Level")),
                    "默认级别缺失，LogImpl.ReloadLevels可能物化未授权级别表：" + level);
            Require(Exact(Read(repository.GetType(), "m_root", repository), log, "log4net.Repository.Hierarchy.RootLogger"),
                "Hierarchy root尚未存在，不能授予root创建。");
            return world;
        }

        /// <summary>捕获所有容器条目和相关实例字段；动态许可严格限制两个固定名称。</summary>
        private static LogState CaptureLog(World world)
        {
            var fixedRows = new List<string>();
            Require(ReferenceEquals(Read(world.logAssembly.GetType("log4net.Core.LoggerManager", true), "s_repositorySelector", null), world.selector),
                "日志全局repository selector引用变化。");
            CaptureFields("wrapperMap", world.root.GetType(), world.root, fixedRows, true);
            Hashtable repositories = Table(Read(world.root.GetType(), "m_repositories", world.root), false, "WrapperMap.m_repositories");
            Require(repositories.Count == 1 && repositories.Cast<DictionaryEntry>().Any(item => ReferenceEquals(item.Key, world.repository) && ReferenceEquals(item.Value, world.wrappers)),
                "日志默认repo或wrapper子表发生变化。");
            CaptureTable("wrapperRepositories", repositories, fixedRows);
            CaptureTableIdentity("Hierarchy.m_ht", world.loggers, fixedRows);
            CaptureTableIdentity("WrapperMap.defaultWrappers", world.wrappers, fixedRows);
            CaptureFields("selector", world.selector.GetType(), world.selector, fixedRows, true);
            foreach (string field in new[] { "m_name2repositoryMap", "m_assembly2repositoryMap", "m_alias2repositoryMap" })
                CaptureTable("selector:" + field, Table(Read(world.selector.GetType(), field, world.selector), false, "DefaultRepositorySelector." + field), fixedRows);
            CaptureFields("repository", world.repository.GetType(), world.repository, fixedRows, true, "m_configurationChangedEvent");
            object rootLogger = Read(world.repository.GetType(), "m_root", world.repository);
            CaptureLogger("rootLogger", world, rootLogger, fixedRows, false);
            CaptureFields("levels", world.levelMap.GetType(), world.levelMap, fixedRows, true);
            CaptureTable("levelMap", world.levels, fixedRows);
            foreach (DictionaryEntry level in world.levels) CaptureFields("level:" + Scalar(level.Key), level.Value.GetType(), level.Value, fixedRows, true);
            foreach (var role in new[] { new[] { "m_rendererMap", "m_map", "m_cache" }, new[] { "m_pluginMap", "m_mapName2Plugin" }, new[] { "m_properties", "m_hashtable" } })
            {
                object value = Read(world.skeleton, role[0], world.repository);
                Require(value != null && value.GetType().Assembly == world.logAssembly, "默认日志配置对象来源未知：" + role[0]);
                CaptureFields(role[0], value.GetType(), value, fixedRows, true);
                foreach (string table in role.Skip(1))
                {
                    Hashtable dictionary = Table(ReadDeep(value.GetType(), table, value), true, value.GetType().FullName + "." + table);
                    Require(dictionary.Count == 0, "默认repository存在未审renderer/plugin/property配置：" + role[0]);
                    CaptureTable(role[0] + ":" + table, dictionary, fixedRows);
                }
            }
            object messages = Read(world.skeleton, "m_configurationMessages", world.repository);
            if (Exact(messages, world.logAssembly, "log4net.Util.EmptyCollection"))
            {
                // 该准确发行的空历史集合不被固定创建链消费；保留身份，不调用厂商Count或枚举器。
                fixedRows.Add("configurationMessages:container=" + Scalar(messages) + ":count=0");
                CaptureFields("configurationMessages", messages.GetType(), messages, fixedRows, true);
            }
            else
                Require(messages == null || messages.GetType() == typeof(ArrayList) && ((ArrayList)messages).Count == 0 ||
                    messages.GetType().IsArray && ((Array)messages).Length == 0,
                    "日志配置消息集合类型未适配：LoggerRepositorySkeleton.m_configurationMessages；实际类型=" + (messages?.GetType().FullName ?? "null"));
            var targets = Names.Select(name => new Entry { name = name, loggerRows = Array.Empty<string>(), wrapperRows = Array.Empty<string>() }).ToArray();
            var loggers = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry item in world.loggers)
            {
                Require(Exact(item.Key, world.logAssembly, "log4net.Repository.Hierarchy.LoggerKey") &&
                    Exact(item.Value, world.logAssembly, "log4net.Repository.Hierarchy.DefaultLoggerFactory+LoggerImpl"),
                    "日志表包含ProvisionNode、自定义Logger或未知key。");
                string name = Read(item.Key.GetType(), "m_name", item.Key) as string;
                Require(name != null && !loggers.ContainsKey(name) && Read(world.loggerBase, "m_name", item.Value) as string == name, "日志key与Logger名称不一致。");
                loggers.Add(name, item.Value);
                int index = Array.IndexOf(Names, name);
                var rows = new List<string>();
                CaptureFields("key", item.Key.GetType(), item.Key, rows, true);
                CaptureLogger("logger", world, item.Value, rows, index >= 0);
                rows.Sort(StringComparer.Ordinal);
                if (index >= 0) { targets[index].logger = Id(item.Value); targets[index].key = Id(item.Key); targets[index].loggerRows = rows.ToArray(); }
                else fixedRows.AddRange(rows.Select(row => "logger:" + GenerateActionCommon.ComputeTextHash(name) + ":" + row));
            }
            var wrappers = new List<object>();
            foreach (DictionaryEntry item in world.wrappers)
            {
                Require((ReferenceEquals(item.Key, rootLogger) || loggers.Values.Any(logger => ReferenceEquals(logger, item.Key))) && item.Value?.GetType() == world.logImpl,
                    "日志wrapper存在未知角色或类型。");
                Require(ReferenceEquals(Read(world.loggerWrapper, "m_logger", item.Value), item.Key), "LogImpl没有指向准确原Logger。");
                wrappers.Add(item.Value);
                string name = Read(world.loggerBase, "m_name", item.Key) as string;
                int index = Array.IndexOf(Names, name);
                var rows = new List<string>(); CaptureFields("wrapper", item.Value.GetType(), item.Value, rows, true); rows.Sort(StringComparer.Ordinal);
                foreach (var role in new[] { new[] { "m_levelDebug", "DEBUG" }, new[] { "m_levelInfo", "INFO" }, new[] { "m_levelWarn", "WARN" }, new[] { "m_levelError", "ERROR" }, new[] { "m_levelFatal", "FATAL" } })
                    Require(world.levels.Cast<DictionaryEntry>().Any(level => level.Key as string == role[1] && ReferenceEquals(Read(world.logImpl, role[0], item.Value), level.Value)), "LogImpl缓存级别与原级别表不一致。");
                string handler = HandlerKey(world.logImpl.GetMethod("LoggerRepositoryConfigurationChanged", BindingFlags.NonPublic | BindingFlags.Instance), item.Value);
                if (index >= 0) { targets[index].wrapper = Id(item.Value); targets[index].wrapperRows = rows.ToArray(); targets[index].handler = handler; }
                else { fixedRows.AddRange(rows.Select(row => "wrapper:" + GenerateActionCommon.ComputeTextHash(name ?? "") + ":" + row)); fixedRows.Add("wrapper-id:" + Id(item.Key) + "=" + Id(item.Value)); }
            }
            Delegate shutdown = Read(world.skeleton, "m_shutdownEvent", world.repository) as Delegate;
            Require(shutdown != null && shutdown.GetInvocationList().Length == 1, "默认repo shutdown订阅不属于已审已有WrapperMap。");
            RequireDelegate(shutdown, world.root.GetType(), "ILoggerRepository_Shutdown", world.root);
            Delegate changed = Read(world.skeleton, "m_configurationChangedEvent", world.repository) as Delegate;
            Delegate[] handlers = changed?.GetInvocationList() ?? Array.Empty<Delegate>();
            Require(handlers.Length == wrappers.Count, "ConfigurationChanged存在未审额外订阅。");
            foreach (Delegate handler in handlers)
                Require(handler.Method.DeclaringType == world.logImpl && handler.Method.Name == "LoggerRepositoryConfigurationChanged" &&
                    wrappers.Any(wrapper => ReferenceEquals(wrapper, handler.Target)), "ConfigurationChanged存在未知委托。");
            fixedRows.Sort(StringComparer.Ordinal);
            var result = new LogState { fixedRows = fixedRows.ToArray(), targets = targets,
                configurationHandlers = handlers.Select(handler => HandlerKey(handler.Method, handler.Target)).ToArray() };
            result.sha256 = LogHash(result); return result;
        }

        /// <summary>完整保留既有条目和订阅顺序；仅允许固定叶名称的新Logger/LogImpl及准确追加订阅。</summary>
        internal static bool TryValidateLogTransition(LogState before, LogState after, bool allowMaterialization, out string error)
        {
            error = null;
            if (!ValidLog(before) || !ValidLog(after) || !before.fixedRows.SequenceEqual(after.fixedRows))
            { error = "UVCS日志原固定容器、配置或既有角色变化。"; return false; }
            var additions = new List<string>();
            for (int i = 0; i < Names.Length; i++)
            {
                Entry old = before.targets[i], current = after.targets[i];
                if (current.logger == null || current.wrapper == null || current.handler == null) { error = "UVCS固定日志物化后角色不完整。"; return false; }
                if (old.logger == null)
                { if (!allowMaterialization) { error = "当前阶段不允许首次Logger物化。"; return false; } }
                else if (old.logger != current.logger || old.key != current.key || !old.loggerRows.SequenceEqual(current.loggerRows))
                { error = "UVCS原有Logger或key状态变化。"; return false; }
                if (old.wrapper == null)
                { if (!allowMaterialization) { error = "当前阶段不允许首次LogImpl物化。"; return false; } additions.Add(current.handler); }
                else if (old.wrapper != current.wrapper || old.handler != current.handler || !old.wrapperRows.SequenceEqual(current.wrapperRows))
                { error = "UVCS原有LogImpl或订阅身份变化。"; return false; }
            }
            if (!after.configurationHandlers.SequenceEqual(before.configurationHandlers.Concat(additions)))
            { error = "ConfigurationChanged增量不是固定PlasticApp/UVCS LogImpl的准确只读处理器。"; return false; }
            return true;
        }

        /// <summary>固定目标是无点叶名称，默认factory只连已有root，不准创建ProvisionNode或修改其它Logger。</summary>
        private static void CaptureLogger(string role, World world, object logger, List<string> rows, bool target)
        {
            Require(Read(world.loggerBase, "m_appenderAttachedImpl", logger) == null, "日志存在未审appender配置。");
            if (target) Require(Read(world.loggerBase, "m_level", logger) == null &&
                ReferenceEquals(Read(world.loggerBase, "m_parent", logger), Read(world.repository.GetType(), "m_root", world.repository)) &&
                ReferenceEquals(Read(world.loggerBase, "m_hierarchy", logger), world.repository) &&
                Read(world.loggerBase, "m_additive", logger) is bool additive && additive, "固定Logger不是默认叶状态。");
            CaptureFields(role, logger.GetType(), logger, rows, true);
            object appenderLock = Read(world.loggerBase, "m_appenderLock", logger);
            Require(Exact(appenderLock, world.logAssembly, "log4net.Util.ReaderWriterLock"), "日志Appender锁身份未知。");
            CaptureFields(role + ":lock", appenderLock.GetType(), appenderLock, rows, true);
        }

        /// <summary>确认实际静态日志字段为准确NetLogger，且绑定本次已审LogImpl。</summary>
        private static void RequireNetLogger(World world, object value, Entry entry)
        {
            Require(value?.GetType() == world.netLogger && Id(Read(world.netLogger, "mLogger", value)) == entry.wrapper,
                "UVCS/PlasticApp实际日志字段不是准确NetLogger/LogImpl。");
        }

        /// <summary>验证固定预编译发行的实际路径、唯一加载、MVID及完整磁盘摘要。</summary>
        private static Assembly VerifyDll(string root, string file, string name, string mvid, string hash, CancellationToken token)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(assembly => assembly.GetName().Name == name).ToArray();
            string path = Path.GetFullPath(Path.Combine(root, "Lib", "Editor", file));
            Require(assemblies.Length == 1 && Path.GetFullPath(assemblies[0].Location) == path &&
                assemblies[0].ManifestModule.ModuleVersionId.ToString("D") == mvid &&
                GenerateActionCommon.ComputeFileHash(path, token) == hash, "UVCS日志发行身份变化：" + file);
            return assemblies[0];
        }

        /// <summary>保留准确BCL同步包装作为厂商锁对象，仅检查底层Hashtable的原始计数和比较器。</summary>
        private static Hashtable Table(object value, bool allowBclComparer, string role)
        {
            Hashtable storage = TableStorage(value, role);
            FieldInfo count = typeof(Hashtable).GetField("_count", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            Require(count?.FieldType == typeof(int), "日志容器原始计数布局未适配：" + role + "；实际类型=" + value.GetType().FullName);
            int size = (int)count.GetValue(storage);
            Require(size >= 0 && size <= 256, "日志容器容量未适配：" + role + "；实际类型=" + value.GetType().FullName + "；count=" + size);
            FieldInfo[] comparers = typeof(Hashtable).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => typeof(IEqualityComparer).IsAssignableFrom(field.FieldType) || typeof(IComparer).IsAssignableFrom(field.FieldType) || typeof(IHashCodeProvider).IsAssignableFrom(field.FieldType)).ToArray();
            foreach (FieldInfo field in comparers)
            {
                object comparer = field.GetValue(storage);
                Require(comparer == null || allowBclComparer && IsKnownComparer(comparer),
                    "日志索引比较器未适配：" + role + "；实际容器=" + value.GetType().FullName + "；比较器=" + comparer?.GetType().FullName);
            }
            // Hierarchy.GetLogger锁住的是m_ht字段本身。不能用解包后的对象替换该监视器身份。
            return (Hashtable)value;
        }

        /// <summary>仅解开当前BCL的单层SyncHashtable，不接纳自定义子类、嵌套或被替换的底层类型。</summary>
        private static Hashtable TableStorage(object value, string role)
        {
            Type type = value?.GetType();
            if (type == typeof(Hashtable)) return (Hashtable)value;
            Type sync = typeof(Hashtable).GetNestedType("SyncHashtable", BindingFlags.NonPublic);
            Require(sync != null && type == sync, "日志容器类型未适配：" + role + "；实际类型=" + (type?.FullName ?? "null"));
            FieldInfo[] fields = sync.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(field => field.FieldType == typeof(Hashtable)).ToArray();
            Require(fields.Length == 1, "BCL SyncHashtable底层字段布局未适配：" + role);
            object storage = fields[0].GetValue(value);
            Require(storage?.GetType() == typeof(Hashtable), "BCL SyncHashtable底层类型未适配：" + role + "；实际类型=" + (storage?.GetType().FullName ?? "null"));
            return (Hashtable)storage;
        }

        /// <summary>同步包装和底层身份均冻结；动态日志表只排除允许新增条目的数量，不排除包装或比较器。</summary>
        private static void CaptureTableIdentity(string role, Hashtable table, List<string> rows)
        {
            Hashtable storage = TableStorage(table, role);
            rows.Add(role + ":container=" + Scalar(table) + ":storage=" + Scalar(storage));
            foreach (FieldInfo field in typeof(Hashtable).GetFields(BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => typeof(IEqualityComparer).IsAssignableFrom(field.FieldType) || typeof(IComparer).IsAssignableFrom(field.FieldType) || typeof(IHashCodeProvider).IsAssignableFrom(field.FieldType)))
            {
                object comparer = field.GetValue(storage);
                rows.Add(role + ":comparer:" + field.Name + "=" + Scalar(comparer));
                if (comparer?.GetType().FullName == "System.Collections.CompatibleComparer")
                    CaptureFields(role + ":legacyComparer", comparer.GetType(), comparer, rows, true);
            }
        }

        /// <summary>只枚举已审BCL容器条目，不调用厂商索引器、Equals或ToString。</summary>
        private static void CaptureTable(string role, Hashtable table, List<string> rows)
        {
            CaptureTableIdentity(role, table, rows);
            rows.AddRange(table.Cast<DictionaryEntry>().Select(item => role + ":" + Scalar(item.Key) + "=" + Scalar(item.Value)).OrderBy(row => row, StringComparer.Ordinal));
        }

        /// <summary>只允许固定BCL不区分大小写单例及包装它们的旧Hashtable兼容器，不接纳委托比较器。</summary>
        private static bool IsKnownComparer(object comparer)
        {
            if (ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase) || ReferenceEquals(comparer, StringComparer.InvariantCultureIgnoreCase) ||
                ReferenceEquals(comparer, CaseInsensitiveComparer.DefaultInvariant) || ReferenceEquals(comparer, CaseInsensitiveHashCodeProvider.DefaultInvariant)) return true;
            if (comparer == null || comparer.GetType().Assembly != typeof(string).Assembly || comparer.GetType().FullName != "System.Collections.CompatibleComparer") return false;
            FieldInfo[] fields = comparer.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return fields.Length == 2 && fields.All(field =>
                ReferenceEquals(field.GetValue(comparer), CaseInsensitiveComparer.DefaultInvariant) ||
                ReferenceEquals(field.GetValue(comparer), CaseInsensitiveHashCodeProvider.DefaultInvariant));
        }

        /// <summary>捕获准确实例或静态backing全部字段；不执行属性getter或序列化厂商对象。</summary>
        private static void CaptureFields(string role, Type type, object instance, List<string> rows, bool includeBases, params string[] excluded)
        {
            for (Type owner = type; owner != null && owner != typeof(object); owner = includeBases ? owner.BaseType : null)
                foreach (FieldInfo field in owner.GetFields(Fields).Where(field => !field.IsLiteral && field.IsStatic == (instance == null) && !excluded.Contains(field.Name)))
                    rows.Add(role + ":" + owner.FullName + ":" + field.Name + "=" + Scalar(field.GetValue(instance)));
        }

        /// <summary>原始字段值只采用BCL标量与同域弱身份，字符串保留完整摘要而不暴露用户内容。</summary>
        private static string Scalar(object value)
        {
            if (value == null) return "null";
            if (value is string text) return "string:" + GenerateActionCommon.ComputeTextHash(text);
            if (value is Type type) return "type:" + type.Assembly.ManifestModule.ModuleVersionId + ":" + type.FullName;
            if (value is Delegate callback) return "delegates:" + string.Join(";", callback.GetInvocationList().Select(item => HandlerKey(item.Method, item.Target)));
            if (value.GetType().IsPrimitive || value.GetType().IsEnum) return value.GetType().FullName + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);
            return "ref:" + value.GetType().Assembly.ManifestModule.ModuleVersionId + ":" + value.GetType().FullName + ":" + Id(value);
        }

        /// <summary>不加载缺席包或类型，同名歧义明确拒绝。</summary>
        private static Type FindOptional(string name)
        {
            Type[] types = AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name, false)).Where(type => type != null).ToArray();
            Require(types.Length <= 1, "UVCS类型存在同名歧义。"); return types.SingleOrDefault();
        }

        /// <summary>逐层读取准确声明字段；读取实例不回退到静态字段。</summary>
        private static object ReadDeep(Type type, string name, object instance)
        {
            for (Type owner = type; owner != null; owner = owner.BaseType)
                if (owner.GetField(name, Fields) != null) return Read(owner, name, instance);
            throw Failure("UVCS字段缺失：" + name);
        }

        /// <summary>严格读实际字段，不调用任何厂商方法。</summary>
        private static object Read(Type type, string name, object instance)
        {
            FieldInfo field = type.GetField(name, Fields);
            Require(field != null && field.IsStatic == (instance == null), "UVCS字段身份变化：" + name);
            return field.GetValue(instance);
        }

        /// <summary>固定委托签名、方法与target均须吻合，不调用事件。</summary>
        private static void RequireDelegate(Delegate value, Type owner, string name, object target)
        { Require(value != null && value.GetInvocationList().Length == 1 && value.Method.DeclaringType == owner && value.Method.Name == name && ReferenceEquals(value.Target, target), "日志委托来源或target未知：" + name); }

        /// <summary>类型须为准确固定预编译发行中的实际类型，不接纳继承或同名替身。</summary>
        private static bool Exact(object value, Assembly assembly, string name) => value != null && value.GetType() == assembly.GetType(name, true);

        /// <summary>创建本域弱身份，不保留厂商对象强引用。</summary>
        private static string Id(object value) => value == null ? null : Identities.GetValue(value, _ => new Identity()).value;

        /// <summary>委托身份包含精确模块、方法token与真实target弱身份。</summary>
        private static string HandlerKey(MethodInfo method, object target) => method.Module.ModuleVersionId + ":" + method.MetadataToken + ":" + Id(target);

        /// <summary>日志状态结构与摘要都完整，缺失目标仅允许出现在首次before。</summary>
        private static bool ValidLog(LogState state) => state != null && state.fixedRows != null && state.fixedRows.Length <= 32768 &&
            state.configurationHandlers != null && state.configurationHandlers.Length <= 256 && state.targets != null && state.targets.Length == 2 &&
            state.targets.Select(item => item?.name).SequenceEqual(Names) && state.targets.All(item => item.loggerRows != null && item.wrapperRows != null &&
                (item.logger != null || item.key == null && item.wrapper == null) && (item.wrapper != null || item.handler == null)) && state.sha256 == LogHash(state);

        /// <summary>日志摘要包含全部固定行、两个目标的完整字段以及原订阅顺序。</summary>
        private static string LogHash(LogState state) => GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new { state.fixedRows, state.targets, state.configurationHandlers }));

        /// <summary>稳定after摘要保留来源、偏好和真实禁用字段，区别于首次完整快照摘要。</summary>
        private static string AfterHash(Snapshot snapshot) => GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
        { snapshot.version, snapshot.domain, snapshot.present, snapshot.source, snapshot.preferenceKey, snapshot.preferenceDefined, snapshot.preferenceValue, snapshot.after, snapshot.stateRows }));

        /// <summary>完整计划摘要保留初次日志before/after，不用after覆盖历史前态。</summary>
        private static string SnapshotHash(Snapshot snapshot) => GenerateActionCommon.ComputeTextHash(Util.Json.Serialize(new
        { snapshot.version, snapshot.domain, snapshot.present, snapshot.source, snapshot.preferenceKey, snapshot.preferenceDefined, snapshot.preferenceValue, snapshot.before, snapshot.after, snapshot.stateRows, snapshot.afterSha256 }));

        /// <summary>在捕获完成后一次生成摘要，防止中间态被当作有效证据。</summary>
        private static void Finish(Snapshot snapshot) { snapshot.afterSha256 = AfterHash(snapshot); snapshot.sha256 = SnapshotHash(snapshot); }

        /// <summary>未闭合条件返回具体阻断，不通过修改状态消除条件。</summary>
        private static void Require(bool condition, string message) { if (!condition) throw Failure(message ?? "UVCS合同不完整。"); }

        /// <summary>统一定位UVCS合同失败。</summary>
        private static InvalidOperationException Failure(string message) => new InvalidOperationException(message);
    }
}
