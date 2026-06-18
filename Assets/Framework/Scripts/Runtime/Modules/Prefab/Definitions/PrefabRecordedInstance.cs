/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabRecordedInstance.cs
 * author:    taoye
 * created:   2026/5/16
 * descrip:   PrefabManager 实例诊断记录数据结构
 ***************************************************************/

using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// PrefabManager 当前跟踪的单条实例记录，供 Inspector 诊断面板只读展示。
    /// </summary>
    public readonly struct PrefabRecordedInstance
    {
        /// <summary>
        /// 已实例化的 GameObject 引用。
        /// </summary>
        public GameObject Instance { get; }
        /// <summary>
        /// 实例化时传入的 Prefab location 字符串。
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// 初始化 PrefabRecordedInstance 记录。
        /// </summary>
        /// <param name="instance">已实例化的 GameObject。</param>
        /// <param name="location">Prefab location 字符串。</param>
        public PrefabRecordedInstance(GameObject instance, string location)
        {
            Instance = instance;
            Location = location;
        }
    }
}
