/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGAPlugin.StaticSuperProperty.cs
 * author:    yingzheng
 * created:   2026/4/20
 * descrip:   TGAPlugin 静态公共事件属性管理
 ***************************************************************/

#if !UNITY_WEBGL
using System.Collections.Generic;
using ThinkingData.Analytics;

namespace NovaFramework.SDK.TGAPlugin.Runtime
{
    public sealed partial class TGAPlugin
    {
        /// <summary>
        /// 静态设置单条公共事件属性，所有后续事件自动携带。
        /// 若已存在同名属性则覆盖。
        /// </summary>
        /// <param name="key">属性键名。</param>
        /// <param name="value">属性值。</param>
        public void SetSuperProperty(string key, object value)
        {
            TDAnalytics.SetSuperProperties(new Dictionary<string, object> { { key, value } });
        }

        /// <summary>
        /// 批量静态设置公共事件属性，所有后续事件自动携带。
        /// 已存在的同名属性将被覆盖。
        /// </summary>
        /// <param name="properties">属性键值对字典。</param>
        public void SetSuperProperties(Dictionary<string, object> properties)
        {
            TDAnalytics.SetSuperProperties(properties);
        }

        /// <summary>
        /// 移除指定静态公共事件属性。
        /// </summary>
        /// <param name="key">属性键名。</param>
        public void UnsetSuperProperty(string key)
        {
            TDAnalytics.UnsetSuperProperty(key);
        }

        /// <summary>
        /// 清除全部静态公共事件属性。
        /// </summary>
        public void ClearSuperProperties()
        {
            TDAnalytics.ClearSuperProperties();
        }
    }
}
#endif
