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
using System.Linq;
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
            /// 直接消费项目维护的 luban.conf，在隔离目录生成并事务发布一个或多个导出描述。
            /// </summary>
            public static class Exporter
            {
                /// <summary>
                /// 生成并发布全部已启用导出描述的代码与数据。
                /// </summary>
                /// <param name="settings">Table Project 与 Runtime 设置。</param>
                /// <returns>完整导出与发布是否成功。</returns>
                public static bool ExportAll(TableSettings settings)
                {
                    return Export(settings, ExportScope.All, null);
                }

                /// <summary>
                /// 生成并发布指定导出描述的代码与数据。
                /// </summary>
                /// <param name="settings">Table Project 设置。</param>
                /// <param name="descriptionIds">需要导出的导出描述 ID；允许同时指定多个。</param>
                /// <returns>全部指定导出描述是否导出成功。</returns>
                public static bool ExportAll(TableSettings settings, params string[] descriptionIds)
                {
                    return Export(settings, ExportScope.All, descriptionIds);
                }

                /// <summary>
                /// 仅生成并发布全部已启用导出描述的代码。
                /// </summary>
                /// <param name="settings">Table Project 与 Runtime 设置。</param>
                /// <returns>代码导出与发布是否成功。</returns>
                public static bool ExportCode(TableSettings settings)
                {
                    return Export(settings, ExportScope.Code, null);
                }

                /// <summary>
                /// 仅生成并发布指定导出描述的代码。
                /// </summary>
                /// <param name="settings">Table Project 设置。</param>
                /// <param name="descriptionIds">需要导出的导出描述 ID；允许同时指定多个。</param>
                /// <returns>全部指定导出描述是否导出成功。</returns>
                public static bool ExportCode(TableSettings settings, params string[] descriptionIds)
                {
                    return Export(settings, ExportScope.Code, descriptionIds);
                }

                /// <summary>
                /// 仅生成并发布全部已启用导出描述的数据。
                /// </summary>
                /// <param name="settings">Table Project 与 Runtime 设置。</param>
                /// <returns>数据导出与发布是否成功。</returns>
                public static bool ExportData(TableSettings settings)
                {
                    return Export(settings, ExportScope.Data, null);
                }

                /// <summary>
                /// 仅生成并发布指定导出描述的数据。
                /// </summary>
                /// <param name="settings">Table Project 设置。</param>
                /// <param name="descriptionIds">需要导出的导出描述 ID；允许同时指定多个。</param>
                /// <returns>全部指定导出描述是否导出成功。</returns>
                public static bool ExportData(TableSettings settings, params string[] descriptionIds)
                {
                    return Export(settings, ExportScope.Data, descriptionIds);
                }

                /// <summary>
                /// 把可序列化 Project/导出描述转换为结构化 Luban 参数，并强制使用暂存输出目录。
                /// </summary>
                /// <param name="project">官方 Luban Project 设置。</param>
                /// <param name="description">当前导出描述。</param>
                /// <param name="stagedCodeDirectory">隔离代码目录。</param>
                /// <param name="stagedDataDirectory">隔离数据目录。</param>
                /// <returns>不经 shell 拼接的 Luban 调用。</returns>
                public static LubanInvocation BuildInvocation(
                    TableLubanProjectSetting project,
                    TableExportDescriptionSetting description,
                    string stagedCodeDirectory,
                    string stagedDataDirectory)
                {
                    var builder = new LubanInvocationBuilder()
                        .WithConfigFile(project.ConfigPath)
                        .WithTarget(description.Target);

                    foreach (string codeTarget in description.CodeTargets ?? new List<string>())
                    {
                        builder.WithCodeTarget(codeTarget);
                    }
                    foreach (string dataTarget in description.DataTargets ?? new List<string>())
                    {
                        builder.WithDataTarget(dataTarget);
                    }
                    foreach (string tag in description.IncludeTags ?? new List<string>())
                    {
                        builder.WithTag(tag);
                    }
                    foreach (string tag in description.ExcludeTags ?? new List<string>())
                    {
                        builder.WithExcludeTag(tag);
                    }
                    foreach (string variant in description.FieldVariants ?? new List<string>())
                    {
                        builder.WithVariant(variant);
                    }
                    if (description.OutputScope == TableOutputScope.SelectedTables)
                    {
                        foreach (string tableName in description.OutputTables ?? new List<string>())
                        {
                            builder.WithOutputTable(tableName);
                        }
                    }
                    foreach (TableLubanExtraArgument argument in description.AdvancedArguments ?? new List<TableLubanExtraArgument>())
                    {
                        if (argument != null)
                        {
                            builder.WithExtraArgument(argument.Name, argument.Value);
                        }
                    }
                    foreach (string templateDirectory in description.CustomTemplateDirs ?? new List<string>())
                    {
                        builder.WithCustomTemplateDirectory(
                            EditorUtil.Luban.ExportHelper.ResolveCustomTemplateDirectory(templateDirectory));
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
                /// 校验设置，并依次运行全部目标导出描述。
                /// </summary>
                /// <param name="settings">Table 设置。</param>
                /// <param name="scope">本次需要发布的产物范围。</param>
                /// <returns>全流程是否成功。</returns>
                /// <param name="descriptionIds">显式 导出描述 ID；为空时使用全部 已启用导出描述。</param>
                private static bool Export(TableSettings settings, ExportScope scope, IReadOnlyCollection<string> descriptionIds)
                {
                    if (!TryResolveJobs(settings, scope, descriptionIds, out List<ExportJob> jobs, out string error))
                    {
                        Log.Error(LogTag.Editor, "Table 导出配置无效：{0}", error);
                        return false;
                    }

                    for (int i = 0; i < jobs.Count; i++)
                    {
                        if (!ExportDescription(jobs[i].Project, jobs[i].Description, scope))
                        {
                            return false;
                        }
                    }
                    AssetDatabase.Refresh();
                    return true;
                }

                /// <summary>
                /// 在独立工作区运行并发布单个导出描述。
                /// </summary>
                /// <param name="project">Luban Project 设置。</param>
                /// <param name="description">当前导出描述。</param>
                /// <param name="scope">本次产物范围。</param>
                /// <returns>该导出描述 是否导出成功。</returns>
                private static bool ExportDescription(
                    TableLubanProjectSetting project,
                    TableExportDescriptionSetting description,
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
                                         description.CodeTargets != null && description.CodeTargets.Count > 0;
                        bool wantsData = scope != ExportScope.Code &&
                                         description.DataTargets != null && description.DataTargets.Count > 0;
                        if (!RunDescriptionGeneration(project, description, wantsCode, wantsData,
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
                            QueueDirectoryPublish(output, stagedCode, description.CodeOutputPath);
                        }
                        if (wantsData)
                        {
                            QueueDirectoryPublish(output, stagedData, description.DataOutputPath);
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
                /// <param name="description">当前导出描述。</param>
                /// <param name="wantsCode">是否生成代码。</param>
                /// <param name="wantsData">是否生成数据。</param>
                /// <param name="stagedCode">最终 C# 暂存目录。</param>
                /// <param name="stagedSchema">Protobuf schema 暂存目录。</param>
                /// <param name="stagedAdapter">Protobuf Table API 适配器暂存目录。</param>
                /// <param name="stagedData">原始单表数据暂存目录。</param>
                /// <returns>全部生成步骤是否成功。</returns>
                private static bool RunDescriptionGeneration(
                    TableLubanProjectSetting project,
                    TableExportDescriptionSetting description,
                    bool wantsCode,
                    bool wantsData,
                    string stagedCode,
                    string stagedSchema,
                    string stagedAdapter,
                    string stagedData)
                {
                    if (!wantsCode)
                    {
                        return RunLuban(BuildInvocationForScope(
                            project, description, null, wantsData ? stagedData : null, false, wantsData));
                    }

                    bool protobuf = description.CodeTargets != null &&
                                    description.CodeTargets.Contains("protobuf3") &&
                                    description.CodeTargets.Contains("cs-newtonsoft-json");
                    if (protobuf)
                    {
                        TableExportDescriptionSetting schemaDescription = CloneDescription(description);
                        schemaDescription.CodeTargets = wantsCode
                            ? new List<string> { "protobuf3" }
                            : new List<string>();
                        LubanInvocation schemaInvocation = BuildInvocationForScope(
                            project, schemaDescription, wantsCode ? stagedSchema : null,
                            wantsData ? stagedData : null, wantsCode, wantsData);
                        if (!RunLuban(schemaInvocation))
                        {
                            return false;
                        }

                        TableExportDescriptionSetting adapterDescription = CloneDescription(description);
                        adapterDescription.CodeTargets = new List<string> { "cs-newtonsoft-json" };
                        adapterDescription.DataTargets = new List<string>();
                        adapterDescription.CustomTemplateDirs = new List<string>(
                            description.CustomTemplateDirs ?? new List<string>());
                        adapterDescription.CustomTemplateDirs.Add(
                            "Packages/com.solotopia.nova.framework/Templates/Luban/table-protobuf");
                        if (!RunLuban(BuildInvocationForScope(
                                project, adapterDescription, stagedAdapter, null, true, false)))
                        {
                            return false;
                        }

                        if (!CompileProtobuf(stagedSchema, stagedAdapter, stagedCode))
                        {
                            return false;
                        }

                        List<string> remainingCodeTargets = description.CodeTargets
                            .Where(target => target != "protobuf3" && target != "cs-newtonsoft-json")
                            .ToList();
                        if (remainingCodeTargets.Count == 0)
                        {
                            return true;
                        }

                        TableExportDescriptionSetting remainingDescription = CloneDescription(description);
                        remainingDescription.CodeTargets = remainingCodeTargets;
                        remainingDescription.DataTargets = new List<string>();
                        return RunLuban(BuildInvocationForScope(
                            project, remainingDescription, stagedCode, null, true, false));
                    }

                    return RunLuban(BuildInvocationForScope(
                        project, description, wantsCode ? stagedCode : null,
                        wantsData ? stagedData : null, wantsCode, wantsData));
                }

                /// <summary>
                /// 浅复制导出描述集合字段，供单次调用安全裁剪 target 和模板目录。
                /// </summary>
                /// <param name="source">源导出描述。</param>
                /// <returns>与源集合互不共享的临时导出描述。</returns>
                private static TableExportDescriptionSetting CloneDescription(TableExportDescriptionSetting source)
                {
                    return new TableExportDescriptionSetting
                    {
                        Id = source.Id,
                        Name = source.Name,
                        Enabled = source.Enabled,
                        Target = source.Target,
                        Format = source.Format,
                        CodeTargets = new List<string>(source.CodeTargets ?? new List<string>()),
                        DataTargets = new List<string>(source.DataTargets ?? new List<string>()),
                        OutputScope = source.OutputScope,
                        OutputTables = new List<string>(source.OutputTables ?? new List<string>()),
                        CodeOutputPath = source.CodeOutputPath,
                        DataOutputPath = source.DataOutputPath,
                        IncludeTags = new List<string>(source.IncludeTags ?? new List<string>()),
                        ExcludeTags = new List<string>(source.ExcludeTags ?? new List<string>()),
                        FieldVariants = new List<string>(source.FieldVariants ?? new List<string>()),
                        AdvancedArguments = new List<TableLubanExtraArgument>(source.AdvancedArguments ?? new List<TableLubanExtraArgument>()),
                        CustomTemplateDirs = new List<string>(source.CustomTemplateDirs ?? new List<string>()),
                    };
                }

                /// <summary>
                /// 根据导出范围裁剪导出描述的 -c/-d 参数，同时保留全部 Luban 原生筛选与扩展参数。
                /// </summary>
                /// <param name="project">Luban Project 设置。</param>
                /// <param name="description">当前导出描述。</param>
                /// <param name="codeDirectory">代码暂存目录。</param>
                /// <param name="dataDirectory">数据暂存目录。</param>
                /// <param name="includeCode">是否包含代码目标。</param>
                /// <param name="includeData">是否包含数据目标。</param>
                /// <returns>按范围裁剪后的调用。</returns>
                private static LubanInvocation BuildInvocationForScope(
                    TableLubanProjectSetting project,
                    TableExportDescriptionSetting description,
                    string codeDirectory,
                    string dataDirectory,
                    bool includeCode,
                    bool includeData)
                {
                    var scoped = new TableExportDescriptionSetting
                    {
                        Target = description.Target,
                        CodeTargets = includeCode ? description.CodeTargets : new List<string>(),
                        DataTargets = includeData ? description.DataTargets : new List<string>(),
                        OutputScope = description.OutputScope,
                        OutputTables = description.OutputTables,
                        IncludeTags = description.IncludeTags,
                        ExcludeTags = description.ExcludeTags,
                        FieldVariants = description.FieldVariants,
                        AdvancedArguments = description.AdvancedArguments,
                        CustomTemplateDirs = description.CustomTemplateDirs,
                    };
                    return BuildInvocation(project, scoped, codeDirectory, dataDirectory);
                }

                /// <summary>
                /// 使用 Table 导出描述的结构化参数执行 UPM 内置 Luban CLI。
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
                /// 把一个完整暂存目录加入输出事务；共享目录中的其他导出描述产物保持不变。
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
                /// 从 TableSettings 解析并校验本次需要导出的全部导出描述。
                /// </summary>
                /// <param name="settings">Table 设置。</param>
                /// <param name="scope">本次产物范围。</param>
                /// <param name="descriptionIds">显式导出描述 ID；为空时使用已启用导出描述。</param>
                /// <param name="jobs">解析出的 Project 与导出描述任务。</param>
                /// <param name="error">失败原因。</param>
                /// <returns>是否解析成功。</returns>
                private static bool TryResolveJobs(
                    TableSettings settings,
                    ExportScope scope,
                    IReadOnlyCollection<string> descriptionIds,
                    out List<ExportJob> jobs,
                    out string error)
                {
                    jobs = new List<ExportJob>();
                    error = null;
                    if (settings?.Projects == null || settings.Projects.Count == 0)
                    {
                        error = "没有配置任何 Luban Project。";
                        return false;
                    }

                    var requestedIds = descriptionIds == null || descriptionIds.Count == 0
                        ? null
                        : new HashSet<string>(descriptionIds, StringComparer.Ordinal);
                    var matchedRequestedIds = new HashSet<string>(StringComparer.Ordinal);
                    foreach (TableLubanProjectSetting project in settings.Projects)
                    {
                        if (project == null || string.IsNullOrWhiteSpace(project.ConfigPath) ||
                            !File.Exists(project.ConfigPath))
                        {
                            error = $"Luban Project '{project?.Name}' 的 luban.conf 不存在。";
                            return false;
                        }

                        DescriptionValidationResult validation = DescriptionValidator.Validate(project.ExportDescriptions);
                        if (!validation.IsValid)
                        {
                            error = $"Luban Project '{project.Name}'：{string.Join(" ", validation.Errors)}";
                            return false;
                        }

                        foreach (TableExportDescriptionSetting description in project.ExportDescriptions)
                        {
                            if (description == null)
                            {
                                continue;
                            }

                            string qualifiedId = project.Id + "/" + description.Id;
                            bool selected = requestedIds == null
                                ? description.Enabled
                                : requestedIds.Contains(qualifiedId) || requestedIds.Contains(description.Id);
                            if (!selected)
                            {
                                continue;
                            }

                            if (requestedIds != null)
                            {
                                if (requestedIds.Contains(qualifiedId)) matchedRequestedIds.Add(qualifiedId);
                                if (requestedIds.Contains(description.Id)) matchedRequestedIds.Add(description.Id);
                            }

                            if (!ValidateJob(description, scope, out error))
                            {
                                return false;
                            }
                            jobs.Add(new ExportJob(project, description));
                        }
                    }
                    if (jobs.Count == 0)
                    {
                        error = "没有选择任何导出描述。";
                        return false;
                    }

                    if (requestedIds != null)
                    {
                        requestedIds.ExceptWith(matchedRequestedIds);
                        if (requestedIds.Count != 0)
                        {
                            error = $"导出描述不存在：{string.Join(", ", requestedIds)}。";
                            return false;
                        }
                    }
                    return true;
                }

                /// <summary>
                /// 校验单个导出任务在指定范围下具备必要 Target 和输出目录。
                /// </summary>
                private static bool ValidateJob(TableExportDescriptionSetting description, ExportScope scope,
                    out string error)
                {
                    error = null;
                    if (string.IsNullOrWhiteSpace(description.Target))
                    {
                        error = $"导出描述 {description.Id} 未配置 Target目标。";
                        return false;
                    }

                    bool hasCode = description.CodeTargets != null && description.CodeTargets.Count > 0;
                    bool hasData = description.DataTargets != null && description.DataTargets.Count > 0;
                    if (scope == ExportScope.Code && !hasCode)
                        error = $"导出描述 {description.Id} 未配置代码目标。";
                    else if (scope == ExportScope.Data && !hasData)
                        error = $"导出描述 {description.Id} 未配置数据目标。";
                    else if (!hasCode && !hasData)
                        error = $"导出描述 {description.Id} 未配置代码目标或数据目标。";
                    else if (hasCode && scope != ExportScope.Data && string.IsNullOrWhiteSpace(description.CodeOutputPath))
                        error = $"导出描述 {description.Id} 未配置代码输出目录。";
                    else if (hasData && scope != ExportScope.Code && string.IsNullOrWhiteSpace(description.DataOutputPath))
                        error = $"导出描述 {description.Id} 未配置数据输出目录。";
                    if (!string.IsNullOrEmpty(error)) return false;
                    return true;
                }

                /// <summary>
                /// 保存已解析的 Project 与导出描述组合。
                /// </summary>
                private readonly struct ExportJob
                {
                    internal ExportJob(TableLubanProjectSetting project, TableExportDescriptionSetting description)
                    {
                        Project = project;
                        Description = description;
                    }

                    internal TableLubanProjectSetting Project { get; }
                    internal TableExportDescriptionSetting Description { get; }
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
