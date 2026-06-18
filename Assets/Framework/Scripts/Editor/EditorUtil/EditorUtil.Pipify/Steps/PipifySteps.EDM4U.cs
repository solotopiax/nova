/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.EDM4U.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Pipify 内置 Step 合集 —— EDM4U 分组（1 个 Step）
 ***************************************************************/

using Cysharp.Threading.Tasks;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录 EDM4U 分组的原子操作：
    /// 强制触发 EDM4U ResolveSync，确保 Assets/GeneratedLocalRepo/** 目录存在，
    /// 从而避免 HybridCLR Generate All 内部触发 BuildPlayer 时 EDM4U 因目标目录缺失而抛异常。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：强制触发 EDM4U Android 依赖解析，重建 Assets/GeneratedLocalRepo/** 目录。
        /// 应排在 HybridCLR Generate All Step 之前执行。
        /// Runner 已不再做 StartAssetEditing + LockReloadAssemblies 批锁定，
        /// EDM4U 的 ResolveSync 直接享受实时 importer，无需在此显式 Stop/Unlock。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("edm4u.android_resolve", "解析 Android 依赖", "EDM4U")]
        internal static UniTask RunResolveAndroidDependencies(PipifyContext ctx)
        {
            EditorUtil.AndroidResolver.Resolve();
            return UniTask.CompletedTask;
        }
    }
}
