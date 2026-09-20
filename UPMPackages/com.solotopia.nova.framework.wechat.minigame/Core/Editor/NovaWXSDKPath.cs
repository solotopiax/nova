/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NovaWXSDKPath.cs
 * author:    taoye
 * created:   2026/9/18
 * descrip:   微信小游戏 SDK 本地副本路径解析
 ***************************************************************/

using System.IO;
using UnityEditor.PackageManager;

namespace WeChatWASM
{
    /// <summary>
    /// 统一解析 Nova 包内微信小游戏 SDK 的工程相对路径与绝对路径。
    /// </summary>
    internal static class NovaWXSDKPath
    {
        internal const string PackageName = "com.solotopia.nova.framework.wechat.minigame";

        /// <summary>
        /// 获取 SDK 在 Unity 工程中的资源根路径。
        /// </summary>
        internal static string AssetRoot
        {
            get
            {
                PackageInfo packageInfo = PackageInfo.FindForAssembly(typeof(NovaWXSDKPath).Assembly);
                string packageRoot = packageInfo == null ? $"Packages/{PackageName}" : packageInfo.assetPath;
                return $"{packageRoot}/Core";
            }
        }

        /// <summary>
        /// 将 SDK 根路径与相对路径片段组合为 Unity 资源路径。
        /// </summary>
        /// <param name="relativeSegments">相对于 SDK 根目录的路径片段。</param>
        /// <returns>使用正斜杠的 Unity 工程相对路径。</returns>
        internal static string GetAssetPath(params string[] relativeSegments)
        {
            string path = AssetRoot;
            for (int i = 0; i < relativeSegments.Length; i++)
            {
                path = Path.Combine(path, relativeSegments[i]);
            }

            return path.Replace('\\', '/');
        }

        /// <summary>
        /// 将 SDK 根路径与相对路径片段组合为文件系统绝对路径。
        /// </summary>
        /// <param name="relativeSegments">相对于 SDK 根目录的路径片段。</param>
        /// <returns>文件系统绝对路径。</returns>
        internal static string GetAbsolutePath(params string[] relativeSegments)
        {
            return Path.GetFullPath(GetAssetPath(relativeSegments));
        }
    }
}
