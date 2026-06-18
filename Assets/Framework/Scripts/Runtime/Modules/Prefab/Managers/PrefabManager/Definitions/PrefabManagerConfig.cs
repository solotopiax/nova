/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrefabManagerConfig.cs
 * author:    taoye
 * created:   2026/5/14
 * descrip:   PrefabManager 启动配置
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// PrefabManager 启动配置。
    /// </summary>
    /// <remarks>
    /// PrefabComponent 在 Initialize 时将 Inspector 字段打包传入。
    /// 当前采用「每实例一 handle + PrefabInstanceTag 单路径回收」策略，无额外配置项；
    /// 此类保留作扩展位，后续可在此追加配置字段。
    /// </remarks>
    [Serializable]
    public class PrefabManagerConfig
    {
    }
}
