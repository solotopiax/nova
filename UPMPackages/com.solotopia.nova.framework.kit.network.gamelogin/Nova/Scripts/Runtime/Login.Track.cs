/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Login.Track.cs
 * author:    taoye
 * created:   2026/7/21
 * descrip:   GameLogin 关键行为埋点
 ***************************************************************/

using System;
using System.Collections.Generic;
using NovaFramework.Runtime;

namespace NovaFramework.Kit.Network.GameLogin.Runtime
{
    public sealed partial class Login
    {
        private static void TrackLogin(
            NetResponse<PbNetLoginResp> response,
            string uid,
            string openId,
            bool forceNewAccount,
            long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { LoginTrackFields.LoginType, ResolveLoginType(uid, openId, forceNewAccount) },
                { LoginTrackFields.LoginResult, response.IsSuccess ? "success" : "failed" },
                { LoginTrackFields.LoginErrorCode, response.ErrorCode },
                { LoginTrackFields.LoginDurationMs, NormalizeDuration(durationMilliseconds) },
            };

            if (response.IsSuccess && response.Data != null)
            {
                properties[LoginTrackFields.LoginRegisterTime] = response.Data.RegisterTime;
                properties[LoginTrackFields.LoginTime] = response.Data.LoginTime;
                properties[LoginTrackFields.LoginIsNewAccount] = response.Data.IsNewAccount;
            }

            WithTrackPlugin(trackPlugin =>
            {
                if (response.IsSuccess && !string.IsNullOrEmpty(openId))
                {
                    trackPlugin.SetUserProperty(LoginTrackFields.OpenId, openId);
                }
                trackPlugin.TrackEvent(LoginTrackEvents.Login, properties);
            });
        }

        private static void TrackLoginException(
            string uid,
            string openId,
            bool forceNewAccount,
            long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { LoginTrackFields.LoginType, ResolveLoginType(uid, openId, forceNewAccount) },
                { LoginTrackFields.LoginResult, "exception" },
                { LoginTrackFields.LoginErrorCode, -1 },
                { LoginTrackFields.LoginDurationMs, NormalizeDuration(durationMilliseconds) },
            };
            WithTrackPlugin(trackPlugin => trackPlugin.TrackEvent(LoginTrackEvents.Login, properties));
        }

        private static void TrackDeleteAccount(
            NetResponse<PbNetDeleteResp> response,
            long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { LoginTrackFields.DeleteAccountResult, response.IsSuccess ? "success" : "failed" },
                { LoginTrackFields.DeleteAccountErrorCode, response.ErrorCode },
                { LoginTrackFields.DeleteAccountDurationMs, NormalizeDuration(durationMilliseconds) },
            };
            WithTrackPlugin(trackPlugin => trackPlugin.TrackEvent(LoginTrackEvents.DeleteAccount, properties));
        }

        private static void TrackDeleteAccountException(long durationMilliseconds)
        {
            var properties = new Dictionary<string, object>
            {
                { LoginTrackFields.DeleteAccountResult, "exception" },
                { LoginTrackFields.DeleteAccountErrorCode, -1 },
                { LoginTrackFields.DeleteAccountDurationMs, NormalizeDuration(durationMilliseconds) },
            };
            WithTrackPlugin(trackPlugin => trackPlugin.TrackEvent(LoginTrackEvents.DeleteAccount, properties));
        }

        private static string ResolveLoginType(string uid, string openId, bool forceNewAccount)
        {
            if (forceNewAccount) return "force_new";
            return string.IsNullOrEmpty(openId) ? "uid" : "third_party";
        }

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
                        Log.Warning(LogTag.SDK, "GameLogin 单个埋点插件上报异常（已隔离）：{0}", exception);
                    }
                }
            }
            catch (Exception exception)
            {
                Log.Warning(LogTag.SDK, "GameLogin 埋点上报异常（已隔离）：{0}", exception);
            }
        }
    }
}
