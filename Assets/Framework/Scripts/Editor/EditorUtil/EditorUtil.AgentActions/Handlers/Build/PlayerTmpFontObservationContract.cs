/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerTmpFontObservationContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   TMP 冻结准备行与现存/缺席观察的纯 DTO 同域校验
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using Row = NovaFramework.Editor.PlayerTmpFontRegistrationContract.ResourceRow;
using Weak = NovaFramework.Editor.PlayerTmpFontRegistrationContract.WeakRow;

namespace NovaFramework.Editor
{
    /// <summary>只验证 Plan 原输入绑定及当前真实库存；不加载字体，不把历史 JSON 摘要标成当前读取。</summary>
    internal static class PlayerTmpFontObservationContract
    {
        internal const string Normal = "normal";
        internal const string Uncleared = "uncleared";
        internal const string ZeroWidth = "zero-width";

        /// <summary>原 Prepare 实际读得的完整逐路径绑定，后态保留其历史语义，不宣称缺席字体的内存仍可读取。</summary>
        [Serializable]
        internal sealed class PreparedFont
        {
            public string path;
            public string kind;
            public string identity;
            public ulong instanceId;
            public bool fontDirty;
            public string fontJsonSha256;
            public string atlasIdentity;
            public int atlasWidth;
            public int atlasHeight;
            public string assetSha256;
            public string metaSha256;
            public string dependencyHash;
        }

        /// <summary>单次真实库存和磁盘观察；缺席字体不填身份、原生 ID、dirty 或 JSON 行，避免冒充读过旧对象。</summary>
        [Serializable]
        internal sealed class Observation
        {
            public string path;
            public bool present;
            public string kind;
            public string identity;
            public ulong instanceId;
            public bool fontDirty;
            public string fontJsonSha256;
            public string atlasIdentity;
            public int atlasWidth;
            public int atlasHeight;
            public string assetSha256;
            public string metaSha256;
            public string dependencyHash;
        }

        /// <summary>从已核对的原逐路径材料重建准确历史行；未知分类或结构直接拒绝，不能复制任意旧行。</summary>
        /// <param name="font">原 Prepare 实际材料；只表示原始输入绑定。</param>
        /// <returns>与原 Prepare 逐字相同的分类行。</returns>
        internal static string ExpectedRow(PreparedFont font)
        {
            if (font == null) throw new InvalidOperationException("TMP 原准备绑定为空。");
            if (font.kind == Normal) return PlayerTmpFontBuildContract.Marker + font.path;
            if (font.kind == Uncleared) return "TMPUnclearedFont=" + font.identity + "|" + font.fontJsonSha256;
            if (font.kind == ZeroWidth) return "TMPZeroWidthFont=" + font.identity + "|" + font.fontJsonSha256 + "|" + font.atlasIdentity + "|" + font.atlasWidth + "|" + font.atlasHeight;
            throw new InvalidOperationException("TMP 原准备分类未知。");
        }

        /// <summary>重建全部已批准的 TMP 历史分类行与 marker；不接纳未来未知前缀或丢弃原 marker。</summary>
        /// <param name="fonts">原 Prepare 的唯一逐路径材料。</param>
        /// <returns>去重排序后的准确 TMP 行集合。</returns>
        internal static string[] ExpectedInputs(PreparedFont[] fonts)
        {
            if (fonts == null) throw new InvalidOperationException("TMP 原准备绑定缺失。");
            return fonts.SelectMany(font => font.kind == Normal ? new[] { ExpectedRow(font) } :
                    new[] { ExpectedRow(font), PlayerTmpFontBuildContract.RegistrationMarker + font.path })
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        /// <summary>验证每条原输入、当前文件与现存对象；缺席仅限原 clean、无引用且真实 Scope 后也已缺席的非清理字体。</summary>
        /// <param name="prepared">原 Prepare 的冻结实际材料。</param>
        /// <param name="originalInputs">明确标记为历史输入绑定的准确 TMP 行，不是缺席对象的当前 JSON。</param>
        /// <param name="observed">本阶段真实磁盘和已加载对象库存。</param>
        /// <param name="beforeRows">原 before 三张资源表。</param>
        /// <param name="beforeWeak">原 before 完整弱槽。</param>
        /// <param name="currentRows">本次真实三张资源表。</param>
        /// <param name="currentWeak">本次真实完整弱槽。</param>
        /// <param name="scopeObserved">真正 Scope after 库存；首次 Scope 采集允许为空。</param>
        /// <param name="scopeRows">真正 Scope after 三表；要求 Scope 证明时不得为空。</param>
        /// <param name="scopeWeak">真正 Scope after 弱槽；要求 Scope 证明时不得为空。</param>
        /// <param name="requireScope">后续回调/Verify 为 true，只有首次 Scope 采集可为 false。</param>
        /// <param name="error">缺失、路径、正文、元数据、dirty 或引用不符原因。</param>
        /// <returns>所有历史行经逐项绑定且当前观察满足有限同域合同才为 true。</returns>
        internal static bool TryValidate(PreparedFont[] prepared, string[] originalInputs, Observation[] observed,
            Row[] beforeRows, Weak[] beforeWeak, Row[] currentRows, Weak[] currentWeak,
            Observation[] scopeObserved, Row[] scopeRows, Weak[] scopeWeak, bool requireScope, out string error)
        {
            error = null;
            if (!ValidPrepared(prepared) || originalInputs == null || !ExpectedInputs(prepared).SequenceEqual(originalInputs, StringComparer.Ordinal) ||
                !ValidObservations(observed) || observed.Length != prepared.Length ||
                !prepared.Select(font => font.path).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(observed.Select(font => font.path).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal) ||
                beforeRows == null || beforeWeak == null || currentRows == null || currentWeak == null ||
                requireScope && (!ValidObservations(scopeObserved) || scopeObserved.Length != prepared.Length || scopeRows == null || scopeWeak == null ||
                    !prepared.Select(font => font.path).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(scopeObserved.Select(font => font.path).OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal)))
                error = "TMP 冻结准备行/当前库存/真实 Scope 材料形状不完整；不能沿用未绑定旧行。";
            else
            {
                foreach (PreparedFont font in prepared)
                {
                    Observation actual = observed.Single(row => row.path == font.path);
                    if (actual.assetSha256 != font.assetSha256 || actual.metaSha256 != font.metaSha256 || actual.dependencyHash != font.dependencyHash)
                    { error = "TMP 原许可路径的实际资产/meta/依赖摘要漂移。"; break; }
                    if (actual.present)
                    {
                        if (actual.kind != font.kind || actual.identity != font.identity || actual.instanceId != font.instanceId || actual.fontJsonSha256 != font.fontJsonSha256 || actual.atlasIdentity != font.atlasIdentity || actual.atlasWidth != font.atlasWidth || actual.atlasHeight != font.atlasHeight ||
                            font.kind != Normal && actual.fontDirty != font.fontDirty)
                        { error = "TMP 现存字体分类、同域实例、完整原输入正文或非清理 dirty 漂移。"; break; }
                    }
                    else if (font.kind == Normal || font.fontDirty || Referenced(font.instanceId, beforeRows, beforeWeak) || Referenced(font.instanceId, currentRows, currentWeak) ||
                        requireScope && (scopeObserved.Single(row => row.path == font.path).present || Referenced(font.instanceId, scopeRows, scopeWeak)))
                    { error = "TMP 缺席字体不是原 clean 且原/后均未使用、真实 Scope after 已缺席的非清理候选。"; break; }
                }
            }
            return error == null;
        }

        /// <summary>检查原绑定字段及原行自洽，未知分类、路径、摘要或重复身份都不能进入缺席证明。</summary>
        /// <param name="fonts">原 Prepare 实际逐路径材料。</param>
        /// <returns>有限材料形状完全正确时为 true。</returns>
        private static bool ValidPrepared(PreparedFont[] fonts) => fonts != null && fonts.All(font => font != null && ValidPath(font.path) &&
            (font.kind == Normal || font.kind == Uncleared || font.kind == ZeroWidth) && font.instanceId != 0 && !string.IsNullOrEmpty(font.identity) &&
            IsSha(font.fontJsonSha256) && IsSha(font.assetSha256) && IsSha(font.metaSha256) && !string.IsNullOrEmpty(font.dependencyHash) &&
            (font.kind != ZeroWidth || !string.IsNullOrEmpty(font.atlasIdentity) && font.atlasWidth == 0 && font.atlasHeight >= 0)) &&
            fonts.Select(font => font.path).Distinct(StringComparer.Ordinal).Count() == fonts.Length &&
            fonts.Select(font => font.identity).Distinct(StringComparer.Ordinal).Count() == fonts.Length && fonts.Select(font => font.instanceId).Distinct().Count() == fonts.Length;

        /// <summary>缺席不携带伪造当前对象值，现存必须有独立原行，所有路径仍有真实磁盘摘要。</summary>
        /// <param name="rows">本阶段真实观察。</param>
        /// <returns>每行语义完整且路径唯一时为 true。</returns>
        private static bool ValidObservations(Observation[] rows) => rows != null && rows.All(row => row != null && ValidPath(row.path) &&
            IsSha(row.assetSha256) && IsSha(row.metaSha256) && !string.IsNullOrEmpty(row.dependencyHash) &&
            (row.present ? row.instanceId != 0 && !string.IsNullOrEmpty(row.identity) && !string.IsNullOrEmpty(row.kind) && IsSha(row.fontJsonSha256) :
                row.instanceId == 0 && row.identity == null && row.kind == null && row.fontJsonSha256 == null && row.atlasIdentity == null && row.atlasWidth == 0 && row.atlasHeight == 0 && !row.fontDirty)) &&
            rows.Select(row => row.path).Distinct(StringComparer.Ordinal).Count() == rows.Length;

        /// <summary>仅用于精确许可实例的引用存在性；完整资源键/值/弱顺序另由原登记合同逐项验证。</summary>
        /// <param name="instanceId">原实际实例 ID。</param>
        /// <param name="rows">完整三表，不可传空缺材料。</param>
        /// <param name="weak">完整弱槽，不可传空缺材料。</param>
        /// <returns>有任意强行或活弱槽引用时为 true。</returns>
        private static bool Referenced(ulong instanceId, Row[] rows, Weak[] weak) => rows.Any(row => row == null || row.fontInstanceId == instanceId) ||
            weak.Any(row => row == null || row.alive && row.fontInstanceId == instanceId);

        /// <summary>只接受原 Unity 逻辑资产路径，不接纳绝对路径、空段或穿越。</summary>
        /// <param name="path">来自 AssetDatabase 的准确逻辑路径。</param>
        /// <returns>Assets/Packages 下规范路径时为 true。</returns>
        private static bool ValidPath(string path) => path != null && (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal)) &&
            !path.Contains('\\') && !path.Split('/').Any(part => part.Length == 0 || part == "." || part == "..");

        /// <summary>验证规范 SHA256 文本，不把原始正文或未知占位符作为绑定。</summary>
        /// <param name="text">当前字段摘要。</param>
        /// <returns>准确 64 位小写十六进制时为 true。</returns>
        private static bool IsSha(string text) => text != null && text.Length == 64 && text.All(value => value >= '0' && value <= '9' || value >= 'a' && value <= 'f');
    }
}
