/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.CheckUpdate.Definitions.cs
 * author:    taoye
 * created:   2026/4/28
 * descrip:   CheckUpdate 工具 —— 嵌套类型定义
 ***************************************************************/

using System.Collections.Generic;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class CheckUpdate
        {
            /// <summary>
            /// 单个包的版本更新信息（immutable，构造器赋值）。
            /// </summary>
            public sealed class UpdateInfo
            {
                /// <summary>
                /// UPM 包名（如 com.solotopia.nova.framework）。
                /// </summary>
                public string PackageName { get; }

                /// <summary>
                /// 当前已安装版本号。
                /// </summary>
                public string CurrentVersion { get; }

                /// <summary>
                /// 远端最新版本号。
                /// </summary>
                public string LatestVersion { get; }

                /// <summary>
                /// 构造包版本更新信息。
                /// </summary>
                /// <param name="packageName">包名。</param>
                /// <param name="currentVersion">本地已安装版本。</param>
                /// <param name="latestVersion">远端最新版本。</param>
                public UpdateInfo(string packageName, string currentVersion, string latestVersion)
                {
                    PackageName = packageName;
                    CurrentVersion = currentVersion;
                    LatestVersion = latestVersion;
                }
            }

            /// <summary>
            /// 跳过提示的配置数据（key=包名，value=被跳过时的 latestVersion）。
            /// </summary>
            private sealed class SkipConfig
            {
                /// <summary>
                /// 跳过记录（包名 -> 被跳过的 latestVersion）。
                /// </summary>
                public Dictionary<string, string> SkipVersions = new Dictionary<string, string>();
            }
        }
    }
}
