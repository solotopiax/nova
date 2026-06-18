/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.HybridCLR.cs
 * author:    taoye
 * created:   2026/5/10
 * descrip:   Pipify 内置 Step 合集 —— HybridCLR 分组（10 个 Step）
 ***************************************************************/

using Cysharp.Threading.Tasks;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录 HybridCLR 分组的原子操作：
    /// link.xml 校验/补全 → 编译业务 DLL → Generate All 一键入口 → 各 Generate 子产物 → AOT DLL 拷贝 → 业务 DLL 拷贝。
    /// 每个方法仅做薄封装，调用 EditorUtil.HybridCLR 对应 public API。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：校验并补全 Assets/link.xml 中的 HybridCLR preserve 记录。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.validate_linkxml", "补全 link.xml", "HybridCLR")]
        internal static UniTask RunValidateLinkXml(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.ValidateLinkXml();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：针对 activeBuildTarget 编译业务 DLL（HybridCLR/CompileDll/ActiveBuildTarget）。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.compile_dll_active_build_target", "编译业务 DLL (ActiveBuildTarget)", "HybridCLR")]
        internal static UniTask RunCompileDllActiveBuildTarget(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.CompileDllActiveBuildTarget();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：执行 HybridCLR 一键 Generate All（PrebuildCommand.GenerateAll）。
        /// 对应菜单 HybridCLR/Generate/All。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.generate_all", "Generate All", "HybridCLR")]
        internal static UniTask RunGenerateAll(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.GenerateAll();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：生成 link.xml（基于热更代码引用）。对应菜单 HybridCLR/Generate/LinkXml。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.generate_linkxml", "Generate LinkXml", "HybridCLR")]
        internal static UniTask RunGenerateLinkXml(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.GenerateLinkXml();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：生成方法桥接与反向 PInvoke 包装（需先生成 AOT DLL）。对应菜单 HybridCLR/Generate/MethodBridgeAndReversePInvokeWrapper。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.generate_method_bridge", "Generate MethodBridge", "HybridCLR")]
        internal static UniTask RunGenerateMethodBridge(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.GenerateMethodBridgeAndReversePInvokeWrapper();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：编译业务 DLL 并生成 AOT 泛型引用。对应菜单 HybridCLR/Generate/AOTGenericReference。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.generate_aot_generic_reference", "Generate AOTGenericReference", "HybridCLR")]
        internal static UniTask RunGenerateAotGenericReference(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.GenerateAotGenericReference();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：生成 Il2Cpp 宏定义与 AssemblyManifest。对应菜单 HybridCLR/Generate/Il2CppDef。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.generate_il2cpp_def", "Generate Il2CppDef", "HybridCLR")]
        internal static UniTask RunGenerateIl2CppDef(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.GenerateIl2CppDef();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：裁剪 AOT DLL（产出 AssembliesPostIl2CppStrip）。对应菜单 HybridCLR/Generate/AOTDlls。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.generate_aot_dlls", "Generate AOTDlls", "HybridCLR")]
        internal static UniTask RunGenerateAotDlls(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.GenerateAotDlls();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：将 AOT 元数据 DLL 从 HybridCLR 输出目录拷贝到 ConfigMasterSO 中配置的目标位置。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.copy_aot_dll", "拷贝 AOT 层 DLL", "HybridCLR")]
        internal static UniTask RunCopyAotDll(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.CopyAotDlls();
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：将业务层热更 DLL 从 HybridCLR 输出目录拷贝到 ConfigMasterSO 中配置的目标位置。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("hybridclr.copy_game_dll", "拷贝业务层 DLL", "HybridCLR")]
        internal static UniTask RunCopyGameDll(PipifyContext ctx)
        {
            EditorUtil.HybridCLR.CopyGameDlls();
            return UniTask.CompletedTask;
        }
    }
}
