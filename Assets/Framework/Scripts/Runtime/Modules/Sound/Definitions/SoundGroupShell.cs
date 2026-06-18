/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundGroupShell.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音组外壳（Inspector 序列化用）
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音组外壳（Inspector 序列化用）。
    /// </summary>
    [Serializable]
    internal sealed class SoundGroupShell
    {
        /// <summary>
        /// 声音组名称。
        /// </summary>
        [SerializeField]
        private string m_Name;
        public string Name => m_Name;

        /// <summary>
        /// 是否避免被同优先级声音替换。
        /// </summary>
        [SerializeField]
        private bool m_AvoidBeingReplacedBySamePriority;
        public bool AvoidBeingReplacedBySamePriority => m_AvoidBeingReplacedBySamePriority;

        /// <summary>
        /// 是否静音。
        /// </summary>
        [SerializeField]
        private bool m_Mute;
        public bool Mute => m_Mute;

        /// <summary>
        /// 音量。
        /// </summary>
        [SerializeField, Range(0f, 1f)]
        private float m_Volume = 1f;
        public float Volume => m_Volume;

        /// <summary>
        /// 声音代理数量。
        /// </summary>
        [SerializeField]
        private int m_AgentCount = 1;
        public int AgentCount => m_AgentCount;
    }
}
