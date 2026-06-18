/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.SDKPluginScanner.cs
 * author:    taoye
 * created:   2026/4/29
 * descrip:   反射扫描并管理 ISDKPluginConfig 实现类型的工具
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// 反射扫描所有实现 ISDKPluginConfig 的可实例化 [Serializable] 类型，
            /// 并提供对 PlatformChannelEntry 指定 DevelopMode 的 SDKConfigs 列表的实例补全与移除操作。
            /// </summary>
            public static class SDKPluginScanner
            {
                /// <summary>
                /// 扫描出的 SDK Plugin Config 条目，包含类型与左树展示名称。
                /// </summary>
                public readonly struct PluginConfigEntry
                {
                    /// <summary>
                    /// Plugin Config 类型。
                    /// </summary>
                    public readonly Type ConfigType;

                    /// <summary>
                    /// 左树展示的中文名称，从实例 DisplayName 读取；空字符串时回退为类型名。
                    /// </summary>
                    public readonly string DisplayName;

                    /// <summary>
                    /// 构造 PluginConfigEntry 实例。
                    /// </summary>
                    /// <param name="configType">Plugin Config 类型。</param>
                    /// <param name="displayName">左树展示名称。</param>
                    public PluginConfigEntry(Type configType, string displayName)
                    {
                        ConfigType = configType;
                        DisplayName = displayName;
                    }
                }

                /// <summary>
                /// 扫描全部已加载程序集中符合条件的 Plugin Config 类型，并读取实例 DisplayName。
                /// <para>条件：非抽象、非接口、实现 ISDKPluginConfig、标注 [Serializable]、有可用无参构造器。</para>
                /// </summary>
                /// <returns>
                /// 满足条件的 PluginConfigEntry 列表；若无匹配则返回空列表。
                /// </returns>
                public static List<PluginConfigEntry> ScanAll()
                {
                    List<PluginConfigEntry> result = new();
                    IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(asm => !IsTestAssembly(asm))   // 排除测试程序集（桩类型如 TestSdkConfig 不入生产扫描）
                        .SelectMany(SafeGetTypes)
                        .Where(t => !t.IsAbstract
                                 && !t.IsInterface
                                 && typeof(ISDKPluginConfig).IsAssignableFrom(t)
                                 && t.GetCustomAttribute<SerializableAttribute>() != null);
                    foreach (Type type in types)
                    {
                        string displayName = type.Name;
                        try
                        {
                            ISDKPluginConfig sample = (ISDKPluginConfig)Activator.CreateInstance(type);
                            if (!string.IsNullOrEmpty(sample.DisplayName)) displayName = sample.DisplayName;
                        }
                        catch (Exception)
                        {
                            // 无无参构造器或构造异常时，退回类型名展示，不中断扫描。
                        }
                        result.Add(new PluginConfigEntry(type, displayName));
                    }
                    return result;
                }

                /// <summary>
                /// 若指定 DevelopMode 下的 SDKConfigs 列表中缺少指定类型的实例，则通过反射补一个空实例并追加至末尾。
                /// <para>已存在同类型实例时直接返回 false，不重复添加。</para>
                /// </summary>
                /// <param name="entry">目标 PlatformChannelEntry，为 null 时直接返回 false。</param>
                /// <param name="mode">目标开发模式。</param>
                /// <param name="configType">要确保存在的 Config 类型，为 null 时直接返回 false。</param>
                /// <returns>true 表示本次新建并添加了实例；false 表示实例已存在或参数无效。</returns>
                public static bool EnsureInstance(PlatformChannelEntry entry, DevelopMode mode, Type configType)
                {
                    if (entry == null || configType == null) return false;
                    List<ISDKPluginConfig> list = entry.GetSDKConfigs(mode);
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (list[i] != null && list[i].GetType() == configType)
                        {
                            return false;
                        }
                    }
                    list.Add((ISDKPluginConfig)Activator.CreateInstance(configType));
                    return true;
                }

                /// <summary>
                /// 从指定 DevelopMode 下的 SDKConfigs 列表中移除所有指定类型的实例。
                /// <para>entry 或 configType 为 null 时提前返回，不抛异常。</para>
                /// </summary>
                /// <param name="entry">目标 PlatformChannelEntry，为 null 时直接返回。</param>
                /// <param name="mode">目标开发模式。</param>
                /// <param name="configType">要移除的 Config 类型，为 null 时直接返回。</param>
                public static void RemoveInstance(PlatformChannelEntry entry, DevelopMode mode, Type configType)
                {
                    if (entry == null || configType == null) return;
                    entry.GetSDKConfigs(mode).RemoveAll(c => c != null && c.GetType() == configType);
                }

                /// <summary>
                /// 安全获取程序集中所有类型的包装方法。
                /// <para>Assembly.GetTypes() 在含有加载失败类型的程序集上会抛出
                /// ReflectionTypeLoadException；此时回退至 e.Types 中非 null 的部分，
                /// 保证扫描过程不被单个损坏的程序集中断。</para>
                /// </summary>
                /// <param name="asm">要枚举类型的目标程序集。</param>
                /// <returns>能成功加载的 Type 枚举；出现加载异常时返回部分结果而非空集合。</returns>
                private static IEnumerable<Type> SafeGetTypes(Assembly asm)
                {
                    try { return asm.GetTypes(); }
                    catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null); }
                }

                /// <summary>
                /// 判断程序集是否为测试程序集（引用 nunit.framework 即视为测试程序集），
                /// 用于把测试桩类型（如 TestSdkConfig）排除出生产扫描。
                /// </summary>
                /// <param name="asm">待判定程序集。</param>
                /// <returns>是测试程序集返回 true。</returns>
                private static bool IsTestAssembly(Assembly asm)
                {
                    foreach (AssemblyName refName in asm.GetReferencedAssemblies())
                    {
                        if (refName.Name == "nunit.framework") return true;
                    }
                    return false;
                }
            }
        }
    }
}
