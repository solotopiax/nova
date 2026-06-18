/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Batch.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 流水线批处理组合，有序持有 BatchItem 列表
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 批处理组合。有序的 BatchItem 列表，有名字与描述，对应一条可执行流水线。
    /// </summary>
    [Serializable]
    public sealed class Batch
    {
        /// <summary>
        /// Batch 名称，在所属 PipifySettingsSO 中唯一。
        /// </summary>
        [SerializeField] private string m_Name;
        public string Name { get => m_Name; set => m_Name = value; }

        /// <summary>
        /// Batch 描述。
        /// </summary>
        [SerializeField] private string m_Description;
        public string Description { get => m_Description; set => m_Description = value; }

        /// <summary>
        /// Step 条目有序列表。
        /// </summary>
        [SerializeField] private List<BatchItem> m_Items = new List<BatchItem>();
        public List<BatchItem> Items => m_Items;
    }
}
