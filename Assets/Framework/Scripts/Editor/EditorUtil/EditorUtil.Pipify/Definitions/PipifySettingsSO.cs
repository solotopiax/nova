/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySettingsSO.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 自动化流水线存档 ScriptableObject
 ***************************************************************/

using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 自动化流水线存档 ScriptableObject（Editor 层，随项目进 git）。
    /// 持有若干 Batch；存档路径由用户通过 PipifyWindow 顶栏选择或创建。
    /// </summary>
    [CreateAssetMenu(menuName = "Nova/Pipify Settings", fileName = "PipifySettings")]
    public sealed class PipifySettingsSO : ScriptableObject
    {
        /// <summary>
        /// 全部 Batch 列表。
        /// </summary>
        [SerializeField] private List<Batch> m_Batches = new List<Batch>();
        public List<Batch> Batches => m_Batches;
    }
}
