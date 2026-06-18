/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  SoundManager.Visitors.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   声音管理器 -- 属性与字段
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace NovaFramework.Runtime
{
    internal sealed partial class SoundManager : SoundManagerBase
    {
        /// <summary>
        /// 声音组集合，<声音组名称, SoundGroup>。
        /// </summary>
        private readonly Dictionary<string, SoundGroup> m_SoundGroups;

        /// <summary>
        /// 正在加载中的声音序列编号集合。
        /// </summary>
        private readonly HashSet<int> m_SoundsLoading;

        /// <summary>
        /// 加载完毕后需要立即释放的声音序列编号集合。
        /// </summary>
        private readonly HashSet<int> m_SoundsToReleaseOnLoad;

        /// <summary>
        /// 已构建的 Luban 声音表对象（表类型, ITable 实例）。
        /// </summary>
        private readonly Dictionary<Type, ITable> m_SoundTableDatas;

        /// <summary>
        /// 按 Name 索引的 SoundRow 缓存，<ISoundRow.Name, ISoundRow>。
        /// </summary>
        private readonly Dictionary<string, ISoundRow> m_SoundRows;

        /// <summary>
        /// 资源管理器。
        /// </summary>
        private IAssetManager m_AssetManager;

        /// <summary>
        /// Helper 挂载的父节点 Transform。
        /// </summary>
        private Transform m_ParentTransform;

        /// <summary>
        /// 声音混音器（可选）。
        /// </summary>
        private AudioMixer m_AudioMixer;

        /// <summary>
        /// 声音单元设置列表，每个单元独立指定 Asset 地址。
        /// </summary>
        private List<SoundUnitSetting> m_SoundUnitsSettings;

        /// <summary>
        /// 序列编号（只增不减）。
        /// </summary>
        private int m_Serial;

        /// <summary>
        /// Luban Tables 类名称（与 Luban 导出配置中的 manager 参数一致）。
        /// </summary>
        private const string c_SoundTablesClassName = "SoundTables";

        /// <summary>
        /// 获取声音组数量。
        /// </summary>
        public override int SoundGroupCount => m_SoundGroups.Count;
    }
}
