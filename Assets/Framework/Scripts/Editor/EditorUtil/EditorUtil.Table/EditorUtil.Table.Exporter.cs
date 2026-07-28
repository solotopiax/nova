/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Table.Exporter.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Table 官方 Luban Project 隔离导出与事务发布
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.IO;
using NovaFramework.Runtime;
using UnityEditor;
using IOPath = System.IO.Path;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Table
        {
            /// <summary>
            /// 直接消费项目维护的 luban.conf，在隔离目录生成并事务发布一个或多个 Profile。
            /// </summary>
            public static class Exporter
            {
                /// <summary>
                /// 生成并发布全部已选择 Profile 的代码与数据。
                /// </summary>
                /// <param name="settings">Table Project 与 Runtime 设置。</param>
                /// <returns>完整导出与发布是否成功。</returns>
                public static bool ExportAll(TableSettings settings)
                {
                    return Export(settings, ExportScope.All, null);
                }

                /// <summary>
                /// 生成并发布指定 Profile 的代码与数据。
                /// </summary>
                /// <param name="settings">Table Project 设置。</param>
                /// <param name="profileIds">需要导出的 Profile ID；允许同时指定多个。</param>
                /// <returns>全部指定 Profile 是否导出成功。</returns>
                public static bool ExportAll(TableSettings settings, params string[] profileIds)
                {
                    return Export(settings, ExportScope.All, profileIds);
                }

                /// <summary>
                /// 仅生成并发布全部已选择 Profile 的代码。
                /// </summary>
                /// <param name="settings">Table Project 与 Runtime 设置。</param>
                /// <returns>代码导出与发布是否成功。</returns>
                public static bool ExportCode(TableSettings settings)
                {
                    return Export(settings, ExportScope.Code, null);
                }

                /// <summary>
                /// 仅生成并发布指定 Profile 的代码。
                /// </summary>
                /// <param name="settings">Table Project 设置。</param>
                /// <param name="profileIds">需要导出的 Profile ID；允许同时指定多个。</param>
                /// <returns>全部指定 Profile 是否导出成功。</returns>
                public static bool ExportCode(TableSettings settings, params string[] profileIds)
                {
                    return Export(settings, ExportScope.Code, profileIds);
                }

                /// <summary>
                /// 仅生成并发布全部已选择 Profile 的数据。
                /// </summary>
                /// <param name="settings">Table Project 与 Runtime 设置。</param>
                /// <returns>数据导出与发布是否成功。</returns>
                public static bool ExportData(TableSettings settings)
                {
                    return Export(settings, ExportScope.Data, null);
                }

                /// <summary>
                /// 仅生成并发布指定 Profile 的数据。
                /// </summary>
                /// <param name="settings">Table Project 设置。</param>
                /// <param name="profileIds">需要导出的 Profile ID；允许同时指定多个。</param>
                /// <returns>全部指定 Profile 是否导出成功。</returns>
                public static bool ExportData(TableSettings settings, params string[] profileIds)
                {
                    return Export(settings, ExportScope.Data, profileIds);
                }

                /// <summary>
                /// 把可序列化 Project/Profile 设置转换为结构化 Luban 参数，并强制使用暂存输出目录。
                /// </summary>
                /// <param name="project">官方 Luban Project 设置。</param>
                /// <param name="profile">当前导出 Profile。</param>
                /// <param name="stagedCodeDirectory">隔离代码目录。</param>
                /// <param name="stagedDataDirectory">隔离数据目录。</param>
                /// <returns>不经 shell 拼接的 Luban 调用。</returns>
                public static LubanInvocation BuildInvocation(
                    TableProjectSettings project,
                    TableExportProfileSetting profile,
                    string stagedCodeDirectory,
                    string stagedDataDirectory)
                {
                    var builder = new LubanInvocationBuilder()
                        .WithConfigFile(project.ConfigPath)
                        .WithTarget(project.Target);

                    foreach (string codeTarget in profile.CodeTargets ?? new List<string>())
                    {
                        builder.WithCodeTarget(codeTarget);
                    }
                    foreach (string dataTarget in profile.DataTargets ?? new List<string>())
                    {
                        builder.WithDataTarget(dataTarget);
                    }
                    foreach (string tag in profile.IncludeTags ?? new List<string>())
                    {
                        builder.WithTag(tag);
                    }
                    foreach (string tag in profile.ExcludeTags ?? new List<string>())
                    {
                        builder.WithExcludeTag(tag);
                    }
                    foreach (string variant in profile.Variants ?? new List<string>())
                    {
                        builder.WithVariant(variant);
                    }
                    foreach (TableLubanExtraArgument argument in profile.ExtraArguments ?? new List<TableLubanExtraArgument>())
                    {
                        if (argument != null)
                        {
                            builder.WithExtraArgument(argument.Name, argument.Value);
                        }
                    }
                    foreach (string templateDirectory in profile.CustomTemplateDirs ?? new List<string>())
                    {
                        builder.WithCustomTemplateDirectory(templateDirectory);
                    }

                    if (!string.IsNullOrWhiteSpace(stagedCodeDirectory))
                    {
                        builder.WithExtraArgument("outputCodeDir", stagedCodeDirectory);
                    }
                    if (!string.IsNullOrWhiteSpace(stagedDataDirectory))
                    {
                        builder.WithExtraArgument("outputDataDir", stagedDataDirectory);
                    }

                    return builder.Build();
                }

                /// <summary>
                /// 校验设置，并依次运行全部目标 Profile。
                /// </summary>
                /// <param name="settings">Table 设置。</param>
                /// <param name="scope">本次需要发布的产物范围。</param>
                /// <returns>全流程是否成功。</returns>
                /// <param name="profileIds">显式 Profile ID；为空时使用全部 Enabled Profile。</param>
                private static bool Export(TableSettings settings, ExportScope scope, IReadOnlyCollection<string> profileIds)
                {
                    if (!TryResolveProfiles(settings, scope, profileIds, out TableProjectSettings project,
                            out List<TableExportProfileSetting> profiles, out string error))
                    {
                        Log.Error(LogTag.Editor, "Table 导出配置无效：{0}", error);
                        return false;
                    }

                    for (int i = 0; i < profiles.Count; i++)
                    {
                        if (!ExportProfile(project, profiles[i], scope))
                        {
                            return false;
                        }
                    }
                    AssetDatabase.Refresh();
                    return true;
                }

                /// <summary>
                /// 在独立工作区运行并发布单个 Profile。
                /// </summary>
                /// <param name="project">Luban Project 设置。</param>
                /// <param name="profile">当前导出 Profile。</param>
                /// <param name="scope">本次产物范围。</param>
                /// <returns>该 Profile 是否导出成功。</returns>
                private static bool ExportProfile(
                    TableProjectSettings project,
                    TableExportProfileSetting profile,
                    ExportScope scope)
                {

                    string workspace = IOPath.GetFullPath(IOPath.Combine(
                        "Library", "Nova", "TableExport", Guid.NewGuid().ToString("N")));
                    string stagedCode = IOPath.Combine(workspace, "code");
                    string stagedSchema = IOPath.Combine(workspace, "schema");
                    string stagedAdapter = IOPath.Combine(workspace, "adapter");
                    string stagedData = IOPath.Combine(workspace, "data");
                    Directory.CreateDirectory(stagedCode);
                    Directory.CreateDirectory(stagedSchema);
                    Directory.CreateDirectory(stagedAdapter);
                    Directory.CreateDirectory(stagedData);

                    using IDisposable lease = EditorUtil.FileSystem.AcquireWorkspace(workspace);
                    try
                    {
                        bool wantsCode = scope != ExportScope.Data &&
                                         profile.CodeTargets != null && profile.CodeTargets.Count > 0;
                        bool wantsData = scope != ExportScope.Code &&
                                         profile.DataTargets != null && profile.DataTargets.Count > 0;
                        if (!RunProfileGeneration(project, profile, wantsCode, wantsData,
                                stagedCode, stagedSchema, stagedAdapter, stagedData))
                        {
                            return false;
                        }
                        if (wantsCode)
                        {
                            NormalizeGeneratedCodeFiles(stagedCode);
                        }

                        using var output = new EditorUtil.FileSystem.OutputApplier(workspace);
                        if (wantsCode)
                        {
                            QueueDirectoryPublish(output, stagedCode, profile.CodeOutputPath);
                        }
                        if (wantsData)
                        {
                            QueueDirectoryPublish(output, stagedData, profile.DataOutputPath);
                        }
                        output.Apply();

                        return true;
                    }
                    catch (Exception exception)
                    {
                        Log.Error(LogTag.Editor, "Table Luban Project 导出失败：{0}", exception);
                        return false;
                    }
                    finally
                    {
                        if (Directory.Exists(workspace))
                        {
                            Directory.Delete(workspace, true);
                        }
                    }
                }

                /// <summary>
                /// 透传 Luban 调用；包含 protobuf3 代码目标时追加 protoc 与 Nova Tables 适配器步骤。
                /// </summary>
                /// <param name="project">Luban Project 设置。</param>
                /// <param name="profile">当前导出 Profile。</param>
                /// <param name="wantsCode">是否生成代码。</param>
                /// <param name="wantsData">是否生成数据。</param>
                /// <param name="stagedCode">最终 C# 暂存目录。</param>
                /// <param name="stagedSchema">Protobuf schema 暂存目录。</param>
                /// <param name="stagedAdapter">Protobuf Table API 适配器暂存目录。</param>
                /// <param name="stagedData">原始单表数据暂存目录。</param>
                /// <returns>全部生成步骤是否成功。</returns>
                private static bool RunProfileGeneration(
                    TableProjectSettings project,
                    TableExportProfileSetting profile,
                    bool wantsCode,
                    bool wantsData,
                    string stagedCode,
                    string stagedSchema,
                    string stagedAdapter,
                    string stagedData)
                {
                    bool protobuf = profile.CodeTargets != null && profile.CodeTargets.Count == 2 &&
                                    profile.CodeTargets.Contains("protobuf3") &&
                                    profile.CodeTargets.Contains("cs-newtonsoft-json");
                    if (protobuf)
                    {
                        TableExportProfileSetting schemaProfile = CloneProfile(profile);
                        schemaProfile.CodeTargets = wantsCode
                            ? new List<string> { "protobuf3" }
                            : new List<string>();
                        LubanInvocation schemaInvocation = BuildInvocationForScope(
                            project, schemaProfile, wantsCode ? stagedSchema : null,
                            wantsData ? stagedData : null, wantsCode, wantsData);
                        if (!RunLuban(schemaInvocation))
                        {
                            return false;
                        }

                        if (!wantsCode)
                        {
                            return true;
                        }

                        TableExportProfileSetting adapterProfile = CloneProfile(profile);
                        adapterProfile.CodeTargets = new List<string> { "cs-newtonsoft-json" };
                        adapterProfile.DataTargets = new List<string>();
                        adapterProfile.CustomTemplateDirs = new List<string>
                        {
                            "Assets/Framework/Templates/Luban/table-protobuf",
                        };
                        if (!RunLuban(BuildInvocationForScope(
                                project, adapterProfile, stagedAdapter, null, true, false)))
                        {
                            return false;
                        }

                        return CompileProtobuf(stagedSchema, stagedAdapter, stagedCode);
                    }

                    return RunLuban(BuildInvocationForScope(
                        project, profile, wantsCode ? stagedCode : null,
                        wantsData ? stagedData : null, wantsCode, wantsData));
                }

                /// <summary>
                /// 浅复制 Profile 集合字段，供单次调用安全裁剪 target 和模板目录。
                /// </summary>
                /// <param name="source">源 Profile。</param>
                /// <returns>与源集合互不共享的临时 Profile。</returns>
                private static TableExportProfileSetting CloneProfile(TableExportProfileSetting source)
                {
                    return new TableExportProfileSetting
                    {
                        Id = source.Id,
                        Enabled = source.Enabled,
                        CodeTargets = new List<string>(source.CodeTargets ?? new List<string>()),
                        DataTargets = new List<string>(source.DataTargets ?? new List<string>()),
                        IncludeTags = new List<string>(source.IncludeTags ?? new List<string>()),
                        ExcludeTags = new List<string>(source.ExcludeTags ?? new List<string>()),
                        Variants = new List<string>(source.Variants ?? new List<string>()),
                        ExtraArguments = new List<TableLubanExtraArgument>(source.ExtraArguments ?? new List<TableLubanExtraArgument>()),
                        CustomTemplateDirs = new List<string>(source.CustomTemplateDirs ?? new List<string>()),
                    };
                }

                /// <summary>
                /// 根据导出范围裁剪 Profile 的 -c/-d 参数，同时保留全部 Luban 原生筛选与扩展参数。
                /// </summary>
                /// <param name="project">Luban Project 设置。</param>
                /// <param name="profile">当前 Profile。</param>
                /// <param name="codeDirectory">代码暂存目录。</param>
                /// <param name="dataDirectory">数据暂存目录。</param>
                /// <param name="includeCode">是否包含代码目标。</param>
                /// <param name="includeData">是否包含数据目标。</param>
                /// <returns>按范围裁剪后的调用。</returns>
                private static LubanInvocation BuildInvocationForScope(
                    TableProjectSettings project,
                    TableExportProfileSetting profile,
                    string codeDirectory,
                    string dataDirectory,
                    bool includeCode,
                    bool includeData)
                {
                    var scoped = new TableExportProfileSetting
                    {
                        CodeTargets = includeCode ? profile.CodeTargets : new List<string>(),
                        DataTargets = includeData ? profile.DataTargets : new List<string>(),
                        IncludeTags = profile.IncludeTags,
                        ExcludeTags = profile.ExcludeTags,
                        Variants = profile.Variants,
                        ExtraArguments = profile.ExtraArguments,
                        CustomTemplateDirs = profile.CustomTemplateDirs,
                    };
                    return BuildInvocation(project, scoped, codeDirectory, dataDirectory);
                }

                /// <summary>
                /// 使用 Table Profile 结构化参数执行 UPM 内置 Luban CLI。
                /// </summary>
                /// <param name="invocation">结构化 Luban 调用。</param>
                /// <returns>进程是否以零退出码完成。</returns>
                private static bool RunLuban(LubanInvocation invocation)
                {
                    string dllPath = EditorUtil.Luban.CliRunner.GetLubanDllPath();
                    string dotnetPath = EditorUtil.Luban.CliRunner.ResolveDotnetPath();
                    if (string.IsNullOrEmpty(dllPath) || string.IsNullOrEmpty(dotnetPath))
                    {
                        return false;
                    }

                    string dllArgument = new LubanInvocation(new[] { dllPath }).ToCommandLine();
                    ProcessRunner.ProcessResult result = ProcessRunner.RunSync(
                        dotnetPath, dllArgument + " " + invocation.ToCommandLine());
                    if (!result.Success)
                    {
                        Log.Error(LogTag.Editor, "Table Luban 执行失败（ExitCode={0}）：\n{1}",
                            result.ExitCode, ProcessRunner.FormatOutput(result));
                    }
                    return result.Success;
                }

                /// <summary>
                /// 编译 Luban 生成的全部 proto，并把 cs_pb 生成的 Tables 包装器一并放入代码目录。
                /// </summary>
                /// <param name="schemaDirectory">Luban 的 protobuf schema 输出目录。</param>
                /// <param name="adapterDirectory">Nova Protobuf Table API 适配器暂存目录。</param>
                /// <param name="codeDirectory">最终 C# 暂存目录。</param>
                /// <returns>protoc 是否成功。</returns>
                private static bool CompileProtobuf(
                    string schemaDirectory,
                    string adapterDirectory,
                    string codeDirectory)
                {
                    if (!EditorUtil.Proto.CliRunner.CompileAll(schemaDirectory, codeDirectory))
                    {
                        return false;
                    }

                    foreach (string wrapper in Directory.GetFiles(adapterDirectory, "*.cs", SearchOption.AllDirectories))
                    {
                        File.Copy(wrapper, IOPath.Combine(codeDirectory, IOPath.GetFileName(wrapper)), true);
                    }
                    return true;
                }

                /// <summary>
                /// 统一 Luban 与 protoc 生成代码的文件结尾，避免不同模板或平台产生多余空行。
                /// </summary>
                /// <param name="codeDirectory">待规范化的代码暂存目录。</param>
                private static void NormalizeGeneratedCodeFiles(string codeDirectory)
                {
                    foreach (string codeFile in Directory.GetFiles(codeDirectory, "*.cs", SearchOption.AllDirectories))
                    {
                        string content = File.ReadAllText(codeFile).TrimEnd('\r', '\n');
                        File.WriteAllText(codeFile, content + System.Environment.NewLine);
                    }
                }

                /// <summary>
                /// 把一个完整暂存目录加入输出事务；共享目录中的其他 Profile 产物保持不变。
                /// </summary>
                /// <param name="output">输出事务。</param>
                /// <param name="stagedDirectory">已验证的暂存目录。</param>
                /// <param name="targetDirectory">正式发布目录。</param>
                private static void QueueDirectoryPublish(
                    EditorUtil.FileSystem.OutputApplier output,
                    string stagedDirectory,
                    string targetDirectory)
                {
                    string fullStaged = IOPath.GetFullPath(stagedDirectory);
                    string fullTarget = IOPath.GetFullPath(targetDirectory);
                    foreach (string stagedFile in Directory.GetFiles(fullStaged, "*", SearchOption.AllDirectories))
                    {
                        string relative = IOPath.GetRelativePath(fullStaged, stagedFile);
                        string target = IOPath.Combine(fullTarget, relative);
                        output.AddReplacement(stagedFile, target);
                    }
                }

                /// <summary>
                /// 从 TableSettings 解析并校验本次需要导出的全部 Profile。
                /// </summary>
                /// <param name="settings">Table 设置。</param>
                /// <param name="scope">本次产物范围。</param>
                /// <param name="profileIds">显式 Profile ID；为空时使用 Enabled Profile。</param>
                /// <param name="project">解析出的 Project。</param>
                /// <param name="profiles">解析出的 Profile 列表。</param>
                /// <param name="error">失败原因。</param>
                /// <returns>是否解析成功。</returns>
                private static bool TryResolveProfiles(
                    TableSettings settings,
                    ExportScope scope,
                    IReadOnlyCollection<string> profileIds,
                    out TableProjectSettings project,
                    out List<TableExportProfileSetting> profiles,
                    out string error)
                {
                    project = settings?.Project;
                    profiles = new List<TableExportProfileSetting>();
                    error = null;
                    if (project == null || string.IsNullOrWhiteSpace(project.ConfigPath) ||
                        !File.Exists(project.ConfigPath))
                    {
                        error = "luban.conf 不存在。";
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(project.Target))
                    {
                        error = "Luban target 不能为空。";
                        return false;
                    }

                    ProfileValidationResult validation = ProfileValidator.Validate(project.Profiles);
                    if (!validation.IsValid)
                    {
                        error = string.Join(" ", validation.Errors);
                        return false;
                    }

                    var requestedIds = profileIds == null || profileIds.Count == 0
                        ? null
                        : new HashSet<string>(profileIds, StringComparer.Ordinal);
                    foreach (TableExportProfileSetting profile in project.Profiles)
                    {
                        if (profile != null && (requestedIds?.Contains(profile.Id) ?? profile.Enabled))
                        {
                            profiles.Add(profile);
                        }
                    }
                    if (profiles.Count == 0)
                    {
                        error = "没有选择任何导出 Profile。";
                        return false;
                    }

                    if (requestedIds != null)
                    {
                        foreach (TableExportProfileSetting profile in profiles)
                        {
                            requestedIds.Remove(profile.Id);
                        }
                        if (requestedIds.Count > 0)
                        {
                            error = $"导出 Profile 不存在：{string.Join(", ", requestedIds)}。";
                            return false;
                        }
                    }

                    foreach (TableExportProfileSetting profile in profiles)
                    {
                        bool hasCode = profile.CodeTargets != null && profile.CodeTargets.Count > 0;
                        bool hasData = profile.DataTargets != null && profile.DataTargets.Count > 0;
                        if (scope == ExportScope.Code && !hasCode)
                        {
                            error = $"Profile {profile.Id} 未配置代码目标。";
                            return false;
                        }
                        if (scope == ExportScope.Data && !hasData)
                        {
                            error = $"Profile {profile.Id} 未配置数据目标。";
                            return false;
                        }
                        if (!hasCode && !hasData)
                        {
                            error = $"Profile {profile.Id} 未配置代码目标或数据目标。";
                            return false;
                        }
                        if (hasCode && scope != ExportScope.Data && string.IsNullOrWhiteSpace(profile.CodeOutputPath))
                        {
                            error = $"Profile {profile.Id} 未配置代码输出目录。";
                            return false;
                        }
                        if (hasData && scope != ExportScope.Code && string.IsNullOrWhiteSpace(profile.DataOutputPath))
                        {
                            error = $"Profile {profile.Id} 未配置数据输出目录。";
                            return false;
                        }
                    }
                    return true;
                }

                private enum ExportScope
                {
                    All,
                    Code,
                    Data,
                }

            }
        }
    }
}
