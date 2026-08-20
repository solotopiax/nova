---
name: nova-project-generate-hybridclr-artifacts
description: Use when 已配置 HybridCLR 的 Nova 消费项目需要按明确 activeBuildTarget、DevelopmentBuild 与激活 ConfigMaster 当前坐标生成 bridge、AOT generic、Il2CppDef、link.xml 和 AOT 裁剪准备产物时使用。
---

# Nova 生成 HybridCLR 预构建产物

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

以下资料仅在当前决策分支按需读取。

只读取当前决策分支需要的事实：先读 `references/contract.json` 冻结输入、写入集、锁和确认门；需要核对生成行为时再读 `Docs/Editor/EditorUtil/EditorUtil.HybridCLR/EditorUtil.HybridCLR.md`，选择既有 Pipify Batch 时才读 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`。不要加载完整构建、Bundle、CDN 或运行时链路。

## 适用范围

该 Operation 只生成当前 `activeBuildTarget + DevelopmentBuild + 激活 ConfigMaster 当前 Platform/Channel/DevelopMode` 对应的 HybridCLR 预构建产物：热更编译输出、MethodBridge/Reverse PInvoke、AOT 泛型引用、Il2CppDef、最终 link.xml、当前 Target 的 AOT 裁剪目录与裁剪所需临时工程。

它不生成最终 Player、平台工程或安装包，不构建 Bundle，不上传 CDN，不拷贝 AOT/Game DLL 到业务资源目标，不执行运行时加载或设备验证。`GenerateAll` 内部用于产出裁剪 AOT DLL 的 script-only `BuildPipeline.BuildPlayer` 是临时生成步骤，不是最终 Player 成功证据。

## 冻结与预检

执行前读取并冻结：消费项目根、当前 `EditorUserBuildSettings.activeBuildTarget`、期望 `DevelopmentBuild`、当前激活 ConfigMaster 的稳定身份及其 `Platform/Channel/DevelopMode`、Build Settings 中已启用场景闭包、HybridCLR 安装/启用状态、解析后的生成输出集合、最终 link.xml 路径和执行入口。

必须确认目标平台模块与 IL2CPP/HybridCLR 生成前提可用，当前 active Target 与冻结值一致，启用场景非空。分别解析 HybridCLR `outputLinkFile` 与当前坐标 `LinkXmlTargetPath`（空值按 `Assets/link.xml`）；两者规范化后不是同一项目内路径时返回 `blocked`，不修改配置来迎合本 Operation。`DevelopMode` 不能代替 `EditorUserBuildSettings.development`。

确认绑定上述全部值、完整生成输出集合、临时裁剪目录、会被重建或失效的生成文件/缓存及执行入口。确认后任一值漂移，旧确认失效并返回 `blocked`。

## 执行入口

Framework 已注册 `nova.project.hotfix.generate-artifacts`，但该操作会清理/重建 HybridCLR 生成目录与缓存，带 `Destructive` 副作用；当前 MCP 的确认来源仍是 caller-asserted，因此尚未开放。不得伪称可经 `nova_project_action` 调用，也不得用任意 C# 执行绕过。以下直接 C#/既有 Batch 仅保留为人工或既有流水线兼容入口；待可信审批通道落地后再迁移为正式 Action 调用。

当前 MCP 连 `describe` 都只返回已开放 Action，因此这里的 ID 只能从当前 Framework 文档确认，不能从 Tool 读取 Schema，也不能 Plan/Execute。请求真正生成时返回 `blocked` 并明确缺口是“可信审批 + MCP allowlist”，不要退化到 `execute_code`、反射或临时 Pipify。Action 内部已固定 `GenerateAll -> ValidateLinkXml`、锁、漂移检查和恢复验证；人工或既有 CI 运行底层入口不属于本 Skill 已完成的 Agent 执行证据。

## 生成后验证

以当前 HybridCLR Settings 的解析结果核对，不猜测硬编码目录：

- 当前 Target 的热更 DLL 编译输出存在；当前 Target 的 `AssembliesPostIl2CppStrip` 目录存在且包含 AOT DLL。
- `GeneratedCppDir` 下 MethodBridge、`UnityVersion.h` 与 `AssemblyManifest.cpp` 存在；MethodBridge 必须包含独立规范行 `// DEVELOPMENT=0` 或 `// DEVELOPMENT=1`，并与冻结 DevelopmentBuild 一致。
- AOT 泛型引用文件、最终 link.xml 和临时裁剪工程已生成；随后执行的 `ValidateLinkXml` 已使当前坐标解析出的每个 AOT metadata assembly 在最终 link.xml 中存在不带 `.dll` 后缀的 `<assembly fullname>` 记录；由该入口新补的记录带 `preserve="all"`，已有同名 assembly 不被重写。
- 等待生成引发的 Unity 编译结束，记录无编译错误证据；再次核对 Target、激活 Master、三维坐标，以及临时切换过的 DevelopmentBuild 已恢复。

最低证据是 `generated-output+compile`。只拿到日志、临时 script-only BuildReport、部分文件或未结束的编译，均不得返回 `success`。

## 结果状态

- `success`：全部冻结值未漂移；generate -> validate 完成；目标 Target 的生成输出、MethodBridge DEVELOPMENT、AOT preserve 与 Unity 编译全部通过；临时状态已恢复。
- `partial`：已产生允许范围内的生成物，但后续生成、validate、输出核对、编译或恢复复核未完成；列出已写入项与剩余风险。
- `blocked`：输入、平台前提、场景、锁、确认、路径对齐或执行入口不足，且尚未产生本次写入；给出最小缺口。
- `not_applicable`：项目未安装/启用 HybridCLR，或请求核心是最终 Player、Bundle、CDN、DLL Copy、运行时/设备验证、HybridCLR 升级或配置修改。

不得把本 Operation 的成功描述为 Player、Bundle、CDN、运行时、IL2CPP 真机或发布成功。最终 Player 构建使用 `nova-project-build-player`；Bundle 使用 `nova-project-build-bundles`；只刷新业务热更 DLL 使用 `nova-project-refresh-hotfix-dlls`。
