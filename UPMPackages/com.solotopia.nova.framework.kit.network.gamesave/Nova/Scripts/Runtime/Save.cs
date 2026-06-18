/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Save.cs
 * author:    taoye
 * created:   2026/5/27
 * descrip:   游戏存档（云存档）业务网络 Service，封装 Get/Set 协议
 ***************************************************************/

using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Kit.Network.GameSave.Runtime
{
    /// <summary>
    /// 游戏存档（云存档）业务网络 Service。
    /// 负责存档数据在客户端与服务端之间的获取与上传，与本机 PersistManager 持久化体系互不重叠。
    /// 通过 Nova.Network.Kit<Save>() 获取实例，不继承任何基类，无参构造即可使用。
    /// 全量与非全量由 PbNetGetGameDataReq.full / PbNetSetGameDataReq.full 决定：
    /// 仅 GetFullAsync / SetFullAsync 走全量，其余接口一律为非全量。
    /// </summary>
    public sealed class Save
    {
        /// <summary>
        /// 当前 Service 实例的调试模式覆盖值。
        /// 为 null 时沿用 NetService.IsDebugMode 全局开关。
        /// </summary>
        private bool? m_DebugModeOverride;

        /// <summary>
        /// 当前 Service 实例缓存的游戏存档版本号。
        /// 由业务侧通过 SetGameVersion 注入，未设置时所有请求 GameVersion 字段保持空串。
        /// </summary>
        private string m_GameVersion = string.Empty;

        /// <summary>
        /// 设置当前 Service 实例的调试模式覆盖。
        /// 设置后仅影响本实例发出的请求；传 null 可恢复沿用全局开关。
        /// </summary>
        /// <param name="debugMode">是否启用调试模式。</param>
        public void SetDebugMode(bool debugMode)
        {
            m_DebugModeOverride = debugMode;
        }

        /// <summary>
        /// 设置当前 Service 实例的游戏存档版本号。
        /// 业务侧（登录/初始化阶段）调用一次后，所有 Get/Set 请求自动写入 GameVersion 字段；
        /// 传 null 时按空串处理。
        /// </summary>
        /// <param name="gameVersion">游戏存档版本号。</param>
        public void SetGameVersion(string gameVersion)
        {
            m_GameVersion = gameVersion ?? string.Empty;
        }

        /// <summary>
        /// 解析获取存档指令行：从 ConfigWindow 配置的 SaveKitConfig.GetCmdName 解析为 INetworkCmdRow。
        /// </summary>
        /// <returns>解析得到的指令行；cmdName 非法时由下层 NetService 处理。</returns>
        private static INetworkCmdRow ResolveGetCmdRow()
        {
            SaveKitConfig config = Nova.Config.GetKitConfig<SaveKitConfig>();
            if (config == null)
            {
                throw new KitConfigMissingException(typeof(SaveKitConfig).FullName);
            }
            return Nova.Network.ResolveNetCmdRow(config.GetCmdName);
        }

        /// <summary>
        /// 解析上传存档指令行：从 ConfigWindow 配置的 SaveKitConfig.SetCmdName 解析为 INetworkCmdRow。
        /// </summary>
        /// <returns>解析得到的指令行；cmdName 非法时由下层 NetService 处理。</returns>
        private static INetworkCmdRow ResolveSetCmdRow()
        {
            SaveKitConfig config = Nova.Config.GetKitConfig<SaveKitConfig>();
            if (config == null)
            {
                throw new KitConfigMissingException(typeof(SaveKitConfig).FullName);
            }
            return Nova.Network.ResolveNetCmdRow(config.SetCmdName);
        }

        /// <summary>
        /// 获取存档（非全量，单 key）。cmdName 取自 ConfigWindow 配置的 SaveKitConfig.GetCmdName。
        /// </summary>
        /// <param name="key">要获取的存档节点 key（小表名称）。</param>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetGetGameDataResp>> GetAsync(string key)
        {
            return SendGetAsync(ResolveGetCmdRow(), new[] { key });
        }

        /// <summary>
        /// 获取存档（非全量，批量 key）。cmdName 取自 ConfigWindow 配置的 SaveKitConfig.GetCmdName。
        /// </summary>
        /// <param name="keys">要获取的存档节点 key 列表。</param>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetGetGameDataResp>> GetAsync(string[] keys)
        {
            return SendGetAsync(ResolveGetCmdRow(), keys);
        }

        /// <summary>
        /// 获取存档（全量）。cmdName 取自 ConfigWindow 配置的 SaveKitConfig.GetCmdName；走全量分支（full=true）。
        /// </summary>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetGetGameDataResp>> GetFullAsync()
        {
            return SendGetFullAsync(ResolveGetCmdRow());
        }

        /// <summary>
        /// 上传存档（非全量，单条）。cmdName 取自 ConfigWindow 配置的 SaveKitConfig.SetCmdName。
        /// </summary>
        /// <param name="key">存档节点 key（小表名称）。</param>
        /// <param name="value">存档节点 value（业务 JSON 序列化后的字符串）。</param>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetSetGameDataResp>> SetAsync(string key, string value)
        {
            return SendSetAsync(ResolveSetCmdRow(), new[] { key }, new[] { value });
        }

        /// <summary>
        /// 上传存档（非全量，批量）。cmdName 取自 ConfigWindow 配置的 SaveKitConfig.SetCmdName。
        /// </summary>
        /// <param name="keys">存档节点 key 数组。</param>
        /// <param name="values">存档节点 value 数组，与 keys 一一对应。</param>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetSetGameDataResp>> SetAsync(string[] keys, string[] values)
        {
            return SendSetAsync(ResolveSetCmdRow(), keys, values);
        }

        /// <summary>
        /// 上传存档（全量）。cmdName 取自 ConfigWindow 配置的 SaveKitConfig.SetCmdName；value 作为整包载荷写入 datas[0]。
        /// </summary>
        /// <param name="value">全量存档载荷（业务整体 JSON 序列化后的字符串）。</param>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        public UniTask<NetResponse<PbNetSetGameDataResp>> SetFullAsync(string value)
        {
            return SendSetFullAsync(ResolveSetCmdRow(), value);
        }

        /// <summary>
        /// 内部非全量 Get 实现，按 keys 组装请求 Body 并交由 NetService 完成全链路。
        /// keys 必须非 null、长度 ≥ 1 且任一元素非 null / 非空字符串；不满足时直接返回 NetErrorCode.PARAM_ERROR，不发起网络请求。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据。</param>
        /// <param name="keys">要获取的存档节点 key 数组。</param>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetGetGameDataResp>> SendGetAsync(INetworkCmdRow cmdRow, string[] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                Log.Warning(LogTag.Network, "Save.GetAsync：非全量接口要求 keys 非 null 且长度 ≥ 1，keys={0}。",
                    keys == null ? "null" : "0");
                return NetResponse<PbNetGetGameDataResp>.Fail(NetErrorCode.PARAM_ERROR, "keys is null or empty");
            }
            for (int i = 0; i < keys.Length; i++)
            {
                if (string.IsNullOrEmpty(keys[i]))
                {
                    Log.Warning(LogTag.Network, "Save.GetAsync：非全量接口要求 keys 中任一元素非 null/空字符串，keys[{0}] 非法。", i);
                    return NetResponse<PbNetGetGameDataResp>.Fail(NetErrorCode.PARAM_ERROR, "keys contains null or empty entry");
                }
            }

            var body = new PbNetGetGameDataReq
            {
                Head = NetBuilder.BuildHeader(),
                Full = false,
            };
            for (int i = 0; i < keys.Length; i++)
            {
                body.Keys.Add(keys[i]);
            }
            return await NetService.SendAsync(cmdRow, body, PbNetGetGameDataResp.Parser, m_DebugModeOverride);
        }

        /// <summary>
        /// 内部全量 Get 实现，仅写入 Full=true，不携带 keys。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据。</param>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetGetGameDataResp>> SendGetFullAsync(INetworkCmdRow cmdRow)
        {
            var body = new PbNetGetGameDataReq
            {
                Head = NetBuilder.BuildHeader(),
                Full = true,
            };
            return await NetService.SendAsync(cmdRow, body, PbNetGetGameDataResp.Parser, m_DebugModeOverride);
        }

        /// <summary>
        /// 内部非全量 Set 实现，按 keys/values 组装 datas 并交由 NetService 完成全链路。
        /// keys 与 values 必须同时非 null、长度一致且 ≥ 1；keys 中任一元素不得为 null/空字符串；values 不得为 null（允许空字符串）。
        /// 任一校验未通过时直接返回 NetErrorCode.PARAM_ERROR，不发起网络请求。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据。</param>
        /// <param name="keys">要写入的存档节点 key 数组。</param>
        /// <param name="values">要写入的存档节点 value 数组，与 keys 一一对应。</param>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetSetGameDataResp>> SendSetAsync(INetworkCmdRow cmdRow, string[] keys, string[] values)
        {
            if (keys == null || values == null || keys.Length != values.Length || keys.Length == 0)
            {
                Log.Warning(LogTag.Network, "Save.SetAsync：非全量接口要求 keys/values 同时非 null、长度一致且 ≥ 1，keys={0}，values={1}。",
                    keys?.Length.ToString() ?? "null", values?.Length.ToString() ?? "null");
                return NetResponse<PbNetSetGameDataResp>.Fail(NetErrorCode.PARAM_ERROR, "keys/values null, empty or length mismatch");
            }
            for (int i = 0; i < keys.Length; i++)
            {
                if (string.IsNullOrEmpty(keys[i]))
                {
                    Log.Warning(LogTag.Network, "Save.SetAsync：非全量接口要求 keys 中任一元素非 null/空字符串，keys[{0}] 非法。", i);
                    return NetResponse<PbNetSetGameDataResp>.Fail(NetErrorCode.PARAM_ERROR, "keys contains null or empty entry");
                }
                if (values[i] == null)
                {
                    Log.Warning(LogTag.Network, "Save.SetAsync：非全量接口要求 values 中任一元素非 null，values[{0}] 为 null。", i);
                    return NetResponse<PbNetSetGameDataResp>.Fail(NetErrorCode.PARAM_ERROR, "values contains null entry");
                }
            }

            var body = new PbNetSetGameDataReq
            {
                Head = NetBuilder.BuildHeader(),
                Full = false,
                GameVersion = m_GameVersion,
                AppVersion = Application.version,
                LastDeviceId = ResolveDeviceId(),
            };
            for (int i = 0; i < keys.Length; i++)
            {
                body.Datas.Add(new PbNetGameDataNode
                {
                    Key = keys[i],
                    Value = values[i],
                });
            }
            return await NetService.SendAsync(cmdRow, body, PbNetSetGameDataResp.Parser, m_DebugModeOverride);
        }

        /// <summary>
        /// 内部全量 Set 实现，写入 Full=true 并将 value 作为整包载荷塞进 datas[0]（Key 为空字符串）。
        /// value 不得为 null（允许空字符串），否则直接返回 NetErrorCode.PARAM_ERROR，不发起网络请求。
        /// </summary>
        /// <param name="cmdRow">NetCmd 指令行数据。</param>
        /// <param name="value">全量存档载荷（业务整体 JSON 序列化后的字符串）。</param>
        /// <returns>包含响应数据或错误信息的 NetResponse。</returns>
        private async UniTask<NetResponse<PbNetSetGameDataResp>> SendSetFullAsync(INetworkCmdRow cmdRow, string value)
        {
            if (value == null)
            {
                Log.Warning(LogTag.Network, "Save.SetFullAsync：全量接口要求 value 非 null（允许空字符串）。");
                return NetResponse<PbNetSetGameDataResp>.Fail(NetErrorCode.PARAM_ERROR, "value is null");
            }

            var body = new PbNetSetGameDataReq
            {
                Head = NetBuilder.BuildHeader(),
                Full = true,
                GameVersion = m_GameVersion,
                AppVersion = Application.version,
                LastDeviceId = ResolveDeviceId(),
            };
            body.Datas.Add(new PbNetGameDataNode
            {
                Key = string.Empty,
                Value = value,
            });
            return await NetService.SendAsync(cmdRow, body, PbNetSetGameDataResp.Parser, m_DebugModeOverride);
        }

        /// <summary>
        /// 解析当前设备 ID。
        /// 取值口径与 NetBuilder.BuildHeader 保持一致：通过 Nova.SDK 注册的 IDeviceIdProvider 取值，未注册或返回 null 时回退空串。
        /// </summary>
        /// <returns>当前设备 ID 字符串；不可用时返回空串。</returns>
        private static string ResolveDeviceId()
        {
            if (Nova.SDK.TryGet<IDeviceIdProvider>(out IDeviceIdProvider deviceIdProvider))
            {
                return deviceIdProvider.GetDeviceID() ?? string.Empty;
            }
            return string.Empty;
        }
    }
}
