/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlatformChannelEntry.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   Platform × Channel 矩阵行，含该组合下按 DevelopMode 分组的公共配置、SDK Plugin 配置与 Kit 配置
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Platform × Channel 矩阵的一行；
    /// 每一行内部按 DevelopMode 独立存储 CommonConfig、SDK 配置与 Kit 配置，
    /// 使 (Platform, Channel, DevelopMode) 三维任意切换都各自保留独立数据。
    /// </summary>
    [Serializable]
    public sealed class PlatformChannelEntry
    {
        /// <summary>
        /// 该条目对应的目标平台。
        /// </summary>
        public PlatformType Platform;

        /// <summary>
        /// 该条目对应的渠道。
        /// </summary>
        public ChannelType Channel;

        /// <summary>
        /// 按 DevelopMode 分组的公共配置列表；
        /// 默认预置 Debug 与 Release 两份空条目。
        /// </summary>
        public List<DevelopModeCommonEntry> CommonByMode = new();

        /// <summary>
        /// 按 DevelopMode 分组的 SDK Plugin 配置列表；
        /// 默认预置 Debug 与 Release 两份空条目。
        /// </summary>
        public List<DevelopModeSDKEntry> SDKConfigsByMode = new();

        /// <summary>
        /// 按 DevelopMode 分组的 Kit 配置列表；
        /// 默认预置 Debug 与 Release 两份空条目。
        /// </summary>
        public List<DevelopModeKitEntry> KitConfigsByMode = new();

        /// <summary>
        /// 无参构造器；为 CommonByMode、SDKConfigsByMode 与 KitConfigsByMode 分别预置 Debug / Release 两份空条目。
        /// </summary>
        public PlatformChannelEntry()
        {
            CommonByMode.Add(new DevelopModeCommonEntry { Mode = DevelopMode.Debug });
            CommonByMode.Add(new DevelopModeCommonEntry { Mode = DevelopMode.Release });
            SDKConfigsByMode.Add(new DevelopModeSDKEntry { Mode = DevelopMode.Debug });
            SDKConfigsByMode.Add(new DevelopModeSDKEntry { Mode = DevelopMode.Release });
            KitConfigsByMode.Add(new DevelopModeKitEntry { Mode = DevelopMode.Debug });
            KitConfigsByMode.Add(new DevelopModeKitEntry { Mode = DevelopMode.Release });
        }

        /// <summary>
        /// 按指定 DevelopMode 获取对应的 CommonConfig。
        /// 若当前列表中不存在该 Mode 的条目，则自动追加一条空条目并返回其 Config，确保返回值不为 null。
        /// </summary>
        /// <param name="mode">目标开发模式。</param>
        /// <returns>该模式对应的 CommonConfig 实例，永不为 null。</returns>
        public CommonConfig GetCommon(DevelopMode mode)
        {
            for (int i = 0; i < CommonByMode.Count; i++)
            {
                if (CommonByMode[i].Mode == mode)
                    return CommonByMode[i].Config;
            }

            var entry = new DevelopModeCommonEntry { Mode = mode };
            CommonByMode.Add(entry);
            return entry.Config;
        }

        /// <summary>
        /// 按指定 DevelopMode 获取对应的 SDK Plugin 配置列表。
        /// 若当前列表中不存在该 Mode 的条目，则自动追加一条空条目并返回其列表，确保返回值不为 null。
        /// </summary>
        /// <param name="mode">目标开发模式。</param>
        /// <returns>该模式对应的 ISDKPluginConfig 列表，永不为 null。</returns>
        public List<ISDKPluginConfig> GetSDKConfigs(DevelopMode mode)
        {
            for (int i = 0; i < SDKConfigsByMode.Count; i++)
            {
                if (SDKConfigsByMode[i].Mode == mode)
                    return SDKConfigsByMode[i].SDKConfigs;
            }

            var entry = new DevelopModeSDKEntry { Mode = mode };
            SDKConfigsByMode.Add(entry);
            return entry.SDKConfigs;
        }

        /// <summary>
        /// 按指定 DevelopMode 获取对应的 Kit 配置列表。
        /// 若当前列表中不存在该 Mode 的条目，则自动追加一条空条目并返回其列表，确保返回值不为 null。
        /// </summary>
        /// <param name="mode">目标开发模式。</param>
        /// <returns>该模式对应的 IKitConfig 列表，永不为 null。</returns>
        public List<IKitConfig> GetKitConfigs(DevelopMode mode)
        {
            for (int i = 0; i < KitConfigsByMode.Count; i++)
            {
                if (KitConfigsByMode[i].Mode == mode)
                    return KitConfigsByMode[i].KitConfigs;
            }

            var entry = new DevelopModeKitEntry { Mode = mode };
            KitConfigsByMode.Add(entry);
            return entry.KitConfigs;
        }
    }

    /// <summary>
    /// 单个 DevelopMode 下的公共配置条目；
    /// 作为 PlatformChannelEntry.CommonByMode 的元素。
    /// </summary>
    [Serializable]
    public sealed class DevelopModeCommonEntry
    {
        /// <summary>
        /// 本条目对应的开发模式；默认为 Debug。
        /// </summary>
        public DevelopMode Mode = DevelopMode.Debug;

        /// <summary>
        /// 该模式下的公共配置实例。
        /// </summary>
        public CommonConfig Config = new();
    }

    /// <summary>
    /// 单个 DevelopMode 下的 SDK Plugin 配置条目；
    /// 作为 PlatformChannelEntry.SDKConfigsByMode 的元素。
    /// </summary>
    [Serializable]
    public sealed class DevelopModeSDKEntry
    {
        /// <summary>
        /// 本条目对应的开发模式；默认为 Debug。
        /// </summary>
        public DevelopMode Mode = DevelopMode.Debug;

        /// <summary>
        /// 本模式下启用的 SDK Plugin 配置列表；
        /// 元素类型为 ISDKPluginConfig 的任意实现，使用 SerializeReference 支持多态序列化。
        /// </summary>
        [SerializeReference]
        public List<ISDKPluginConfig> SDKConfigs = new();
    }

    /// <summary>
    /// 单个 DevelopMode 下的 Kit 配置条目；
    /// 作为 PlatformChannelEntry.KitConfigsByMode 的元素。
    /// </summary>
    [Serializable]
    public sealed class DevelopModeKitEntry
    {
        /// <summary>
        /// 本条目对应的开发模式；默认为 Debug。
        /// </summary>
        public DevelopMode Mode = DevelopMode.Debug;

        /// <summary>
        /// 本模式下启用的 Kit 配置列表；
        /// 元素类型为 IKitConfig 的任意实现，使用 SerializeReference 支持多态序列化。
        /// </summary>
        [SerializeReference]
        public List<IKitConfig> KitConfigs = new();
    }
}
