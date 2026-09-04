---
name: nova-project-preflight-build
description: Use when Nova 项目组要在实际构建 Player 前，只读检查目标平台、场景、Config、YooAsset Package 与 HybridCLR 前置是否就绪时使用。
---

# Nova Player 构建前置检查

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 `references/contract.json`。仅在解释规则或确认构建边界时，才按需读取 `Docs/Onboarding/VALIDATION.md`、`Docs/Editor/EditorUtil/EditorUtil.ProjectGuard.md` 与 `Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md`；不要预先加载 Bundle、HybridCLR、SDK 或平台的全部文档。Action Schema 只从运行中的 `nova.project.build.inspect-readiness` Descriptor 获取，不在 Skill 中复制字段定义。

## 范围与冻结输入

这是严格只读的构建前检查，不执行 Player、Bundle、HybridCLR Generate、Android Resolve、平台切换、Config 导出或任何修复。先冻结唯一项目根、目标 `BuildTarget`、可选的目标 YooAsset Package，以及本次只检查哪个构建目标。目标平台或 Package 不唯一时返回 `blocked`。

- 目标必须与 Unity 当前 `activeBuildTarget` 一致；本 Skill 不自动切换平台。
- 检查读取启用的 Build Settings 场景、项目 Build Support、`ProjectSettings/Nova/Globals.json` 的精确 ConfigMaster 绑定、当前 Platform/Channel/DevelopMode 与 ConfigRuntimeSO 坐标、场景 Asset/App 渠道快照、YooAsset Settings/Collector/Package 结构，以及启用时的 HybridCLR 安装与 link.xml 路径一致性。
- 规则检查完成不等于可以构建。Action 返回 `success` 只说明只读检查执行完成；本 Skill 必须继续读取 `data.ready`、`errorCount`、`warningCount` 和逐条规则。
- 不用旧 BuildReport、旧 APK/AAB、旧 Xcode/WebGL 工程或“上次能打”替代当前工程快照。

## Action 与证据

1. 通过 MCP 的 `nova_project_action` 对 `nova.project.build.inspect-readiness` 执行 `describe`，只读取当前 Request Schema。
2. 使用冻结的 target 与可选 packageName 进行 `plan`；该 Action 无领域写入，但仍需等待 Unity 处于非编译、非更新、非 Play 的稳定状态。
3. 执行并按 Receipt `verify`，记录规则 ID、状态、消息和已脱敏证据。不得因某条 Error 看起来容易修就顺手修改项目。
4. `data.ready=true` 且 `errorCount=0` 时，本 Skill 返回 `success`；Warning 原样保留，说明它为何不阻断。存在 Error 时返回 `blocked`，并给出最小后续 Operation；Action 无法开放、Unity 不稳定或证据不完整时返回 `partial`。

输出必须明确：目标平台、当前活动平台、目标 Package、场景闭包、Config 坐标、Bundle/HybridCLR 状态、错误与警告规则，以及未执行构建的边界。若请求实际是分析已经失败的构建，转入 `nova-project-diagnose-build`；若用户已确认要构建，转入 `nova-project-build-player`。

不默认切换平台、保存资产、修改 Build Settings/Config/Collector/Package/HybridCLR、生成产物、清缓存、构建、安装设备、使用凭据、外部写入或执行 Git 操作。
