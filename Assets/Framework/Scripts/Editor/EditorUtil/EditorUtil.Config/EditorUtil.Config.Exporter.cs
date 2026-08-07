/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.Exporter.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   将 ConfigMasterSO 当前 Platform×Channel×DevelopMode 导出为独立 ConfigRuntimeSO.asset 的工具入口
 ***************************************************************/

using System.Collections.Generic;
using System.Linq;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;
using YooAsset;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// 将 ConfigMasterSO 当前 Platform×Channel×DevelopMode 组合导出为独立 ConfigRuntimeSO.asset 的工具入口。
            /// </summary>
            public static class Exporter
            {
                /// <summary>
                /// 执行导出操作，将指定 Platform、Channel、DevelopMode 对应的配置写入目标路径。
                /// <para>若目标目录不存在则自动递归创建；目标路径已有资产时覆盖写入而非重建，以保留已有引用。</para>
                /// </summary>
                /// <param name="master">源数据 ConfigMasterSO 实例；为 null 时静默返回 null。</param>
                /// <param name="platform">目标平台类型。</param>
                /// <param name="channel">目标渠道类型。</param>
                /// <param name="mode">目标开发模式（Debug / Release）。</param>
                /// <param name="savePath">导出资产的 Assets 相对路径（如 Assets/Config/Runtime.asset）。</param>
                /// <returns>
                /// 成功时返回写入完毕的 ConfigRuntimeSO 实例；master 为 null 或未找到对应条目时返回 null。
                /// </returns>
                public static ConfigRuntimeSO Export(ConfigMasterSO master, PlatformType platform, ChannelType channel, DevelopMode mode, string savePath)
                {
                    if (master == null) return null;
                    if (!master.TryGetEntry(platform, channel, out var entry)) return null;

                    WriteYooAssetSettings(master, platform, channel, mode);

                    string dir = System.IO.Path.GetDirectoryName(savePath);
                    if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    {
                        System.IO.Directory.CreateDirectory(dir);
                    }

                    ConfigRuntimeSO existing = AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(savePath);
                    ConfigRuntimeSO target = existing != null ? existing : ScriptableObject.CreateInstance<ConfigRuntimeSO>();

                    // D6.2：顶层类按当前坐标掩码裁剪取数，经 DimensionalResolver 解析后写入 Runtime SO
                    DimensionalResolver.HybridCLRResult hybridCLR = DimensionalResolver.ResolveHybridCLR(master, platform, channel, mode);

                    target.DevelopMode = mode;
                    target.Namespace = DimensionalResolver.ResolveNamespace(master, platform, channel, mode);
                    target.AppConfigs = CloneAppConfigs(master.GetAppConfigs(platform, channel, mode));
                    target.Platform = platform;
                    target.Channel = channel;
                    target.EnabledSDKConfigs = FilterEnabled(entry, mode, master.EnabledSDKs);
                    target.EnabledKitConfigs = FilterEnabledKits(entry, mode, master.EnabledKits);
                    target.HybridConfigs = new HybridConfigs
                    {
                        GameEntranceProcedureName = hybridCLR.GameEntranceProcedureName,
                        AotMetadataDlls = hybridCLR.AotMetadataDlls.Select(e => new DllAssetEntry(e.AssetLocation)).ToList(),
                        GameDlls = hybridCLR.GameDlls.Select(e => new DllAssetEntry(e.AssetLocation)).ToList(),
                    };
                    target.Custom = CloneCustomConfigData(master.Custom);

                    if (existing == null)
                    {
                        AssetDatabase.CreateAsset(target, savePath);
                    }
                    else
                    {
                        EditorUtility.SetDirty(target);
                    }

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    return target;
                }

                /// <summary>
                /// 按导出坐标解析 ConfigMaster 中的模板，并单向写入指定 YooAssetSettings.asset。
                /// </summary>
                private static void WriteYooAssetSettings(
                    ConfigMasterSO master,
                    PlatformType platform,
                    ChannelType channel,
                    DevelopMode mode)
                {
                    DimensionalResolver.YooAssetResult config =
                        DimensionalResolver.ResolveYooAsset(master, platform, channel, mode);
                    if (string.IsNullOrWhiteSpace(config.YooAssetSettingsPath))
                    {
                        return;
                    }

                    YooAssetSettings settings =
                        AssetDatabase.LoadAssetAtPath<YooAssetSettings>(config.YooAssetSettingsPath);
                    if (settings == null)
                    {
                        throw new System.InvalidOperationException(
                            $"YooAsset 配置导出失败：找不到 YooAssetSettings.asset：{config.YooAssetSettingsPath}");
                    }

                    PlaceholderContext context = EditorUtil.Placeholder.FromConfigMaster(
                        master,
                        platform,
                        channel,
                        EditorUtil.Placeholder.ResolveDefaultPackageName(),
                        Application.version,
                        System.DateTime.Now);
                    string yooFolderName = Util.Placeholder.Resolve(config.YooFolderName, context) ?? string.Empty;
                    string packageFilePrefix = Util.Placeholder.Resolve(config.PackageFilePrefix, context) ?? string.Empty;
                    if (PathUtility.ContainsInvalidFileNameChars(yooFolderName))
                    {
                        throw new System.InvalidOperationException(
                            $"YooAsset 配置导出失败：YooFolderName 包含非法文件名字符：{yooFolderName}");
                    }
                    if (PathUtility.ContainsInvalidFileNameChars(packageFilePrefix))
                    {
                        throw new System.InvalidOperationException(
                            $"YooAsset 配置导出失败：PackageFilePrefix 包含非法文件名字符：{packageFilePrefix}");
                    }

                    settings.YooFolderName = yooFolderName;
                    settings.PackageFilePrefix = packageFilePrefix;
                    EditorUtility.SetDirty(settings);
                    AssetDatabase.SaveAssetIfDirty(settings);
                    YooAssetConfiguration.SetSettings(settings);
                }

                /// <summary>
                /// 深拷贝 AppConfigs，返回与源互不影响的新实例。
                /// <para>src 为 null 时直接返回 null，不抛出异常。</para>
                /// </summary>
                /// <param name="src">待拷贝的源 AppConfigs 实例。</param>
                /// <returns>
                /// 字段值与 src 相同的新 AppConfigs 实例；src 为 null 时返回 null。
                /// </returns>
                private static AppConfigs CloneAppConfigs(AppConfigs src)
                {
                    if (src == null) return null;
                    return new AppConfigs
                    {
                        AppID = src.AppID,
                        AppAesKey = src.AppAesKey,
                        AppAesIV = src.AppAesIV,
                        CustomConfigCmdName = src.CustomConfigCmdName,
                        CustomName = src.CustomName,
                    };
                }

                /// <summary>
                /// 深拷贝本地自定义键值配置，避免 Runtime 导出物与 ConfigMasterSO 共用列表引用。
                /// </summary>
                /// <param name="src">待拷贝的本地配置；为 null 时返回空实例。</param>
                /// <returns>与源键值相同且列表独立的新实例。</returns>
                private static CustomConfigData CloneCustomConfigData(CustomConfigData src)
                {
                    CustomConfigData result = new CustomConfigData();
                    if (src?.Entries == null)
                    {
                        return result;
                    }

                    for (int i = 0; i < src.Entries.Count; i++)
                    {
                        CustomConfigEntry entry = src.Entries[i];
                        if (entry == null)
                        {
                            continue;
                        }

                        result.Entries.Add(new CustomConfigEntry
                        {
                            Key = entry.Key,
                            Value = entry.Value,
                        });
                    }
                    return result;
                }

                /// <summary>
                /// 从 PlatformChannelEntry 指定 DevelopMode 的 SDKConfigs 中筛选出类型全名在 enabledTypeNames 白名单内的项。
                /// <para>空引用的 SDK 配置项会被跳过；enabledTypeNames 为空时返回空列表。</para>
                /// </summary>
                /// <param name="entry">包含全量 SDK 配置列表的平台渠道条目。</param>
                /// <param name="mode">目标开发模式。</param>
                /// <param name="enabledTypeNames">已启用的 SDK 配置类型全名白名单列表。</param>
                /// <returns>
                /// 仅包含白名单内类型的 ISDKPluginConfig 列表；无匹配项时返回空列表。
                /// </returns>
                private static List<ISDKPluginConfig> FilterEnabled(PlatformChannelEntry entry, DevelopMode mode, List<string> enabledTypeNames)
                {
                    List<ISDKPluginConfig> result = new();
                    // enabledTypeNames 为 null 时与空列表语义相同，直接返回（对称 FilterEnabledKits）
                    if (enabledTypeNames == null) return result;
                    List<ISDKPluginConfig> sdkConfigs = entry.GetSDKConfigs(mode);
                    for (int i = 0; i < sdkConfigs.Count; i++)
                    {
                        ISDKPluginConfig cfg = sdkConfigs[i];
                        if (cfg == null) continue;
                        string typeName = cfg.GetType().FullName;
                        if (enabledTypeNames.Contains(typeName))
                        {
                            result.Add(cfg);
                        }
                    }
                    return result;
                }

                /// <summary>
                /// 从 PlatformChannelEntry 指定 DevelopMode 的 KitConfigs 中筛选出类型全名在 enabledTypeNames 白名单内的项。
                /// <para>空引用的 Kit 配置项会被跳过；enabledTypeNames 为空时返回空列表。</para>
                /// </summary>
                /// <param name="entry">包含全量 Kit 配置列表的平台渠道条目。</param>
                /// <param name="mode">目标开发模式。</param>
                /// <param name="enabledTypeNames">已启用的 Kit 配置类型全名白名单列表。</param>
                /// <returns>
                /// 仅包含白名单内类型的 IKitConfig 列表；无匹配项时返回空列表。
                /// </returns>
                private static List<IKitConfig> FilterEnabledKits(PlatformChannelEntry entry, DevelopMode mode, List<string> enabledTypeNames)
                {
                    List<IKitConfig> result = new();
                    if (enabledTypeNames == null) return result;
                    List<IKitConfig> kitConfigs = entry.GetKitConfigs(mode);
                    for (int i = 0; i < kitConfigs.Count; i++)
                    {
                        IKitConfig cfg = kitConfigs[i];
                        if (cfg == null) continue;
                        string typeName = cfg.GetType().FullName;
                        if (enabledTypeNames.Contains(typeName))
                        {
                            result.Add(cfg);
                        }
                    }
                    return result;
                }
            }
        }
    }
}
