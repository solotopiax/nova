/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Bind.Track.cs
 * author:    taoye
 * created:   2026/7/21
 * descrip:   GameBind 关键行为埋点
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameBind.Runtime
{
    public sealed partial class Bind
    {
        /// <summary>
        /// 上报绑定结果，并将第三方登录提供方转换为稳定的埋点字符串值。
        /// </summary>
        /// <param name="response">绑定响应。</param>
        /// <param name="provider">第三方登录提供方。</param>
        /// <param name="openId">目标第三方用户标识。</param>
        /// <param name="durationMilliseconds">请求耗时，单位毫秒。</param>
        private static void TrackBind(
            NetResponse<PbNetBindResp> response,
            ThirdLoginProvider provider,
            string openId,
            long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { BindTrackFields.BindProvider, ResolveProvider(provider) },
                { BindTrackFields.BindResult, ResolveBindResult(response) },
                { BindTrackFields.BindErrorCode, response.ErrorCode },
                { BindTrackFields.BindDurationMs, NormalizeDuration(durationMilliseconds) },
            };

            WithTrackPlugin(trackPlugin =>
            {
                if (response.IsSuccess && !string.IsNullOrEmpty(openId))
                {
                    trackPlugin.SetUserProperty(BindTrackFields.OpenId, openId);
                }
                trackPlugin.TrackEvent(BindTrackEvents.Bind, properties);
            });
        }

        /// <summary>
        /// 上报绑定请求异常，并保留第三方登录提供方和耗时维度。
        /// </summary>
        /// <param name="provider">第三方登录提供方。</param>
        /// <param name="durationMilliseconds">请求耗时，单位毫秒。</param>
        private static void TrackBindException(ThirdLoginProvider provider, long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { BindTrackFields.BindProvider, ResolveProvider(provider) },
                { BindTrackFields.BindResult, "exception" },
                { BindTrackFields.BindErrorCode, -1 },
                { BindTrackFields.BindDurationMs, NormalizeDuration(durationMilliseconds) },
            };
            WithTrackPlugin(trackPlugin => trackPlugin.TrackEvent(BindTrackEvents.Bind, properties));
        }

        private static void TrackQueryConflict(
            NetResponse<PbNetBindConflictResp> response,
            long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { BindTrackFields.QueryConflictResult, response.IsSuccess ? "success" : "failed" },
                { BindTrackFields.QueryConflictErrorCode, response.ErrorCode },
                { BindTrackFields.QueryConflictDurationMs, NormalizeDuration(durationMilliseconds) },
            };
            WithTrackPlugin(trackPlugin => trackPlugin.TrackEvent(BindTrackEvents.QueryConflict, properties));
        }

        private static void TrackQueryConflictException(long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { BindTrackFields.QueryConflictResult, "exception" },
                { BindTrackFields.QueryConflictErrorCode, -1 },
                { BindTrackFields.QueryConflictDurationMs, NormalizeDuration(durationMilliseconds) },
            };
            WithTrackPlugin(trackPlugin => trackPlugin.TrackEvent(BindTrackEvents.QueryConflict, properties));
        }

        private static void TrackResolve(
            NetResponse<PbNetBindResolveResp> response,
            string openId,
            string choice,
            string verifyCode,
            long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { BindTrackFields.ResolveChoice, ResolveChoice(choice) },
                { BindTrackFields.ResolveHasVerifyCode, !string.IsNullOrEmpty(verifyCode) },
                { BindTrackFields.ResolveResult, ResolveResolveResult(response) },
                { BindTrackFields.ResolveErrorCode, response.ErrorCode },
                { BindTrackFields.ResolveDurationMs, NormalizeDuration(durationMilliseconds) },
            };

            WithTrackPlugin(trackPlugin =>
            {
                if (response.IsSuccess && response.Data != null)
                {
                    string finalUid = response.Data.FinalUid ?? string.Empty;
                    if (!string.IsNullOrEmpty(finalUid))
                    {
                        trackPlugin.SetUserId(finalUid);
                    }
                    if (!string.IsNullOrEmpty(openId))
                    {
                        trackPlugin.SetUserProperty(BindTrackFields.OpenId, openId);
                    }
                }
                trackPlugin.TrackEvent(BindTrackEvents.Resolve, properties);
            });
        }

        private static void TrackResolveException(
            string choice,
            string verifyCode,
            long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { BindTrackFields.ResolveChoice, ResolveChoice(choice) },
                { BindTrackFields.ResolveHasVerifyCode, !string.IsNullOrEmpty(verifyCode) },
                { BindTrackFields.ResolveResult, "exception" },
                { BindTrackFields.ResolveErrorCode, -1 },
                { BindTrackFields.ResolveDurationMs, NormalizeDuration(durationMilliseconds) },
            };
            WithTrackPlugin(trackPlugin => trackPlugin.TrackEvent(BindTrackEvents.Resolve, properties));
        }

        private static string ResolveBindResult(NetResponse<PbNetBindResp> response)
        {
            if (response.IsSuccess) return "success";
            return response.ErrorCode == BindErrorCode.ErrBindConflict ? "conflict" : "failed";
        }

        private static string ResolveResolveResult(NetResponse<PbNetBindResolveResp> response)
        {
            if (response.IsSuccess) return "success";
            if (response.ErrorCode == BindErrorCode.ErrBindConflict) return "conflict";
            return response.ErrorCode == BindErrorCode.ErrBindBusy ? "busy" : "failed";
        }

        /// <summary>
        /// 将第三方登录提供方转换为埋点约定的小写名称，未知值统一返回 unknown。
        /// </summary>
        /// <param name="provider">第三方登录提供方。</param>
        /// <returns>埋点使用的第三方登录提供方名称。</returns>
        private static string ResolveProvider(ThirdLoginProvider provider)
        {
            switch (provider)
            {
                case ThirdLoginProvider.Facebook: return "facebook";
                case ThirdLoginProvider.Google: return "google";
                case ThirdLoginProvider.Apple: return "apple";
                case ThirdLoginProvider.Wechat: return "wechat";
                default: return "unknown";
            }
        }

        private static string ResolveChoice(string choice)
            => choice == "guest" || choice == "existing" ? choice : "unknown";

        private static int NormalizeDuration(long durationMilliseconds)
            => (int)Math.Min(int.MaxValue, Math.Max(0L, durationMilliseconds));

        private static void WithTrackPlugin(Action<ITrackPlugin> action)
        {
            try
            {
                if (Nova.SDK == null)
                {
                    return;
                }

                IReadOnlyList<ITrackPlugin> trackPlugins = Nova.SDK.GetAll<ITrackPlugin>();
                for (int i = 0; i < trackPlugins.Count; i++)
                {
                    try
                    {
                        action(trackPlugins[i]);
                    }
                    catch (Exception exception)
                    {
                        Log.Warning(LogTag.SDK, "GameBind 单个埋点插件上报异常（已隔离）：{0}", exception);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.SDK, "GameBind 埋点上报异常（已隔离）：{0}", exception);
            }
        }
    }
}
