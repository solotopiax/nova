/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateComponent.Visitors.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动组件-访问器
 ***************************************************************/

using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 振动组件。
    /// </summary>
    public sealed partial class VibrateComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前振动管理器类型名称。
        /// </summary>
        [Tooltip("振动管理器实现类全名")]
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.VibrateManager";
        public string CurManagerTypeName => m_CurManagerTypeName;

        /// <summary>
        /// 振动设置。
        /// </summary>
        [Tooltip("振动设置")]
        [SerializeField]
        private VibrateSettings m_Settings;

        /// <summary>
        /// 振动管理器实例。
        /// </summary>
        private IVibrateManager m_VibrateManager;

        /// <summary>
        /// 异步加载振动数据任务完成源。
        /// </summary>
        private UniTaskCompletionSource<bool> m_LoadTcs;

        /// <summary>
        /// 是否已加载完成。
        /// </summary>
        public bool IsLoadOver { get; private set; }

    }
}
