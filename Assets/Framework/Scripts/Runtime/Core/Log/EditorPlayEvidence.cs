/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename: EditorPlayEvidence.cs
 * descrip:  向编辑器提供 Play 模式的框架运行证据。
 ***************************************************************/

#if UNITY_EDITOR
using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    [Flags]
    internal enum EditorPlayModule
    {
        Asset = 1,
        Procedure = 2,
        Table = 4,
        Localization = 8,
        UI = 16,
        Network = 32
    }

    /// <summary>只在 Play 中通知编辑器。模块使用在同一次 Play 中只通知一次。</summary>
    internal static class EditorPlayEvidence
    {
        private static EditorPlayModule s_Used;
        private static bool s_TablesReady;
        internal static event Action<EditorPlayModule> ModuleUsed;
        internal static event Action<string, PlatformType, ChannelType, DevelopMode?> ConfigLoadFinished;
        internal static event Action TablesReady;
        internal static event Action<string> ProcedureEntered;
        internal static event Action<string> ProcedureFailed;

        /// <summary>每次进入 Play 重新观察，兼容禁用域重载的项目。</summary>
        internal static void ResetModules()
        {
            s_Used = 0;
            s_TablesReady = false;
        }

        /// <summary>每次 Play 只通知指定模块的首次成功使用。</summary>
        internal static void Use(EditorPlayModule module)
        {
            try
            {
                if ((s_Used & module) != 0 || ModuleUsed == null || !Application.isPlaying)
                    return;
                s_Used |= module;
                ModuleUsed.Invoke(module);
            }
            catch (Exception) { /* 观察不参与运行结果。 */ }
        }

        /// <summary>已注册真实表数据，独立于业务取表次数。</summary>
        internal static void RegisterTableEvidence()
        {
            try
            {
                if (s_TablesReady || TablesReady == null || !Application.isPlaying)
                    return;
                s_TablesReady = true;
                TablesReady.Invoke();
            }
            catch (Exception) { /* 观察不参与表注册结果。 */ }
        }

        /// <summary>在实际加载结算时通知 Editor，仅复制可公开的配置坐标。</summary>
        internal static void FinishConfigLoad(ConfigRuntimeSO config, Exception failure)
        {
            try
            {
                if (!Application.isPlaying)
                    return;
                string outcome = failure == null ? "success" : failure is OperationCanceledException ? "cancelled" : "failed";
                ConfigLoadFinished?.Invoke(outcome, config?.Platform ?? PlatformType.None,
                    config?.Channel ?? ChannelType.None, config?.DevelopMode);
            }
            catch (Exception) { /* 观察不参与配置加载结果。 */ }
        }

        internal static void Enter(ProcedureBase procedure)
        {
            if (!Application.isPlaying)
                return;
            Use(EditorPlayModule.Procedure);
            try { ProcedureEntered?.Invoke(StartupStage(procedure)); }
            catch (Exception) { /* 观察不参与流程切换。 */ }
        }

        internal static void Fail(ProcedureBase procedure)
        {
            if (!Application.isPlaying)
                return;
            try { ProcedureFailed?.Invoke(StartupStage(procedure)); }
            catch (Exception) { /* 观察不影响失败回退。 */ }
        }

        private static string StartupStage(ProcedureBase procedure) => procedure switch
        {
            ProcedureSplash => "splash",
            ProcedureCheckVersion => "version-check",
            ProcedureAppDownload => "app-download",
            ProcedureHotfix => "hotfix",
            ProcedureLoadDll => "load-dll",
            _ => null
        };
    }
}
#endif
