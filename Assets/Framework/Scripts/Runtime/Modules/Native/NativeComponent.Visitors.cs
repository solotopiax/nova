/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeComponent.Visitors.cs
 * author:    taoye
 * created:   2026/8/7
 * descrip:   NativeComponent 字段定义
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    public sealed partial class NativeComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前 NativeManager 实现类全名。
        /// </summary>
        [Tooltip("NativeManager 的实现类全名")]
        [SerializeField]
        private string m_CurNativeManagerTypeName = "NovaFramework.Runtime.NativeManager";

        /// <summary>
        /// 获取当前 NativeManager 实现类全名。
        /// </summary>
        public string CurNativeManagerTypeName => m_CurNativeManagerTypeName;

        /// <summary>
        /// NativeManager 初始化配置。
        /// </summary>
        [Tooltip("NativeManager 初始化配置")]
        [SerializeField]
        private NativeManagerConfig m_NativeManagerConfig = new NativeManagerConfig();

        /// <summary>
        /// 获取 NativeManager 初始化配置。
        /// </summary>
        public NativeManagerConfig NativeManagerConfig => m_NativeManagerConfig;

        /// <summary>
        /// NativeManager 私有接口引用，不得向 Component 外部暴露。
        /// </summary>
        private INativeManager m_NativeManager;
    }
}
