/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AppsFlyerConversionListener.cs
 * author:    yingzheng
 * created:   2026/4/28
 * descrip:   AF转化数据回调监听器，MonoBehaviour包装，供initSDK第3参数使用
 ***************************************************************/

#if !UNITY_WEBGL
using AppsFlyerSDK;
using UnityEngine;

namespace NovaFramework.SDK.AppsFlyerPlugin.Runtime
{
    /// <summary>
    /// AppsFlyer转化数据回调MonoBehaviour监听器。
    /// 由AppsFlyerPlugin在OnInitializeAsync内动态创建并绑定，
    /// 接收AF SDK的IAppsFlyerConversionData四个回调后路由至AppsFlyerPlugin的internal处理方法。
    /// </summary>
    public sealed class AppsFlyerConversionListener : MonoBehaviour, IAppsFlyerConversionData
    {
        /// <summary>
        /// 绑定宿主插件实例，所有回调将委托至该实例的internal处理方法。
        /// </summary>
        /// <param name="owner">持有本监听器的AppsFlyerPlugin实例。</param>
        public void Bind(AppsFlyerPlugin owner)
        {
            m_Owner = owner;
        }

        /// <summary>
        /// IAppsFlyerConversionData回调：归因数据获取成功。
        /// 将回调转发至宿主插件的HandleConversionDataSuccess。
        /// </summary>
        /// <param name="conversionData">归因数据JSON字符串。</param>
        public void onConversionDataSuccess(string conversionData)
        {
            if (m_Owner == null)
            {
                return;
            }
            m_Owner.HandleConversionDataSuccess(conversionData);
        }

        /// <summary>
        /// IAppsFlyerConversionData回调：归因数据获取失败。
        /// 将回调转发至宿主插件的HandleConversionDataFail。
        /// </summary>
        /// <param name="error">错误描述信息。</param>
        public void onConversionDataFail(string error)
        {
            if (m_Owner == null)
            {
                return;
            }
            m_Owner.HandleConversionDataFail(error);
        }

        /// <summary>
        /// IAppsFlyerConversionData回调：深度链接打开（热启动/冷启动）。
        /// 将回调转发至宿主插件的HandleAppOpenAttribution。
        /// </summary>
        /// <param name="attributionData">深度链接归因数据JSON字符串。</param>
        public void onAppOpenAttribution(string attributionData)
        {
            if (m_Owner == null)
            {
                return;
            }
            m_Owner.HandleAppOpenAttribution(attributionData);
        }

        /// <summary>
        /// IAppsFlyerConversionData回调：深度链接打开失败。
        /// 将回调转发至宿主插件的HandleAppOpenAttributionFailure。
        /// </summary>
        /// <param name="error">错误描述信息。</param>
        public void onAppOpenAttributionFailure(string error)
        {
            if (m_Owner == null)
            {
                return;
            }
            m_Owner.HandleAppOpenAttributionFailure(error);
        }

        /// <summary>
        /// 宿主插件引用，回调转发目标；由Bind方法注入。
        /// </summary>
        private AppsFlyerPlugin m_Owner;
    }
}
#endif
