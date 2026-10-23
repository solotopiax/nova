/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.ImportantUpdates.Changelog.cs
 * author:    taoye
 * created:   2026/10/21
 * descrip:   已安装版本重要更新的 CHANGELOG 提取与严格语义版本比较
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        /// <summary>
        /// 已安装版本重要更新的内部工具，独立于远端新版本检查的比较规则。
        /// </summary>
        internal static partial class ImportantUpdates
        {
            /// <summary>
            /// 单个正式版本的原始重要更新正文。
            /// </summary>
            internal sealed class ReleaseNotes
            {
                /// <summary>
                /// CHANGELOG 标题中的完整语义版本，保留构建元数据。
                /// </summary>
                internal string Version;

                /// <summary>
                /// 仅移除外侧空白行及最终换行的 Markdown 正文。
                /// </summary>
                internal string Body;
            }

            /// <summary>
            /// 保留原始文本位置的一行，用于不改写正文换行和行内空白。
            /// </summary>
            private sealed class ChangelogLine
            {
                internal int Start;
                internal string Text;
            }

            /// <summary>
            /// 二级标题及其 Important 分类边界；无 Important 的历史节不要求严格格式。
            /// </summary>
            private sealed class ChangelogSection
            {
                internal string Heading;
                internal bool IsRoot;
                internal string VersionKey;
                internal int ImportantCount;
                internal int BodyStart;
                internal int BodyEnd;
            }

            /// <summary>
            /// 已验证的 SemVer 标识；数字使用字符串保存，避免整数溢出改变排序。
            /// </summary>
            private sealed class ReleaseVersion
            {
                internal string Major;
                internal string Minor;
                internal string Patch;
                internal string[] Prerelease;
            }

            /// <summary>
            /// 可识别的 Markdown ATX 标题，最多允许三个前导空格。
            /// </summary>
            private static readonly Regex s_ChangelogHeadingPattern = new Regex(
                @"^( {0,3})(#{1,6})(?:[ \t]+(.*)|[ \t]*)$", RegexOptions.CultureInvariant);

            /// <summary>
            /// 兼容识别历史无分隔空白的版本边界，防止后续 Important 被错误归入前一版。
            /// </summary>
            private static readonly Regex s_ChangelogLegacyHeadingPattern = new Regex(
                @"^ {0,3}##(\[.*)$", RegexOptions.CultureInvariant);

            /// <summary>
            /// 标题中的候选版本，用于发现有 Important 的版本重复出现。
            /// </summary>
            private static readonly Regex s_ChangelogVersionKeyPattern = new Regex(
                @"^\[([^\]\r\n]+)\]", RegexOptions.CultureInvariant);

            /// <summary>
            /// 带日期的正式版本标题；仅在该版本声明 Important 时强制使用。
            /// </summary>
            private static readonly Regex s_ChangelogReleasePattern = new Regex(
                @"^\[([^\]\r\n]+)\][ \t]+-[ \t]+([0-9]{4}-[0-9]{2}-[0-9]{2})$",
                RegexOptions.CultureInvariant);

            /// <summary>
            /// 完整 SemVer 结构；预发布纯数字标识的前导零另行校验。
            /// </summary>
            private static readonly Regex s_ReleaseVersionPattern = new Regex(
                @"\A(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(?:-([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?\z",
                RegexOptions.CultureInvariant);

            /// <summary>
            /// 按文件顺序提取正式版本下唯一的 Important 正文，忽略未发布节和空正文。
            /// 栅栏代码块与 HTML 注释中的标题不参与边界识别；重要版本格式错误或重复时明确拒绝。
            /// </summary>
            /// <param name="markdown">完整 CHANGELOG，可含 UTF-8 BOM、CRLF 或 LF。</param>
            /// <returns>保留版本原文与 Markdown 正文的正式版本列表。</returns>
            /// <exception cref="ArgumentNullException">输入为空引用。</exception>
            /// <exception cref="FormatException">Important 无归属、分类重复、版本重复或重要版本格式非法。</exception>
            internal static List<ReleaseNotes> ParseChangelog(string markdown)
            {
                if (markdown == null)
                    throw new ArgumentNullException(nameof(markdown));

                List<ChangelogLine> lines = ReadChangelogLines(markdown);
                List<ChangelogSection> sections = new List<ChangelogSection>();
                ChangelogSection current = null;
                bool inComment = false;
                char fenceCharacter = '\0';
                int fenceLength = 0;

                for (int index = 0; index < lines.Count; index++)
                {
                    string text = lines[index].Text;
                    if (index == 0 && text.Length > 0 && text[0] == '\uFEFF')
                        text = text.Substring(1);

                    // 代码块内的注释标记仅是正文，不能影响代码块外的 HTML 注释状态。
                    if (fenceLength > 0)
                    {
                        if (IsClosingChangelogFence(text, fenceCharacter, fenceLength))
                            fenceLength = 0;
                        continue;
                    }

                    // 栅栏信息段中的 HTML 标记也是代码元数据，不能开启代码块外的注释状态。
                    if (!inComment && TryOpenChangelogFence(text, out fenceCharacter, out fenceLength))
                        continue;
                    string visible = MaskChangelogComments(text, ref inComment);

                    Match heading = s_ChangelogHeadingPattern.Match(visible);
                    if (!heading.Success)
                    {
                        Match legacy = s_ChangelogLegacyHeadingPattern.Match(visible);
                        if (legacy.Success)
                        {
                            CloseImportantBody(current, index);
                            // 旧格式仅保留节边界；一旦声明 Important，后续校验必须拒绝其非法标题。
                            current = CreateChangelogSection(TrimChangelogHeading(legacy.Groups[1].Value), false);
                            sections.Add(current);
                        }
                        continue;
                    }

                    int level = heading.Groups[2].Length;
                    string title = TrimChangelogHeading(heading.Groups[3].Value);
                    bool isImportant = string.Equals(title, "Important", StringComparison.OrdinalIgnoreCase);
                    if (isImportant && (level != 3 || title != "Important" || current == null))
                        throw new FormatException("Important 必须使用有版本归属的三级标题：### Important。");

                    if (level <= 2)
                    {
                        CloseImportantBody(current, index);
                        current = null;
                        if (level == 2)
                        {
                            current = CreateChangelogSection(title, heading.Groups[1].Length == 0);
                            sections.Add(current);
                        }
                    }
                    else if (level == 3)
                    {
                        CloseImportantBody(current, index);
                        if (isImportant)
                        {
                            current.ImportantCount++;
                            current.BodyStart = index + 1;
                            current.BodyEnd = -1;
                        }
                    }
                }

                CloseImportantBody(current, lines.Count);
                return ExtractChangelogReleases(markdown, lines, sections);
            }

            /// <summary>
            /// 按完整 SemVer 比较两个正式版本或预发布版本；构建元数据不参与优先级。
            /// </summary>
            /// <param name="a">左侧严格语义版本。</param>
            /// <param name="b">右侧严格语义版本。</param>
            /// <returns>左侧较小返回负数，相同优先级返回零，较大返回正数。</returns>
            /// <exception cref="ArgumentNullException">任一版本为空引用。</exception>
            /// <exception cref="FormatException">任一版本不是合法 SemVer。</exception>
            internal static int CompareReleaseVersions(string a, string b)
            {
                ReleaseVersion left = ParseReleaseVersion(a, nameof(a));
                ReleaseVersion right = ParseReleaseVersion(b, nameof(b));
                int result = CompareVersionNumber(left.Major, right.Major);
                if (result == 0)
                    result = CompareVersionNumber(left.Minor, right.Minor);
                if (result == 0)
                    result = CompareVersionNumber(left.Patch, right.Patch);
                if (result != 0)
                    return result;
                if (left.Prerelease.Length == 0 || right.Prerelease.Length == 0)
                    return (left.Prerelease.Length == 0 ? 1 : 0).CompareTo(right.Prerelease.Length == 0 ? 1 : 0);

                int count = Math.Min(left.Prerelease.Length, right.Prerelease.Length);
                for (int index = 0; index < count; index++)
                {
                    string leftPart = left.Prerelease[index];
                    string rightPart = right.Prerelease[index];
                    bool leftNumber = IsReleaseVersionNumber(leftPart);
                    bool rightNumber = IsReleaseVersionNumber(rightPart);
                    result = leftNumber && rightNumber
                        ? CompareVersionNumber(leftPart, rightPart)
                        : leftNumber != rightNumber
                            ? (leftNumber ? -1 : 1)
                            : string.Compare(leftPart, rightPart, StringComparison.Ordinal);
                    if (result != 0)
                        return result;
                }

                return left.Prerelease.Length.CompareTo(right.Prerelease.Length);
            }

            /// <summary>
            /// 扫描原始行位置，保留 LF、CRLF 或 CR 的正文切片边界。
            /// </summary>
            private static List<ChangelogLine> ReadChangelogLines(string markdown)
            {
                List<ChangelogLine> lines = new List<ChangelogLine>();
                int position = 0;
                while (position < markdown.Length)
                {
                    int start = position;
                    while (position < markdown.Length && markdown[position] != '\r' && markdown[position] != '\n')
                        position++;
                    lines.Add(new ChangelogLine { Start = start, Text = markdown.Substring(start, position - start) });
                    if (position < markdown.Length && markdown[position++] == '\r' && position < markdown.Length && markdown[position] == '\n')
                        position++;
                }

                return lines;
            }

            /// <summary>
            /// 以等宽空格屏蔽 HTML 注释，避免注释中的伪标题及拼接标题被误识别。
            /// </summary>
            private static string MaskChangelogComments(string text, ref bool inComment)
            {
                if (!inComment && text.IndexOf("<!--", StringComparison.Ordinal) < 0)
                    return text;
                StringBuilder visible = new StringBuilder(text);
                int position = 0;
                while (position < text.Length)
                {
                    if (!inComment)
                    {
                        position = text.IndexOf("<!--", position, StringComparison.Ordinal);
                        if (position < 0)
                            break;
                        inComment = true;
                    }

                    int closing = text.IndexOf("-->", position, StringComparison.Ordinal);
                    int end = closing < 0 ? text.Length : closing + 3;
                    for (int index = position; index < end; index++)
                        visible[index] = ' ';
                    position = end;
                    inComment = closing < 0;
                }

                return visible.ToString();
            }

            /// <summary>
            /// 识别最多三个前导空格的反引号或波浪线代码栅栏；反引号信息段不能再含反引号。
            /// </summary>
            private static bool TryOpenChangelogFence(string text, out char character, out int length)
            {
                int start = 0;
                while (start < text.Length && text[start] == ' ')
                    start++;
                character = start < text.Length ? text[start] : '\0';
                length = 0;
                if (start > 3 || (character != '`' && character != '~'))
                    return false;
                while (start + length < text.Length && text[start + length] == character)
                    length++;
                if (length >= 3 && (character != '`' || text.IndexOf('`', start + length) < 0))
                    return true;
                length = 0;
                return false;
            }

            /// <summary>
            /// 仅允许相同字符、长度不短于起始栅栏且尾部为空白的代码块闭合行。
            /// </summary>
            private static bool IsClosingChangelogFence(string text, char character, int minimumLength)
            {
                if (!TryOpenChangelogFence(text, out char closingCharacter, out int closingLength) ||
                    closingCharacter != character || closingLength < minimumLength)
                    return false;
                int start = 0;
                while (start < text.Length && text[start] == ' ')
                    start++;
                for (int index = start + closingLength; index < text.Length; index++)
                {
                    if (text[index] != ' ' && text[index] != '\t')
                        return false;
                }

                return true;
            }

            /// <summary>
            /// 移除标题的尾部空白和可选闭合井号，不修改正文。
            /// </summary>
            private static string TrimChangelogHeading(string title)
            {
                string trimmed = title.TrimEnd(' ', '\t');
                int end = trimmed.Length;
                while (end > 0 && trimmed[end - 1] == '#')
                    end--;
                if (end < trimmed.Length && end > 0 && (trimmed[end - 1] == ' ' || trimmed[end - 1] == '\t'))
                    trimmed = trimmed.Substring(0, end).TrimEnd(' ', '\t');
                return trimmed;
            }

            /// <summary>
            /// 在下一个同级或更高标题处冻结 Important 正文结束位置。
            /// </summary>
            private static void CloseImportantBody(ChangelogSection section, int end)
            {
                if (section != null && section.ImportantCount > 0 && section.BodyEnd < 0)
                    section.BodyEnd = end;
            }

            /// <summary>
            /// 保存版本候选身份及标题合法性；历史格式也作为边界，但不能声明 Important。
            /// </summary>
            private static ChangelogSection CreateChangelogSection(string title, bool isRoot)
            {
                Match key = s_ChangelogVersionKeyPattern.Match(title);
                return new ChangelogSection
                {
                    Heading = title,
                    IsRoot = isRoot,
                    VersionKey = key.Success ? key.Groups[1].Value : null,
                    BodyEnd = -1
                };
            }

            /// <summary>
            /// 验证重要版本及重复声明，再切片提取非空正式正文；未发布正文不进入结果。
            /// </summary>
            private static List<ReleaseNotes> ExtractChangelogReleases(
                string markdown, List<ChangelogLine> lines, List<ChangelogSection> sections)
            {
                Dictionary<string, int> occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (ChangelogSection section in sections)
                {
                    if (section.VersionKey == null)
                        continue;
                    occurrences.TryGetValue(section.VersionKey, out int count);
                    occurrences[section.VersionKey] = count + 1;
                }

                List<ReleaseNotes> releases = new List<ReleaseNotes>();
                foreach (ChangelogSection section in sections)
                {
                    if (section.ImportantCount == 0)
                        continue;
                    if (section.ImportantCount > 1)
                        throw new FormatException($"CHANGELOG 版本 [{section.VersionKey}] 重复声明 Important。");
                    if (section.VersionKey != null && occurrences[section.VersionKey] > 1)
                        throw new FormatException($"CHANGELOG 含 Important 的版本 [{section.VersionKey}] 重复出现。");
                    if (!section.IsRoot)
                        throw new FormatException("含 Important 的版本必须使用根级二级标题。");
                    if (string.Equals(section.VersionKey, "Unreleased", StringComparison.OrdinalIgnoreCase))
                        continue;

                    Match header = s_ChangelogReleasePattern.Match(section.Heading);
                    if (!header.Success || !DateTime.TryParseExact(header.Groups[2].Value, "yyyy-MM-dd",
                            CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                        throw new FormatException($"含 Important 的版本标题必须为 ## [版本] - YYYY-MM-DD：{section.Heading}");
                    string version = header.Groups[1].Value;
                    ParseReleaseVersion(version, nameof(markdown));
                    string body = SliceImportantBody(markdown, lines, section.BodyStart, section.BodyEnd);
                    if (body.Length > 0)
                        releases.Add(new ReleaseNotes { Version = version, Body = body });
                }

                return releases;
            }

            /// <summary>
            /// 只删除正文外侧空白行与最终换行，内部换行、缩进及 Markdown 原样保留。
            /// </summary>
            private static string SliceImportantBody(string markdown, List<ChangelogLine> lines, int start, int end)
            {
                while (start < end && string.IsNullOrWhiteSpace(lines[start].Text))
                    start++;
                while (end > start && string.IsNullOrWhiteSpace(lines[end - 1].Text))
                    end--;
                if (start == end)
                    return string.Empty;
                ChangelogLine last = lines[end - 1];
                return markdown.Substring(lines[start].Start, last.Start + last.Text.Length - lines[start].Start);
            }

            /// <summary>
            /// 严格验证 SemVer 的核心段、预发布与构建标识，不接受简写、前缀、空白或数字前导零。
            /// </summary>
            private static ReleaseVersion ParseReleaseVersion(string version, string parameterName)
            {
                if (version == null)
                    throw new ArgumentNullException(parameterName);
                Match match = s_ReleaseVersionPattern.Match(version);
                if (!match.Success)
                    throw new FormatException($"非法 SemVer 版本：{version}");
                string[] prerelease = match.Groups[4].Success ? match.Groups[4].Value.Split('.') : Array.Empty<string>();
                foreach (string identifier in prerelease)
                {
                    if (identifier.Length > 1 && identifier[0] == '0' && IsReleaseVersionNumber(identifier))
                        throw new FormatException($"SemVer 预发布数字标识不能含前导零：{version}");
                }

                return new ReleaseVersion
                {
                    Major = match.Groups[1].Value,
                    Minor = match.Groups[2].Value,
                    Patch = match.Groups[3].Value,
                    Prerelease = prerelease
                };
            }

            /// <summary>
            /// 判断已通过标识语法验证的字符串是否完全由 ASCII 数字组成。
            /// </summary>
            private static bool IsReleaseVersionNumber(string identifier)
            {
                foreach (char character in identifier)
                {
                    if (character < '0' || character > '9')
                        return false;
                }

                return true;
            }

            /// <summary>
            /// 比较没有前导零的非负整数字符串，不受整数类型上限影响。
            /// </summary>
            private static int CompareVersionNumber(string a, string b)
            {
                int length = a.Length.CompareTo(b.Length);
                return length == 0 ? string.Compare(a, b, StringComparison.Ordinal) : length;
            }
        }
    }
}
