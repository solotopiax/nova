---
name: nova-project-resolve-android-dependencies
description: Use when Nova 项目组在接入或升级 Android SDK/Kit 后，需要冻结 EDM4U 依赖图并受控重建、核验 Android 原生依赖输出时使用。
---

# Nova 解析 Android 原生依赖

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 `references/contract.json`。仅在确认 EDM4U 行为时读取 `Docs/Editor/EditorUtil/EditorUtil.AndroidResolver/EditorUtil.AndroidResolver.md`；仅在本任务来自既有 Pipify 流程时读取 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`；仅在需要理解 Action 的确认与恢复语义时读取 `Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md`。不要展开所有 SDK、Gradle、HybridCLR 或构建文档。

## 范围与冻结输入

本 Operation 只处理消费项目当前完整 EDM4U Android 依赖图的 Force Resolve。它不安装或升级 UPM 包、不修改 registry、不切换平台、不构建 Player，也不证明 Gradle、SDK 初始化或真机运行成功。

先冻结唯一项目根、`activeBuildTarget=Android`、EDM4U 已加载状态、非空完整依赖图及摘要、受管写入边界和执行入口。依赖图为空、EDM4U 不可用、当前平台不是 Android 或 Resolve 目标不明确时返回 `not_applicable` 或 `blocked`，不得以清目录来“重新试一次”。

固定写入边界仅包括 EDM4U 状态文件、`Assets/GeneratedLocalRepo/**`、`Assets/Plugins/Android/**` 中 EDM4U 管理的依赖文件与 Gradle/Manifest 模板。仓库 URL 只进入 SHA-256 摘要，不能把私有 Maven 地址、userinfo、query Token 或凭据写入 Plan、Receipt、日志或报告。

## 执行入口

Framework 已注册并开放 `nova.project.android.resolve-dependencies`，类型为 `Generate`。它会通过 EDM4U Force Resolve 删除或替换既有受管输出，因此仍声明 `Destructive` 与精确确认门；不得退化为 `execute_code`、反射、菜单模拟、临时 C# 或直接删除 `GeneratedLocalRepo`。

正式流程必须保持：

1. `describe` 当前 Schema，按冻结的 Android Target 建立只读 Plan；Plan 冻结依赖图摘要和精确写入集。
2. 用户确认绑定该 Plan 后只执行一次 Force Resolve。依赖图漂移、返回失败、异常或 domain reload 都不得自动重放。
3. 正常 Execute Receipt 再进入 Verify，核对当前依赖图、`AndroidResolverDependencies.xml`、全部受管文件和 `GeneratedLocalRepo` SHA-256。
4. domain reload 后只有缺少成功标记的 Recovery Receipt 时最高为 `partial`；不能凭目录存在推断成功。

只有依赖图、解析状态、完整受管文件集合和 Artifact 摘要全部一致，且 Action Verify 返回完整证据时才报告 `success`。Tool 缺失、Action 未注册或输入不完整时返回 `blocked`；人工或 CI 已经执行但只有部分可信证据时为 `partial`。需要安装 SDK/Kit 时先走 `nova-project-onboard-sdk-kit`；已经失败的 Player/Gradle 构建诊断走 `nova-project-diagnose-build`。

不默认修改 UPM、Config、业务源码、Gradle 业务自定义、Manifest 业务节点、HybridCLR、Bundle/Player、设备、厂商后台、凭据、外部系统或 Git。
