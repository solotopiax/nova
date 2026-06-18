/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AssetUtil.cs
 * author:    yingzheng
 * created:   2026/6/12
 * descrip:   Nova Runtime Debugger 运行时代码。
 ***************************************************************/
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace NovaFramework.Runtime
{
    using System.IO;
    using UnityEngine;

    public static class AssetUtil
    {
#if UNITY_EDITOR

        //	This makes it easy to create, name and place unique new ScriptableObject asset files.
        /// <summary>
        /// </summary>
        public static T CreateAsset<T>() where T : ScriptableObject
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (path == "")
            {
                path = "Assets";
            }
            else if (System.IO.Path.GetExtension(path) != "")
            {
                path = path.Replace(System.IO.Path.GetFileName(path), "");
            }

            return CreateAsset<T>(path, "New " + typeof (T).Name);
        }

        public static T CreateAsset<T>(string path, string name) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(path))
            {
                path = "Assets";
            }

            if (!name.EndsWith(".asset"))
            {
                name += ".asset";
            }

            var assetPathAndName = AssetDatabase.GenerateUniqueAssetPath(path + "/" + name);

            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, assetPathAndName);
            AssetDatabase.SaveAssets();

            return asset;
        }

        public static void SelectAssetInProjectView(Object asset)
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
        }

#endif
    }
}
