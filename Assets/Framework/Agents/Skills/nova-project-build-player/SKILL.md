---
name: nova-project-build-player
description: Use when Nova 项目组需要按明确目标平台、Build Settings 场景、DevelopmentBuild、DevelopMode 与本地输出路径生成 Player 安装包或平台工程，并核验 BuildReport 时使用。
---

# Nova 构建本地 Player

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`、`Docs/Onboarding/VALIDATION.md` 与 `Docs/Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md`。仅在项目明确选择已有 Pipify Batch 时读取 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`；仅在当前项目启用 HybridCLR 或目标为 Android 时读取对应 ABI、输出与签名段落。

该 Operation 只生成本地 Player 安装包或平台工程与 BuildReport，不部署 CDN、不上传商店，不执行 Git 或 UPM 发布，也不以“打包”为由自动运行无关导出。

## Input

冻结项目根、执行入口、目标平台、当前启用的 Build Settings Scene 闭包、完整输出路径或输出文件夹、`BuildMode`、`developmentBuild` 与 `DevelopMode`。`developmentBuild` 控制 Unity Development 选项；`DevelopMode` 只决定自动产物名中的 Debug / Release 段，二者必须分别确认。

Android 还要冻结 APK/AAB/导出工程、SplitApplicationBinary 与签名策略。目标平台、Scene 闭包、输出路径、任一构建模式或平台专属选项不明确时返回 `blocked`；不要依赖 `BuildPackage` 的 Debug 降级替用户猜测模式。

## 已有 Action Adapter

Framework 已注册 `nova.project.player.build`，但它同时声明 `Destructive` 与 `ExternalWrite`；当前 MCP 的确认来源仍是 caller-asserted，因此尚未开放。不得伪称可经 `nova_project_action` 调用，也不得用任意 C# 执行绕过。现有 C#/Pipify 入口只作为人工或既有流水线兼容路径；待可信审批通道落地后，本 Skill 再切换到正式 Action。

当前 MCP 连 `describe` 都只返回已开放 Action，因此这里的 ID 只能从当前 Framework 文档确认，不能从 Tool 读取 Schema，也不能 Plan/Execute。请求真正构建时返回 `blocked` 并明确缺口是“可信审批 + MCP allowlist”，不要退化到 `execute_code`、反射或临时 Pipify。人工或既有 CI 使用底层 C#/Pipify 不属于本 Skill 已完成的 Agent 执行证据。

## Artifact → Evidence

Artifact 是 `BuildReport.summary.outputPath` 指向的本地 APK、AAB、Xcode/Android 工程或 WebGL 目录，以及对应 BuildReport。只有 BuildResult 为 `Succeeded`、输出存在并对应冻结的 Target、Scene、模式和路径时才返回 `success`；代码编译、旧包存在或 Bundle 成功都不能代替 Player 构建证据。

报告实际 Adapter、冻结参数、BuildReport 摘要、产物路径、临时设置恢复结果与未执行的 Player smoke。构建失败、产物归属不明或恢复异常时返回 `partial` 或 `blocked`；本 Skill 不据此声称真机运行、商店上传或外部服务验证成功。
