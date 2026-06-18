/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManagerConfig.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器配置
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 声音管理器配置。
    /// </summary>
    public class SoundManagerConfig
    {
        /// <summary>
        /// 声音单元设置列表，每个单元指定独立的 Asset 地址。
        /// </summary>
        public List<SoundUnitSetting> SoundUnitsSettings;

        /// <summary>
        /// Helper 挂载的父节点 Transform。
        /// </summary>
        public Transform ParentTransform;

        /// <summary>
        /// 声音混音器（可选）。
        /// </summary>
        public AudioMixer AudioMixer;
    }
}
