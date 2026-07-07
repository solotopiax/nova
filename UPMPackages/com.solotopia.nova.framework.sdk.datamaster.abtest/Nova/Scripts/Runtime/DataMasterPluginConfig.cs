/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  DataMasterPluginConfig.cs
 * author:    taoye
 * created:   2026/7/3
 * descrip:   DataMaster 插件运行期初始化配置；作为 ISDKPluginConfig 由
 *            ConfigMasterSO 静态配置，SDKManager 按 RequiredConfigType 自动
 *            注入给 DataMasterPlugin.OnInitializeAsync。
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.SDK.StarlusDataMaster.ABTest.Runtime
{
    /// <summary>
    /// DataMaster 插件初始化所需数据：AppId / AesKey 由数据平台后台提供，DefaultConfig
    /// 为随包发布的默认配置文本（断网或服务端未下发时的兜底）。
    /// 标注 [Serializable] 以便被 ConfigWindow SDKPluginScanner 扫描到，并可作为
    /// SDKConfigsByMode 的 [SerializeReference] 条目持久化；由 Editor 面板直接编辑字段值。
    /// </summary>
    [Serializable]
    public sealed class DataMasterPluginConfig : ISDKPluginConfig
    {
        /// <summary>
        /// 应用 ID 序列化字段，由数据平台后台提供，写入拉取与上报请求。
        /// </summary>
        [SerializeField, Tooltip("应用 ID。填写数据平台后台为当前应用分配的 AppId。")]
        private string m_AppId;

        /// <summary>
        /// 服务端密钥序列化字段，用于请求体、响应体与本地缓存值的 AES 加解密。
        /// </summary>
        [SerializeField, Tooltip("服务端密钥。填写数据平台后台提供的 AES 密钥，用于请求/响应/本地缓存加解密。")]
        private string m_AesKey;

        /// <summary>
        /// 默认配置文本序列化字段，随包发布，作为断网或服务端未下发时的兜底配置。
        /// </summary>
        [SerializeField, Tooltip("默认配置文本。拖入随包发布的默认配置 JSON（策划导出的开发客户端配置），作为兜底。")]
        private TextAsset m_DefaultConfig;

        /// <summary>
        /// 应用 ID，由数据平台后台提供。
        /// </summary>
        public string AppId => m_AppId;

        /// <summary>
        /// 服务端密钥，用于请求/响应/本地缓存加解密。
        /// </summary>
        public string AesKey => m_AesKey;

        /// <summary>
        /// 随包默认配置文本，可为空（空时以服务端下发为准，未下发则无兜底值）。
        /// </summary>
        public TextAsset DefaultConfig => m_DefaultConfig;

        /// <summary>
        /// ConfigWindow 左树展示的中文名称。
        /// </summary>
        public string DisplayName => "DataMaster ABTest";

        /// <summary>
        /// 无参构造器；供 ConfigWindow SDKPluginScanner 通过 Activator 创建空实例使用。
        /// </summary>
        public DataMasterPluginConfig() { }

        /// <summary>
        /// 构造 DataMasterPluginConfig 实例。
        /// </summary>
        /// <param name="appId">应用 ID。</param>
        /// <param name="aesKey">服务端密钥。</param>
        /// <param name="defaultConfig">随包默认配置文本，可为 null。</param>
        public DataMasterPluginConfig(string appId, string aesKey, TextAsset defaultConfig = null)
        {
            m_AppId = appId;
            m_AesKey = aesKey;
            m_DefaultConfig = defaultConfig;
        }
    }
}
