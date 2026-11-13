/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.UIBindings.Generation.cs
 * author:    taoye
 * created:   2026/11/12
 * descrip:   可选 UIView partial 字段生成、归属保护与编译前后边界
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using Path = System.IO.Path;
using ReflectionField = System.Reflection.FieldInfo;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class UIBindings
        {
            /// <summary>只读预演可选 partial 字段源码；不改业务文件、不导入、不编译，也不绑定 Prefab。</summary>
            /// <param name="request">精确业务 UIView、待声明字段与真实目标对象；TypeName 是字段声明类型。</param>
            /// <returns>准确输出路径、完整源码、受保护来源与无变化状态。</returns>
            /// <exception cref="InvalidOperationException">非 partial、成员冲突、无归属或已手改的生成文件均明确拒绝。</exception>
            public static GenerationPlan PreviewGeneration(BindingRequest request)
            {
                BindingRequest input = CopyRequest(request);
                Context context = Resolve(input);
                AssertWritable(context);
                Type owner = context.View.GetType();
                if (owner.IsNested || owner.IsGenericType || !ValidIdentifier(owner.Name))
                    throw new InvalidOperationException("可选生成仅支持非嵌套、非泛型的普通业务 UIView；原有脚本和手动绑定照常可用。 ");
                string output = (Path.GetDirectoryName(context.ScriptPath) + "/" + owner.Name + ".UIBindings.g.cs").Replace('\\', '/');
                string absolute = OwnedAbsolute(output);
                if (output == context.ScriptPath || File.Exists(absolute + ".tmp"))
                    throw new InvalidOperationException("生成目标与来源冲突或已有未完成临时文件。 ");
                if (!File.Exists(absolute) && File.Exists(absolute + ".meta"))
                    throw new InvalidOperationException("生成位置保留了无对应源码的元数据，无法证明归属，拒绝接管：" + output);
                if (File.Exists(absolute) && ((File.GetAttributes(absolute) & FileAttributes.ReadOnly) != 0 || !AssetDatabase.IsOpenForEdit(output)))
                    throw new InvalidOperationException("生成文件当前不可写。 ");

                SourceSnapshot[] sources = CaptureSources(context, output);
                var businessSources = new List<string>();
                foreach (SourceSnapshot source in sources.Where(value => value.Path.EndsWith(".cs", StringComparison.Ordinal)))
                {
                    string code = StripCommentsAndLiterals(File.ReadAllText(Absolute(source.Path)));
                    if (!Regex.IsMatch(code, @"\b(?:class|struct|interface|enum|record)\s+" + Regex.Escape(owner.Name) + @"\b")) continue;
                    ValidatePartialDeclaration(code, owner, source.Path);
                    businessSources.Add(code);
                }
                if (businessSources.Count == 0)
                    throw new InvalidOperationException("无法从当前业务程序集核实实际 UIView 声明，不能生成源码。 ");
                string primary = StripCommentsAndLiterals(File.ReadAllText(Absolute(context.ScriptPath)));
                ValidatePartialDeclaration(primary, owner, context.ScriptPath);

                GeneratedField[] previous = ReadGenerated(context, output);
                var fields = previous.ToDictionary(field => field.Name, StringComparer.Ordinal);
                foreach (Binding binding in input.Bindings)
                {
                    Type type = BindingType(binding.TypeName);
                    BindingTarget(context, binding, type);
                    if (fields.TryGetValue(binding.FieldName, out GeneratedField existing))
                    {
                        if (existing.Type != type) throw new InvalidOperationException("已有生成字段不能换类型：" + binding.FieldName);
                        continue;
                    }
                    for (Type current = owner; current != null; current = current.BaseType)
                        if (current.GetMember(binding.FieldName, MemberFlags).Length != 0)
                            throw new InvalidOperationException("字段名与既有成员冲突：" + binding.FieldName + "；请保留原声明并走 Preview/Apply，或明确选择其他新字段名。 ");
                    if (businessSources.Any(code => Regex.IsMatch(code, @"\b" + Regex.Escape(binding.FieldName) + @"\b")))
                        throw new InvalidOperationException("业务源码已使用同名标识符，无法证明没有声明冲突：" + binding.FieldName + "；请换新字段名，或手动声明并编译后直接绑定。 ");
                    fields.Add(binding.FieldName, new GeneratedField { Name = binding.FieldName, Type = type });
                }
                GeneratedField[] all = fields.Values.OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();
                if (all.Length > 128) throw new InvalidOperationException("单个 UIView 的工具生成字段最多一百二十八个；请保留业务手动声明并分批绑定。 ");
                string body = GeneratedBody(owner, all);
                string ownerKey = AssetDatabase.AssetPathToGUID(context.ScriptPath) + "\t" + owner.AssemblyQualifiedName;
                string schema = string.Join("\n", all.Select(field => field.Name + "\t" + field.Type.AssemblyQualifiedName));
                string text = "// <nova-ui-bindings owner=\"" + Encode(ownerKey) + "\" fields=\"" + Encode(schema) + "\" sha256=\"" + HashText(body) + "\" />\n" + body;
                return new GenerationPlan
                {
                    Before = Capture(context), Input = input, Fields = all, Path = output, Text = text,
                    ExistingHash = FileHash(absolute), ExistingMetaHash = FileHash(absolute + ".meta"), ProtectedSources = sources,
                };
            }

            /// <summary>只写已预演的一个归属文件；不触发 Unity 导入或编译，调用方必须完成编译重载后重新预演绑定。</summary>
            /// <param name="plan">PreviewGeneration 返回的未消费计划。</param>
            /// <returns>源码字节回执；RequiresCompilation 始终为 true，不能据此宣称编译或绑定完成。</returns>
            /// <exception cref="InvalidOperationException">任意保护来源、字段、输出或元数据已漂移。</exception>
            /// <exception cref="AggregateException">写入和恢复均失败；Data 的 RecoveryPathDataKey 保存恢复目录。</exception>
            public static GenerationReceipt Generate(GenerationPlan plan)
            {
                if (plan == null) throw new ArgumentNullException(nameof(plan));
                if (plan.Consumed != 0) throw new InvalidOperationException("生成计划已消费，请重新 PreviewGeneration；不能重放。 ");
                plan.Consumed = 1;
                Context context = Resolve(plan.Input);
                AssertWritable(context);
                AssertSnapshot(context, plan.Before);
                GenerationPlan current = PreviewGeneration(plan.Input);
                if (current.Path != plan.Path || current.Text != plan.Text || current.ExistingHash != plan.ExistingHash ||
                    current.ExistingMetaHash != plan.ExistingMetaHash || !SourcesEqual(current.ProtectedSources, plan.ProtectedSources))
                    throw new InvalidOperationException("生成来源、程序集或输出已变化，请重新 PreviewGeneration。 ");
                bool changed = current.HasChanges;
                if (changed)
                {
                    string absolute = OwnedAbsolute(plan.Path);
                    string recovery = Path.Combine(ProjectRoot(), "Library", "Nova", "UIBindings", Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(recovery);
                    string backup = Path.Combine(recovery, "before.cs");
                    bool existed = File.Exists(absolute);
                    try { if (existed) File.Copy(absolute, backup, false); }
                    catch { Directory.Delete(recovery, true); throw; }
                    try
                    {
                        WriteAtomic(absolute, plan.Text);
                        if (FileHash(absolute) != HashText(plan.Text)) throw new IOException("生成文件落盘摘要不一致。 ");
                        Directory.Delete(recovery, true);
                    }
                    catch (Exception failure)
                    {
                        try
                        {
                            if (existed) WriteAtomicBytes(absolute, File.ReadAllBytes(backup));
                            else if (File.Exists(absolute)) File.Delete(absolute);
                            if (FileHash(absolute) != plan.ExistingHash || FileHash(absolute + ".meta") != plan.ExistingMetaHash)
                                throw new IOException("原生成文件或元数据未完整恢复。 ");
                            Directory.Delete(recovery, true);
                        }
                        catch (Exception restoreFailure)
                        {
                            var aggregate = new AggregateException("生成失败且恢复不完整，请保留恢复目录：" + recovery, failure, restoreFailure);
                            aggregate.Data[RecoveryPathDataKey] = recovery;
                            throw aggregate;
                        }
                        throw;
                    }
                }
                return new GenerationReceipt
                {
                    OutputPath = plan.Path, OutputHash = FileHash(Absolute(plan.Path)), ScriptPath = context.ScriptPath,
                    ScriptHash = FileHash(Absolute(context.ScriptPath)), ScriptGuid = AssetDatabase.AssetPathToGUID(context.ScriptPath),
                    Changed = changed, RequiresCompilation = true, Sources = CopySources(plan.ProtectedSources),
                };
            }

            /// <summary>只读核验源码归属、完整输出和受保护来源；不把此结果当作编译、重载或绑定证据。</summary>
            /// <param name="receipt">Generate 返回的准确源码回执。</param>
            /// <returns>文件归属、完整字节和全部保护来源仍匹配时为 true。</returns>
            public static bool VerifyGeneration(GenerationReceipt receipt)
            {
                try
                {
                    if (receipt == null || string.IsNullOrEmpty(receipt.OutputHash) || receipt.Sources == null || receipt.Sources.Length == 0 ||
                        FileHash(OwnedAbsolute(receipt.OutputPath)) != receipt.OutputHash ||
                        FileHash(OwnedAbsolute(receipt.ScriptPath)) != receipt.ScriptHash ||
                        AssetDatabase.AssetPathToGUID(receipt.ScriptPath) != receipt.ScriptGuid) return false;
                    MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(receipt.ScriptPath);
                    Type type = script == null ? null : script.GetClass();
                    if (type == null) return false;
                    var context = new Context { ScriptPath = receipt.ScriptPath, Script = script };
                    ReadGenerated(context, receipt.OutputPath);
                    return SourcesEqual(CaptureSources(context, receipt.OutputPath), receipt.Sources);
                }
                catch (Exception) { return false; }
            }

            /// <summary>枚举实际业务程序集当前源码及定义文件，拒绝跨工程来源，生成目标独立保护。</summary>
            private static SourceSnapshot[] CaptureSources(Context context, string output)
            {
                Type type = context.Script.GetClass();
                string name = type.Assembly.GetName().Name;
                UnityEditor.Compilation.Assembly[] owners = CompilationPipeline.GetAssemblies(AssembliesType.Editor).Where(value => value.name == name).ToArray();
                if (owners.Length != 1) throw new InvalidOperationException("无法唯一定位业务 UIView 的实际编译程序集。 ");
                var paths = new HashSet<string>(StringComparer.Ordinal) { context.ScriptPath };
                foreach (string source in owners[0].sourceFiles)
                {
                    string path = ProjectPath(source);
                    if (path != output) paths.Add(path);
                }
                string definition = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(name);
                if (!string.IsNullOrEmpty(definition)) paths.Add(ProjectPath(definition));
                return paths.OrderBy(path => path, StringComparer.Ordinal).Select(path =>
                {
                    string absolute = Absolute(path);
                    if (!File.Exists(absolute)) throw new InvalidOperationException("业务程序集有尚未导入或已移除的源码，请先完成编译：" + path);
                    return new SourceSnapshot { Path = path, Hash = FileHash(absolute), MetaHash = FileHash(absolute + ".meta") };
                }).ToArray();
            }

            /// <summary>绑定也冻结生成 partial；即使来源尚未重新导入，也不能依据旧编译字段写 Prefab。</summary>
            private static SourceSnapshot[] CaptureBindingSources(Context context)
            {
                var sources = CaptureSources(context, null).ToList();
                string generated = (Path.GetDirectoryName(context.ScriptPath) + "/" + context.View.GetType().Name + ".UIBindings.g.cs").Replace('\\', '/');
                if (!sources.Any(value => value.Path == generated)) sources.Add(new SourceSnapshot
                {
                    Path = generated, Hash = FileHash(Absolute(generated)), MetaHash = FileHash(Absolute(generated) + ".meta"),
                });
                return sources.OrderBy(value => value.Path, StringComparer.Ordinal).ToArray();
            }

            /// <summary>本工具的生成文件必须已成为真实序列化字段；即使只绑定旧字段，也不跳过生成后的编译阶段。</summary>
            private static void AssertOwnedGeneratedCompiled(Context context)
            {
                string output = (Path.GetDirectoryName(context.ScriptPath) + "/" + context.View.GetType().Name + ".UIBindings.g.cs").Replace('\\', '/');
                string absolute = Absolute(output);
                if (!File.Exists(absolute) || !File.ReadAllText(absolute).StartsWith("// <nova-ui-bindings", StringComparison.Ordinal)) return;
                ReflectionField[] fields = BusinessFields(context.View.GetType()).ToArray();
                using (var serialized = new SerializedObject(context.View))
                    foreach (GeneratedField generated in ReadGenerated(context, output))
                    {
                        ReflectionField[] matches = fields.Where(field => field.Name == generated.Name).ToArray();
                        if (matches.Length != 1 || matches[0].FieldType != generated.Type || !IsObjectField(matches[0]) ||
                            serialized.FindProperty(generated.Name)?.propertyType != SerializedPropertyType.ObjectReference)
                            throw new InvalidOperationException("生成字段尚未完成真实编译和重载，请先导入并编译后重新 Preview：" + generated.Name);
                    }
            }

            /// <summary>将当前业务程序集的实际源路径规整到工程内，不能扫描维护仓或通过链接读其他工程。</summary>
            private static string ProjectPath(string path)
            {
                string absolute = Path.GetFullPath(Path.IsPathRooted(path) ? path : Absolute(path));
                string root = ProjectRoot() + Path.DirectorySeparatorChar;
                if (!absolute.StartsWith(root, StringComparison.Ordinal)) throw new InvalidOperationException("业务程序集包含工程外来源，本工具不扫描或修改：" + Path.GetFileName(path));
                string relative = absolute.Substring(root.Length).Replace('\\', '/');
                if (!relative.StartsWith("Assets/", StringComparison.Ordinal))
                    throw new InvalidOperationException("业务程序集定义或源码不在当前项目 Assets 中，本工具不扫描其他包。 ");
                for (string current = absolute; current != null && current != ProjectRoot(); current = Path.GetDirectoryName(current))
                    if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidOperationException("业务程序集来源不能经过符号链接。 ");
                return relative;
            }

            /// <summary>按顺序比较路径、源码及元数据，不借用旧编译程序集中的成员替代当前输入快照。</summary>
            private static bool SourcesEqual(SourceSnapshot[] left, SourceSnapshot[] right) => left != null && right != null && left.Length == right.Length &&
                left.Zip(right, (a, b) => a != null && b != null && a.Path == b.Path && a.Hash == b.Hash && a.MetaHash == b.MetaHash).All(value => value);

            /// <summary>返回来源快照的深复制，调用方无法改写单次计划内部保护集。</summary>
            private static SourceSnapshot[] CopySources(SourceSnapshot[] sources) => sources?.Select(value => new SourceSnapshot
                { Path = value.Path, Hash = value.Hash, MetaHash = value.MetaHash }).ToArray();

            /// <summary>只接受可清楚核实的顶层 partial 声明；不自动改写非 partial 或复杂条件编译的业务源码。</summary>
            private static void ValidatePartialDeclaration(string code, Type owner, string path)
            {
                if (code.IndexOf('#') >= 0) throw new InvalidOperationException("业务 UIView 声明含条件编译，本工具不推测有效声明：" + path);
                Match[] namespaces = Regex.Matches(code, @"\bnamespace\s+([A-Za-z_][A-Za-z0-9_]*(?:\s*\.\s*[A-Za-z_][A-Za-z0-9_]*)*)\s*([;{])").Cast<Match>().ToArray();
                string expected = owner.Namespace ?? string.Empty;
                if (namespaces.Length > 1 || (namespaces.Length == 0 ? string.Empty : Regex.Replace(namespaces[0].Groups[1].Value, @"\s", string.Empty)) != expected)
                    throw new InvalidOperationException("无法精确核实业务 UIView 的命名空间；请手动声明字段并编译后绑定：" + path);
                Match[] declarations = Regex.Matches(code, @"(?<modifiers>(?:\b(?:public|internal|protected|private|abstract|sealed|static|partial|new)\s+)*)\bclass\s+" + Regex.Escape(owner.Name) + @"\b").Cast<Match>().ToArray();
                if (declarations.Length != 1 || !Regex.IsMatch(declarations[0].Groups["modifiers"].Value, @"\bpartial\b"))
                    throw new InvalidOperationException("可选生成要求业务类预先声明 partial；请由业务方仅为该类加 partial，或保留原脚本并使用既有字段绑定：" + path);
                int depth = 0;
                for (int index = 0; index < declarations[0].Index; index++)
                {
                    if (code[index] == '{') depth++;
                    if (code[index] == '}') depth--;
                }
                int expectedDepth = namespaces.Length == 1 && namespaces[0].Groups[2].Value == "{" ? 1 : 0;
                if (depth != expectedDepth) throw new InvalidOperationException("不为嵌套业务类型自动生成 partial：" + path);
            }

            /// <summary>去除注释、字符和字符串正文但保留换行及代码位置，避免把文字中的 class 或 partial 当作声明。</summary>
            private static string StripCommentsAndLiterals(string source)
            {
                var result = source.ToCharArray();
                for (int index = 0; index < source.Length; index++)
                {
                    int start = index;
                    if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '/')
                    {
                        while (index < source.Length && source[index] != '\n') index++;
                    }
                    else if (source[index] == '/' && index + 1 < source.Length && source[index + 1] == '*')
                    {
                        index += 2;
                        while (index + 1 < source.Length && !(source[index] == '*' && source[index + 1] == '/')) index++;
                        index = Math.Min(source.Length, index + 2);
                    }
                    else if (source[index] == '"' || source[index] == '\'')
                    {
                        char quote = source[index++];
                        bool verbatim = start > 0 && source[start - 1] == '@';
                        while (index < source.Length)
                        {
                            if (!verbatim && source[index] == '\\') { index += Math.Min(2, source.Length - index); continue; }
                            if (source[index] == quote)
                            {
                                if (verbatim && index + 1 < source.Length && source[index + 1] == quote) { index += 2; continue; }
                                index++;
                                break;
                            }
                            index++;
                        }
                    }
                    else continue;
                    for (int cursor = start; cursor < index; cursor++) if (result[cursor] != '\n' && result[cursor] != '\r') result[cursor] = ' ';
                    index--;
                }
                return new string(result);
            }

            /// <summary>读取并重建本工具的归属文件；标记、owner、字段模式、摘要或正文不符均禁止覆盖。</summary>
            private static GeneratedField[] ReadGenerated(Context context, string output)
            {
                string absolute = OwnedAbsolute(output);
                if (!File.Exists(absolute)) return Array.Empty<GeneratedField>();
                string text = File.ReadAllText(absolute);
                int newline = text.IndexOf('\n');
                if (newline < 0) throw new InvalidOperationException("目标文件没有有效生成归属，拒绝覆盖：" + output);
                Match marker = Regex.Match(text.Substring(0, newline), "^// <nova-ui-bindings owner=\"(?<owner>[A-Za-z0-9+/=]+)\" fields=\"(?<fields>[A-Za-z0-9+/=]+)\" sha256=\"(?<hash>[0-9a-f]{64})\" />$");
                Type owner = context.Script.GetClass();
                string ownerKey = AssetDatabase.AssetPathToGUID(context.ScriptPath) + "\t" + owner.AssemblyQualifiedName;
                string body = text.Substring(newline + 1);
                if (!marker.Success || Decode(marker.Groups["owner"].Value) != ownerKey || HashText(body) != marker.Groups["hash"].Value)
                    throw new InvalidOperationException("目标文件无本工具有效归属或正文已手改，拒绝覆盖：" + output);
                var fields = new List<GeneratedField>();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (string line in Decode(marker.Groups["fields"].Value).Split('\n'))
                {
                    string[] values = line.Split('\t');
                    if (values.Length != 2 || !ValidIdentifier(values[0]) || SystemField(values[0]) || !names.Add(values[0]))
                        throw new InvalidOperationException("生成文件的字段模式已损坏：" + output);
                    fields.Add(new GeneratedField { Name = values[0], Type = BindingType(values[1]) });
                }
                if (fields.Count == 0 || fields.Count > 128 || GeneratedBody(owner, fields.ToArray()) != body)
                    throw new InvalidOperationException("生成文件正文与归属字段模式不一致，拒绝覆盖：" + output);
                return fields.ToArray();
            }

            /// <summary>生成只包含字段的 partial；不含运行时自动查找、整数索引、生命周期覆盖或事件操作。</summary>
            private static string GeneratedBody(Type owner, GeneratedField[] fields)
            {
                var text = new StringBuilder();
                text.Append("/***************************************************************\n * (c) copyright 2026 - 2030, Solotopia\n * All Rights Reserved.\n * -------------------------------------------------------------\n * filename:  ").Append(owner.Name).Append(".UIBindings.g.cs\n * author:    taoye\n * created:   2026/11/12\n * descrip:   可选控件引用字段；由 UIBindings 维护，不放置业务逻辑\n ***************************************************************/\n\n");
                string indent = string.IsNullOrEmpty(owner.Namespace) ? string.Empty : "    ";
                if (indent.Length != 0) text.Append("namespace ").Append(owner.Namespace).Append("\n{\n");
                text.Append(indent).Append("/// <summary>可选生成的控件引用；业务事件和生命周期保留在业务源码中。</summary>\n");
                text.Append(indent).Append(owner.IsPublic ? "public" : "internal").Append(" partial class ").Append(owner.Name).Append("\n").Append(indent).Append("{\n");
                foreach (GeneratedField field in fields)
                    text.Append(indent).Append("    /// <summary>由精确 Prefab 绑定计划赋值的控件引用。</summary>\n").Append(indent)
                        .Append("    [global::UnityEngine.SerializeField]\n").Append(indent).Append("    private global::")
                        .Append(QualifiedTypeName(field.Type)).Append(' ').Append(field.Name).Append(" = null;\n\n");
                text.Append(indent).Append("}\n");
                if (indent.Length != 0) text.Append("}\n");
                return text.ToString();
            }

            /// <summary>由已验证的真实类型生成 C# 完整名称，关键字命名的 namespace 或嵌套类型显式转义。</summary>
            private static string QualifiedTypeName(Type type) => string.Join(".", type.FullName.Replace('+', '.').Split('.')
                .Select(part => ValidIdentifier(part) ? part : "@" + part));

            /// <summary>把已验证模式编码为单行标记，不让名称或类型文本破坏归属语法。</summary>
            private static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

            /// <summary>仅解码归属元数据；解析失败直接阻断，不猜测修复。</summary>
            private static string Decode(string text) => Encoding.UTF8.GetString(Convert.FromBase64String(text));

            /// <summary>用唯一同目录临时文件原子替换源码；失败后只清理本次精确临时文件。</summary>
            private static void WriteAtomic(string path, string text)
                => WriteAtomicBytes(path, new UTF8Encoding(false).GetBytes(text));

            /// <summary>原子写入精确字节；恢复时保留原编码、BOM和换行，绝不重建原文。</summary>
            private static void WriteAtomicBytes(string path, byte[] bytes)
            {
                string temporary = path + ".nova-ui-" + Guid.NewGuid().ToString("N") + "~";
                try
                {
                    File.WriteAllBytes(temporary, bytes);
                    if (File.Exists(path)) File.Replace(temporary, path, null);
                    else File.Move(temporary, path);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
        }
    }
}
