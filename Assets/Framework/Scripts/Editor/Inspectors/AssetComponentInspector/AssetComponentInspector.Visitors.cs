/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetComponentInspector.Visitors.cs
 * author:    taoye
 * created:   2026/3/4
 * descrip:   Asset组件编辑器面板定制 —— 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using UnityEditor;

namespace NovaFramework.Editor
{
    internal sealed partial class AssetComponentInspector : BaseComponentInspector
    {
        // ① Manager 选择

        /// <summary>
        /// 当前 AssetManager 类型名称。
        /// </summary>
        private SerializedProperty m_CurAssetManagerTypeName;

        /// <summary>
        /// AssetManager 所有类型名称。
        /// </summary>
        private List<string> m_AssetManagerTypeNames;

        // ② 加载模式

        /// <summary>
        /// 编辑器下资源加载模式。
        /// </summary>
        private SerializedProperty m_EditorPlayMode;

        /// <summary>
        /// 终端下资源加载模式。
        /// </summary>
        private SerializedProperty m_RuntimePlayMode;

        // ③ 资源包配置

        /// <summary>
        /// 资源包名列表。
        /// </summary>
        private SerializedProperty m_Packages;

        /// <summary>
        /// 默认包名。
        /// </summary>
        private SerializedProperty m_DefaultPackageName;

        /// <summary>
        /// 资源解密器类型。
        /// </summary>
        private SerializedProperty m_DecryptorType;

        /// <summary>
        /// 场景卸载时是否自动清理。
        /// </summary>
        private SerializedProperty m_AutoCleanupOnSceneUnload;

        // ④ 热更配置（开关 + 服务器分发 + 下载行为）

        /// <summary>
        /// 是否启用热更新。
        /// </summary>
        private SerializedProperty m_EnableHotfix;

        /// <summary>
        /// Debug 主机服务器地址 URL。
        /// </summary>
        private SerializedProperty m_HostServerUrlDebug;

        /// <summary>
        /// Debug 备用主机服务器地址 URL。
        /// </summary>
        private SerializedProperty m_HostServerUrlFallbackDebug;

        /// <summary>
        /// Release 主机服务器地址 URL。
        /// </summary>
        private SerializedProperty m_HostServerUrlRelease;

        /// <summary>
        /// Release 备用主机服务器地址 URL。
        /// </summary>
        private SerializedProperty m_HostServerUrlFallbackRelease;

        /// <summary>
        /// 补丁就绪是否自动开始下载。
        /// </summary>
        private SerializedProperty m_AutoHotfix;

        /// <summary>
        /// 失败或取消时是否强制退出。
        /// </summary>
        private SerializedProperty m_QuitOnFailedOrCancel;

        /// <summary>
        /// 下载最大并发数。
        /// </summary>
        private SerializedProperty m_MaxDownloadConcurrency;

        /// <summary>
        /// 下载失败重试次数。
        /// </summary>
        private SerializedProperty m_RetryDownloadCount;

        /// <summary>
        /// 版本检查超时（秒）。
        /// </summary>
        private SerializedProperty m_CheckTimeout;

        /// <summary>
        /// 文件下载空闲超时（秒）。
        /// </summary>
        private SerializedProperty m_IdleTimeout;

        /// <summary>
        /// 启动期切片下载 tag 列表。
        /// </summary>
        private SerializedProperty m_LaunchHotfixTags;

        /// <summary>
        /// 热更完成后是否自动清理磁盘旧缓存。
        /// </summary>
        private SerializedProperty m_AutoClearUnusedCacheOnHotfix;
    }
}
