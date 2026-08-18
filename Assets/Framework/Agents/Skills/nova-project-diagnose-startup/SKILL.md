---
name: nova-project-diagnose-startup
description: Use when Nova 消费项目出现编译失败、Play 被门禁、启动黑屏、流程未进入、配置或资源初始化失败，并需要只读定位最早失败阶段与直接证据时使用。
---

# Nova 启动故障诊断

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`、`Docs/Onboarding/VALIDATION.md` 与 `Docs/Editor/EditorUtil/EditorUtil.ProjectGuard.md`。仅在证据指向对应阶段时继续读取 `Docs/Runtime/Modules/Procedure/ProcedureComponent.md`、`Docs/Runtime/Modules/Config/ConfigComponent.md` 或 `Docs/Runtime/Modules/Asset/AssetComponent.md`，不预加载无关模块。

该 Operation 只读诊断，不修改源码、配置、Scene、Prefab、Build Settings 或生成物，不安装或升级包，也不通过删除 `Library/`、缓存或输出目录尝试修复。

## Input

冻结项目根、可复现的启动现象、复现入口，以及目标平台、渠道与 DevelopMode。若目标上下文会改变诊断路径却不明确，返回 `blocked` 并只提出一个最小澄清问题。另行确认是否允许启动 Unity、触发编译或进入 Play；只读授权不隐含这些状态变更。

## 已有 Action Adapter

1. 使用已解析宿主的 Python 3.9+ 运行 `nova_skills.py resolve` 确认项目实际解析到的 Framework 包与 Docs：macOS/Linux 使用 `python3`，Windows 使用 `py -3`。
2. 先检查已有编译结果、Console / Editor.log 和当前入口 Scene；需要结构证据时调用 `EditorUtil.ProjectGuard.ValidateQuick()` 或 `ValidatePlay()` 获取只读报告。Warning 本身不是启动失败根因。
3. 按“编译 → Play 门禁 → Nova/Manager Awake → Procedure → Config → Asset/热更 → 网络或外部服务”定位最早出现直接失败证据的阶段，并在该阶段停止扩散扫描。
4. 仅在用户允许且当前 Unity 可用时复现编译或 Play。不得因发现修复点而顺手写入；将修复建议交给匹配的写入 Skill 或用户确认后的后续任务。

## Artifact → Evidence

交付一份诊断报告，包含冻结输入、复现步骤、最早失败阶段、文件/规则 ID/日志原文位置、根因置信度、推荐下一步与未验证项。区分客户端事实、服务端事实和推断，不用客户端日志声称服务端已修复。

有直接静态或日志证据并完整回答诊断目标时返回 `success`；缺少 Unity、Play、设备或外部服务证据时返回 `partial`；关键输入或复现权限缺失时返回 `blocked`。`success` 只表示诊断交付完成，不表示故障已修复。
