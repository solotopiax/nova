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
    internal enum EditorPlayModule
    {
        Asset = 1,
        Procedure = 2,
        Table = 4
    }

    /// <summary>只在 Play 中通知编辑器。模块使用在同一次 Play 中只通知一次。</summary>
    internal static class EditorPlayEvidence
    {
        private static EditorPlayModule s_Used;
        internal static event Action<EditorPlayModule> ModuleUsed;
        internal static event Action<string> ProcedureEntered;
        internal static event Action<string> ProcedureFailed;

        internal static void ResetModules() => s_Used = 0;

        internal static void Use(EditorPlayModule module)
        {
            if ((s_Used & module) != 0 || !Application.isPlaying)
                return;
            s_Used |= module;
            try { ModuleUsed?.Invoke(module); }
            catch (Exception) { /* 观察不参与运行结果。 */ }
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
