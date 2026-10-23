/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  GenerateBuildDataAssemblyContract.cs
 * author:    taoye
 * created:   2026/10/19
 * descrip:   Generate 正常构建延迟加载的准确安装 DTO 程序集合同
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;

namespace NovaFramework.Editor
{
    /// <summary>只接受已审安装文件的真实加载状态；不加载程序集、不运行其构造器，不影响普通 Player。</summary>
    internal static class GenerateBuildDataAssemblyContract
    {
        internal const string RelativePath = "Resources/Scripting/Managed/BeeBuildProgramCommon.Data.dll";
        internal const string SourceSha256 = "e4b53cb1b1ea9bcafcfa91b6532cefd31c0172742c4ce813659cadf251db7fce";
        internal const string FullName = "BeeBuildProgramCommon.Data, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";
        internal const string Mvid = "69aa79d7-17c6-4a93-a1c7-1551d6687056";
        internal const string Row = "LoadedAssembly=" + FullName + "|" + Mvid;
        private const string Prefix = "LoadedAssembly=BeeBuildProgramCommon.Data,";

        /// <summary>单次真实读取的文件身份与加载数量；只持字符串/标量，不持 Assembly 或任何 Native 对象。</summary>
        [Serializable]
        internal sealed class Snapshot
        {
            /// <summary>当前 wire 版本；旧材料缺少该独立快照不获得有限加载许可。</summary>
            public int version = 1;
            /// <summary>同次回调包快照的实际域标识；跨域仅由正式重载调用点授权比较。</summary>
            public string domain;
            /// <summary>从受信 EditorApplication 取得并规范化的本次实际安装根；不接受 Action 请求路径。</summary>
            public string contentsPath;
            /// <summary>准确安装相对角色解析后的完整路径；禁止同名副本、链接及替代路径。</summary>
            public string path;
            /// <summary>完整安装 DLL 原文字节 SHA-256。</summary>
            public string sha256;
            /// <summary>完整安装 DLL 长度，必须为已审 4608 字节。</summary>
            public long length;
            /// <summary>已加载真实程序集完整名称；未加载时为 null。</summary>
            public string loadedFullName;
            /// <summary>已加载真实模块 MVID；未加载时为 null。</summary>
            public string loadedMvid;
            /// <summary>已加载实际 Location；未加载时为 null。</summary>
            public string loadedLocation;
            /// <summary>当前域所有同 simple-name 非动态程序集数量，只接受 0 或 1。</summary>
            public int loadedCount;
        }

        /// <summary>只读安装文件和已经加载的元数据，明确拒绝同名重复或捕获期间加载状态与原行不一致。</summary>
        /// <param name="domain">同次真实 packageCallbacks 域；不能借后来域覆盖原 after。</param>
        /// <param name="inputs">同次完整原 callback inputs，保持其全部行与原顺序。</param>
        /// <returns>准确真实加载状态；不调用 Assembly.Load、SDK、构造器或构建入口。</returns>
        internal static Snapshot Capture(string domain, string[] inputs)
        {
            string contents = Path.GetFullPath(EditorApplication.applicationContentsPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string installed = Path.GetFullPath(Path.Combine(contents, RelativePath));
            RequireUnlinkedFile(installed);
            byte[] bytes = File.ReadAllBytes(installed);
            string sha;
            using (var hash = SHA256.Create()) sha = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            var loaded = AppDomain.CurrentDomain.GetAssemblies().Where(value => !value.IsDynamic && value.GetName().Name == "BeeBuildProgramCommon.Data").ToArray();
            if (loaded.Length > 1) throw new InvalidOperationException("Generate DTO 程序集同名加载不唯一。");
            var assembly = loaded.SingleOrDefault();
            var result = new Snapshot
            {
                domain = domain, contentsPath = contents, path = installed, sha256 = sha, length = bytes.LongLength, loadedCount = loaded.Length,
                loadedFullName = assembly?.FullName, loadedMvid = assembly?.ManifestModule.ModuleVersionId.ToString(), loadedLocation = assembly?.Location,
            };
            RequireUnlinkedFile(installed);
            if (!ValidSnapshot(result, inputs)) throw new InvalidOperationException("Generate DTO 程序集文件、元数据或原加载行不符合准确合同。");
            return result;
        }

        /// <summary>验证固定版本的完整 DTO 与其原始排序行，不执行任何现场读取。</summary>
        /// <param name="snapshot">实际 Capture 的完整快照，或纯测试独立材料。</param>
        /// <param name="inputs">与该快照同次捕获的全部原行。</param>
        /// <returns>文件静态身份、唯一数量、准确加载位置及行存在性全部满足时为 true。</returns>
        internal static bool ValidSnapshot(Snapshot snapshot, string[] inputs)
        {
            if (snapshot == null || snapshot.version != 1 || string.IsNullOrEmpty(snapshot.domain) || !ValidPathRole(snapshot) || snapshot.sha256 != SourceSha256 || snapshot.length != 4608 ||
                inputs == null || inputs.Any(value => value == null) || !inputs.SequenceEqual(inputs.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))) return false;
            string[] rows = inputs.Where(value => value.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
            if (snapshot.loadedCount == 0) return snapshot.loadedFullName == null && snapshot.loadedMvid == null && snapshot.loadedLocation == null && rows.Length == 0;
            return snapshot.loadedCount == 1 && snapshot.loadedFullName == FullName && snapshot.loadedMvid == Mvid && snapshot.loadedLocation == snapshot.path && rows.Length == 1 && rows[0] == Row;
        }

        /// <summary>写前保持同域、同加载状态及完整准确文件绑定，不接纳提前加载。</summary>
        /// <param name="before">原 Plan 快照。</param><param name="current">实际重捕获的写前快照。</param>
        /// <returns>全部可序列化字段逐值相等且两个快照均满足固定静态形状时为 true。</returns>
        internal static bool BeforeEquals(Snapshot before, Snapshot current)
        {
            return before != null && current != null && ValidSnapshot(before, before.loadedCount == 1 ? new[] { Row } : Array.Empty<string>()) &&
                ValidSnapshot(current, current.loadedCount == 1 ? new[] { Row } : Array.Empty<string>()) && before.domain == current.domain && before.path == current.path && before.contentsPath == current.contentsPath && before.loadedCount == current.loadedCount;
        }

        /// <summary>仅本次正式 Generate 可把未加载的准确 DTO 变为已加载；warm 输入与较晚同域保持严格。</summary>
        /// <param name="before">真实 Plan 文件/加载前态。</param><param name="after">真实执行后文件/加载后态。</param><param name="current">本次实际同域复核材料。</param>
        /// <param name="beforeRows">完整原输入行。</param><param name="afterRows">完整真实 after 行。</param><param name="currentRows">完整真实 current 行。</param>
        /// <param name="error">不匹配原因，不返回原始行或配置内容。</param>
        /// <returns>唯一准许的插入或严格 warm 全等；不校验或改写原 callback 总哈希。</returns>
        internal static bool TryValidateAfter(Snapshot before, Snapshot after, Snapshot current, string[] beforeRows, string[] afterRows, string[] currentRows, out string error)
        {
            error = null;
            if (!ValidSnapshot(before, beforeRows) || !ValidSnapshot(after, afterRows) || !ValidSnapshot(current, currentRows) || before.domain != after.domain || after.domain != current.domain || before.path != after.path || after.path != current.path || before.contentsPath != after.contentsPath || after.contentsPath != current.contentsPath)
                error = "Generate DTO 程序集原三态文件、数量、加载位置或域不完整。";
            else if (!afterRows.SequenceEqual(currentRows, StringComparer.Ordinal) || after.loadedCount != current.loadedCount)
                error = "Generate DTO 程序集实际 after 到同域 current 漂移。";
            else if (before.loadedCount == after.loadedCount && !beforeRows.SequenceEqual(afterRows, StringComparer.Ordinal))
                error = "Generate DTO 程序集 warm 前态必须保持完整原行。";
            else if (before.loadedCount != after.loadedCount && (before.loadedCount != 0 || after.loadedCount != 1 || !beforeRows.Concat(new[] { Row }).OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(afterRows, StringComparer.Ordinal)))
                error = "Generate 只准准确 DTO 一行由 absent 变为唯一已加载；其它原行必须逐字保持。";
            return error == null;
        }

        /// <summary>新域仅在正式首次重载证明已通过后独立比较这一 DTO 的自然缺席或存在，绝不改原材料。</summary>
        /// <param name="after">Execute 已认证且完整保存的实际文件/加载后态。</param><param name="current">本次新域真实 Capture。</param><param name="afterRows">原域完整实际行。</param><param name="currentRows">新域完整实际行。</param>
        /// <param name="firstReloadApproved">唯一生产调用点已取得原 token/payload 的真实首次域许可；domain 不等本身不构成许可。</param>
        /// <param name="persistentCurrentRows">独立数组，仅在缺席时补入已认证准确行供持久义务比较，不替代当前 DTO。</param>
        /// <param name="error">缺失证明、元数据或其它行漂移的具体原因。</param>
        /// <returns>完整两态除这一准确行的域自然存在性外逐字相等时为 true。</returns>
        internal static bool TryGetReloadComparison(Snapshot after, Snapshot current, string[] afterRows, string[] currentRows, bool firstReloadApproved, out string[] persistentCurrentRows, out string error)
        {
            persistentCurrentRows = null; error = null;
            if (!firstReloadApproved || !ValidSnapshot(after, afterRows) || !ValidSnapshot(current, currentRows) || after.domain == current.domain || after.path != current.path || after.contentsPath != current.contentsPath || after.loadedCount == 0 && current.loadedCount != 0)
                error = "Generate DTO 新域比较缺少真实首次重载许可或完整原 after 文件身份。";
            else
            {
                string[] comparison = after.loadedCount == 1 && current.loadedCount == 0 ? currentRows.Concat(new[] { Row }).OrderBy(value => value, StringComparer.Ordinal).ToArray() : currentRows.ToArray();
                if (!afterRows.SequenceEqual(comparison, StringComparer.Ordinal)) error = "Generate 新域除准确 DTO 自然存在性外仍有程序集或输入漂移。";
                else persistentCurrentRows = comparison;
            }
            return error == null;
        }

        /// <summary>纯检查规范绝对安装根与准确相对角色，实际根的权威来源只在 Capture 中读取。</summary>
        /// <param name="snapshot">已冻结安装根与解析文件路径。</param><returns>无相对跳转、非规范路径或同名替代位置时为 true。</returns>
        private static bool ValidPathRole(Snapshot snapshot)
        {
            try
            {
                return !string.IsNullOrEmpty(snapshot.contentsPath) && Path.IsPathRooted(snapshot.contentsPath) &&
                    snapshot.contentsPath == Path.GetFullPath(snapshot.contentsPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) &&
                    snapshot.path == Path.GetFullPath(Path.Combine(snapshot.contentsPath, RelativePath));
            }
            catch (Exception error) when (error is ArgumentException || error is NotSupportedException || error is PathTooLongException) { return false; }
        }

        /// <summary>检查准确文件及每级现存父链，禁止链接和目录替代；不创建、修复或加载文件。</summary>
        /// <param name="path">必须既存的准确安装 DLL 路径。</param>
        private static void RequireUnlinkedFile(string path)
        {
            if (!File.Exists(path)) throw new InvalidOperationException("Generate DTO 安装文件缺失。");
            for (string current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("Generate DTO 安装文件或父链包含链接。");
        }
    }
}
