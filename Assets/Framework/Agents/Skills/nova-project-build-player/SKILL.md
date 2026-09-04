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

Framework 已注册并开放 `nova.project.player.build`。先用 `describe` 读取当前 Schema，再以冻结的 Target、Scene、模式和输出路径执行 `plan -> execute -> verify`；含覆盖或外部路径写入时仍必须使用当前一次性 PlanId 确认。不得退化到 `execute_code`、反射或临时 C#。

用户明确选择项目中已有的完整 Pipify Batch 时，改用已开放的 `nova.project.pipify.run-batch`，传入当前活动 `PipifySettings` GUID 与精确 Batch 名称。Plan 必须展示设置文件 Hash、活动 BuildTarget 和有序 Step/参数快照；Execute 只登记一次异步任务，随后用 recovery token 轮询 Verify。任务失败、断线或 domain reload 后不得自动重放。

## Artifact → Evidence

Artifact 是 `BuildReport.summary.outputPath` 指向的本地 APK、AAB、Xcode/Android 工程或 WebGL 目录，以及对应 BuildReport。只有 BuildResult 为 `Succeeded`、输出存在并对应冻结的 Target、Scene、模式和路径时才返回 `success`；代码编译、旧包存在或 Bundle 成功都不能代替 Player 构建证据。

报告实际 Adapter、冻结参数、BuildReport 摘要、产物路径、临时设置恢复结果与未执行的 Player smoke。构建失败、产物归属不明或恢复异常时返回 `partial` 或 `blocked`；本 Skill 不据此声称真机运行、商店上传或外部服务验证成功。
