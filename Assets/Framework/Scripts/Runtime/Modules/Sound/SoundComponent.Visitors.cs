/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundComponent.Visitors.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音组件-访问器
 ***************************************************************/

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Audio;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音组件。
    /// </summary>
    public sealed partial class SoundComponent : FrameworkComponent
    {
        /// <summary>
        /// 当前声音管理器类型名称。
        /// </summary>
        [Tooltip("声音管理器实现类全名")]
        [SerializeField]
        private string m_CurManagerTypeName = "NovaFramework.Runtime.SoundManager";
        public string CurManagerTypeName => m_CurManagerTypeName;

        /// <summary>
        /// 声音混音器。
        /// </summary>
        [Tooltip("声音混音器（可选），用于 AudioMixerGroup 路由")]
        [SerializeField]
        private AudioMixer m_AudioMixer;
        public AudioMixer AudioMixer => m_AudioMixer;

        /// <summary>
        /// 声音组外壳集合（Inspector 配置）。
        /// </summary>
        [Tooltip("声音组配置集合")]
        [SerializeField]
        private SoundGroupShell[] m_SoundGroupShells;

        /// <summary>
        /// 声音设置。
        /// </summary>
        [Tooltip("声音设置")]
        [SerializeField]
        private SoundSettings m_Settings;

        /// <summary>
        /// 声音管理器实例。
        /// </summary>
        private ISoundManager m_SoundManager;

        /// <summary>
        /// 异步加载声音数据任务完成源。
        /// </summary>
        private UniTaskCompletionSource<bool> m_LoadTcs;

        /// <summary>
        /// 是否已加载完成。
        /// </summary>
        public bool IsLoadOver { get; private set; }

        /// <summary>
        /// 获取声音组数量。
        /// </summary>
        public int SoundGroupCount => m_SoundManager != null ? m_SoundManager.SoundGroupCount : 0;
    }
}
