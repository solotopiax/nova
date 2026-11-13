/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.SpriteAtlas.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   项目显式选择的图集预演、生成与恢复步骤
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>可选图集步骤参数；默认只读预演，不自动选择配置或插入已有 Batch。</summary>
    [Serializable]
    public sealed class SpriteAtlasGenerationStepArgs
    {
        /// <summary>项目已保存的图集生成配置精确路径，不接受自动扫描或默认目录。</summary>
        [Tooltip("已保存的图集生成配置路径，例如项目自有配置目录中的 .asset。")]
        public string SettingsAssetPath = "";

        /// <summary>为 true 时只报告差异；项目明确选择 false 后才改变它声明的输出。</summary>
        [Tooltip("默认只预演；核对目录和差异后，明确关闭此项才会生成图集。")]
        public bool PreviewOnly = true;
    }

    /// <summary>显式恢复图集生成事务的参数；恢复目录严格由配置 GUID 推导。</summary>
    [Serializable]
    public sealed class SpriteAtlasGenerationRecoveryStepArgs
    {
        /// <summary>拥有遗留事务的已保存项目配置精确路径。</summary>
        public string SettingsAssetPath = "";
    }

    internal static partial class PipifySteps
    {
        /// <summary>
        /// 预演或生成已声明主图集。仅在项目主动选择此 Step 后执行，实际生成时应由项目排在 Bundle 构建之前。
        /// 不修改其他 Batch、源图片、资源收集器或全局 Sprite Packer 设置；错误沿 Runner 原路径中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的上下文。</param>
        /// <param name="p">明确配置路径和默认只读预演开关。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("spriteatlas.generate", "生成图集", "构建资源", ParamsType = typeof(SpriteAtlasGenerationStepArgs))]
        internal static UniTask RunGenerateSpriteAtlases(PipifyContext ctx, SpriteAtlasGenerationStepArgs p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            SpriteAtlasGenerationPlan plan = EditorUtil.SpriteAtlasGeneration.Preview(p.SettingsAssetPath);
            Debug.Log($"[Pipify] 图集预演：启用={plan.Enabled}，输出={plan.OutputFolder}，指纹={plan.Fingerprint}");
            foreach (SpriteAtlasGenerationEntry entry in plan.Entries)
                Debug.Log($"[Pipify] 图集差异：{entry.Change} {entry.OutputPath}，Sprite={entry.Sources.Count}");
            if (!p.PreviewOnly) EditorUtil.SpriteAtlasGeneration.Apply(plan);
            return UniTask.CompletedTask;
        }

        /// <summary>按配置自身 GUID 恢复未完成图集事务，不重新生成，不接受任意备份路径。</summary>
        /// <param name="ctx">Runner 下发的上下文。</param>
        /// <param name="p">拥有事务的配置路径。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("spriteatlas.recover", "恢复图集生成", "构建资源", ParamsType = typeof(SpriteAtlasGenerationRecoveryStepArgs))]
        internal static UniTask RunRecoverSpriteAtlases(PipifyContext ctx, SpriteAtlasGenerationRecoveryStepArgs p)
        {
            if (p == null) throw new ArgumentNullException(nameof(p));
            EditorUtil.SpriteAtlasGeneration.Recover(p.SettingsAssetPath);
            return UniTask.CompletedTask;
        }
    }
}
