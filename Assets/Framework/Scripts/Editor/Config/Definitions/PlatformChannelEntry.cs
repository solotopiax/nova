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
using NovaFramework.Runtime;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Platform × Channel 矩阵的一行；
    /// 每一行内部按 DevelopMode 独立存储 AppConfigs、PrivacyConfigs、SDK 配置与 Kit 配置，
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
        public List<DevelopModeAppConfigsEntry> AppConfigsByMode = new();

        /// <summary>
        /// 按 DevelopMode 分组的隐私配置列表；默认预置 Debug 与 Release 两份空条目。
        /// </summary>
        public List<DevelopModePrivacyConfigsEntry> PrivacyConfigsByMode = new();

        /// <summary>
        /// 旧版按 DevelopMode 分组的 CommonConfig 数据缓冲；迁移成功后清空。
        /// </summary>
        [SerializeField, HideInInspector]
        private List<DevelopModeAppConfigsEntry> CommonByMode;

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
        /// 无参构造器；为 AppConfigsByMode、PrivacyConfigsByMode、SDKConfigsByMode 与 KitConfigsByMode 分别预置 Debug / Release 两份空条目。
        /// </summary>
        public PlatformChannelEntry()
        {
            AppConfigsByMode.Add(new DevelopModeAppConfigsEntry { Mode = DevelopMode.Debug });
            AppConfigsByMode.Add(new DevelopModeAppConfigsEntry { Mode = DevelopMode.Release });
            PrivacyConfigsByMode.Add(new DevelopModePrivacyConfigsEntry { Mode = DevelopMode.Debug });
            PrivacyConfigsByMode.Add(new DevelopModePrivacyConfigsEntry { Mode = DevelopMode.Release });
            SDKConfigsByMode.Add(new DevelopModeSDKEntry { Mode = DevelopMode.Debug });
            SDKConfigsByMode.Add(new DevelopModeSDKEntry { Mode = DevelopMode.Release });
            KitConfigsByMode.Add(new DevelopModeKitEntry { Mode = DevelopMode.Debug });
            KitConfigsByMode.Add(new DevelopModeKitEntry { Mode = DevelopMode.Release });
        }

        /// <summary>
        /// 按指定 DevelopMode 获取对应的 AppConfigs。
        /// 若当前列表中不存在该 Mode 的条目，则自动追加一条空条目并返回其 Config，确保返回值不为 null。
        /// </summary>
        /// <param name="mode">目标开发模式。</param>
        /// <returns>该模式对应的 AppConfigs 实例，永不为 null。</returns>
        public AppConfigs GetAppConfigs(DevelopMode mode)
        {
            for (int i = 0; i < AppConfigsByMode.Count; i++)
            {
                if (AppConfigsByMode[i].Mode == mode)
                    return AppConfigsByMode[i].Config;
            }

            var entry = new DevelopModeAppConfigsEntry { Mode = mode };
            AppConfigsByMode.Add(entry);
            return entry.Config;
        }

        /// <summary>
        /// 按指定 DevelopMode 获取对应的 PrivacyConfigs；缺少条目时自动补齐空配置。
        /// </summary>
        /// <param name="mode">目标开发模式。</param>
        /// <returns>该模式对应的 PrivacyConfigs 实例，永不为 null。</returns>
        public PrivacyConfigs GetPrivacyConfigs(DevelopMode mode)
        {
            PrivacyConfigsByMode ??= new List<DevelopModePrivacyConfigsEntry>();
            for (int i = 0; i < PrivacyConfigsByMode.Count; i++)
            {
                if (PrivacyConfigsByMode[i].Mode == mode)
                    return PrivacyConfigsByMode[i].Config;
            }

            var entry = new DevelopModePrivacyConfigsEntry { Mode = mode };
            PrivacyConfigsByMode.Add(entry);
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

        /// <summary>
        /// 验证旧版 CommonByMode 通过 FormerlySerializedAs 恢复后的条目是否可安全使用。
        /// </summary>
        /// <param name="error">失败时返回首个无效条目的位置；成功时为 null。</param>
        /// <returns>列表与条目均有效时返回 true。</returns>
        internal bool ValidateLegacyData(out string error)
        {
            error = null;
            List<DevelopModeAppConfigsEntry> source = CommonByMode != null && CommonByMode.Count > 0
                ? CommonByMode
                : AppConfigsByMode;
            if (source == null)
            {
                error = "AppConfigsByMode 为空。";
                return false;
            }

            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] == null)
                {
                    error = $"AppConfigsByMode[{i}] 为空。";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 完成矩阵行的旧字段迁移收尾；当前字段重命名由 FormerlySerializedAs 直接恢复，无需再次复制。
        /// </summary>
        internal void ApplyLegacyData()
        {
            if (CommonByMode != null &&
                (CommonByMode.Count > 0 || AppConfigsByMode == null || AppConfigsByMode.Count == 0))
            {
                AppConfigsByMode = new List<DevelopModeAppConfigsEntry>(CommonByMode);
                CommonByMode = null;
            }
            else
            {
                CommonByMode = null;
            }
            AppConfigsByMode ??= new List<DevelopModeAppConfigsEntry>();
        }
    }

    /// <summary>
    /// 单个 DevelopMode 下的公共配置条目；
    /// 作为 PlatformChannelEntry.AppConfigsByMode 的元素。
    /// </summary>
    [Serializable]
    public sealed class DevelopModeAppConfigsEntry
    {
        /// <summary>
        /// 本条目对应的开发模式；默认为 Debug。
        /// </summary>
        public DevelopMode Mode = DevelopMode.Debug;

        /// <summary>
        /// 该模式下的公共配置实例。
        /// </summary>
        public AppConfigs Config = new();
    }

    /// <summary>
    /// 单个 DevelopMode 下的隐私配置包装项。
    /// </summary>
    [Serializable]
    public sealed class DevelopModePrivacyConfigsEntry
    {
        /// <summary>
        /// 该条目对应的开发模式。
        /// </summary>
        public DevelopMode Mode;

        /// <summary>
        /// 该模式下的隐私配置。
        /// </summary>
        public PrivacyConfigs Config = new();
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
