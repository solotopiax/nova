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
        /// Step 参数：使用当前 Config 的 OSS 配置部署指定目录。
        /// 两个路径仅覆盖本次执行快照，不回写 ConfigMasterSO。
        /// </summary>
        [Serializable]
        public sealed class CdnDeployParams
        {
            /// <summary>
            /// 待上传的项目根相对目录，支持 Platform、Channel、Package、Version 占位符。
            /// </summary>
            [InspectorName("本地目录")]
            public string LocalDirectory;

            /// <summary>
            /// 当前 Config 的 PresetOSSPath 后缀，支持 Platform、Channel、Package、Version 占位符。
            /// </summary>
            [InspectorName("云端目录")]
            [PipifyCdnRemotePath]
            public string RemoteDirectory;
        }

    }
}
