/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.WorkspaceActive.cs
 * author:    taoye
 * created:   2026/5/28
 * descrip:   工程级激活 ConfigMaster 锚点；通过 ProjectSettings/Nova/Globals.json 持久化 GUID，
 *            提供四段回退加载策略，根除多 sample 共存时 FindAssets 玄学命中问题。
 ***************************************************************/

using System;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// 工程级激活 ConfigMaster 锚点。
            /// <para>通过 ProjectSettings/Nova/Globals.json 持久化当前激活 ConfigMaster 的 GUID，</para>
            /// <para>提供四段回退加载策略，根除多 sample 共存时 FindAssets 玄学命中问题。</para>
            /// </summary>
            public static class WorkspaceActive
            {
                /// <summary>
                /// Globals.json 相对工程根目录的路径。
                /// </summary>
                private const string c_GlobalsRelPath = "ProjectSettings/Nova/Globals.json";

                /// <summary>
                /// Globals.json 的序列化模型。
                /// <para>字段名即 JSON key，刻意保持 camelCase 以便 Globals.json 文件人工读写时形如 {"configMasterGuid": "..."}，不遵循公有字段首字母大写约定。</para>
                /// </summary>
                [Serializable]
                private sealed class GlobalsJson
                {
                    /// <summary>
                    /// 激活 ConfigMaster 的 AssetDatabase GUID，唯一权威 key。
                    /// </summary>
                    public string configMasterGuid;

                    /// <summary>
                    /// 激活 ConfigMaster 的 Assets 相对路径，仅供肉眼排查；加载逻辑不消费。
                    /// </summary>
                    public string configMasterPathHint;
                }

                /// <summary>
                /// 获取当前激活的 ConfigMasterSO，按多段回退策略返回，失败返回 null。
                /// <para>① 当前活跃 scene 在 Assets/Samples/X/ 下，且与 Globals.json 缓存的 ConfigMaster 不在同一 sample 根 → 优先按 scene 重新推断（覆盖 Globals.json，避免多 sample 切换串味）</para>
                /// <para>② Globals.json 存在 + GUID 加载成功 → 返回（pathHint 变化时自动回写）</para>
                /// <para>③ Globals.json 存在 + GUID 加载失败（资产被删）→ 返回 null</para>
                /// <para>④ Globals.json 不存在 + 当前活跃 scene 在 Assets/Samples/X/ 下 → 推断写入并返回</para>
                /// <para>⑤ Globals.json 不存在 + 不在 sample scene → 返回 null</para>
                /// </summary>
                /// <returns>当前激活的 ConfigMasterSO 实例；无法解析时返回 null。</returns>
                public static ConfigMasterSO Get()
                {
                    string projectRoot = GetProjectRoot();
                    string globalsPath = System.IO.Path.Combine(projectRoot, c_GlobalsRelPath);

                    if (!File.Exists(globalsPath))
                        return TryInferFromOpenedSampleScene();

                    string json = File.ReadAllText(globalsPath);
                    GlobalsJson globals = null;
                    try
                    {
                        globals = JsonUtility.FromJson<GlobalsJson>(json);
                    }
                    catch (Exception e)
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] Globals.json 解析失败，跳过加载：{0}", e.Message);
                        return null;
                    }

                    if (globals == null || string.IsNullOrEmpty(globals.configMasterGuid))
                        return TryInferFromOpenedSampleScene();

                    string assetPath = AssetDatabase.GUIDToAssetPath(globals.configMasterGuid);
                    if (string.IsNullOrEmpty(assetPath))
                        return TryInferFromOpenedSampleScene();

                    // 多 sample 共存时的强制重推断分支：当前 scene 在 Assets/Samples/X/ 下，
                    // 且 X != 缓存 ConfigMaster 所在 sample 根，按 scene 重新推断并覆盖 Globals.json
                    ConfigMasterSO sceneInferred = TryInferIfSampleSceneMismatched(assetPath);
                    if (sceneInferred != null)
                        return sceneInferred;

                    ConfigMasterSO master = AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(assetPath);
                    if (master == null)
                        return null;

                    // pathHint 已过时则回写
                    if (globals.configMasterPathHint != assetPath)
                    {
                        globals.configMasterPathHint = assetPath;
                        WriteGlobals(globalsPath, globals);
                    }

                    return master;
                }

                /// <summary>
                /// 当当前活跃 scene 在 Assets/Samples/&lt;sampleRoot&gt;/ 下，且 sampleRoot
                /// 与缓存的 ConfigMaster 所在 sampleRoot 不一致时，按 scene 重新推断 ConfigMaster
                /// 并覆盖 Globals.json，避免多 sample 共存时切换 scene 串味。
                /// 不满足条件返回 null（调用方继续走默认 GUID 加载分支）。
                /// </summary>
                /// <param name="cachedAssetPath">缓存的 ConfigMaster.asset 路径。</param>
                /// <returns>重推断成功返回新 ConfigMasterSO；不需要重推断返回 null。</returns>
                private static ConfigMasterSO TryInferIfSampleSceneMismatched(string cachedAssetPath)
                {
                    var scene = EditorSceneManager.GetActiveScene();
                    if (string.IsNullOrEmpty(scene.path)) return null;
                    string scenePath = scene.path.Replace('\\', '/');
                    if (!scenePath.StartsWith("Assets/Samples/")) return null;

                    string sceneSampleRoot = ExtractSampleRoot(scenePath);
                    string cachedSampleRoot = ExtractSampleRoot(cachedAssetPath.Replace('\\', '/'));
                    if (string.IsNullOrEmpty(sceneSampleRoot)) return null;
                    if (sceneSampleRoot == cachedSampleRoot) return null;

                    return TryInferFromOpenedSampleScene();
                }

                /// <summary>
                /// 从形如 Assets/Samples/&lt;Pkg&gt;/&lt;Ver&gt;/&lt;Sample&gt;/... 或
                /// Assets/Samples/&lt;Demo&gt;/... 的路径里取出 Assets/Samples/ 之后的第一段作为 sample 根标识，
                /// 用于判断"两条路径是否属于同一 sample"。
                /// </summary>
                /// <param name="path">资产或 scene 的 Assets 相对路径（前向斜杠）。</param>
                /// <returns>sample 根的第一段；非 Assets/Samples/ 路径返回空字符串。</returns>
                private static string ExtractSampleRoot(string path)
                {
                    if (string.IsNullOrEmpty(path)) return string.Empty;
                    const string c_Prefix = "Assets/Samples/";
                    if (!path.StartsWith(c_Prefix)) return string.Empty;
                    int slashIdx = path.IndexOf('/', c_Prefix.Length);
                    return slashIdx < 0 ? path.Substring(c_Prefix.Length) : path.Substring(c_Prefix.Length, slashIdx - c_Prefix.Length);
                }

                /// <summary>
                /// 获取当前激活 ConfigMasterSO 所配对的 ConfigRuntimeSO。
                /// <para>通过 WorkspaceActive.Get() 锚定激活 master，再按 ADR-033 布局约定</para>
                /// <para>（master 在 DemoRoot/Editor/ConfigMaster.asset，runtime 在 DemoRoot/Configs/ConfigRuntime.asset）</para>
                /// <para>从 masterPath 上溯两级得到 DemoRoot，拼出 runtime 路径并加载返回。</para>
                /// <para>无激活 master 时 Warning 并返回 null（成因①）；</para>
                /// <para>路径上溯异常（master 不在预期 Samples 布局下）时 Warning 并返回 null（成因②）；</para>
                /// <para>配对 ConfigRuntime.asset 不存在（未导出）时 Warning 并返回 null（成因③）。</para>
                /// </summary>
                /// <returns>激活 master 配对的 ConfigRuntimeSO；任一失败条件时返回 null。</returns>
                public static ConfigRuntimeSO GetActiveRuntime()
                {
                    ConfigMasterSO master = Get();
                    if (master == null)
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 无激活 ConfigMaster，无法定位 ConfigRuntime。");
                        return null;
                    }

                    string masterPath = AssetDatabase.GetAssetPath(master);
                    if (string.IsNullOrEmpty(masterPath))
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 激活 ConfigMaster 的 AssetDatabase 路径为空，无法定位 ConfigRuntime。");
                        return null;
                    }

                    // master 在 <DemoRoot>/Editor/ConfigMaster.asset
                    // 上溯两级：去掉文件名 → <DemoRoot>/Editor；再去掉目录名 → <DemoRoot>
                    string editorDir = System.IO.Path.GetDirectoryName(masterPath)?.Replace('\\', '/');
                    string demoRoot = System.IO.Path.GetDirectoryName(editorDir)?.Replace('\\', '/');

                    if (string.IsNullOrEmpty(demoRoot))
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 激活 ConfigMaster 路径层级不足，无法上溯至 DemoRoot：{0}", masterPath);
                        return null;
                    }

                    string runtimePath = $"{demoRoot}/Configs/ConfigRuntime.asset";
                    ConfigRuntimeSO runtime = AssetDatabase.LoadAssetAtPath<ConfigRuntimeSO>(runtimePath);
                    if (runtime == null)
                    {
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 激活 master 的 ConfigRuntime 未导出：{0}", runtimePath);
                        return null;
                    }

                    return runtime;
                }

                /// <summary>
                /// 显式设置激活 ConfigMasterSO，以 GUID + pathHint 原子写入 Globals.json。
                /// <para>master 为 null 时静默返回。</para>
                /// </summary>
                /// <param name="master">要设置为激活的 ConfigMasterSO 实例。</param>
                public static void Set(ConfigMasterSO master)
                {
                    if (master == null) return;

                    string assetPath = AssetDatabase.GetAssetPath(master);
                    if (string.IsNullOrEmpty(assetPath)) return;

                    string guid = AssetDatabase.AssetPathToGUID(assetPath);
                    if (string.IsNullOrEmpty(guid)) return;

                    GlobalsJson globals = new GlobalsJson
                    {
                        configMasterGuid = guid,
                        configMasterPathHint = assetPath,
                    };

                    string projectRoot = GetProjectRoot();
                    string globalsPath = System.IO.Path.Combine(projectRoot, c_GlobalsRelPath);
                    WriteGlobals(globalsPath, globals);
                    Log.Debug(LogTag.Editor, "[WorkspaceActive] 已设置激活 ConfigMaster：{0}（{1}）", assetPath, guid);
                }

                /// <summary>
                /// 推断当前活跃 sample scene 所属的 ConfigMaster.asset；推断成功则同时写入 Globals.json。
                /// <para>从 scene 所在目录起逐级向上扫，每层尝试 `Editor/ConfigMaster.asset`，第一个命中即返回；</para>
                /// <para>到 `Assets/Samples/` 边界（含）即停。同一逻辑同时覆盖：</para>
                /// <para>① 开发态扁平结构 `Assets/Samples/{Demo}/{Scene}.unity`</para>
                /// <para>② UPM 导入态嵌套结构 `Assets/Samples/{PackageDisplayName}/{Version}/{SampleDisplayName}/{Scene}.unity`</para>
                /// </summary>
                /// <returns>推断命中的 ConfigMasterSO；未命中返回 null。</returns>
                private static ConfigMasterSO TryInferFromOpenedSampleScene()
                {
                    var scene = EditorSceneManager.GetActiveScene();
                    if (string.IsNullOrEmpty(scene.path)) return null;
                    if (!scene.path.StartsWith("Assets/Samples/")) return null;

                    string dir = System.IO.Path.GetDirectoryName(scene.path)?.Replace('\\', '/');
                    while (!string.IsNullOrEmpty(dir) && dir.StartsWith("Assets/Samples/"))
                    {
                        string candidate = $"{dir}/Editor/ConfigMaster.asset";
                        ConfigMasterSO master = AssetDatabase.LoadAssetAtPath<ConfigMasterSO>(candidate);
                        if (master != null)
                        {
                            Set(master);
                            Log.Debug(LogTag.Editor, "[WorkspaceActive] 从活跃 scene 推断激活 ConfigMaster：{0}", candidate);
                            return master;
                        }
                        dir = System.IO.Path.GetDirectoryName(dir)?.Replace('\\', '/');
                    }
                    return null;
                }

                /// <summary>
                /// 将 GlobalsJson 序列化为 JSON 并原子写入目标路径（tmp + rename）。
                /// </summary>
                /// <param name="path">目标文件的绝对路径。</param>
                /// <param name="globals">待写入的 GlobalsJson 实例。</param>
                private static void WriteGlobals(string path, GlobalsJson globals)
                {
                    string json = JsonUtility.ToJson(globals, prettyPrint: true);
                    string dir = System.IO.Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    string tmp = path + ".tmp";
                    try
                    {
                        File.WriteAllText(tmp, json);
                        // .NET Standard 2.1 无 File.Move(overwrite) 重载，使用 File.Replace 实现原子覆盖
                        if (File.Exists(path))
                            File.Replace(tmp, path, null);
                        else
                            File.Move(tmp, path);
                    }
                    catch (Exception e)
                    {
                        if (File.Exists(tmp)) File.Delete(tmp);
                        Log.Warning(LogTag.Editor, "[WorkspaceActive] 写入 Globals.json 失败：{0}", e.Message);
                    }
                }

                /// <summary>
                /// 获取 Unity 工程根目录的绝对路径（Application.dataPath 的父目录）。
                /// </summary>
                /// <returns>工程根目录绝对路径。</returns>
                private static string GetProjectRoot()
                {
                    return System.IO.Path.GetDirectoryName(Application.dataPath);
                }
            }
        }
    }
}
