/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LegacyNetworkPackageMigration.Initializer.cs
 * author:    taoye
 * created:   2026/9/2
 * descrip:   已下架网络包迁移自动入口
 ***************************************************************/

using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace NovaFramework.Editor.Migrations
{
    internal static class LegacyNetworkPackageMigrationInitializer
    {
        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            EditorApplication.delayCall -= TryRun;
            EditorApplication.delayCall += TryRun;
        }

        private static void TryRun()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                EditorApplication.delayCall += TryRun;
                return;
            }

            try
            {
                if (!LegacyNetworkPackageMigration.Run(out string summary))
                {
                    return;
                }

                Client.Resolve();
                Debug.Log("[Nova 网络迁移] 已从 manifest 移除下架的 BestHTTP 包并触发 UPM Resolve：" + summary);
            }
            catch (Exception exception)
            {
                Debug.LogError("[Nova 网络迁移] 自动清理未完成，下次加载将继续重试。\n" + exception.Message);
            }
        }
    }
}
