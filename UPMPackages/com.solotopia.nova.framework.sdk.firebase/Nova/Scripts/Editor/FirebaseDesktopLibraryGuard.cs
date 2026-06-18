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
    /// Firebase 桌面核心原生库缺失检测与导入引导。
    /// <para>开源仓为控制体积、且 Firebase 官方将桌面支持定位为「仅开发期 beta、不用于发布」，</para>
    /// <para>未随包分发 x86_64 桌面核心库（FirebaseCppApp）。真机构建（Android / iOS）不依赖该库、不受影响；</para>
    /// <para>仅在 Editor 中调试 Firebase 时需要，缺失时本类在 Console 与弹窗给出导入引导。</para>
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
                "该库是 Firebase 官方的 beta 桌面库（仅供 Editor 开发期模拟使用，官方明确不用于真机发布），" +
                "因单文件体积超过 GitHub 100MB 限制、且属「仅开发用」性质，未随开源仓分发。\n" +
                "真机构建（Android / iOS）不依赖该库，不受影响。\n\n" +
                "若需在 Editor 中调试 Firebase 功能，请从 Firebase 官方 Unity SDK 补齐：\n" +
                "  1. 下载：" + c_DownloadUrl + "\n" +
                "  2. 解压后，在 Unity 中 Assets > Import Package > Custom Package 导入对应 .unitypackage；\n" +
                "  3. 或手动将 SDK 内 Firebase/Plugins/x86_64/ 下的 FirebaseCppApp 桌面库拷回本包同名目录：\n" +
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
