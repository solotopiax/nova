/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManager.SoundGroupHelper.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器 -- 声音组辅助器
 ***************************************************************/

using UnityEngine;
using UnityEngine.Audio;

namespace NovaFramework.Runtime
{
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 声音组辅助器。
        /// <para>挂载在声音组 GameObject 上，持有 AudioMixerGroup 引用。</para>
        /// </summary>
        internal sealed class SoundGroupHelper : MonoBehaviour
        {
            /// <summary>
            /// 混音组引用。
            /// </summary>
            [SerializeField]
            private AudioMixerGroup m_AudioMixerGroup;
            public AudioMixerGroup AudioMixerGroup { get => m_AudioMixerGroup; set => m_AudioMixerGroup = value; }
        }
    }
}
