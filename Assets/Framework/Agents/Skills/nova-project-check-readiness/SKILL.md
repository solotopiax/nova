---
name: nova-project-check-readiness
description: Use when 项目组需要评估陌生 Unity 项目能否接入、继续使用或安全开展 Nova 开发，或希望在修改前得到阻塞项、风险与证据分级时使用。
---

# Nova 项目就绪检查

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

再读取 `references/contract.json`，只检查用户声明的项目范围。该 Skill 只报告，不安装包、不修复配置、不删除缓存。

## 渐进式披露

- L0：先用 frontmatter、Catalog 与共同底线判断是否是项目就绪评估。
- L1：只读取本 Skill 与 `references/contract.json`，锁定用户声明的项目范围。
- L2：仅在目标已明确时读取对应的项目文件、包解析结果和所需模块 Docs；需要 Unity 证据时再确认并连接 Unity。
- L3：只调用只读检查 Adapter，不把诊断变成修复。
- L4：按实际取得的静态、Unity 或 Play 证据分级报告，缺口保持 `partial`。

## 检查步骤

1. 用已解析宿主的 Python 3.9+ 运行 `nova_skills.py resolve` 确认消费项目实际解析到的 Framework 包：macOS/Linux 使用 `python3`，Windows 使用 `py -3`；记录 Unity 版本、Framework 版本、目标平台、当前场景和 Build Settings 的可见事实。
2. 从项目事实而非 Sample 推断拓扑：检查 Nova 托管场景是否使用完整 canonical `Nova.prefab`、运行拓扑是否唯一、Content 场景是否声明加载/卸载责任、资源和配置入口由谁管理。
3. 检查任务所需的最小依赖：业务 asmdef、已存在 UIGroup、Config/Table/资源入口、Pipify 或直接 Build 入口。不要把“未使用 Pipify”或 Warning 当成阻塞。
4. 仅在 Unity 已打开且用户允许时收集编译、ProjectGuard 或 Play 证据；区分 Hard Error、Warning、Release Strict，不把静态检查写成运行验证。
5. 对“接入 Nova”额外确认目标 Nova 版本、替换还是并存既有框架、私有依赖可用性、目标平台和允许的迁移范围；这些信息不足时保持 `blocked`。

## 报告格式

按 `ready`、`blocked`、`risk`、`unknown` 四类列出每项结论的文件、版本、规则 ID 或日志证据。最终结果只能是：

- `success`：本次只读评估已完整交付，不等于项目已通过运行验证；
- `partial`：部分证据不可获取，明确缺口；
- `blocked`：关键目标或前置条件缺失；
- `not_applicable`：目标不是 Nova 项目组消费端范围。

不要基于引用数量判断是否接入成功，不要修改 `manifest.json`、PackageCache、Framework 包、场景或 Prefab，也不要建议删除 `Library/` 作为默认诊断动作。
