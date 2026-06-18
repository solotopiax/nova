/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.HybridCLR.cs
 * author:    taoye
 * created:   2026/5/8
 * descrip:   HybridCLR Pipeline 自动化工具 —— 公开接口
 ***************************************************************/

using HybridCLR.Editor.Commands;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// HybridCLR Pipeline 原子操作合集。
        /// 提供 link.xml 校验补全、HybridCLR Generate 一揽子入口、AOT / 业务 DLL 拷贝等独立方法，
        /// 由 Pipify Steps 按需编排为流水线；框架不再提供主观性的全流程封装。
        /// 路径通过 HybridCLR.Editor.SettingsUtil 读取，尊重用户在 ProjectSettings 中的自定义配置。
        /// DLL 列表配置通过 EditorUtil.Config.WorkspaceActive 锚定当前激活的 ConfigMaster。
        /// </summary>
        public static partial class HybridCLR
        {
            /// <summary>
            /// 域重载后挂载 sample DLL 自动补齐守卫：
            /// 启动期对当前激活 sample 检查一次，切换 Single 场景后再延迟检查一次。
            /// 仅对 Assets/Samples/ 下的 ConfigMaster 生效，且仅在目标 DLL 缺失或源文件更新时执行拷贝。
            /// </summary>
            [InitializeOnLoadMethod]
            private static void HookAutoSyncActiveSampleDlls()
            {
                EditorSceneManager.sceneOpened -= OnSceneOpenedAutoSyncActiveSampleDlls;
                EditorSceneManager.sceneOpened += OnSceneOpenedAutoSyncActiveSampleDlls;
                EditorApplication.delayCall += TryAutoSyncActiveSampleDlls;
            }

            /// <summary>
            /// Scene 打开后延迟一帧检查当前激活 sample 的 DLL 副本是否需要补齐。
            /// </summary>
            private static void OnSceneOpenedAutoSyncActiveSampleDlls(Scene scene, OpenSceneMode mode)
            {
                if (mode != OpenSceneMode.Single)
                {
                    return;
                }

                EditorApplication.delayCall += TryAutoSyncActiveSampleDlls;
            }

            /// <summary>
            /// 校验 Assets/link.xml，缺失则补全 ConfigMasterSO.AotMetadataDlls 每项的 preserve 记录。
            /// </summary>
            public static void ValidateLinkXml()
            {
                ConfigMasterSO master = ResolveActiveMasterOrThrow();
                var hybrid = ResolveHybridCLRForCurrentCoord(master);
                ValidateAndPatchLinkXml(hybrid.AotMetadataDlls);
            }

            /// <summary>
            /// HybridCLR/Generate/All：按 HybridCLR 预设顺序执行编译热更 DLL + 全部 Generate 产物（桥接 / AOT 泛型引用 / Il2CppDef / AOT 裁剪 DLL / LinkXml）。
            /// 对应 HybridCLR 菜单中的一键入口，等价于依次手动点击 Generate 子菜单的全部项。
            /// </summary>
            public static void GenerateAll()
            {
                PrebuildCommand.GenerateAll();
            }

            /// <summary>
            /// HybridCLR/Generate/LinkXml：编译 ActiveBuildTarget 热更 DLL 并基于热更代码引用生成 link.xml。
            /// </summary>
            public static void GenerateLinkXml()
            {
                LinkGeneratorCommand.GenerateLinkXml();
            }

            /// <summary>
            /// HybridCLR/Generate/MethodBridgeAndReversePInvokeWrapper：基于 ActiveBuildTarget 生成方法桥接与反向 PInvoke 包装。
            /// 需要先执行 Generate/AOTDlls（或 Generate/All）产出 AOT 裁剪 DLL。
            /// </summary>
            public static void GenerateMethodBridgeAndReversePInvokeWrapper()
            {
                MethodBridgeGeneratorCommand.GenerateMethodBridgeAndReversePInvokeWrapper();
            }

            /// <summary>
            /// HybridCLR/Generate/AOTGenericReference：编译 ActiveBuildTarget 热更 DLL 并生成 AOT 泛型引用 cs 文件。
            /// </summary>
            public static void GenerateAotGenericReference()
            {
                AOTReferenceGeneratorCommand.CompileAndGenerateAOTGenericReference();
            }

            /// <summary>
            /// HybridCLR/Generate/Il2CppDef：生成 Il2Cpp 宏定义头文件与 AssemblyManifest.cpp。
            /// </summary>
            public static void GenerateIl2CppDef()
            {
                Il2CppDefGeneratorCommand.GenerateIl2CppDef();
            }

            /// <summary>
            /// HybridCLR/Generate/AOTDlls：在 ActiveBuildTarget 下执行 AOT DLL 裁剪，产出 AssembliesPostIl2CppStrip 目录。
            /// </summary>
            public static void GenerateAotDlls()
            {
                StripAOTDllCommand.GenerateStripedAOTDlls();
            }

            /// <summary>
            /// HybridCLR/CompileDll/ActiveBuildTarget：针对当前 activeBuildTarget 编译热更业务 DLL，产出到 HotUpdateDllsOutputDir。
            /// 仅编译不做 AOT 裁剪、不生成桥接等产物，适合只需要更新热更 DLL 的场景。
            /// </summary>
            public static void CompileDllActiveBuildTarget()
            {
                CompileDllCommand.CompileDllActiveBuildTarget();
            }

            /// <summary>
            /// 拷贝 AOT 元数据 DLL 到 ConfigMasterSO 中各条目配置的目标位置。
            /// 源/目标路径均从 DllMasterAssetEntry.SourceLocation / TargetLocation 读取，均为项目根相对的具体文件路径，含文件名与扩展名（源端如 .dll，目标端如 .dll.bytes），路径支持 {ActiveBuildTarget} 占位符自动解析。
            /// </summary>
            public static void CopyAotDlls()
            {
                ConfigMasterSO master = ResolveActiveMasterOrThrow();
                var hybrid = ResolveHybridCLRForCurrentCoord(master);
                CopyDllEntries(hybrid.AotMetadataDlls, "AOT 元数据");
            }

            /// <summary>
            /// 拷贝业务层热更 DLL 到 ConfigMasterSO 中各条目配置的目标位置。
            /// 源/目标路径均从 DllMasterAssetEntry.SourceLocation / TargetLocation 读取，均为项目根相对的具体文件路径，含文件名与扩展名（源端如 .dll，目标端如 .dll.bytes），路径支持 {ActiveBuildTarget} 占位符自动解析。
            /// </summary>
            public static void CopyGameDlls()
            {
                ConfigMasterSO master = ResolveActiveMasterOrThrow();
                var hybrid = ResolveHybridCLRForCurrentCoord(master);
                CopyDllEntries(hybrid.GameDlls, "业务 DLL");
            }

            /// <summary>
            /// 尝试为当前激活 sample 自动补齐 ConfigMaster 配置的 DLL 副本。
            /// 失败仅记 Warning，不打断编辑器启动与切场景流程。
            /// </summary>
            public static void TryAutoSyncActiveSampleDlls()
            {
                ConfigMasterSO master = Config.WorkspaceActive.Get();
                if (master == null)
                {
                    return;
                }

                TryAutoSyncSampleDlls(master);
            }
        }
    }
}
