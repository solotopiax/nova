using System;
using UnityEngine;
using UnityEditor;

// modify: by taoye - 新增 LoadSettingDataAtPath 重载与 ExplicitPathProvider 注入点；
//         BundleCollectorSetting 等 Editor-only Settings 优先按 Nova ConfigMaster 显式路径加载，
//         替代 AssetDatabase.FindAssets 全工程扫描，根除多 sample 共存命中错副本问题。

namespace YooAsset.Editor
{
    /// <summary>
    /// 编辑器配置文件加载器
    /// </summary>
    public static class SettingLoader
    {
        // modify: by taoye - 外部（Nova ConfigMaster）注册显式路径回调；命中即旁路全工程扫描。
        private static Func<Type, string> s_explicitPathProvider;

        /// <summary>
        /// 注册显式路径回调；同一时刻仅保留一个 provider，重复注册覆盖前值。
        /// <para>provider 收到 settingType，需返回 Assets 相对路径或 null/empty 表示该类型无显式配置。</para>
        /// </summary>
        /// <param name="provider">类型 → 资产路径的回调；传 null 则注销。</param>
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

            // modify: by taoye - 优先走外部注入的显式路径，命中则跳过全工程扫描，避免多副本异常。
            if (s_explicitPathProvider != null)
            {
                string explicitPath = s_explicitPathProvider(settingType);
                var explicitSetting = LoadSettingDataAtPath<TSetting>(explicitPath);
                if (explicitSetting != null) return explicitSetting;
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
        /// 按显式资产路径加载 Settings；空路径返回 null，资产不存在返回 null。
        /// </summary>
        /// <typeparam name="TSetting">配置文件类型，必须继承自 ScriptableObject。</typeparam>
        /// <param name="assetPath">Assets 相对路径（如 Assets/Samples/MainDemo/Editor/BundleCollectorSetting.asset）。</param>
        /// <returns>加载到的配置实例；路径为空或资产不存在时返回 null。</returns>
        public static TSetting LoadSettingDataAtPath<TSetting>(string assetPath)
            where TSetting : ScriptableObject
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            return AssetDatabase.LoadAssetAtPath<TSetting>(assetPath);
        }
    }
}
