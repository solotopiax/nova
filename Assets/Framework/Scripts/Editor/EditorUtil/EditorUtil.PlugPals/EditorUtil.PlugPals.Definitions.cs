/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.PlugPals.Definitions.cs
 * author:    taoye
 * created:   2026/4/21
 * descrip:   PlugPals 工具 —— 嵌套类型定义
 ***************************************************************/

using System.Collections.Generic;
using Newtonsoft.Json;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class PlugPals
        {
            /// <summary>
            /// 包分类枚举，用于分类标签过滤（基于包名前缀匹配）。
            /// </summary>
            public enum PackageCategory
            {
                /// <summary>
                /// 全部。
                /// </summary>
                All,

                /// <summary>
                /// SDK（包名以 com.solotopia.nova.framework.sdk 开头）。
                /// </summary>
                SDK,

                /// <summary>
                /// 框架核心（包名以 com.solotopia.nova.framework 开头，且不含 .sdk. 和 .kit.）。
                /// </summary>
                Framework,

                /// <summary>
                /// 业务核心（包名以 com.solotopia.nova.framework.kit 开头）。
                /// </summary>
                Kit,

                /// <summary>
                /// 其他（前面都未匹配）。
                /// </summary>
                Other,
            }

            /// <summary>
            /// 包安装状态枚举。
            /// </summary>
            public enum PackageStatus
            {
                /// <summary>
                /// 未安装。
                /// </summary>
                NotInstalled,

                /// <summary>
                /// 已安装且版本一致。
                /// </summary>
                Installed,

                /// <summary>
                /// 已安装但有更新版本可用。
                /// </summary>
                Upgradeable,

                /// <summary>
                /// 已安装但来源为非仓库引用（git/file:），仅可清除卸载。
                /// </summary>
                NonRegistry,
            }

            /// <summary>
            /// Verdaccio API 返回的包信息（字段小写以匹配 JSON 键名）。
            /// </summary>
            public class VerdaccioPackageInfo
            {
                /// <summary>
                /// 包名（如 com.solotopia.nova.framework）。
                /// </summary>
                [JsonProperty("name")]
                public string Name;

                /// <summary>
                /// 最新版本号。
                /// </summary>
                [JsonProperty("version")]
                public string Version;

                /// <summary>
                /// 包显示名称。
                /// </summary>
                [JsonProperty("displayName")]
                public string DisplayName;

                /// <summary>
                /// 包描述。
                /// </summary>
                [JsonProperty("description")]
                public string Description;

                /// <summary>
                /// 包关键字列表，用于判定分类（对应 package.json 的 keywords 字段）。
                /// </summary>
                [JsonProperty("keywords")]
                public string[] Keywords;

                /// <summary>
                /// 内核版本号（第三方库包体本身的版本号）。
                /// </summary>
                [JsonProperty("coreVersion")]
                public string CoreVersion;

                /// <summary>
                /// 包依赖字典（包名 -> 版本号），来自 package.json dependencies。
                /// </summary>
                [JsonProperty("dependencies")]
                public Dictionary<string, string> Dependencies;

                /// <summary>
                /// Nova 消费侧扩展元数据。
                /// </summary>
                [JsonProperty("nova")]
                public NovaPackageMetadata Nova;
            }

            /// <summary>
            /// 显示用包条目（PascalCase，公开使用）。
            /// </summary>
            public class PackageDisplayEntry
            {
                /// <summary>
                /// 包名（如 com.solotopia.nova.framework）。
                /// </summary>
                public string Name;

                /// <summary>
                /// 显示名称。
                /// </summary>
                public string DisplayName;

                /// <summary>
                /// 包描述。
                /// </summary>
                public string Description;

                /// <summary>
                /// 本地已安装版本（null 表示未安装）。
                /// </summary>
                public string LocalVersion;

                /// <summary>
                /// 远程最新版本。
                /// </summary>
                public string LatestVersion;

                /// <summary>
                /// 安装状态。
                /// </summary>
                public PackageStatus Status;

                /// <summary>
                /// 包分类（根据包名前缀计算）。
                /// </summary>
                public PackageCategory Category;

                /// <summary>
                /// 内核版本号（第三方库包体本身的版本号，可为 null）。
                /// </summary>
                public string CoreVersion;

                /// <summary>
                /// 包依赖字典（包名 -> 版本号），用于安装前缺库提示。
                /// </summary>
                public Dictionary<string, string> Dependencies;

                /// <summary>
                /// Nova 消费侧扩展元数据，用于缺库引导与宏同步。
                /// </summary>
                public NovaPackageMetadata Nova;
            }

            /// <summary>
            /// package.json 的消费侧最小模型。
            /// </summary>
            public class PackageJsonData
            {
                /// <summary>
                /// 包名。
                /// </summary>
                public string name;

                /// <summary>
                /// 显示名称。
                /// </summary>
                public string displayName;

                /// <summary>
                /// 版本号。
                /// </summary>
                public string version;

                /// <summary>
                /// 依赖字典（包名 -> 版本号）。
                /// </summary>
                public Dictionary<string, string> dependencies;

                /// <summary>
                /// Nova 扩展元数据。
                /// </summary>
                public NovaPackageMetadata nova;
            }

            /// <summary>
            /// Nova package.json 扩展元数据。
            /// </summary>
            public class NovaPackageMetadata
            {
                /// <summary>
                /// 必须的三方库引导信息（依赖包名 -> 引导信息）。
                /// </summary>
                public Dictionary<string, RequiredLibraryGuide> requiredLibraries;

                /// <summary>
                /// 包自带声明的作用域仓库（如 MAX 依赖的 AppLovin 官方私有云仓库）。
                /// 安装/升级时按 url upsert 到 manifest，卸载时按 url 移除整条；
                /// 被这些 scopes 前缀覆盖的依赖在依赖检测时直接放行。
                /// </summary>
                public List<ScopedRegistry> scopedRegistries;
            }

            /// <summary>
            /// 必须三方库的显示与购买信息。
            /// </summary>
            public class RequiredLibraryGuide
            {
                /// <summary>
                /// 展示名称。
                /// </summary>
                public string displayName;

                /// <summary>
                /// 购买或下载地址。
                /// </summary>
                public string purchaseUrl;
            }

            /// <summary>
            /// 缺失的必须三方库信息。
            /// </summary>
            public class MissingRequiredLibraryInfo
            {
                /// <summary>
                /// 缺失包名。
                /// </summary>
                public string PackageName;

                /// <summary>
                /// 依赖声明版本。
                /// </summary>
                public string RequiredVersion;

                /// <summary>
                /// 展示名称。
                /// </summary>
                public string DisplayName;

                /// <summary>
                /// 购买或下载地址。
                /// </summary>
                public string PurchaseUrl;

                /// <summary>
                /// 依赖它的包名。
                /// </summary>
                public string DependentPackageName;

                /// <summary>
                /// 依赖它的包展示名。
                /// </summary>
                public string DependentPackageDisplayName;
            }

            /// <summary>
            /// 依赖命中的 registry 来源（用于自动配 scope）。
            /// </summary>
            public class RegistrySource
            {
                /// <summary>
                /// 仓库 URL。
                /// </summary>
                public string Url;

                /// <summary>
                /// 仓库名称。
                /// </summary>
                public string Name;
            }

            /// <summary>
            /// 安装前依赖检测结果。
            /// </summary>
            public class DependencyCheckResult
            {
                /// <summary>
                /// 缺失库（本地无 + 非 com.unity + registry 未命中）。
                /// </summary>
                public List<MissingRequiredLibraryInfo> Missing = new List<MissingRequiredLibraryInfo>();

                /// <summary>
                /// 命中 registry、需随主包自动配 scope 安装的依赖（依赖名 -> 命中来源）。
                /// </summary>
                public Dictionary<string, RegistrySource> ToAutoScope = new Dictionary<string, RegistrySource>();
            }

            /// <summary>
            /// manifest.json 序列化结构。
            /// </summary>
            public class ManifestData
            {
                /// <summary>
                /// 作用域仓库列表（为 null 时序列化自动忽略）。
                /// </summary>
                [JsonProperty(Order = 0)]
                public List<ScopedRegistry> scopedRegistries;

                /// <summary>
                /// 依赖字典（包名 -> 版本号或路径）。
                /// </summary>
                [JsonProperty(Order = 1)]
                public Dictionary<string, string> dependencies;
            }

            /// <summary>
            /// 作用域仓库配置。
            /// </summary>
            public class ScopedRegistry
            {
                /// <summary>
                /// 仓库名称。
                /// </summary>
                public string name;

                /// <summary>
                /// 仓库 URL。
                /// </summary>
                public string url;

                /// <summary>
                /// 包作用域列表。
                /// </summary>
                public List<string> scopes;
            }

            /// <summary>
            /// package.json 中的版本信息（仅用于读取 file: 引用包的本地版本）。
            /// </summary>
            public class LocalPackageJson
            {
                /// <summary>
                /// 版本号。
                /// </summary>
                public string version;
            }

            /// <summary>
            /// packages-lock.json 序列化结构。
            /// </summary>
            public class PackagesLockData
            {
                /// <summary>
                /// 所有已解析包条目（包名 -> 条目信息）。
                /// </summary>
                public Dictionary<string, PackagesLockEntry> dependencies;
            }

            /// <summary>
            /// packages-lock.json 中单个包条目。
            /// </summary>
            public class PackagesLockEntry
            {
                /// <summary>
                /// 版本号或路径引用（如 "1.0.0" 或 "file:../UPMPackages/xxx"）。
                /// </summary>
                public string version;

                /// <summary>
                /// 包来源类型（registry/local/git/builtin/embedded）。
                /// </summary>
                public string source;

                /// <summary>
                /// 依赖层级（0 为直接依赖，>0 为传递依赖）。
                /// </summary>
                public int depth;
            }
        }
    }
}
