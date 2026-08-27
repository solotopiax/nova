/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.YooAssetInjector.cs
 * author:    taoye
 * created:   2026/5/28
 * descrip:   Asset 模块编辑期注入层；按 ConfigMaster 显式路径字段注入 YooAssetSettings，
 *            替代 Resources.Load 全工程扫描，根除多 sample 共存时命中错副本的问题。
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using YooAsset;
using YooAsset.Editor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// Asset 模块编辑期注入层。
            /// <para>按 ConfigMaster 中显式声明的路径字段注入 YooAssetSettings 与 BundleCollectorSetting，</para>
            /// <para>替代 Resources.Load / AssetDatabase.FindAssets 全工程扫描，根除多 sample 共存时命中错副本问题。</para>
            /// </summary>
            public static class YooAssetInjector
            {
                /// <summary>
                /// 按 ConfigMaster 当前三维坐标解析 YooAssetSettingsPath 并注入 YooAssetConfiguration。
                /// <para>切换前先清除上一工作区的静态配置；master 为 null、路径为空或资产失效时保持未注入状态。</para>
                /// </summary>
                /// <param name="master">提供路径字段的 ConfigMasterSO 实例。</param>
                public static void Inject(ConfigMasterSO master)
                {
                    // 切换激活 ConfigMaster 时同步作废 YooAsset 内部静态缓存，
                    // 让 BundleCollectorWindow 等下次访问 Setting 时重新走显式路径 provider，
                    // 避免多 sample 共存时仍命中旧 sample 的 BundleCollectorSetting 引用。
                    BundleCollectorSettingData.ResetCache();
                    YooAssetConfiguration.SetSettings(null);
                    if (master == null) return;

                    DimensionalResolver.YooAssetResult resolved = DimensionalResolver.ResolveYooAsset(
                        master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
                    string path = resolved.YooAssetSettingsPath;
                    if (string.IsNullOrEmpty(path)) return;
                    YooAssetSettings settings = AssetDatabase.LoadAssetAtPath<YooAssetSettings>(path);
                    if (settings == null)
                    {
                        Log.Warning(LogTag.Editor, "[YooAssetInjector] 找不到 YooAssetSettings，路径：{0}", path);
                        return;
                    }
                    YooAssetConfiguration.SetSettings(settings);
                    Log.Debug(LogTag.Editor, "[YooAssetInjector] 已注入 YooAssetSettings：{0}", path);
                }

                /// <summary>
                /// 按显式路径字符串直接注入 YooAssetSettings，无需传入 ConfigMasterSO。
                /// <para>供维度化坐标切换场景使用：DimensionalResolver 已解析出当前坐标对应路径，直接按路径注入即可。</para>
                /// <para>path 为空时静默返回；路径资产不存在时打印 Warning 后返回。</para>
                /// </summary>
                /// <param name="path">YooAssetSettings.asset 项目根相对路径（以 Assets/ 开头）。</param>
                public static void InjectByPath(string path)
                {
                    BundleCollectorSettingData.ResetCache();
                    YooAssetConfiguration.SetSettings(null);
                    if (string.IsNullOrEmpty(path)) return;
                    YooAssetSettings settings = AssetDatabase.LoadAssetAtPath<YooAssetSettings>(path);
                    if (settings == null)
                    {
                        Log.Warning(LogTag.Editor, "[YooAssetInjector] 找不到 YooAssetSettings，路径：{0}", path);
                        return;
                    }
                    YooAssetConfiguration.SetSettings(settings);
                    Log.Debug(LogTag.Editor, "[YooAssetInjector] 已注入 YooAssetSettings（按路径）：{0}", path);
                }

                /// <summary>
                /// 按 ConfigMaster 当前三维坐标解析 BundleCollectorSettingPath 并加载 BundleCollectorSetting。
                /// <para>master 为 null 或路径为空时返回 null；路径资产不存在时返回 null。</para>
                /// </summary>
                /// <param name="master">提供路径字段的 ConfigMasterSO 实例。</param>
                /// <returns>加载到的 BundleCollectorSetting 实例；未配置或不存在时返回 null。</returns>
                public static BundleCollectorSetting LoadBundleCollector(ConfigMasterSO master)
                {
                    if (master == null) return null;
                    DimensionalResolver.YooAssetResult resolved = DimensionalResolver.ResolveYooAsset(
                        master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode);
                    string path = resolved.BundleCollectorSettingPath;
                    if (string.IsNullOrEmpty(path)) return null;
                    return SettingLoader.LoadSettingDataAtPath<BundleCollectorSetting>(path);
                }

                /// <summary>
                /// 域重载时向 YooAsset 的 SettingLoader 注册显式路径回调，
                /// 让 BundleCollectorWindow 等 YooAsset 内部窗口直接走当前激活 ConfigMaster 的显式路径加载，
                /// 替代 AssetDatabase.FindAssets 全工程扫描。
                /// <para>未激活 ConfigMaster 或对应路径字段为空时回调返回 null，YooAsset 自动回退到全工程扫描兜底。</para>
                /// </summary>
                [InitializeOnLoadMethod]
                private static void RegisterYooAssetExplicitPathProvider()
                {
                    SettingLoader.RegisterExplicitPathProvider(type =>
                    {
                        ConfigMasterSO master = WorkspaceActive.Get();
                        if (master == null) return null;
                        if (type == typeof(BundleCollectorSetting))
                        {
                            return DimensionalResolver.ResolveYooAsset(
                                master, master.CurrentPlatform, master.CurrentChannel, master.CurrentDevelopMode)
                                .BundleCollectorSettingPath;
                        }
                        return null;
                    });
                }

                /// <summary>
                /// 域重载时延迟按当前激活 ConfigMaster 注入一次 YooAssetSettings。
                /// Sample 场景切换的重新注入由 EditorUtil.SceneRoute 统一编排，避免多个监听器重复注入。
                /// <para>解决场景：Editor 启动后从未打开 ConfigWindow 即触发构建/查询 YooAssetSettings 的流程，
                /// 避免 YooAssetConfiguration.s_settings 仍为 null → 走 Resources.Load 全工程兜底导致命中错副本。</para>
                /// </summary>
                [InitializeOnLoadMethod]
                private static void InjectActiveSettingsOnEditorLoad()
                {
                    EditorApplication.delayCall += () =>
                    {
                        if (!WorkspaceActive.ReconcileScene(EditorSceneManager.GetActiveScene().path))
                        {
                            Inject(null);
                            return;
                        }
                        Inject(WorkspaceActive.Get());
                    };
                }
            }
        }
    }
}
