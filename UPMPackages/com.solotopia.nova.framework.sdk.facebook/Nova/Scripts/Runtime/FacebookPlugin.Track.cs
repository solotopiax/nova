/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FacebookPlugin.Track.cs
 * author:    taoye
 * created:   2026/8/17
 * descrip:   Facebook 买量打点桥接
 ***************************************************************/

using System.Collections.Generic;
using Facebook.Unity;
using NovaFramework.Runtime;

namespace NovaFramework.SDK.Facebook
{
    /// <summary>
    /// Facebook App Events 买量打点实现，承接 Nova 的 IAcquisitionTrackPlugin 统一接口。
    /// </summary>
    public sealed partial class FacebookPlugin
    {
        /// <summary>
        /// 将当前业务用户 ID 同步给 Facebook App Events，用于后续买量事件归因。
        /// </summary>
        /// <param name="userId">业务用户标识。</param>
        public void SetUserId(string userId)
        {
            if (!FB.IsInitialized || string.IsNullOrWhiteSpace(userId))
            {
                return;
            }

            FB.Mobile.UserID = userId;
        }

        /// <summary>
        /// 使用 Nova 通用打点载荷上报 Facebook App Event。
        /// </summary>
        /// <param name="evt">通用打点事件载荷。</param>
        public void TrackEvent(TrackEvent evt)
        {
            if (evt == null)
            {
                return;
            }

            TrackEvent(evt.Name, evt.Parameters);
        }

        /// <summary>
        /// 使用事件名和参数字典上报 Facebook App Event。
        /// </summary>
        /// <param name="eventName">事件名。</param>
        /// <param name="parameters">事件参数。</param>
        public void TrackEvent(string eventName, Dictionary<string, object> parameters)
        {
            if (!FB.IsInitialized || string.IsNullOrEmpty(eventName))
            {
                return;
            }

            FB.LogAppEvent(eventName, null, BuildFacebookParameters(parameters));
        }

        /// <summary>
        /// 将 Nova 打点参数转换为 Facebook App Events 可接受的参数集合。
        /// </summary>
        /// <param name="parameters">Nova 打点参数。</param>
        /// <returns>Facebook App Events 参数；无有效参数时返回 null。</returns>
        private static Dictionary<string, object> BuildFacebookParameters(Dictionary<string, object> parameters)
        {
            if (parameters == null || parameters.Count == 0)
            {
                return null;
            }

            Dictionary<string, object> converted = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in parameters)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                {
                    continue;
                }

                converted[pair.Key] = ConvertFacebookParameterValue(pair.Value);
            }

            return converted.Count > 0 ? converted : null;
        }

        /// <summary>
        /// 保留基础类型参数，将 Facebook 不直接识别的对象转换为字符串。
        /// </summary>
        /// <param name="value">原始参数值。</param>
        /// <returns>Facebook 可兼容的参数值。</returns>
        private static object ConvertFacebookParameterValue(object value)
        {
            return value is string
                || value is bool
                || value is int
                || value is long
                || value is float
                || value is double
                || value is decimal
                    ? value
                    : value.ToString();
        }
    }
}
