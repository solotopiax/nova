/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGAPlugin.DynamicSuperProperty.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   TGAPlugin 动态公共事件属性管理
 ***************************************************************/

#if !UNITY_WEBGL
using System.Collections.Generic;

namespace NovaFramework.SDK.TGAPlugin.Runtime
{
    public sealed partial class TGAPlugin
    {
        /// <summary>
        /// 注册动态公共事件属性，每次上报时 SDK 回调获取最新值。
        /// 若已存在同名属性则覆盖初始值。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">属性初始值。</param>
        public void SetDynamicSuperProperty(string key, object value)
        {
            lock (m_DynamicSuperProperties)
            {
                m_DynamicSuperProperties[key] = value;
            }
        }

        /// <summary>
        /// 批量注册动态公共事件属性，每次上报时 SDK 回调获取最新值。
        /// 已存在的同名属性将被覆盖。
        /// </summary>
        /// <param name="properties">属性键值对字典。</param>
        public void SetDynamicSuperProperties(Dictionary<string, object> properties)
        {
            lock (m_DynamicSuperProperties)
            {
                foreach (var kv in properties)
                {
                    m_DynamicSuperProperties[kv.Key] = kv.Value;
                }
            }
        }

        /// <summary>
        /// 移除指定动态公共事件属性。
        /// </summary>
        /// <param name="key">属性键名。</param>
        public void RemoveDynamicSuperProperty(string key)
        {
            lock (m_DynamicSuperProperties)
            {
                m_DynamicSuperProperties.Remove(key);
            }
        }

        /// <summary>
        /// 清除全部动态公共事件属性。
        /// </summary>
        public void ClearDynamicSuperProperties()
        {
            lock (m_DynamicSuperProperties)
            {
                m_DynamicSuperProperties.Clear();
            }
        }

        /// <summary>
        /// TDDynamicSuperPropertiesHandler 回调，每次上报事件前 SDK 调用此方法获取动态公共属性快照。
        /// 框架动态属性与业务动态属性合并后返回，业务属性同名 key 优先覆盖框架属性。
        /// </summary>
        /// <returns>合并框架与业务动态公共属性后的字典快照。</returns>
        public Dictionary<string, object> GetDynamicSuperProperties()
        {
            var business = BuildDynamicSuperPropertiesSnapshot();
            return MergeProperties(m_FrameworkDynamicProperties, business);
        }
    }
}
#endif
