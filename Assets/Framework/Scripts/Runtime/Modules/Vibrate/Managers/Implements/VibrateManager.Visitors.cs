/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  VibrateManager.Visitors.cs
 * author:    taoye
 * created:   2026/4/9
 * descrip:   振动管理器 -- 属性与字段
 ***************************************************************/

using System.Collections.Generic;
using System.Threading;

namespace NovaFramework.Runtime
{
    internal sealed partial class VibrateManager : VibrateManagerBase
    {
        /// <summary>
        /// 资源管理器，在 Initialize 中获取并缓存，供 LubanDataReceiver 委托使用。
        /// </summary>
        private IAssetManager m_AssetManager;

        /// <summary>
        /// Emphasis 振动分组缓存（组名 -> 强调振动数据行列表）。
        /// </summary>
        private Dictionary<string, List<IVibrateEmphasisRow>> m_EmphasisGroups;

        /// <summary>
        /// Custom 振动分组缓存（组名 -> 自定义振动数据行列表）。
        /// </summary>
        private Dictionary<string, List<IVibrateCustomRow>> m_CustomGroups;

        /// <summary>
        /// Emphasis 振动单元设置列表。
        /// </summary>
        private List<VibrateUnitSetting> m_EmphasisUnitsSettings;
        /// <summary>
        /// Custom 振动单元设置列表。
        /// </summary>
        private List<VibrateUnitSetting> m_CustomUnitsSettings;

        /// <summary>
        /// 组合播放取消令牌源（全局单实例，任何新的组合播放先取消旧的）。
        /// </summary>
        private CancellationTokenSource m_PlayCts;

#if !NOVA_NICEVIBRATIONS
        /// <summary>
        /// 振动启用开关（无 NiceVibrations 时的内部备用字段）。
        /// </summary>
        private bool m_Enable = true;
#endif
    }
}
