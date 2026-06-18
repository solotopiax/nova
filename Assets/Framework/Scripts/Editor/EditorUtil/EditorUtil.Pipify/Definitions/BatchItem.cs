/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  BatchItem.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 流水线 Batch 中的一个步骤条目
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Batch 中的一个条目。
    /// 持有稳定的 StepId 和独立的参数 JSON；同一 StepId 可在同一 Batch 中出现多次且互不影响。
    /// </summary>
    [Serializable]
    public sealed class BatchItem
    {
        /// <summary>
        /// Step 稳定 ID（重命名 DisplayName 不影响存档）。
        /// </summary>
        [SerializeField] private string m_StepId;
        public string StepId { get => m_StepId; set => m_StepId = value; }

        /// <summary>
        /// 参数序列化结果（Util.Json 序列化）。无参 Step 为空字符串。
        /// </summary>
        [SerializeField] private string m_ParamsJson;
        public string ParamsJson { get => m_ParamsJson; set => m_ParamsJson = value; }
    }
}
