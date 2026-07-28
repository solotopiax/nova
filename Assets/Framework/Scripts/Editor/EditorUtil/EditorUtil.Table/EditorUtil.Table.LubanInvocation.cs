/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Table.LubanInvocation.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   Table Luban CLI 结构化参数模型与构建器
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using NovaFramework.Runtime;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Table
        {
            /// <summary>
            /// 保存一次 Luban 调用的独立参数 Token，并仅在进程边界执行平台无关的安全引用。
            /// </summary>
            public sealed class LubanInvocation
            {
                /// <summary>
                /// 创建结构化 Luban 调用并复制参数，避免后续拼接引入空格或引号歧义。
                /// </summary>
                /// <param name="arguments">不包含可执行文件路径的参数 Token。</param>
                public LubanInvocation(IEnumerable<string> arguments)
                {
                    Arguments = new ReadOnlyCollection<string>(new List<string>(arguments ?? Array.Empty<string>()));
                }

                public IReadOnlyList<string> Arguments { get; }

                /// <summary>
                /// 将参数 Token 渲染为 ProcessRunner 当前接受的命令行字符串。
                /// </summary>
                /// <returns>每个必要 Token 已单独引用和转义的命令行。</returns>
                public string ToCommandLine()
                {
                    var builder = new StringBuilder();
                    for (int i = 0; i < Arguments.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(' ');
                        }

                        AppendQuotedArgument(builder, Arguments[i] ?? string.Empty);
                    }

                    return builder.ToString();
                }

                /// <summary>
                /// 按 Process 参数规则写入单个 Token；包含空白或引号时进行双引号引用。
                /// </summary>
                /// <param name="builder">接收渲染结果的字符串构建器。</param>
                /// <param name="argument">原始参数 Token。</param>
                private static void AppendQuotedArgument(StringBuilder builder, string argument)
                {
                    bool requiresQuotes = argument.Length == 0 || argument.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '"' }) >= 0;
                    if (!requiresQuotes)
                    {
                        builder.Append(argument);
                        return;
                    }

                    builder.Append('"');
                    int backslashCount = 0;
                    foreach (char character in argument)
                    {
                        if (character == '\\')
                        {
                            backslashCount++;
                            continue;
                        }

                        if (character == '"')
                        {
                            builder.Append('\\', backslashCount * 2 + 1);
                            builder.Append('"');
                            backslashCount = 0;
                            continue;
                        }

                        builder.Append('\\', backslashCount);
                        backslashCount = 0;
                        builder.Append(character);
                    }

                    builder.Append('\\', backslashCount * 2);
                    builder.Append('"');
                }
            }

            /// <summary>
            /// 按稳定顺序构建 Luban 的重复代码目标、数据目标与通用筛选参数。
            /// </summary>
            public sealed class LubanInvocationBuilder
            {
                private readonly List<string> m_CodeTargets = new List<string>();
                private readonly List<string> m_DataTargets = new List<string>();
                private readonly List<string> m_Tags = new List<string>();
                private readonly List<string> m_ExcludeTags = new List<string>();
                private readonly List<string> m_Variants = new List<string>();
                private readonly List<string> m_CustomTemplateDirectories = new List<string>();
                private readonly List<KeyValuePair<string, string>> m_ExtraArguments = new List<KeyValuePair<string, string>>();
                private string m_ConfigFile = string.Empty;
                private string m_Target = string.Empty;

                /// <summary>
                /// 设置本次调用使用的官方 Luban Project 配置文件。
                /// </summary>
                /// <param name="configFile">luban.conf 路径。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithConfigFile(string configFile)
                {
                    m_ConfigFile = configFile ?? string.Empty;
                    return this;
                }

                /// <summary>
                /// 设置 luban.conf 中本次调用使用的唯一 target。
                /// </summary>
                /// <param name="target">Luban Project target 名称。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithTarget(string target)
                {
                    m_Target = target ?? string.Empty;
                    return this;
                }

                /// <summary>
                /// 追加 Profile 中声明的所有代码和数据目标。
                /// </summary>
                /// <param name="profile">待展开的导出 Profile。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithProfile(TableExportProfileSetting profile)
                {
                    if (profile == null)
                    {
                        throw new ArgumentNullException(nameof(profile));
                    }

                    m_CodeTargets.AddRange(profile.CodeTargets ?? new List<string>());
                    m_DataTargets.AddRange(profile.DataTargets ?? new List<string>());
                    return this;
                }

                /// <summary>
                /// 追加一个 Luban 代码生成目标，允许同次调用重复出现 -c。
                /// </summary>
                /// <param name="target">代码生成目标名。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithCodeTarget(string target)
                {
                    m_CodeTargets.Add(target);
                    return this;
                }

                /// <summary>
                /// 追加一个 Luban 数据生成目标，允许同次调用重复出现 -d。
                /// </summary>
                /// <param name="target">数据生成目标名。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithDataTarget(string target)
                {
                    m_DataTargets.Add(target);
                    return this;
                }

                /// <summary>
                /// 追加一个 Luban tag 过滤值。
                /// </summary>
                /// <param name="tag">tag 值。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithTag(string tag)
                {
                    m_Tags.Add(tag);
                    return this;
                }

                /// <summary>
                /// 追加一个 Luban 排除 tag 过滤值。
                /// </summary>
                /// <param name="tag">需要从本次导出排除的 tag 值。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithExcludeTag(string tag)
                {
                    m_ExcludeTags.Add(tag);
                    return this;
                }

                /// <summary>
                /// 追加一个 Luban variant 过滤值。
                /// </summary>
                /// <param name="variant">variant 值。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithVariant(string variant)
                {
                    m_Variants.Add(variant);
                    return this;
                }

                /// <summary>
                /// 追加一个 Luban 自定义模板根目录，允许按优先级重复传入。
                /// </summary>
                /// <param name="directory">包含代码 target 子目录的模板根目录。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithCustomTemplateDirectory(string directory)
                {
                    m_CustomTemplateDirectories.Add(directory);
                    return this;
                }

                /// <summary>
                /// 追加一个 Luban -x 扩展参数，名称与值在进程边界前保持结构化。
                /// </summary>
                /// <param name="name">扩展参数名。</param>
                /// <param name="value">扩展参数值。</param>
                /// <returns>当前构建器。</returns>
                public LubanInvocationBuilder WithExtraArgument(string name, string value)
                {
                    m_ExtraArguments.Add(new KeyValuePair<string, string>(name, value));
                    return this;
                }

                /// <summary>
                /// 生成不可变调用参数，参数顺序固定为目标、配置、过滤条件和扩展参数。
                /// </summary>
                /// <returns>可直接交给 Table CLI Runner 的结构化调用。</returns>
                public LubanInvocation Build()
                {
                    var arguments = new List<string>();
                    AppendRepeated(arguments, "-c", m_CodeTargets);
                    AppendRepeated(arguments, "-d", m_DataTargets);
                    if (!string.IsNullOrWhiteSpace(m_ConfigFile))
                    {
                        arguments.Add("--conf");
                        arguments.Add(m_ConfigFile);
                    }
                    if (!string.IsNullOrWhiteSpace(m_Target))
                    {
                        arguments.Add("-t");
                        arguments.Add(m_Target);
                    }
                    AppendRepeated(arguments, "-i", m_Tags);
                    AppendRepeated(arguments, "-e", m_ExcludeTags);
                    AppendRepeated(arguments, "--variant", m_Variants);
                    AppendRepeated(arguments, "--customTemplateDir", m_CustomTemplateDirectories);
                    foreach (KeyValuePair<string, string> pair in m_ExtraArguments)
                    {
                        arguments.Add("-x");
                        arguments.Add($"{pair.Key}={pair.Value}");
                    }

                    return new LubanInvocation(arguments);
                }

                /// <summary>
                /// 将一组值展开为重复的选项和值 Token。
                /// </summary>
                /// <param name="arguments">接收结果的参数列表。</param>
                /// <param name="option">每个值前使用的选项。</param>
                /// <param name="values">按原顺序展开的值。</param>
                private static void AppendRepeated(List<string> arguments, string option, IEnumerable<string> values)
                {
                    foreach (string value in values)
                    {
                        arguments.Add(option);
                        arguments.Add(value);
                    }
                }
            }
        }
    }
}
