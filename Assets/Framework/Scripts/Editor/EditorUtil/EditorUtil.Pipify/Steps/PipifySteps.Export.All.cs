/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Export.All.cs
 * author:    Codex
 * created:   2026/7/23
 * descrip:   Pipify 内置 Step 合集 —— 全量 Excel 导出编排
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）：按稳定顺序聚合全部 Excel 派生导出。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// 聚合导出中的单个原子步骤定义。
        /// </summary>
        internal readonly struct ExcelExportStepDefinition
        {
            /// <summary>
            /// 创建原子导出步骤定义。
            /// </summary>
            /// <param name="id">对应的 Pipify Step ID。</param>
            /// <param name="run">复用的原子 Step 委托。</param>
            internal ExcelExportStepDefinition(string id, Func<PipifyContext, UniTask> run)
            {
                Id = id;
                Run = run;
            }

            internal string Id { get; }
            internal Func<PipifyContext, UniTask> Run { get; }
        }

        /// <summary>
        /// 全量 Excel 导出的固定顺序；明确排除 Config 与 Network Proto。
        /// </summary>
        internal static readonly ExcelExportStepDefinition[] AllExcelExportSteps =
        {
            new ExcelExportStepDefinition("export.table.data", RunExportTableData),
            new ExcelExportStepDefinition("export.table.code", RunExportTableCode),
            new ExcelExportStepDefinition("export.ui.data", RunExportUIData),
            new ExcelExportStepDefinition("export.ui.code", RunExportUICode),
            new ExcelExportStepDefinition("export.localization.text.data", RunExportLocalizationTextData),
            new ExcelExportStepDefinition("export.localization.text.code", RunExportLocalizationTextCode),
            new ExcelExportStepDefinition("export.localization.supportedlangs", RunExportLocalizationSupportedLanguages),
            new ExcelExportStepDefinition("export.localization.font.data", RunExportLocalizationFontData),
            new ExcelExportStepDefinition("export.localization.font.code", RunExportLocalizationFontCode),
            new ExcelExportStepDefinition("export.network.hostkey.data", RunExportNetworkHostKeyData),
            new ExcelExportStepDefinition("export.network.hostkey.code", RunExportNetworkHostKeyCode),
            new ExcelExportStepDefinition("export.network.netcmd.data", RunExportNetworkNetCmdData),
            new ExcelExportStepDefinition("export.network.netcmd.code", RunExportNetworkNetCmdCode),
            new ExcelExportStepDefinition("export.sound.data", RunExportSoundData),
            new ExcelExportStepDefinition("export.sound.code", RunExportSoundCode),
            new ExcelExportStepDefinition("export.vibrate.emphasis.data", RunExportVibrateEmphasisData),
            new ExcelExportStepDefinition("export.vibrate.emphasis.code", RunExportVibrateEmphasisCode),
            new ExcelExportStepDefinition("export.vibrate.custom.data", RunExportVibrateCustomData),
            new ExcelExportStepDefinition("export.vibrate.custom.code", RunExportVibrateCustomCode),
        };

        /// <summary>
        /// Step：顺序执行所有 Excel 派生的数据、类型与辅助清单导出。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>全部原子导出完成后的 UniTask。</returns>
        [PipifyStep("export.excel.all", "批量导出所有 Excel", "导出资源/全部")]
        internal static async UniTask RunExportAllExcel(PipifyContext ctx)
        {
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            for (int i = 0; i < AllExcelExportSteps.Length; i++)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                await AllExcelExportSteps[i].Run(ctx);
            }
        }
    }
}
