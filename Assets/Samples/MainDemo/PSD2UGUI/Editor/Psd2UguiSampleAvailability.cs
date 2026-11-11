/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Psd2UguiSampleAvailability.cs
 * author:    taoye
 * created:   2026/11/10
 * descrip:   缺少可选插件时收起示例编辑数据，保留普通 uGUI 产物
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NovaFramework.Samples.Psd2Ugui.Editor
{
    /// <summary>
    /// 只管理本示例内引用 PSD2UGUI 脚本的资产，不安装插件、不删组件、不改业务场景。
    /// 资产连同 meta 在原位与 Authoring~ 之间移动，避免缺插件时导入 Missing Script。
    /// </summary>
    [InitializeOnLoad]
    internal static class Psd2UguiSampleAvailability
    {
        private const string c_PackageName = "cn.efunstudio.psd2ugui";
        private const string c_ConverterType = "UGF.EditorTools.Psd2UGUI.Psd2UIFormConverter";
        private const string c_PluginScriptGuid = "a6c1e4c70b6d4cf5b4c7b8f61a236f90";
        private const string c_StorageDirectory = "Authoring~";
        private static bool s_RemovingPackage;

        /// <summary>重载后检查实际类型；包卸载前优先收起资产，避免保留失效脚本引用。</summary>
        static Psd2UguiSampleAvailability()
        {
            EditorApplication.delayCall += RefreshAvailability;
            Events.registeringPackages += OnRegisteringPackages;
        }

        /// <summary>仅处理本插件的真实移除；版本替换不当成卸载，也不触碰其它包。</summary>
        private static void OnRegisteringPackages(PackageRegistrationEventArgs args)
        {
            if (!args.removed.Any(p => p.name == c_PackageName)) return;
            s_RemovingPackage = true;
            RefreshAvailability();
        }

        /// <summary>按当前加载类型同步状态；编辑器忙碌时延后，不在 Play 中迁移资产。</summary>
        private static void RefreshAvailability()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.delayCall += RefreshAvailability;
                return;
            }

            bool available = !s_RemovingPackage && AppDomain.CurrentDomain.GetAssemblies()
                .Any(assembly => assembly.GetType(c_ConverterType, false) != null);
            foreach (string guid in AssetDatabase.FindAssets("Psd2UguiSampleAvailability t:MonoScript"))
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(scriptPath) != "Psd2UguiSampleAvailability.cs") continue;
                string root = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath)).Replace('\\', '/');
                try { Synchronize(root, available); }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PSD2UGUI 示例] 未移动或覆盖编辑数据：{ex.Message} 此检查不能取消 Unity 的包卸载；卸载前请先保存并关闭编辑树。");
                }
            }
        }

        /// <summary>
        /// 根据插件是否可用移动本示例的编辑资产；所有目标先检查冲突，任何文件都不覆盖。
        /// 普通图片、PSD、生成的 uGUI 和其它目录不参与；返回实际移动的资产数。
        /// </summary>
        internal static int Synchronize(string root, bool available)
        {
            string storage = root + "/" + c_StorageDirectory;
            string sourceRoot = available ? storage : root;
            if (!Directory.Exists(sourceRoot)) return 0;

            var moves = new List<KeyValuePair<string, string>>();
            foreach (string file in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                string source = file.Replace('\\', '/');
                if (!available && source.StartsWith(storage + "/", StringComparison.Ordinal)) continue;
                if (!IsPluginAsset(source)) continue;
                string relative = source.Substring(sourceRoot.Length + 1);
                string target = (available ? root : storage) + "/" + relative;
                if (!File.Exists(source + ".meta"))
                    throw new IOException($"缺少资源标识，停止移动：{source}.meta");
                if (File.Exists(target) || File.Exists(target + ".meta"))
                    throw new IOException($"目标已有同名文件，请先处理两份编辑数据：{target}");
                moves.Add(new KeyValuePair<string, string>(source, target));
            }
            if (moves.Count == 0) return 0;
            if (!available) EnsureSaved(moves.Select(pair => pair.Key).ToArray());

            var completed = new List<KeyValuePair<string, string>>();
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                // 忽略目录不属于 AssetDatabase，使用文件移动保留原始序列化字节和 GUID。
                foreach (var pair in moves)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(pair.Value));
                    MoveFile(pair.Key, pair.Value, completed);
                    MoveFile(pair.Key + ".meta", pair.Value + ".meta", completed);
                }
            }
            catch
            {
                // 不删除源文件；异常时按相反次序还原本次已经完成的移动。
                for (int i = completed.Count - 1; i >= 0; i--)
                {
                    var pair = completed[i];
                    File.Move(pair.Value, pair.Key);
                }
                throw;
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                AssetDatabase.Refresh();
            }
            return moves.Count;
        }

        /// <summary>只识别含已知插件脚本 GUID 的文本资产；不读取或移动普通二进制素材。</summary>
        private static bool IsPluginAsset(string path)
        {
            string extension = Path.GetExtension(path);
            return (extension == ".asset" || extension == ".prefab")
                && File.ReadAllText(path).Contains("guid: " + c_PluginScriptGuid);
        }

        /// <summary>拒绝迁移未保存编辑；本检查不能取消包卸载，只关闭将收起且已保存的 Prefab Stage。</summary>
        private static void EnsureSaved(string[] paths)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && paths.Contains(stage.assetPath))
            {
                if (stage.scene.isDirty)
                    throw new InvalidOperationException("请先保存当前 PSD2UGUI 编辑树，再关闭编辑界面或卸载插件。");
                StageUtility.GoToMainStage();
            }
            foreach (UnityEngine.Object asset in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                if (asset != null && EditorUtility.IsDirty(asset) && paths.Contains(AssetDatabase.GetAssetPath(asset)))
                    throw new InvalidOperationException($"请先保存编辑数据：{AssetDatabase.GetAssetPath(asset)}");
            }
        }

        /// <summary>移动一个文件并立即登记，以便后续文件失败时回退；禁止覆盖。</summary>
        private static void MoveFile(string source, string target, List<KeyValuePair<string, string>> completed)
        {
            File.Move(source, target);
            completed.Add(new KeyValuePair<string, string>(source, target));
        }
    }
}
