/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  FirebaseConfigFileGuard.cs
 * author:    yingzheng
 * created:   2026/6/17
 * descrip:   FirebaseDemo 配置文件守卫。
 ***************************************************************/

using UnityEditor;
using UnityEngine;

namespace NovaFramework.Sdk.Firebase.Samples.Editor
{
    internal static class FirebaseConfigFileGuard
    {
        private const string c_AndroidConfigFileName = "google-services.json";
        private const string c_IosConfigFileName = "GoogleService-Info.plist";
        private const string c_FirebaseConsoleUrl = "https://console.firebase.google.com/?hl=zh-cn";

        public static void CheckActiveBuildTarget()
        {
            if (!TryGetMissingConfigFile(
                    EditorUserBuildSettings.activeBuildTarget,
                    out string missingFileName))
            {
                return;
            }

            FirebaseConfigMissingWindow.Open(EditorUserBuildSettings.activeBuildTarget, missingFileName);
        }

        private static bool TryGetMissingConfigFile(
            BuildTarget activeBuildTarget,
            out string missingFileName)
        {
            return TryGetMissingConfigFile(activeBuildTarget, ContainsAssetFile, out missingFileName);
        }

        private static bool TryGetMissingConfigFile(
            BuildTarget activeBuildTarget,
            System.Func<string, bool> containsFile,
            out string missingFileName)
        {
            missingFileName = null;

            switch (activeBuildTarget)
            {
                case BuildTarget.Android:
                    missingFileName = c_AndroidConfigFileName;
                    break;
                case BuildTarget.iOS:
                    missingFileName = c_IosConfigFileName;
                    break;
                default:
                    return false;
            }

            if (containsFile(missingFileName))
            {
                missingFileName = null;
                return false;
            }

            return true;
        }

        private static bool ContainsAssetFile(string fileName)
        {
            string nameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(fileName);
            string[] guids = AssetDatabase.FindAssets(nameWithoutExtension, new[] { "Assets" });

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileName(assetPath) == fileName)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class FirebaseConfigMissingWindow : EditorWindow
        {
            private BuildTarget m_BuildTarget;
            private string m_MissingFileName;

            public static void Open(BuildTarget buildTarget, string missingFileName)
            {
                FirebaseConfigMissingWindow window = CreateInstance<FirebaseConfigMissingWindow>();
                window.titleContent = new GUIContent("Firebase 配置缺失");
                window.m_BuildTarget = buildTarget;
                window.m_MissingFileName = missingFileName;
                window.minSize = new Vector2(520f, 210f);
                window.maxSize = new Vector2(520f, 210f);
                window.ShowUtility();
                window.Focus();
            }

            private void OnGUI()
            {
                const float padding = 16f;

                EditorGUILayout.BeginVertical();
                GUILayout.Space(padding);

                EditorGUILayout.LabelField("Firebase 配置缺失", EditorStyles.boldLabel);
                GUILayout.Space(8f);
                EditorGUILayout.LabelField(
                    $"当前平台为 {m_BuildTarget}，Assets 目录下未找到 {m_MissingFileName}。",
                    EditorStyles.wordWrappedLabel);
                GUILayout.Space(6f);
                EditorGUILayout.LabelField(
                    "请打开 Firebase 控制台下载当前应用对应的配置文件，并放入 Assets 目录后再继续使用 FirebaseDemo。",
                    EditorStyles.wordWrappedLabel);
                GUILayout.Space(6f);
              
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("前往 Firebase 控制台", GUILayout.Width(160f), GUILayout.Height(26f)))
                {
                    Application.OpenURL(c_FirebaseConsoleUrl);
                    Close();
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("知道了", GUILayout.Width(96f), GUILayout.Height(26f)))
                {
                    Close();
                }
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(padding);
                EditorGUILayout.EndVertical();
            }
        }
    }
}
