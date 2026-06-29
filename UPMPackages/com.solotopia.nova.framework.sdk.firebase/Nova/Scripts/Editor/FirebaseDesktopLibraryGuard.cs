/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebaseDesktopLibraryGuard.cs
 * author:    nova
 * created:   2026/6/18
 * descrip:   检测 Firebase 桌面(Editor beta)核心原生库是否缺失，缺失时在 Console 与弹窗引导从官方 SDK 导入
 ***************************************************************/

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.SDK.FirebasePlugin.Editor
{
    /// <summary>
    /// Firebase 桌面核心原生库缺失检测与导入引导（LFS 兜底）。
    /// <para>桌面核心库（FirebaseCppApp）现已由 Git LFS 承载、随开源仓分发，正常 clone 会自动 smudge 还原。</para>
    /// <para>仅当用户未安装 Git LFS 客户端导致拿到指针文件、或手动删除了该库时，本类作为兜底在 Console 与弹窗给出补齐引导。</para>
    /// <para>Firebase 官方将桌面支持定位为「仅开发期 beta、不用于发布」，真机构建（Android / iOS）不依赖该库、不受影响。</para>
    /// </summary>
    [InitializeOnLoad]
    internal static class FirebaseDesktopLibraryGuard
    {
        /// <summary>会话级去重标记，避免每次域重载重复弹窗。</summary>
        private const string c_SessionKey = "Nova.Firebase.DesktopLibChecked";

        /// <summary>Firebase 官方 Unity SDK 下载地址。</summary>
        private const string c_DownloadUrl = "https://firebase.google.com/download/unity";

        /// <summary>本脚本相对包根的子路径，用于反推包根目录。</summary>
        private const string c_SelfRelPath = "/Nova/Scripts/Editor/FirebaseDesktopLibraryGuard.cs";

        static FirebaseDesktopLibraryGuard()
        {
            // 延迟到首帧执行，避免 import / 域重载期间 AssetDatabase 尚未就绪
            EditorApplication.delayCall += Check;
        }

        private static void Check()
        {
            if (SessionState.GetBool(c_SessionKey, false))
            {
                return;
            }

            SessionState.SetBool(c_SessionKey, true);

            string packageRoot = ResolvePackageRoot();
            if (string.IsNullOrEmpty(packageRoot))
            {
                return;
            }

            string libDir = packageRoot + "/Firebase/Plugins/x86_64";

#if UNITY_EDITOR_OSX
            const string c_LibName = "FirebaseCppApp-12_10_1.bundle";
            bool present = Directory.Exists(libDir + "/" + c_LibName) || File.Exists(libDir + "/" + c_LibName);
#elif UNITY_EDITOR_WIN
            const string c_LibName = "FirebaseCppApp-12_10_1.dll";
            bool present = File.Exists(libDir + "/" + c_LibName);
#else
            const string c_LibName = "FirebaseCppApp-12_10_1.so";
            bool present = File.Exists(libDir + "/" + c_LibName);
#endif

            if (present)
            {
                return;
            }

            string message =
                "检测到 Firebase 桌面（Editor）核心原生库缺失：\n" +
                "  " + c_LibName + "\n\n" +
                "该库现由 Git LFS 承载、随开源仓分发，正常 clone 会自动还原。本提示通常意味着：\n" +
                "  · 你未安装 Git LFS 客户端，clone 时只拿到了指针文件而非真实内容；\n" +
                "  · 或该库被手动删除。\n\n" +
                "Firebase 官方将该桌面库定位为 beta（仅供 Editor 开发期模拟，不用于真机发布），" +
                "真机构建（Android / iOS）不依赖该库，不受影响。\n\n" +
                "补齐方式（任选其一）：\n" +
                "  1. 安装 Git LFS 后在仓库根目录执行 git lfs pull，拉取真实内容；\n" +
                "  2. 从 Firebase 官方 Unity SDK 补齐：" + c_DownloadUrl + "\n" +
                "     解压后 Assets > Import Package > Custom Package 导入对应 .unitypackage；\n" +
                "     或手动将 SDK 内 Firebase/Plugins/x86_64/ 下的 FirebaseCppApp 桌面库拷回：\n" +
                "     " + libDir;

            Debug.LogWarning("[Nova][Firebase] " + message);
            EditorUtility.DisplayDialog(
                "Firebase 桌面库缺失 / Firebase desktop library missing",
                message,
                "知道了 / OK");
        }

        /// <summary>通过本脚本的 MonoScript 资源路径反推包根目录（兼容 Packages/ 与 UPMPackages/ 两种位置）。</summary>
        private static string ResolvePackageRoot()
        {
            string[] guids = AssetDatabase.FindAssets("FirebaseDesktopLibraryGuard t:MonoScript");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                int index = assetPath.IndexOf(c_SelfRelPath, StringComparison.Ordinal);
                if (index > 0)
                {
                    return assetPath.Substring(0, index);
                }
            }

            return null;
        }
    }
}
