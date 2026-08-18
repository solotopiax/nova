using System;
using UnityEngine;
using UnityEditor;

// modify: local fork - 支持 Nova 注册显式配置路径并在未命中时回退到上游全工程扫描。

namespace YooAsset.Editor
{
    /// <summary>
    /// 编辑器配置文件加载器
    /// </summary>
    public static class SettingLoader
    {
        private static Func<Type, string> s_explicitPathProvider;

        /// <summary>
        /// 注册配置资产显式路径提供器；重复注册会覆盖旧值，传入 null 会注销提供器。
        /// </summary>
        /// <param name="provider">按配置类型返回 Assets 相对路径的回调；无显式配置时返回 null 或空字符串。</param>
        public static void RegisterExplicitPathProvider(Func<Type, string> provider)
        {
            s_explicitPathProvider = provider;
        }

        /// <summary>
        /// 加载指定类型的配置文件，如果不存在则自动创建
        /// </summary>
        /// <typeparam name="TSetting">配置文件类型，必须继承自 ScriptableObject</typeparam>
        /// <returns>加载或新创建的配置文件实例</returns>
        public static TSetting LoadSettingData<TSetting>() where TSetting : ScriptableObject
        {
            var settingType = typeof(TSetting);

            // Nova 的 ConfigMaster 可以为多份同类型配置指定当前激活路径；路径未配置或资产不存在时保持上游扫描语义。
            if (s_explicitPathProvider != null)
            {
                string explicitPath = s_explicitPathProvider(settingType);
                TSetting explicitSetting = LoadSettingDataAtPath<TSetting>(explicitPath);
                if (explicitSetting != null)
                    return explicitSetting;
            }

            var guids = AssetDatabase.FindAssets($"t:{settingType.Name}");
            if (guids.Length == 0)
            {
                Debug.LogWarning($"Creating new '{settingType.Name}.asset' file.");
                var setting = ScriptableObject.CreateInstance<TSetting>();
                string filePath = $"Assets/{settingType.Name}.asset";
                AssetDatabase.CreateAsset(setting, filePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return setting;
            }
            else
            {
                if (guids.Length != 1)
                {
                    foreach (var guid in guids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guid);
                        Debug.LogWarning($"Found multiple files: '{path}'.");
                    }
                    throw new InvalidOperationException($"Found multiple {settingType.Name} files.");
                }

                string filePath = AssetDatabase.GUIDToAssetPath(guids[0]);
                var setting = AssetDatabase.LoadAssetAtPath<TSetting>(filePath);
                if (setting == null)
                    throw new InvalidOperationException($"Failed to load {settingType.Name} at path: '{filePath}'.");
                return setting;
            }
        }

        /// <summary>
        /// 按显式 Assets 相对路径加载指定类型的配置资产。
        /// </summary>
        /// <typeparam name="TSetting">必须继承 ScriptableObject 的配置类型。</typeparam>
        /// <param name="assetPath">Assets 相对路径；为空或资产不存在时不命中。</param>
        /// <returns>已加载的配置实例；路径为空或资产不存在时返回 null。</returns>
        public static TSetting LoadSettingDataAtPath<TSetting>(string assetPath)
            where TSetting : ScriptableObject
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;
            return AssetDatabase.LoadAssetAtPath<TSetting>(assetPath);
        }
    }
}
