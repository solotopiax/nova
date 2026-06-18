/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  TGADynamicSuperPropertyListener.cs
 * author:    yingzheng
 * created:   2026/4/29
 * descrip:   TGA 动态公共属性回调监听器，MonoBehaviour 包装，供 TDAnalytics.SetDynamicSuperProperties 使用
 ***************************************************************/

#if !UNITY_WEBGL
using System.Collections.Generic;
using ThinkingData.Analytics;
using UnityEngine;

namespace NovaFramework.SDK.TGAPlugin.Runtime
{
    /// <summary>
    /// TGA 动态公共属性回调 MonoBehaviour 监听器。
    /// 由 TGAPlugin 在 OnInitializeAsync 内动态创建并绑定，接收 TDA SDK 的
    /// TDDynamicSuperPropertiesHandler.GetDynamicSuperProperties 回调后路由至 TGAPlugin 的处理方法。
    /// </summary>
    public sealed class TGADynamicSuperPropertyListener : MonoBehaviour, TDDynamicSuperPropertiesHandler
    {
        /// <summary>
        /// 绑定宿主插件实例，所有回调将委托至该实例。
        /// </summary>
        /// <param name="owner">持有本监听器的 TGAPlugin 实例。</param>
        public void Bind(TGAPlugin owner)
        {
            m_Owner = owner;
        }

        /// <summary>
        /// TDDynamicSuperPropertiesHandler 回调：SDK 每次上报事件前获取动态公共属性快照。
        /// 宿主未绑定时返回空字典，避免 SDK 上报路径抛 NullReferenceException。
        /// </summary>
        /// <returns>框架与业务动态公共属性合并后的字典快照；宿主未绑定时返回空字典。</returns>
        public Dictionary<string, object> GetDynamicSuperProperties()
        {
            if (m_Owner == null)
            {
                return new Dictionary<string, object>();
            }
            return m_Owner.GetDynamicSuperProperties();
        }

        /// <summary>
        /// 宿主插件引用，回调转发目标；由 Bind 方法注入。
        /// </summary>
        private TGAPlugin m_Owner;
    }
}
#endif
