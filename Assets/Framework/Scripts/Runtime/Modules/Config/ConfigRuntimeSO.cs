/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ConfigRuntimeSO.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   运行时配置导出物 SO
 ***************************************************************/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 导出物 SO；ConfigWindow 每次导出覆盖，运行时由 Config 模块加载后解析。
    /// </summary>
    public sealed class ConfigRuntimeSO : ScriptableObject
    {
        /// <summary>
        /// 本次导出的开发模式；与 Platform / Channel 共同构成配置三维索引。
        /// </summary>
        public DevelopMode DevelopMode;

        /// <summary>
        /// 全局业务命名空间；直接从 ConfigMasterSO.Namespace 导出，不随 DevelopMode 变化。
        /// </summary>
        public string Namespace;

        /// <summary>
        /// 全局公共配置；字段已单值化，由导出侧按 DevelopMode 选取后写入。
        /// </summary>
        public CommonConfig Common;

        /// <summary>
        /// 本次导出目标平台。
        /// </summary>
        public PlatformType Platform;

        /// <summary>
        /// 本次导出目标渠道。
        /// </summary>
        public ChannelType Channel;

        /// <summary>
        /// 本次导出启用的 SDK Plugin 配置列表；未启用的类型不写入。
        /// 业务层通过 GetSDKPluginConfig 泛型方法取值，直接读写列表应仅限导出侧。
        /// </summary>
        [SerializeReference]
        public List<ISDKPluginConfig> EnabledSDKConfigs = new();

        /// <summary>
        /// 本次导出启用的 Kit 配置列表；未启用的类型不写入。
        /// 业务层通过 GetKitConfig 泛型方法取值，直接读写列表应仅限导出侧。
        /// </summary>
        [SerializeReference]
        public List<IKitConfig> EnabledKitConfigs = new();

        /// <summary>
        /// 业务入口 Procedure 相对类型名（不含 namespace），如 ProcedurePreload；
        /// 由 ProcedureLoadDll 在 DLL 加载后用于注册业务 Procedure 入口。
        /// </summary>
        public string GameEntranceProcedureName;

        /// <summary>
        /// AOT 元数据 DLL 列表；描述每个 AOT DLL 在 AB 包中的寻址信息，
        /// 由 ProcedureLoadDll 按序加载以支持泛型共享。
        /// </summary>
        public List<DllAssetEntry> AotMetadataDlls = new();

        /// <summary>
        /// 业务 DLL 列表；描述每个业务 DLL 在 AB 包中的寻址信息，
        /// 由 ProcedureLoadDll 按序加载后注册业务程序集。
        /// </summary>
        public List<DllAssetEntry> GameDlls = new();

        /// <summary>
        /// 按泛型类型从 EnabledSDKConfigs 中取对应 SDK Plugin 配置实例。
        /// </summary>
        /// <typeparam name="T">
        /// 目标配置类型，须实现 ISDKPluginConfig。
        /// </typeparam>
        /// <returns>
        /// 找到时返回对应实例；未启用或类型不匹配时返回 null。
        /// </returns>
        public T GetSDKPluginConfig<T>() where T : class, ISDKPluginConfig
        {
            if (EnabledSDKConfigs == null)
            {
                return null;
            }
            for (int i = 0; i < EnabledSDKConfigs.Count; i++)
            {
                if (EnabledSDKConfigs[i] is T match)
                {
                    return match;
                }
            }
            return null;
        }

        /// <summary>
        /// 按 Type 从 EnabledSDKConfigs 中取对应 SDK Plugin 配置实例（非泛型版，供反射调用）。
        /// </summary>
        /// <param name="type">
        /// 目标配置类型对象；传入 null 时直接返回 null。
        /// </param>
        /// <returns>
        /// 找到时返回对应实例；type 为 null、未启用或类型不匹配时返回 null。
        /// </returns>
        public ISDKPluginConfig GetSDKPluginConfig(Type type)
        {
            if (type == null || EnabledSDKConfigs == null)
            {
                return null;
            }
            for (int i = 0; i < EnabledSDKConfigs.Count; i++)
            {
                if (EnabledSDKConfigs[i] != null && EnabledSDKConfigs[i].GetType() == type)
                {
                    return EnabledSDKConfigs[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 按泛型类型从 EnabledKitConfigs 中取对应 Kit 配置实例。
        /// </summary>
        /// <typeparam name="T">
        /// 目标配置类型，须实现 IKitConfig。
        /// </typeparam>
        /// <returns>
        /// 找到时返回对应实例；未启用或类型不匹配时返回 null。
        /// </returns>
        public T GetKitConfig<T>() where T : class, IKitConfig
        {
            if (EnabledKitConfigs == null)
            {
                return null;
            }
            for (int i = 0; i < EnabledKitConfigs.Count; i++)
            {
                if (EnabledKitConfigs[i] is T match)
                {
                    return match;
                }
            }
            return null;
        }

        /// <summary>
        /// 按 Type 从 EnabledKitConfigs 中取对应 Kit 配置实例（非泛型版，供反射调用）。
        /// </summary>
        /// <param name="type">
        /// 目标配置类型对象；传入 null 时直接返回 null。
        /// </param>
        /// <returns>
        /// 找到时返回对应实例；type 为 null、未启用或类型不匹配时返回 null。
        /// </returns>
        public IKitConfig GetKitConfig(Type type)
        {
            if (type == null || EnabledKitConfigs == null)
            {
                return null;
            }
            for (int i = 0; i < EnabledKitConfigs.Count; i++)
            {
                if (EnabledKitConfigs[i] != null && EnabledKitConfigs[i].GetType() == type)
                {
                    return EnabledKitConfigs[i];
                }
            }
            return null;
        }
    }
}
