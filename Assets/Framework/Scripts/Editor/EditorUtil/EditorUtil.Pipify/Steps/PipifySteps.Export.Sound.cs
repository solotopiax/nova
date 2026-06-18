/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Export.Sound.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Pipify 内置 Step 合集 —— 导出分组（Sound 导出）
 ***************************************************************/

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录导出分组 Sound 子类的原子操作：Sound 数据导出与类型导出。
    /// 每个方法仅做薄封装，调用 EditorUtil.Sound.Exporter 对应 public API。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Luban target 名称（Sound 模块）。
        /// </summary>
        private const string c_SoundExportTargetName = "sound";

        /// <summary>
        /// Luban manager 类名（Sound 模块）。
        /// </summary>
        private const string c_SoundExportManagerName = "SoundTables";

        /// <summary>
        /// Step：导出 Sound 数据（遍历 SoundSettings 所有单元，逐一写出 JSON 数据文件）。
        /// 通过 Helpers.ResolveComponentOnNova 定位 SoundComponent；
        /// 反射读取 m_Settings 字段；若 settings 为 null 或单元列表为空则抛出 InvalidOperationException。
        /// 每个单元独立调用 EditorUtil.Sound.Exporter.ExportData，跳过 unitSetting 为 null 的情况。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.sound.data", "导出 Sound 数据", "导出资源/Sound")]
        internal static UniTask RunExportSoundData(PipifyContext ctx)
        {
            SoundComponent soundComponent = Helpers.ResolveComponentOnNova<SoundComponent>();
            SoundSettings settings = ResolveSoundSettings(soundComponent);

            if (settings.SoundUnitsSettings == null || settings.SoundUnitsSettings.Count == 0)
            {
                throw new InvalidOperationException("[Pipify] SoundSettings.SoundUnitsSettings 未配置，请在 SoundComponent Inspector 中添加数据单元后重试。");
            }

            string sourceDirPath = settings.SourceDirPath;
            if (string.IsNullOrEmpty(sourceDirPath))
            {
                throw new InvalidOperationException("[Pipify] SoundSettings.SourceDirPath 未配置，请在 SoundComponent Inspector 中填写数据源目录路径后重试。");
            }

            foreach (SoundUnitSetting unit in settings.SoundUnitsSettings)
            {
                EditorUtil.Sound.Exporter.ExportData(sourceDirPath, settings, unit, c_SoundExportTargetName, c_SoundExportManagerName);
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出 Sound 类型（为当前 SoundSettings 全量生成 C# 类型代码）。
        /// 通过 Helpers.ResolveComponentOnNova 定位 SoundComponent；
        /// 反射读取 m_Settings 字段；从 SoundUnitsSettings 中推断类型输出目录（取第一个非空 ClassesExportPath）；
        /// 若所有单元均未配置类型导出路径则抛出 InvalidOperationException。
        /// 调用 EditorUtil.Sound.Exporter.ExportCode（unitSetting 传 null 表示全量导出）。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.sound.code", "导出 Sound 类型", "导出资源/Sound")]
        internal static UniTask RunExportSoundCode(PipifyContext ctx)
        {
            SoundComponent soundComponent = Helpers.ResolveComponentOnNova<SoundComponent>();
            SoundSettings settings = ResolveSoundSettings(soundComponent);

            string sourceDirPath = settings.SourceDirPath;
            if (string.IsNullOrEmpty(sourceDirPath))
            {
                throw new InvalidOperationException("[Pipify] SoundSettings.SourceDirPath 未配置，请在 SoundComponent Inspector 中填写数据源目录路径后重试。");
            }

            string classExportPath = null;
            if (settings.SoundUnitsSettings != null)
            {
                foreach (SoundUnitSetting unit in settings.SoundUnitsSettings)
                {
                    if (!string.IsNullOrEmpty(unit.ClassesExportPath))
                    {
                        classExportPath = unit.ClassesExportPath;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(classExportPath))
            {
                throw new InvalidOperationException("[Pipify] SoundSettings 所有单元均未配置类型导出路径（ClassesExportPath），请在 SoundComponent Inspector 中填写后重试。");
            }

            EditorUtil.Sound.Exporter.ExportCode(sourceDirPath, settings, null, classExportPath, null, c_SoundExportTargetName, c_SoundExportManagerName);

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 通过反射从 SoundComponent 获取 SoundSettings 实例；settings 为 null 时抛出 InvalidOperationException。
        /// </summary>
        /// <param name="soundComponent">目标 SoundComponent 实例。</param>
        /// <returns>SoundSettings 实例。</returns>
        private static SoundSettings ResolveSoundSettings(SoundComponent soundComponent)
        {
            var field = typeof(SoundComponent).GetField("m_Settings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            SoundSettings settings = field?.GetValue(soundComponent) as SoundSettings;
            if (settings == null)
            {
                throw new InvalidOperationException("[Pipify] SoundComponent.m_Settings 未配置，请在 SoundComponent Inspector 中指定 SoundSettings 后重试。");
            }
            return settings;
        }
    }
}
