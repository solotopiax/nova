/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PlayerTmpFontRegistrationContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   TMP 已加载精确字体正常登记的逐行纯后验合同
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    /// <summary>只核对已捕获 DTO；不加载字体、不保留 Unity 对象、不执行厂商登记。</summary>
    internal static class PlayerTmpFontRegistrationContract
    {
        internal const string References = "s_FontAssetReferences";
        internal const string Names = "s_FontAssetNameReferenceLookup";
        internal const string Families = "s_FontAssetFamilyNameAndStyleReferenceLookup";
        private const string UpperLookup = "-------------------------------- !-#$%&-()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[-]^_`ABCDEFGHIJKLMNOPQRSTUVWXYZ{|}~-";

        /// <summary>只包含写前已加载字体的同域身份、受保护正文摘要及源驱动 hash；不携带字体强引用。</summary>
        [Serializable]
        internal sealed class Font
        {
            public string identity;
            public ulong instanceId;
            public string entityKey;
            public bool cleared;
            public bool allowAtlasReference;
            public bool atlasReferencePresent;
            public string protectionSha256;
            public int nameHash;
            public int familyHash;
            public int styleHash;
            public int materialHash;
            public int expectedNameHash;
            public int expectedFamilyHash;
            public int expectedStyleHash;
            public int expectedMaterialHash;
        }

        /// <summary>三张资源表的完整准确行，引用用同域 ID 与持久身份绑定，数值保留真实读取结果。</summary>
        [Serializable]
        internal sealed class ResourceRow
        {
            public string table;
            public string key;
            public string fontIdentity;
            public ulong fontInstanceId;
            public int nameHash;
            public int familyHash;
            public int styleHash;
            public long familyStyleHash;
        }

        /// <summary>保留列表每个槽位；alive=false 是 TryGetTarget 的真实失效结果，不把已销毁活对象当空槽。</summary>
        [Serializable]
        internal sealed class WeakRow
        {
            public bool alive;
            public string fontIdentity;
            public ulong fontInstanceId;
        }

        /// <summary>复现已 pin 源中的 GetHashCode/ToUpperFast 或 GetSimpleHashCode，不调用字体或厂商 getter。</summary>
        /// <param name="value">当前 name 或 FaceInfo 的既有字符串。</param>
        /// <param name="simple">true 使用材质名的逐字符原值算法。</param>
        /// <returns>与已审 TMP 源一致的 32 位 hash。</returns>
        internal static int PredictHash(string value, bool simple = false)
        {
            int hash = 0;
            foreach (char character in value ?? string.Empty)
            {
                char next = simple || character >= UpperLookup.Length ? character : UpperLookup[character];
                hash = unchecked(((hash << 5) + hash) ^ next);
            }
            return hash;
        }

        /// <summary>与 FontAssetRef 构造器一致：零 name hash 使用 family hash，不能忽略这条键计算分支。</summary>
        /// <param name="font">已冻结字体描述。</param>
        /// <returns>资源引用行与 name lookup 的准确键。</returns>
        internal static int EffectiveName(Font font) => font.expectedNameHash != 0 ? font.expectedNameHash : font.expectedFamilyHash;

        /// <summary>按已审有符号 style 高32位与无符号 family 低32位组合键。</summary>
        /// <param name="font">已冻结字体描述。</param>
        /// <returns>准确 family/style 64 位键。</returns>
        internal static long FamilyStyle(Font font) => ((long)font.expectedStyleHash << 32) | (uint)font.expectedFamilyHash;

        /// <summary>每次 Capture 返回前复核真实形状和原 clear 每条行，保持旧异常在 Plan/写前就拒绝的失败时点。</summary>
        /// <param name="fonts">本次实际已加载的许可字体描述。</param>
        /// <param name="rows">本次三表完整行。</param>
        /// <param name="weak">本次含失效槽的完整弱列表。</param>
        /// <param name="error">形状、hash backing 或 clear 行异常原因。</param>
        /// <returns>只证明捕获材料满足原前置门，不代表已正常清理或登记完成。</returns>
        internal static bool TryValidateCapturedState(Font[] fonts, ResourceRow[] rows, WeakRow[] weak, out string error)
        {
            error = null;
            if (!ValidFonts(fonts) || !ValidRows(rows) || !ValidWeak(weak) || !ClearRowsValid(fonts, rows))
                error = "TMP 捕获的完整行/字体 backing 形状或 clear 原资源键不符；在计划/写前拒绝，不推迟到构建后。";
            return error == null;
        }

        /// <summary>验证完整 before→after 登记，以及 after→current 的精确已记录缓存；旧材料缺行时拒绝。</summary>
        /// <param name="beforeFonts">写前实际已加载的唯一许可字体。</param>
        /// <param name="afterFonts">正式链后仍自然加载的字体及其真实保护摘要。</param>
        /// <param name="currentFonts">Verify 当前仍自然加载的字体。</param>
        /// <param name="beforeRows">三张表完整写前行。</param>
        /// <param name="afterRows">正式链后完整行。</param>
        /// <param name="currentRows">当前完整行。</param>
        /// <param name="beforeWeak">完整写前弱槽。</param>
        /// <param name="afterWeak">正式链后完整弱槽。</param>
        /// <param name="currentWeak">当前完整弱槽。</param>
        /// <param name="error">缺失、未知新增、替换、顺序或正文变化的原因。</param>
        /// <returns>只有精确源合同允许的单调登记成立时返回 true。</returns>
        internal static bool TryValidate(Font[] beforeFonts, Font[] afterFonts, Font[] currentFonts,
            ResourceRow[] beforeRows, ResourceRow[] afterRows, ResourceRow[] currentRows,
            WeakRow[] beforeWeak, WeakRow[] afterWeak, WeakRow[] currentWeak, out string error)
        {
            error = null;
            if (!ValidFonts(beforeFonts) || !ValidFonts(afterFonts) || !ValidFonts(currentFonts) ||
                !ValidRows(beforeRows) || !ValidRows(afterRows) || !ValidRows(currentRows) ||
                !ValidWeak(beforeWeak) || !ValidWeak(afterWeak) || !ValidWeak(currentWeak))
                error = "TMP 逐行登记材料缺失、重复或形状未知；旧回执不能升级。";
            else if (!ClearRowsValid(beforeFonts, beforeRows) || !ClearRowsValid(afterFonts, afterRows) || !ClearRowsValid(currentFonts, currentRows))
                error = "TMP 已批准 clear 字体存在错误原引用/hash 或多余错误 lookup 键；不能把旧异常行规整为正常。";
            else if (!FontsPreserved(beforeFonts, afterFonts, false) || !FontsPreserved(afterFonts, currentFonts, true) ||
                !RequiredFontsPresent(beforeFonts, afterFonts, beforeRows, beforeWeak) || !RequiredFontsPresent(beforeFonts, afterFonts, afterRows, afterWeak) ||
                !RequiredFontsPresent(afterFonts, currentFonts, currentRows, currentWeak))
                error = "TMP 登记字体不是写前同一已加载实例，或正文/元数据/hash backing 发生未知变化。";
            else if (Util.Json.Serialize(afterRows) != Util.Json.Serialize(currentRows) || Util.Json.Serialize(afterWeak) != Util.Json.Serialize(currentWeak))
                error = "TMP 当前完整资源表或弱槽与正式 after 不同。";
            else if (!TryValidateAppend(beforeFonts, afterFonts, beforeRows, afterRows, beforeWeak, afterWeak, out error))
                return false;
            return error == null;
        }

        /// <summary>检查字体身份唯一、完整保护摘要及 backing 只处于零或源预测值；不接受 after 自签的新 hash。</summary>
        /// <param name="fonts">单次真实观测集合。</param>
        /// <returns>材料形状和有限 hash 状态正确时返回 true。</returns>
        private static bool ValidFonts(Font[] fonts)
        {
            return fonts != null && fonts.All(font => font != null && font.instanceId != 0 && !string.IsNullOrEmpty(font.identity) && !string.IsNullOrEmpty(font.entityKey) &&
                !string.IsNullOrEmpty(font.protectionSha256) && (font.nameHash == 0 || font.nameHash == font.expectedNameHash) &&
                (font.familyHash == 0 || font.familyHash == font.expectedFamilyHash) && (font.styleHash == 0 || font.styleHash == font.expectedStyleHash) &&
                (font.materialHash == 0 || font.materialHash == font.expectedMaterialHash)) &&
                fonts.Select(font => font.instanceId).Distinct().Count() == fonts.Length && fonts.Select(font => font.identity).Distinct(StringComparer.Ordinal).Count() == fonts.Length && fonts.Select(font => font.entityKey).Distinct(StringComparer.Ordinal).Count() == fonts.Length;
        }

        /// <summary>保留原 clear 组的每一行检查，额外错误 key 不因原样保留而被放行。</summary>
        /// <param name="fonts">本次真实字体及其当前 backing。</param>
        /// <param name="rows">单次完整资源行。</param>
        /// <returns>所有关联 clear 行均为准确源键，已有引用 backing 也一致时返回 true。</returns>
        private static bool ClearRowsValid(Font[] fonts, ResourceRow[] rows)
        {
            foreach (Font font in fonts.Where(value => value.cleared))
                foreach (ResourceRow row in rows.Where(value => value.fontInstanceId == font.instanceId))
                    if (!ExactRow(row, font) || row.table == References &&
                        (font.nameHash != font.expectedNameHash || font.familyHash != font.expectedFamilyHash || font.styleHash != font.expectedStyleHash)) return false;
            return true;
        }

        /// <summary>后态只能保留同域原候选；未被使用且自然卸载的候选可缺席，但重新加载的新实例不能补位。</summary>
        /// <param name="before">唯一许可的原描述集合。</param>
        /// <param name="after">当前自然加载集合。</param>
        /// <param name="exactBacking">true 要求已记录 after 的 hash backing 完全相等。</param>
        /// <returns>所有仍存在字体保护字段逐项相同且 backing 只正常初始化时返回 true。</returns>
        private static bool FontsPreserved(Font[] before, Font[] after, bool exactBacking)
        {
            foreach (Font actual in after)
            {
                Font original = before.SingleOrDefault(font => font.instanceId == actual.instanceId);
                if (original == null || original.identity != actual.identity || original.entityKey != actual.entityKey || original.cleared != actual.cleared ||
                    original.allowAtlasReference != actual.allowAtlasReference ||
                    original.atlasReferencePresent != actual.atlasReferencePresent && (exactBacking || !original.allowAtlasReference || original.atlasReferencePresent) ||
                    original.protectionSha256 != actual.protectionSha256 || original.expectedNameHash != actual.expectedNameHash || original.expectedFamilyHash != actual.expectedFamilyHash ||
                    original.expectedStyleHash != actual.expectedStyleHash || original.expectedMaterialHash != actual.expectedMaterialHash ||
                    !BackingPreserved(original.nameHash, actual.nameHash, original.expectedNameHash, exactBacking) ||
                    !BackingPreserved(original.familyHash, actual.familyHash, original.expectedFamilyHash, exactBacking) ||
                    !BackingPreserved(original.styleHash, actual.styleHash, original.expectedStyleHash, exactBacking) ||
                    !BackingPreserved(original.materialHash, actual.materialHash, original.expectedMaterialHash, exactBacking)) return false;
            }
            return true;
        }

        /// <summary>原有强资源行或活弱槽引用的许可字体不能消失；只允许未使用候选自然卸载。</summary>
        /// <param name="allowed">此前实际存在的有限许可字体。</param>
        /// <param name="present">当前自然加载描述。</param>
        /// <param name="rows">需要继续保护的完整强引用行。</param>
        /// <param name="weak">需要继续保护的完整弱槽。</param>
        /// <returns>所有被引用许可字体仍有同 ID 的保护证据时返回 true。</returns>
        private static bool RequiredFontsPresent(Font[] allowed, Font[] present, ResourceRow[] rows, WeakRow[] weak) => allowed.All(font =>
            !rows.Any(row => row.fontInstanceId == font.instanceId) && !weak.Any(row => row.alive && row.fontInstanceId == font.instanceId) ||
            present.Any(value => value.instanceId == font.instanceId));

        /// <summary>只有零 backing 到已冻结预测值是正常初始化；已计算值不能清零或变成第三值。</summary>
        /// <param name="before">写前 backing。</param>
        /// <param name="after">真实后态 backing。</param>
        /// <param name="expected">源算法和写前名称字段的预测值。</param>
        /// <param name="exact">是否要求已记录后态逐值相等。</param>
        /// <returns>精确相等或明确零到预测值时返回 true。</returns>
        private static bool BackingPreserved(int before, int after, int expected, bool exact) => before == after || !exact && before == 0 && after == expected;

        /// <summary>检查准确三表、唯一 table/key 与有效对象身份；lookup 行不得伪造 FontAssetRef 的额外数值。</summary>
        /// <param name="rows">实际完整资源行。</param>
        /// <returns>只含已审表及唯一完整行时返回 true。</returns>
        private static bool ValidRows(ResourceRow[] rows) => rows != null && rows.All(row => row != null &&
            (row.table == References || row.table == Names || row.table == Families) && !string.IsNullOrEmpty(row.key) &&
            !string.IsNullOrEmpty(row.fontIdentity) && row.fontInstanceId != 0 &&
            (row.table == References || row.nameHash == 0 && row.familyHash == 0 && row.styleHash == 0 && row.familyStyleHash == 0)) &&
            rows.Select(row => row.table + "|" + row.key).Distinct(StringComparer.Ordinal).Count() == rows.Length;

        /// <summary>保留真实失效槽；活目标必须有完整身份且不能重复，避免把替换或重排藏入聚合摘要。</summary>
        /// <param name="rows">按实际槽位顺序读取的列表。</param>
        /// <returns>元素完整且活目标唯一时返回 true。</returns>
        private static bool ValidWeak(WeakRow[] rows) => rows != null && rows.All(row => row != null &&
            (row.alive ? !string.IsNullOrEmpty(row.fontIdentity) && row.fontInstanceId != 0 : row.fontIdentity == null && row.fontInstanceId == 0)) &&
            rows.Where(row => row.alive).Select(row => row.fontInstanceId).Distinct().Count() == rows.Count(row => row.alive);

        /// <summary>逐行证明原 winner 完整保留、新键值精确、弱槽首空复用，并校验碰撞与弱登记可由同一源顺序产生。</summary>
        /// <param name="allowed">写前实际许可字体。</param>
        /// <param name="present">后态仍存在的准确字体。</param>
        /// <param name="beforeRows">写前三表。</param>
        /// <param name="afterRows">后态三表。</param>
        /// <param name="beforeWeak">写前弱槽。</param>
        /// <param name="afterWeak">后态弱槽。</param>
        /// <param name="error">具体不符原因。</param>
        /// <returns>完整有限追加合同成立时返回 true。</returns>
        private static bool TryValidateAppend(Font[] allowed, Font[] present, ResourceRow[] beforeRows, ResourceRow[] afterRows, WeakRow[] beforeWeak, WeakRow[] afterWeak, out string error)
        {
            error = null;
            var old = beforeRows.ToDictionary(row => row.table + "|" + row.key, StringComparer.Ordinal);
            var actual = afterRows.ToDictionary(row => row.table + "|" + row.key, StringComparer.Ordinal);
            if (old.Any(pair => !actual.TryGetValue(pair.Key, out ResourceRow row) || Util.Json.Serialize(pair.Value) != Util.Json.Serialize(row)))
            { error = "TMP 原资源行或 collision winner 被删除/替换。"; return false; }
            ResourceRow[] additions = afterRows.Where(row => !old.ContainsKey(row.table + "|" + row.key)).ToArray();
            ResourceRow[] newReferences = additions.Where(row => row.table == References).ToArray();
            var edges = new Dictionary<ulong, HashSet<ulong>>();
            foreach (ResourceRow row in newReferences)
            {
                Font font = present.SingleOrDefault(value => value.instanceId == row.fontInstanceId);
                if (font == null || !allowed.Any(value => value.instanceId == font.instanceId) || !ExactRow(row, font) ||
                    !afterWeak.Any(value => value.alive && value.fontInstanceId == font.instanceId && value.fontIdentity == font.identity) ||
                    font.nameHash != font.expectedNameHash || font.familyHash != font.expectedFamilyHash || font.styleHash != font.expectedStyleHash || font.materialHash != font.expectedMaterialHash)
                { error = "TMP 新引用不是原已加载字体的准确预测键/值或缺失真实弱登记。"; return false; }
                edges[row.fontInstanceId] = new HashSet<ulong>();
            }
            foreach (ResourceRow row in additions.Where(value => value.table != References))
            {
                Font font = present.SingleOrDefault(value => value.instanceId == row.fontInstanceId);
                if (font == null || !newReferences.Any(reference => reference.fontInstanceId == font.instanceId) || !ExactRow(row, font))
                { error = "TMP 新 lookup 不是同次新引用的准确键/值。"; return false; }
            }
            foreach (ResourceRow reference in newReferences)
            {
                Font font = present.Single(value => value.instanceId == reference.fontInstanceId);
                foreach (string table in new[] { Names, Families })
                {
                    string key = LookupKey(table, font);
                    if (!actual.TryGetValue(table + "|" + key, out ResourceRow winner))
                    { error = "TMP 新引用缺少官方必须存在的 name/family lookup。"; return false; }
                    if (old.ContainsKey(table + "|" + key)) continue;
                    ResourceRow winningReference = newReferences.SingleOrDefault(value => value.fontInstanceId == winner.fontInstanceId);
                    if (winningReference == null) { error = "TMP 新 lookup winner 不属于本次准确新引用。"; return false; }
                    if (winner.fontInstanceId != reference.fontInstanceId) AddEdge(edges, winner.fontInstanceId, reference.fontInstanceId);
                }
            }
            if (afterWeak.Length < beforeWeak.Length) { error = "TMP 弱槽列表被截短。"; return false; }
            var registered = new List<ulong>();
            bool freeSeen = false;
            for (int index = 0; index < afterWeak.Length; index++)
            {
                WeakRow row = afterWeak[index];
                WeakRow original = index < beforeWeak.Length ? beforeWeak[index] : null;
                if (original?.alive == true)
                {
                    if (Util.Json.Serialize(original) != Util.Json.Serialize(row)) { error = "TMP 原活弱目标被移动、替换或丢失。"; return false; }
                    continue;
                }
                if (!row.alive) { freeSeen = true; if (index >= beforeWeak.Length) { error = "TMP 追加未知空弱槽。"; return false; } continue; }
                Font font = present.SingleOrDefault(value => value.instanceId == row.fontInstanceId);
                if (freeSeen || font == null || font.identity != row.fontIdentity || !allowed.Any(value => value.instanceId == font.instanceId) ||
                    !afterRows.Any(value => value.table == References && ExactRow(value, font)) ||
                    font.nameHash != font.expectedNameHash || font.familyHash != font.expectedFamilyHash || font.styleHash != font.expectedStyleHash || font.materialHash != font.expectedMaterialHash)
                { error = "TMP 新弱目标不是原字体，或没有先复用第一个失效槽。"; return false; }
                if (!edges.ContainsKey(font.instanceId)) edges[font.instanceId] = new HashSet<ulong>();
                if (registered.Count != 0) AddEdge(edges, registered[registered.Count - 1], font.instanceId);
                registered.Add(font.instanceId);
            }
            if (!Acyclic(edges)) { error = "TMP collision winner 与实际弱登记顺序不能由同一 ContainsKey-first-wins 源路径产生。"; return false; }
            return true;
        }

        /// <summary>只比较源定义的准确键与值，包含零名称 hash 的 family 回退。</summary>
        /// <param name="row">真实表行。</param>
        /// <param name="font">写前冻结后仍存在的准确字体。</param>
        /// <returns>键、对象和引用数值完全一致时返回 true。</returns>
        private static bool ExactRow(ResourceRow row, Font font) => row.fontInstanceId == font.instanceId && row.fontIdentity == font.identity &&
            (row.table == References ? row.key == font.entityKey && row.nameHash == EffectiveName(font) && row.familyHash == font.expectedFamilyHash &&
                row.styleHash == font.expectedStyleHash && row.familyStyleHash == FamilyStyle(font) : row.key == LookupKey(row.table, font));

        /// <summary>生成已审 map 的十进制键，不受宿主区域设置影响。</summary>
        /// <param name="table">准确 name 或 family/style 表名。</param>
        /// <param name="font">写前预测描述。</param>
        /// <returns>源定义键的 invariant 文本。</returns>
        private static string LookupKey(string table, Font font) => (table == Names ? EffectiveName(font).ToString(System.Globalization.CultureInfo.InvariantCulture) : FamilyStyle(font).ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>记录某次登记必须早于另一次登记的顺序约束；不把未知对象加入许可集合。</summary>
        /// <param name="edges">本次有限登记依赖图。</param>
        /// <param name="first">必须先登记的原字体 ID。</param>
        /// <param name="later">必须后登记的原字体 ID。</param>
        private static void AddEdge(Dictionary<ulong, HashSet<ulong>> edges, ulong first, ulong later)
        {
            if (!edges.ContainsKey(first)) edges[first] = new HashSet<ulong>();
            if (!edges.ContainsKey(later)) edges[later] = new HashSet<ulong>();
            edges[first].Add(later);
        }

        /// <summary>拓扑核验所有 first-wins 与弱槽顺序约束；拒绝互相矛盾的两表 winner。</summary>
        /// <param name="edges">只含本次原字体 ID 的有限图。</param>
        /// <returns>至少存在一个符合已审源码的登记顺序时返回 true。</returns>
        private static bool Acyclic(Dictionary<ulong, HashSet<ulong>> edges)
        {
            var remaining = edges.ToDictionary(pair => pair.Key, pair => pair.Value.Count);
            var ready = new Queue<ulong>(remaining.Where(pair => pair.Value == 0).Select(pair => pair.Key));
            int count = 0;
            while (ready.Count != 0)
            {
                ulong current = ready.Dequeue();
                count++;
                foreach (ulong parent in edges.Where(pair => pair.Value.Contains(current)).Select(pair => pair.Key))
                    if (--remaining[parent] == 0) ready.Enqueue(parent);
            }
            return count == edges.Count;
        }
    }
}
