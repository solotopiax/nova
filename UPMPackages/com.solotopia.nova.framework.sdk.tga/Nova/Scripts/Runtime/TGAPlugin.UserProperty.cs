/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGAPlugin.UserProperty.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   TGAPlugin 用户属性管理（Set/SetOnce/Add/Append/Unset/Delete）
 ***************************************************************/

#if !UNITY_WEBGL
using System.Collections.Generic;
using NovaFramework.Runtime;
using ThinkingData.Analytics;

namespace NovaFramework.SDK.TGAPlugin.Runtime
{
    public sealed partial class TGAPlugin
    {
        /// <summary>
        /// 设置单条用户属性，重复调用将覆盖旧值。
        /// 内部委托字典重载，合并逻辑统一在字典重载中处理。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">属性值。</param>
        public void UserSet(string key, object value)
        {
            UserSet(new Dictionary<string, object> { { key, value } });
        }

        /// <summary>
        /// 批量设置用户属性，重复调用将覆盖旧值。
        /// 自动合并框架 UserSet 属性，业务属性同名 key 优先覆盖框架属性。
        /// </summary>
        /// <param name="properties">属性键值对字典。</param>
        public void UserSet(Dictionary<string, object> properties)
        {
            TDAnalytics.UserSet(MergeProperties(m_FrameworkUserSetProperties, properties));
        }

        /// <summary>
        /// 首次设置单条用户属性，已存在则忽略。
        /// 内部委托字典重载，合并逻辑统一在字典重载中处理。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">属性值。</param>
        public void UserSetOnce(string key, object value)
        {
            UserSetOnce(new Dictionary<string, object> { { key, value } });
        }

        /// <summary>
        /// 批量首次设置用户属性，已存在的属性将被忽略。
        /// 自动合并框架 UserSetOnce 属性，业务属性同名 key 优先覆盖框架属性。
        /// </summary>
        /// <param name="properties">属性键值对字典。</param>
        public void UserSetOnce(Dictionary<string, object> properties)
        {
            TDAnalytics.UserSetOnce(MergeProperties(m_FrameworkUserSetOnceProperties, properties));
        }

        /// <summary>
        /// 对数值型用户属性进行累加。
        /// value 必须为 int、long、float 或 double，否则记录警告并跳过。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">累加值，需为数值类型。</param>
        public void UserAdd(string key, object value)
        {
            if (!IsNumericValue(value))
            {
                Log.Warning(LogTag.TGA, $"TGAPlugin.UserAdd：key={key} 的 value 类型 {value?.GetType().Name} 不是数值类型，跳过。");
                return;
            }
            TDAnalytics.UserAdd(new Dictionary<string, object> { { key, value } });
        }

        /// <summary>
        /// 批量对数值型用户属性进行累加。
        /// 逐条校验每个 value 是否为数值类型，非数值条目记录警告并跳过，剩余有效条目批量上报。
        /// </summary>
        /// <param name="properties">属性键值对字典，value 需为数值类型。</param>
        public void UserAdd(Dictionary<string, object> properties)
        {
            var validProperties = new Dictionary<string, object>(properties.Count);
            foreach (var kv in properties)
            {
                if (IsNumericValue(kv.Value))
                {
                    validProperties[kv.Key] = kv.Value;
                }
                else
                {
                    Log.Warning(LogTag.TGA, $"TGAPlugin.UserAdd：key={kv.Key} 的 value 类型 {kv.Value?.GetType().Name} 不是数值类型，跳过。");
                }
            }
            if (validProperties.Count > 0)
            {
                TDAnalytics.UserAdd(validProperties);
            }
        }

        /// <summary>
        /// 向列表型用户属性追加元素，不去重。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">待追加的元素列表。</param>
        public void UserAppend(string key, List<object> value)
        {
            TDAnalytics.UserAppend(new Dictionary<string, object> { { key, value } });
        }

        /// <summary>
        /// 批量向列表型用户属性追加元素，调用方须确保字典中每个 value 为 List 类型。
        /// </summary>
        /// <param name="properties">属性键值对字典，value 应为 List 类型。</param>
        public void UserAppend(Dictionary<string, object> properties)
        {
            TDAnalytics.UserAppend(properties);
        }

        /// <summary>
        /// 取消设置指定用户属性。
        /// </summary>
        /// <param name="key">属性键名。</param>
        public void UserUnset(string key)
        {
            TDAnalytics.UserUnset(key);
        }

        /// <summary>
        /// 批量取消设置多个用户属性。
        /// </summary>
        /// <param name="keys">属性键名列表。</param>
        public void UserUnset(List<string> keys)
        {
            foreach (string key in keys)
            {
                TDAnalytics.UserUnset(key);
            }
        }

        /// <summary>
        /// 删除当前用户的所有属性数据。
        /// </summary>
        public void UserDelete()
        {
            TDAnalytics.UserDelete();
        }
    }
}
#endif
