/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigManager.cs
 * author:    taoye
 * created:   2025/12/5
 * descrip:   配置管理器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Google.Protobuf;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 配置管理器实现；AB 异步加载 ConfigRuntimeSO，
    /// 运行期以 ConfigRuntimeSO 为本地数据源，并在后台等待 Network 就绪后刷新磁盘缓存与远端应用配置。
    /// </summary>
    internal sealed partial class ConfigManager : ConfigManagerBase
    {
        /// <summary>
        /// 构造器；无参，供反射创建。
        /// </summary>
        public ConfigManager() { }

        /// <summary>
        /// 初始化；接收 Component 构造的 ConfigManagerConfig 并获取 AssetManager。
        /// </summary>
        /// <param name="config">
        /// 配置信息，含 ConfigRuntimeSO Asset 地址（AssetLocation）。
        /// </param>
        /// <remarks>先建立新生命周期再取消旧等待，避免同步重入被覆盖；旧取消回调异常会传播，并可能跳过其后的 Dispose 与等待者通知。此入口不清已加载配置，不等同完整重载。</remarks>
        public override void Initialize(ConfigManagerConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }
            if (string.IsNullOrEmpty(config.AssetLocation))
            {
                throw new ArgumentException("ConfigManagerConfig.AssetLocation 不能为空。", nameof(config));
            }

            m_AssetLocation = config.AssetLocation;
            m_AssetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
            UniTaskCompletionSource previousLoad = m_ConfigLoadTcs;
            m_ConfigLoadTcs = null;
            CancellationTokenSource previousLifecycle = m_LifecycleCts;
            m_LifecycleCts = new CancellationTokenSource();
            // 新状态先接管，旧取消通知中的同步重入不再被本次初始化尾部覆盖。
            previousLifecycle?.Cancel();
            previousLifecycle?.Dispose();
            previousLoad?.TrySetCanceled();
        }

        /// <summary>
        /// 管理器轮询；本模块加载后为稳态无需轮询。
        /// </summary>
        public override void Update() { }

        /// <summary>
        /// 关闭并清理管理器；Release ConfigRuntimeSO 句柄使引用计数归零，清空内部引用。
        /// </summary>
        /// <remarks>先摘取并清空旧字段，再释放句柄；取消回调异常也会进入清理 finally 并结束启动等待。取消请求不代表底层传输已经退出。</remarks>
        public override void Shutdown()
        {
            CancellationTokenSource lifecycle = m_LifecycleCts;
            UniTaskCompletionSource load = m_ConfigLoadTcs;
            UniTaskCompletionSource<bool> refresh = m_AppConfigRefreshTcs;
            UniTaskCompletionSource<bool> startup = m_StartupAppConfigTcs;
            IAssetHandle<ConfigRuntimeSO> handle = m_ConfigHandle;
            m_LifecycleCts = null;
            m_ConfigLoadTcs = null;
            m_AppConfigRefreshTcs = null;
            m_StartupAppConfigTcs = null;
            m_ConfigHandle = null;
            m_Runtime = null;
            m_AppConfigSnapshot = null;
            m_Custom = null;
            m_IsLoadOver = false;
            m_AssetLocation = null;
            m_AssetManager = null;
            // 所有旧字段先摘走再通知；等待者同步重入创建的新生命周期不被旧清场覆盖。
            try
            {
                handle?.Release();
            }
            finally
            {
                try
                {
                    lifecycle?.Cancel();
                }
                finally
                {
                    lifecycle?.Dispose();
                    refresh?.TrySetCanceled();
                    startup?.TrySetResult(false);
                    load?.TrySetCanceled();
                }
            }
        }

        /// <summary>
        /// 异步加载 ConfigRuntimeSO，加载成功后直接持有引用作为数据源；
        /// 已加载时幂等返回（m_IsLoadOver 短路），避免 AB 引用计数紊乱；
        /// 加载中共享同一完成源；关闭时取消等待，迟到句柄由本次加载归还，不发布旧配置。
        /// 加载失败时 Log.Error 并重新抛出异常，由 Procedure 层决定启动流程是否中断。
        /// </summary>
        /// <returns>
        /// 加载完成的异步任务。
        /// </returns>
        public override async UniTask LoadAsync()
        {
#if UNITY_EDITOR
            // Editor 未结束的隐私存档维护必须先恢复；幂等短路也不能绕过门禁。
            PrivacyAesMaintenanceGuard.AssertReady();
#endif
            if (m_IsLoadOver)
            {
                return;
            }

            if (m_ConfigLoadTcs != null)
            {
                await m_ConfigLoadTcs.Task;
                return;
            }

            var completion = new UniTaskCompletionSource();
            m_ConfigLoadTcs = completion;
            RunConfigLoadAsync(completion, m_AssetManager, m_AssetLocation, m_LifecycleCts).Forget();
            await completion.Task;
        }

        /// <summary>
        /// 执行唯一一轮 ConfigRuntimeSO 加载，校验原生命周期后提交，完成或失败后清除本轮共享任务。
        /// </summary>
        /// <param name="completion">本轮所有调用者共享的完成源。</param>
        /// <param name="assetManager">发起时捕获的资源管理器，避免关闭后读取空字段。</param>
        /// <param name="assetLocation">发起时捕获的配置资源地址。</param>
        /// <param name="lifecycle">发起时的管理器生命周期；关闭或重新初始化后禁止提交。</param>
        /// <returns>完成句柄处理与本轮共享任务通知的异步任务。</returns>
        private async UniTask RunConfigLoadAsync(
            UniTaskCompletionSource completion,
            IAssetManager assetManager,
            string assetLocation,
            CancellationTokenSource lifecycle)
        {
            IAssetHandle<ConfigRuntimeSO> handle = null;
            Exception failure = null;
            try
            {
                // 不提前取消资源等待；若关闭后资源迟到，仍取得句柄并在 finally 唯一归还。
                handle = await assetManager.LoadAsync<ConfigRuntimeSO>(assetLocation);
                if (lifecycle == null || !ReferenceEquals(m_LifecycleCts, lifecycle) || lifecycle.IsCancellationRequested)
                {
                    throw new OperationCanceledException("ConfigManager 加载所属生命周期已结束。");
                }

                if (handle == null || handle.Asset == null)
                {
                    throw new InvalidOperationException("ConfigRuntimeSO 加载结果为 null。");
                }

                if (handle.Asset.Platform == PlatformType.None || handle.Asset.Channel == ChannelType.None ||
                !Enum.IsDefined(typeof(PlatformType), handle.Asset.Platform) ||
                !Enum.IsDefined(typeof(ChannelType), handle.Asset.Channel))
                {
                    throw new InvalidOperationException(
                        $"ConfigRuntimeSO 的 Platform 与 Channel 必须是非 None 的有效值：{handle.Asset.Platform}/{handle.Asset.Channel}。");
                }

                Util.Encrypt.AES.InitializeFromConfig(handle.Asset.PrivacyConfigs);
                m_ConfigHandle = handle;
                m_Runtime = handle.Asset;
                m_AppConfigSnapshot = new AppConfigSnapshot(m_Runtime.Custom);
                TryLoadAppConfigCache();
                m_Custom = new CustomConfig(this);
                int enabledCount = m_Runtime.EnabledSDKConfigs != null ? m_Runtime.EnabledSDKConfigs.Count : 0;
                m_IsLoadOver = true;
                Log.Debug(LogTag.Config, "Config 成功加载，共计 1 份儿通用配置，{0} 个 SDKPluginConfig 数据。", enabledCount);
                BeginStartupAppConfigRefresh();
                handle = null;
            }
            catch (Exception exception)
            {
                failure = exception;
                if (!(exception is OperationCanceledException))
                {
                    Log.Error(LogTag.Config, "ConfigManager 加载 ConfigRuntimeSO 异常：location={0}, Error={1}", assetLocation, exception);
                }
            }
            finally
            {
                if (handle != null)
                {
                    if (ReferenceEquals(m_ConfigHandle, handle))
                    {
                        m_ConfigHandle = null;
                        m_Runtime = null;
                        m_AppConfigSnapshot = null;
                        m_Custom = null;
                        m_IsLoadOver = false;
                    }
                    try
                    {
                        handle.Release();
                    }
                    catch (Exception releaseException)
                    {
                        failure = failure ?? releaseException;
                    }
                }
                if (ReferenceEquals(m_ConfigLoadTcs, completion))
                {
                    m_ConfigLoadTcs = null;
                }
            }

            if (failure == null)
            {
                completion.TrySetResult();
            }
            else
            {
                completion.TrySetException(failure);
            }
        }

        /// <summary>
        /// 按 JSONPath 读取当前 Custom 配置字符串；远端与本地均未命中时返回调用方默认值。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="defaultValue">本地未声明时的调用方默认值。</param>
        /// <returns>当前生效字符串或 defaultValue。</returns>
        public override string GetString(string key, string defaultValue = null)
        {
            return m_AppConfigSnapshot != null ? m_AppConfigSnapshot.GetString(key, defaultValue) : defaultValue;
        }

        /// <summary>
        /// 按 JSONPath 读取 int；当前远端值非法时回退 ConfigRuntimeSO 本地默认字符串。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="defaultValue">本地默认字符串也无法转换时的调用方默认值。</param>
        /// <returns>转换后的 int 或 defaultValue。</returns>
        public override int GetInt(string key, int defaultValue = default)
        {
            if (m_AppConfigSnapshot?.IsRemoteNull(key) == true)
            {
                return defaultValue;
            }
            string effectiveValue = m_AppConfigSnapshot?.GetString(key, null);
            if (int.TryParse(effectiveValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                return value;
            }

            string localValue = m_AppConfigSnapshot?.GetLocalString(key, null);
            if (!string.Equals(effectiveValue, localValue, StringComparison.Ordinal) &&
                int.TryParse(localValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                Log.Warning(LogTag.Config, "应用配置 int 转换失败，已回退本地默认值：path={0}", key);
                return value;
            }

            if (effectiveValue != null)
            {
                Log.Warning(LogTag.Config, "应用配置 int 转换失败，已回退调用方默认值：path={0}", key);
            }
            return defaultValue;
        }

        /// <summary>
        /// 按 JSONPath 读取 float；使用固定区域格式，当前远端值非法时回退本地默认字符串。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="defaultValue">本地默认字符串也无法转换时的调用方默认值。</param>
        /// <returns>转换后的 float 或 defaultValue。</returns>
        public override float GetFloat(string key, float defaultValue = default)
        {
            if (m_AppConfigSnapshot?.IsRemoteNull(key) == true)
            {
                return defaultValue;
            }
            string effectiveValue = m_AppConfigSnapshot?.GetString(key, null);
            if (float.TryParse(effectiveValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return value;
            }

            string localValue = m_AppConfigSnapshot?.GetLocalString(key, null);
            if (!string.Equals(effectiveValue, localValue, StringComparison.Ordinal) &&
                float.TryParse(localValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                Log.Warning(LogTag.Config, "应用配置 float 转换失败，已回退本地默认值：path={0}", key);
                return value;
            }

            if (effectiveValue != null)
            {
                Log.Warning(LogTag.Config, "应用配置 float 转换失败，已回退调用方默认值：path={0}", key);
            }
            return defaultValue;
        }

        /// <summary>
        /// 按 JSONPath 读取 bool；支持 true/false 与 1/0，当前远端值非法时回退本地默认字符串。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="defaultValue">本地默认字符串也无法转换时的调用方默认值。</param>
        /// <returns>转换后的 bool 或 defaultValue。</returns>
        public override bool GetBool(string key, bool defaultValue = default)
        {
            if (m_AppConfigSnapshot?.IsRemoteNull(key) == true)
            {
                return defaultValue;
            }
            string effectiveValue = m_AppConfigSnapshot?.GetString(key, null);
            if (TryParseBoolean(effectiveValue, out bool value))
            {
                return value;
            }

            string localValue = m_AppConfigSnapshot?.GetLocalString(key, null);
            if (!string.Equals(effectiveValue, localValue, StringComparison.Ordinal) && TryParseBoolean(localValue, out value))
            {
                Log.Warning(LogTag.Config, "应用配置 bool 转换失败，已回退本地默认值：path={0}", key);
                return value;
            }

            if (effectiveValue != null)
            {
                Log.Warning(LogTag.Config, "应用配置 bool 转换失败，已回退调用方默认值：path={0}", key);
            }
            return defaultValue;
        }

        /// <summary>
        /// 解析应用配置布尔字符串；除标准 true/false 外兼容 GM 常用的 1/0。
        /// </summary>
        /// <param name="value">待解析字符串。</param>
        /// <param name="result">解析成功后的布尔值。</param>
        /// <returns>字符串符合支持格式时返回 true。</returns>
        private static bool TryParseBoolean(string value, out bool result)
        {
            if (bool.TryParse(value, out result))
            {
                return true;
            }
            if (string.Equals(value, "1", StringComparison.Ordinal))
            {
                result = true;
                return true;
            }
            if (string.Equals(value, "0", StringComparison.Ordinal))
            {
                result = false;
                return true;
            }
            result = default;
            return false;
        }

        /// <summary>
        /// 尝试按 JSONPath 读取当前 Custom 配置字符串。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="value">命中时的当前生效字符串。</param>
        /// <returns>云端或本地路径存在且不是显式 null 时返回 true。</returns>
        public override bool TryGetString(string key, out string value)
        {
            if (m_AppConfigSnapshot != null)
            {
                return m_AppConfigSnapshot.TryGetString(key, out value);
            }
            value = null;
            return false;
        }

        /// <summary>
        /// 等待本次启动自动拉取的最终结果；只读取保留的完成源，不启动或重试请求。
        /// </summary>
        /// <param name="cancellationToken">只取消当前等待者，不取消共享启动任务。</param>
        /// <returns>启动拉取成功返回 true；尚未加载、跳过、失败或关闭返回 false。</returns>
        public override UniTask<bool> WaitForStartupAppConfigAsync(CancellationToken cancellationToken = default)
        {
            return m_StartupAppConfigTcs != null
                ? m_StartupAppConfigTcs.Task.AttachExternalCancellation(cancellationToken)
                : UniTask.FromResult(false);
        }

        /// <summary>
        /// 显式拉取一轮 GM 后台应用配置；并发调用合并为同一请求，失败时保留当前快照。
        /// </summary>
        /// <returns>远端快照已成功解析并切换到内存返回 true。</returns>
        /// <remarks>独立发起时沿用普通业务请求超时；并发合流已有启动请求时，沿用该请求的单次超时设置。</remarks>
        public override UniTask<bool> RefreshAppConfigAsync()
        {
            return RefreshAppConfigWithTimeoutAsync(-1f);
        }

        /// <summary>
        /// 合流一轮应用配置刷新；新请求将单次超时传到主备请求链，已有请求保持原设置。
        /// </summary>
        /// <param name="requestTimeout">每次物理请求独享的超时秒数；负数使用普通业务请求超时。</param>
        /// <returns>本轮远端快照已应用返回 true，失败或关闭返回 false。</returns>
        private async UniTask<bool> RefreshAppConfigWithTimeoutAsync(float requestTimeout)
        {
            if (!m_IsLoadOver || m_AppConfigSnapshot == null)
            {
                Log.Warning(LogTag.Config, "应用配置刷新已跳过：ConfigRuntimeSO 尚未加载完成。");
                return false;
            }

            if (m_AppConfigRefreshTcs != null)
            {
                return await m_AppConfigRefreshTcs.Task;
            }

            UniTaskCompletionSource<bool> tcs = new UniTaskCompletionSource<bool>();
            m_AppConfigRefreshTcs = tcs;
            bool succeeded = false;
            try
            {
                succeeded = await RefreshAppConfigCoreAsync(requestTimeout);
            }
            catch (OperationCanceledException)
            {
                succeeded = false;
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Config, "应用配置刷新异常，继续使用当前快照：{0}", e);
                succeeded = false;
            }
            finally
            {
                tcs.TrySetResult(succeeded);
                if (ReferenceEquals(m_AppConfigRefreshTcs, tcs))
                {
                    m_AppConfigRefreshTcs = null;
                }
            }
            return succeeded;
        }

        /// <summary>
        /// 单次远端拉取核心流程：解析 NetCmd、发送 Proto、校验完整 JSON、持久化并切换内存快照。
        /// </summary>
        /// <param name="requestTimeout">每次主备请求独享的超时秒数；负数使用普通业务请求超时。</param>
        /// <returns>远端快照已切换到内存返回 true。</returns>
        private async UniTask<bool> RefreshAppConfigCoreAsync(float requestTimeout)
        {
            CancellationTokenSource lifecycle = m_LifecycleCts;
            AppConfigSnapshot snapshot = m_AppConfigSnapshot;
            if (lifecycle == null || lifecycle.IsCancellationRequested || snapshot == null)
            {
                return false;
            }
            CancellationToken ct = lifecycle.Token;
            AppConfigs appConfigs = m_Runtime?.AppConfigs;
            if (!HasAppConfigRemoteRefreshSettings(appConfigs))
            {
                Log.Warning(LogTag.Config, "Custom 配置刷新已跳过：CustomConfigCmdName 或 CustomName 未配置。");
                return false;
            }

            INetworkManager networkManager = FrameworkManagersGroup.GetManager<INetworkManager>();
            if (networkManager == null)
            {
                Log.Warning(LogTag.Config, "应用配置刷新已跳过：INetworkManager 不存在。");
                return false;
            }

            INetworkCmdRow cmdRow = networkManager.ResolveNetCmdRow(appConfigs.CustomConfigCmdName);
            if (cmdRow == null)
            {
                Log.Warning(LogTag.Config, "Custom 配置刷新失败：未找到 NetCmd [{0}]。", appConfigs.CustomConfigCmdName);
                return false;
            }

            PbNetAppCustomConfigReq request = new PbNetAppCustomConfigReq
            {
                Head = NetBuilder.BuildHeader(),
                Key = appConfigs.CustomName,
            };
            NetResponse<PbNetAppCustomConfigResp> response = await NetService.SendAsync(
                cmdRow,
                request,
                PbNetAppCustomConfigResp.Parser,
                requestTimeout,
                ct);

            // 旧请求即使在关闭、重新初始化后迟到，也不能写入新生命周期或新快照。
            if (ct.IsCancellationRequested || !ReferenceEquals(m_LifecycleCts, lifecycle) ||
                !ReferenceEquals(m_AppConfigSnapshot, snapshot))
            {
                return false;
            }
            if (response == null || !response.IsSuccess || response.Data == null)
            {
                Log.Warning(LogTag.Config, "应用配置拉取失败，继续使用当前快照：code={0}, message={1}",
                    response?.ErrorCode ?? NetErrorCode.NETWORK_ERROR,
                    response?.ErrorMessage ?? "empty response");
                return false;
            }

            if (!snapshot.TryParseRemoteJson(
                    response.Data.Value,
                    out JObject remoteRoot,
                    out string parseError))
            {
                Log.Error(LogTag.Config, "应用配置响应无效，继续使用当前快照：{0}", parseError);
                return false;
            }

            AppConfigCachePayload payload = new AppConfigCachePayload
            {
                Name = appConfigs.CustomName,
                Json = remoteRoot.ToString(Newtonsoft.Json.Formatting.None),
            };
            string cacheJson = Util.Json.Serialize(payload, Newtonsoft.Json.Formatting.None);
            if (!AppConfigDiskCache.TryWriteAtomic(GetAppConfigCachePath(), cacheJson, out string cacheError))
            {
                // 磁盘不可写不应丢弃本轮已成功取得的配置；内存仍切换，下一次启动回退旧缓存或本地默认值。
                Log.Warning(LogTag.Config, "应用配置磁盘缓存写入失败，本轮仅更新内存：{0}", cacheError);
            }

            snapshot.ApplyRemote(remoteRoot);
            Log.Debug(LogTag.Config, "应用配置拉取成功：{0}", JsonFormatter.Default.Format(response.Data));
            return true;
        }

        /// <summary>
        /// 从固定缓存文件恢复与当前 CustomName 匹配的远端快照；损坏或名称不匹配时回退本地默认值。
        /// </summary>
        private void TryLoadAppConfigCache()
        {
            AppConfigs appConfigs = m_Runtime?.AppConfigs;
            if (!HasAppConfigRemoteRefreshSettings(appConfigs))
            {
                return;
            }

            if (!AppConfigDiskCache.TryRead(GetAppConfigCachePath(), out string json, out string readError))
            {
                if (!string.IsNullOrEmpty(readError))
                {
                    Log.Warning(LogTag.Config, "应用配置磁盘缓存读取失败，使用本地默认值：{0}", readError);
                }
                return;
            }

            try
            {
                AppConfigCachePayload payload = Util.Json.Deserialize<AppConfigCachePayload>(json);
                if (payload == null || !string.Equals(payload.Name, appConfigs.CustomName, StringComparison.Ordinal))
                {
                    return;
                }

                if (!m_AppConfigSnapshot.TryReplaceRemoteJson(payload.Json, out string parseError))
                {
                    Log.Warning(LogTag.Config, "Custom 配置磁盘缓存内容无效，使用本地默认值：{0}", parseError);
                    return;
                }
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Config, "应用配置磁盘缓存解析失败，使用本地默认值：{0}", e.Message);
            }
        }

        /// <summary>
        /// 建立本次启动唯一的结果并发起后台刷新；方法立即返回，不阻塞 Config.LoadAsync。
        /// </summary>
        private void BeginStartupAppConfigRefresh()
        {
            if (m_StartupAppConfigTcs != null)
            {
                return;
            }

            var completion = new UniTaskCompletionSource<bool>();
            m_StartupAppConfigTcs = completion;
            if (!HasAppConfigRemoteRefreshSettings(m_Runtime?.AppConfigs))
            {
                completion.TrySetResult(false);
                return;
            }
            WaitNetworkAndRefreshAppConfigAsync(completion, m_LifecycleCts).Forget();
        }

        /// <summary>
        /// 仅当 NetCmd 与后台配置项名称都有效时允许读取缓存或进入远端请求链。
        /// </summary>
        private static bool HasAppConfigRemoteRefreshSettings(AppConfigs appConfigs)
        {
            return appConfigs != null &&
                   !string.IsNullOrWhiteSpace(appConfigs.CustomConfigCmdName) &&
                   !string.IsNullOrWhiteSpace(appConfigs.CustomName);
        }

        /// <summary>
        /// 后台等待标准 NetworkManager 路由就绪后自动刷新，并保留成功、失败或跳过的最终结果。
        /// </summary>
        /// <param name="completion">本次启动专属的完成源；不会被后续手动刷新替换。</param>
        /// <param name="lifecycle">发起时的生命周期；关闭或重新初始化后不再开始旧请求。</param>
        private async UniTaskVoid WaitNetworkAndRefreshAppConfigAsync(
            UniTaskCompletionSource<bool> completion,
            CancellationTokenSource lifecycle)
        {
            bool succeeded = false;
            try
            {
                if (lifecycle == null)
                {
                    return;
                }
                INetworkReadySignal readySignal = FrameworkManagersGroup.GetManager<INetworkReadySignal>();
                if (readySignal == null)
                {
                    Log.Warning(LogTag.Config, "应用配置自动刷新已跳过：当前 NetworkManager 未实现框架内部就绪信号。");
                    return;
                }

                CancellationToken ct = lifecycle.Token;
                await readySignal.WaitUntilReadyAsync(ct);
                ct.ThrowIfCancellationRequested();
                if (!ReferenceEquals(m_LifecycleCts, lifecycle))
                {
                    return;
                }
                succeeded = await RefreshAppConfigWithTimeoutAsync(readySignal.StartupAppConfigRequestTimeout);
            }
            catch (OperationCanceledException)
            {
                // 框架关闭时结束等待。
            }
            catch (Exception e)
            {
                Log.Warning(LogTag.Config, "应用配置自动刷新异常，继续使用当前快照：{0}", e);
            }
            finally
            {
                // 失败同样固化结果；后续等待或预加载重试不得再次发送本轮启动请求。
                completion.TrySetResult(succeeded);
            }
        }

        /// <summary>
        /// 获取固定且无路径注入风险的应用配置缓存路径。
        /// </summary>
        /// <returns>persistentDataPath 下的缓存完整路径。</returns>
        private static string GetAppConfigCachePath()
        {
            return System.IO.Path.Combine(Application.persistentDataPath, "Config", "app-custom-config.json");
        }

        /// <summary>
        /// 按泛型类型取 SDK Plugin 配置实例；透传 ConfigRuntimeSO.GetSDKPluginConfig，未启用返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// SDK Plugin 所需配置类型，须实现 ISDKPluginConfig。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；未启用时返回 null。
        /// </returns>
        public override T GetSDKPluginConfig<T>() => m_Runtime?.GetSDKPluginConfig<T>();

        /// <summary>
        /// 按类型对象取 SDK Plugin 配置实例（非泛型版）；透传 ConfigRuntimeSO.GetSDKPluginConfig，type 为 null 或未启用返回 null。
        /// </summary>
        /// <param name="type">
        /// SDK Plugin 所需配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；未启用或 type 为 null 时返回 null。
        /// </returns>
        public override ISDKPluginConfig GetSDKPluginConfig(Type type) => m_Runtime?.GetSDKPluginConfig(type);

        /// <summary>
        /// 按泛型类型取 Kit 配置实例；透传 ConfigRuntimeSO.GetKitConfig，未启用返回 null。
        /// </summary>
        /// <typeparam name="T">
        /// 目标 Kit 配置类型。
        /// </typeparam>
        /// <returns>
        /// 对应类型的配置实例；Runtime 未就绪或未启用返回 null。
        /// </returns>
        public override T GetKitConfig<T>() => m_Runtime?.GetKitConfig<T>();

        /// <summary>
        /// 按类型对象取 Kit 配置实例（非泛型版）；透传 ConfigRuntimeSO.GetKitConfig，type 为 null 或未启用返回 null。
        /// </summary>
        /// <param name="type">
        /// 目标 Kit 配置类型对象。
        /// </param>
        /// <returns>
        /// 对应类型的配置实例；Runtime 未就绪、type 为 null 或未启用返回 null。
        /// </returns>
        public override IKitConfig GetKitConfig(Type type) => m_Runtime?.GetKitConfig(type);

        /// <summary>
        /// 当前已加载的所有启用 SDK Plugin 配置集合；未加载时返回空集合。
        /// </summary>
        /// <returns>
        /// SDK Plugin 配置只读集合。
        /// </returns>
        public override IReadOnlyCollection<ISDKPluginConfig> GetAllPluginConfigs()
        {
            return m_Runtime?.EnabledSDKConfigs ?? (IReadOnlyCollection<ISDKPluginConfig>)System.Array.Empty<ISDKPluginConfig>();
        }
    }
}
