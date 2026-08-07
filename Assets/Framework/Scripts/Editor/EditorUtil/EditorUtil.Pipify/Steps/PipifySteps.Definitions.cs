/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Definitions.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 内置 Step 合集 —— 嵌套参数类集中定义（>1 个参数类统一此处）
 ***************************************************************/

using System;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件集中定义所有 Step 参数类（嵌套类型），避免参数类分散在各 Step 分组文件中。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step 参数：一键打包所需配置。
        /// 字段全部 public，供 Util.Json 序列化 / 反序列化及 CLI 参数覆盖使用。
        /// </summary>
        [Serializable]
        public sealed class PackageParams
        {
            /// <summary>
            /// 目标构建平台（Android / iOS / StandaloneWindows64 等）。
            /// </summary>
            public BuildTarget Target;

            /// <summary>
            /// 打包方式，对应 Build Profiles 中 Build 按钮的三种触发形态：Build / Clean Build / Force skip data build。
            /// </summary>
            public EditorUtil.Build.BuildMode BuildMode;

            /// <summary>
            /// 是否开发构建（DevelopmentBuild），与 BuildMode 正交可叠加。
            /// </summary>
            public bool DevelopmentBuild;

            /// <summary>
            /// 是否拆分应用 Binary（Split Application Binary）。
            /// Android 专用；对应 Android Player Settings → Split Application Binary。
            /// 实际 API：PlayerSettings.Android.splitApplicationBinary。
            /// </summary>
            [PipifyVisibleWhen(nameof(Target), (int)BuildTarget.Android)]
            public bool SplitApplicationBinary;

            /// <summary>
            /// 是否构建 AAB（Android App Bundle）而非 APK。
            /// 仅在 Target == Android 且非导出 Google Android 工程时生效；
            /// 导出工程模式（EditorUserBuildSettings.exportAsGoogleAndroidProject）下此选项无效。
            /// </summary>
            [PipifyVisibleWhen(nameof(Target), (int)BuildTarget.Android)]
            public bool BuildAppBundle;

            /// <summary>
            /// 是否在本次打包期间临时应用 Android keystore 签名配置。
            /// 为 false 时，Pipify 不修改当前 PlayerSettings 中的 Android 签名配置。
            /// </summary>
            [PipifyVisibleWhen(nameof(Target), (int)BuildTarget.Android)]
            public bool UseAndroidKeystore;

            /// <summary>
            /// Android keystore 路径。Unity 支持项目相对路径或绝对路径。
            /// </summary>
            [PipifyVisibleWhen(nameof(UseAndroidKeystore), 1)]
            public string AndroidKeystorePath;

            /// <summary>
            /// Android keystore 密码。
            /// </summary>
            [PipifyVisibleWhen(nameof(UseAndroidKeystore), 1)]
            [PipifyPassword]
            public string AndroidKeystorePass;

            /// <summary>
            /// Android keystore 内的 key alias 名称。
            /// </summary>
            [PipifyVisibleWhen(nameof(UseAndroidKeystore), 1)]
            public string AndroidKeyalias;

            /// <summary>
            /// Android key alias 密码。
            /// </summary>
            [PipifyVisibleWhen(nameof(UseAndroidKeystore), 1)]
            [PipifyPassword]
            public string AndroidKeyaliasPass;

            /// <summary>
            /// 导出文件夹路径（遵循项目根相对路径规范；产物文件名按固定格式自动生成）。
            /// 格式：{productName字母数字}_{Debug|Release}_{bundleVersion}_{yyyy_MM_dd_HH_mm}[.apk|.aab]。
            /// 绝对路径直接使用；相对路径基于项目根解析；文件夹不存在时自动创建。
            /// </summary>
            public string OutputFolderPath;
        }

        /// <summary>
        /// Step 参数：指定本次 ConfigRuntime 导出的三维配置坐标。
        /// 字段顺序同时决定 PipifyWindow 中三个枚举下拉框的绘制顺序。
        /// </summary>
        [Serializable]
        public sealed class ConfigExportParams
        {
            /// <summary>
            /// 本次导出的目标平台。
            /// </summary>
            public PlatformType Platform;

            /// <summary>
            /// 本次导出的目标渠道。
            /// </summary>
            public ChannelType Channel;

            /// <summary>
            /// 本次导出的开发模式。
            /// </summary>
            public DevelopMode DevelopMode;
        }

        /// <summary>
        /// 从指定 ConfigMaster 当前坐标创建 Config 导出参数快照，不修改源资产。
        /// </summary>
        /// <param name="master">参数默认值来源；不可为空。</param>
        /// <returns>与当前三维坐标一致的独立参数实例。</returns>
        internal static ConfigExportParams CreateConfigExportParams(ConfigMasterSO master)
        {
            if (master == null) throw new ArgumentNullException(nameof(master));
            return new ConfigExportParams
            {
                Platform = master.CurrentPlatform,
                Channel = master.CurrentChannel,
                DevelopMode = master.CurrentDevelopMode,
            };
        }

        /// <summary>
        /// Step 参数：打开文件夹所需配置。
        /// 字段全部 public，供 Util.Json 序列化 / 反序列化及 CLI 参数覆盖使用。
        /// </summary>
        [Serializable]
        public sealed class OpenFolderParams
        {
            /// <summary>
            /// 目标路径，遵循项目根相对路径规范；空字符串或不存在时回退到项目根目录。
            /// 绝对路径直接打开；文件路径会自动取其所在目录。
            /// </summary>
            public string Path;
        }

        /// <summary>
        /// Step 参数：飞书自定义机器人 Webhook 与待发送文本。
        /// 字段值随 PipifySettingsSO 保存，公开发布副本由统一脱敏器替换为占位符。
        /// </summary>
        [Serializable]
        [PipifyHelpBox(
            "文案支持标准占位符，发送前按当前 ConfigMaster 配置替换：",
            "{Platform}=当前平台；{Channel}=当前渠道；{Package}=YooAsset 默认资源包名",
            "{Version}=Application.version；{Time}=发送时间（yyyy-MM-dd-HH-mm-ss）",
            "示例：构建完成 {Platform}-{Channel}-{Version} {Time}")]
        public sealed class FeishuWebhookParams
        {
            /// <summary>
            /// 飞书自定义机器人 Webhook URL；窗口中遮罩显示，存档中仍为明文。
            /// </summary>
            [InspectorName("Webhook URL")]
            [PipifyPassword]
            public string WebhookUrl;

            /// <summary>
            /// 发送给飞书机器人的自定义文本内容。
            /// </summary>
            [InspectorName("文案")]
            [TextArea(3, 8)]
            public string MessageText;
        }

        /// <summary>
        /// Step 参数：使用当前 Config 的 OSS 配置承载版本检查文件位置，并部署指定热更资源目录。
        /// 四个路径仅覆盖本次执行快照，不回写 ConfigMasterSO。
        /// </summary>
        [Serializable]
        public sealed class CdnDeployParams
        {
            /// <summary>
            /// 大版本更新规则配置文件的项目根相对位置。
            /// </summary>
            [InspectorName("版本检查-本地文件位置")]
            public string VersionCheckLocalFilePath;

            /// <summary>
            /// 大版本更新规则配置文件在当前 Config PresetOSSPath 后的远端位置。
            /// </summary>
            [InspectorName("版本检查-云端文件位置")]
            [PipifyCdnRemotePath]
            public string VersionCheckRemoteFilePath;

            /// <summary>
            /// 是否从本地目录锚点自动关联最后生成的完整 YooAsset 版本目录。
            /// </summary>
            [InspectorName("自动关联最新版本")]
            [PipifyHelpBox(
                "默认开启；执行时会从下方目录锚点关联最新完整的 YooAsset 版本目录。",
                "版本识别规则与 Config 一致，文件名前缀取自当前 ConfigMaster 当前维度的 YooAsset 配置。")]
            public bool AutoLinkLatestVersion = true;

            /// <summary>
            /// 待上传目录或自动关联锚点的项目根相对路径，支持 Platform、Channel、Package、Version 占位符。
            /// </summary>
            [InspectorName("热更资源-本地目录位置")]
            public string LocalDirectory;

            /// <summary>
            /// 当前 Config 的 PresetOSSPath 后缀，支持 Platform、Channel、Package、Version 占位符。
            /// </summary>
            [InspectorName("热更资源-云端目录位置")]
            [PipifyCdnRemotePath]
            public string RemoteDirectory;

            /// <summary>
            /// 上传前是否清理本次版本检查文件与热更资源远端目录。
            /// </summary>
            [InspectorName("清理云端文件和目录")]
            [PipifyHelpBox(
                "默认关闭；勾选后会在上传前清理本次部署目标。",
                "只清理本次部署涉及的文件和目录，不会清空整个 PresetOSSPath。",
                "清理失败时立即停止，不继续上传。")]
            public bool CleanRemoteFilesAndDirectories;
        }

        /// <summary>
        /// Step 参数：启动资源校验白名单设备 ID、三个 YooAsset 版本文件及各自远端目录。
        /// 所有字段仅覆盖本次执行快照，不回写 ConfigMasterSO。
        /// </summary>
        [Serializable]
        [PipifyHelpBox(
            "设备 ID 每行填写一项；执行时会去除空项和首尾空白，并生成 VersionsCheckWhiteList.json 字符串数组。",
            "配置文件使用完整云端文件位置，三个版本文件使用云端目录；配置文件位置为空或非法时不上传配置文件。",
            "本地文件位置和云端目录位置支持 {Platform}/{Channel}/{Package}/{Version} 占位符。")]
        public sealed class CdnWhitelistDeployParams
        {
            /// <summary>
            /// 白名单稳定设备 ID，多行文本中每行一项。
            /// </summary>
            [InspectorName("配置文件-设备ID（每行一个设备ID）")]
            [TextArea(3, 8)]
            public string DeviceIDs;

            /// <summary>
            /// 当前 Config 的 PresetOSSPath 后缀及文件名，仅用于 VersionsCheckWhiteList.json。
            /// </summary>
            [InspectorName("配置文件-云端文件位置")]
            [PipifyCdnRemotePath]
            public string WhitelistRemoteFilePath;

            /// <summary>
            /// 是否以 .bytes 路径为锚点自动关联最新完整版本的三个 YooAsset 元数据文件。
            /// </summary>
            [InspectorName("自动关联最新版本")]
            [PipifyHelpBox(
                "默认开启；执行时会从下方 .bytes 路径锚点关联最新完整版本的 .bytes/.hash/.version。",
                "文件命名取自当前 ConfigMaster 当前维度的 YooAsset 配置，三个文件始终来自同一版本。")]
            public bool AutoLinkLatestVersion = true;

            /// <summary>
            /// YooAsset Manifest 二进制版本文件或自动关联锚点的项目根相对位置。
            /// </summary>
            [InspectorName("版本文件(.bytes)-本地文件位置")]
            public string ManifestBytesLocalFilePath;

            /// <summary>
            /// YooAsset Manifest 哈希版本文件的项目根相对位置。
            /// </summary>
            [InspectorName("版本文件(.hash)-本地文件位置")]
            public string ManifestHashLocalFilePath;

            /// <summary>
            /// YooAsset 包版本文件的项目根相对位置。
            /// </summary>
            [InspectorName("版本文件(.version)-本地文件位置")]
            public string PackageVersionLocalFilePath;

            /// <summary>
            /// 当前 Config 的 PresetOSSPath 后缀，三个 YooAsset 版本文件上传到该目录。
            /// </summary>
            [InspectorName("版本文件-云端目录位置")]
            [PipifyCdnRemotePath]
            public string RemoteDirectory;

            /// <summary>
            /// 上传前是否清理本次白名单文件与版本文件远端目录。
            /// </summary>
            [InspectorName("清理云端文件和目录")]
            [PipifyHelpBox(
                "默认关闭；勾选后会在上传前清理本次部署目标。",
                "只清理本次部署涉及的文件和目录，不会清空整个 PresetOSSPath。",
                "清理失败时立即停止，不继续上传。")]
            public bool CleanRemoteFilesAndDirectories;
        }

        /// <summary>
        /// Step 参数：Cloudflare Zone、访问令牌与待清理缓存 URL。
        /// 三个字段仅覆盖本次执行快照，不回写 ConfigMasterSO。
        /// </summary>
        [Serializable]
        public sealed class CdnPurgeParams
        {
            /// <summary>
            /// Cloudflare Zone ID。
            /// </summary>
            [InspectorName("Zone ID")]
            public string ZoneID;

            /// <summary>
            /// Cloudflare API Token；窗口中遮罩显示，存档中仍为明文。
            /// </summary>
            [InspectorName("API Token")]
            [PipifyPassword]
            public string Token;

            /// <summary>
            /// 英文逗号、分号或换行分隔的待清理缓存 URL。
            /// </summary>
            [InspectorName("缓存路径")]
            [TextArea(3, 8)]
            public string CachePaths;
        }

    }
}
