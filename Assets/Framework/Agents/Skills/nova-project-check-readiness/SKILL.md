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
- L2：仅在目标已明确时读取对应的项目文件、包解析结果和所需模块 Docs；若已安装或本次范围点名 Nova SDK/Kit，先定向读取其解析元数据与包内 `Nova/{Doc,Docs,DOCS}/INDEX.md`，再按该 INDEX 路由所需资料；需要 Unity 证据时再确认并连接 Unity。
- L3：只调用只读检查 Adapter，不把诊断变成修复。
- L4：按实际取得的静态、Unity 或 Play 证据分级报告，缺口保持 `partial`。

## 检查步骤

1. 用已解析宿主的 Python 3.9+ 运行 `nova_skills.py resolve` 确认消费项目实际解析到的 Framework 包：macOS/Linux 使用 `python3`，Windows 使用 `py -3`；记录 Unity 版本、Framework 版本、目标平台、当前场景和 Build Settings 的可见事实。
2. 从项目事实而非 Sample 推断拓扑：检查 Nova 托管场景是否使用完整 canonical `Nova.prefab`、运行拓扑是否唯一、Content 场景是否声明加载/卸载责任、资源和配置入口由谁管理。
3. 检查任务所需的最小依赖：业务 asmdef、已存在 UIGroup、Config/Table/资源入口、Pipify 或直接 Build 入口。不要把“未使用 Pipify”或 Warning 当成阻塞。
4. 对已安装或被点名的 `com.solotopia.nova.framework.sdk.*` / `com.solotopia.nova.framework.kit.*` 进入只读扩展包分支：从 `Packages/manifest.json`、`Packages/packages-lock.json` 与实际解析包根记录 package id、声明与解析版本、source 和根路径；先读取包内首个存在的 `Nova/Doc/INDEX.md`、`Nova/Docs/INDEX.md` 或 `Nova/DOCS/INDEX.md`，不要因没有 INDEX 而宽扫全包。
5. 只沿该包 INDEX 的必要入口确认精确 Config 类型及其完整名称、配置路由/协议、资源归属与地址、目标平台或厂商前置。需要 Unity 类型证据时，通过 `nova_project_action` 调用只读 Action `nova.project.config.inspect-plugin-types`，按 `kind=sdk|kit|all` 取得稳定元数据；不要直接调用 Scanner 或构造消费项目插件类型。扫描只证明 Editor 已发现类型，不证明已启用、路由可达、资源存在或运行时成功。Tool 尚不可用时保留为 `partial`，不得退化为任意 C#。
6. 缺少包内 INDEX、Config 类型、路由、资源、平台前置或 Unity 扫描证据时，逐项列为 `unknown` 或 `partial`，不猜测、不安装包、不改版本、不补配置或尝试修复。包未安装也不是默认 Hard Error，除非它是用户已冻结目标的必需前置。
7. 仅在 Unity 已打开且用户允许时收集编译、ProjectGuard、受控类型扫描或 Play 证据；区分 Hard Error、Warning、Release Strict，不把静态检查写成运行验证。
8. 对“接入 Nova”额外确认目标 Nova 版本、替换还是并存既有框架、私有依赖可用性、目标平台和允许的迁移范围；这些信息不足时保持 `blocked`。

## 报告格式

按 `ready`、`blocked`、`risk`、`unknown` 四类列出每项结论的文件、版本、规则 ID 或日志证据；扩展包项还应给出 package id、版本、source、INDEX 路径以及未取得的 Config/路由/资源/平台证据。最终结果只能是：

- `success`：本次只读评估已完整交付，不等于项目已通过运行验证；
- `partial`：部分证据不可获取，明确缺口；
- `blocked`：关键目标或前置条件缺失；
- `not_applicable`：目标不是 Nova 项目组消费端范围。

不要基于引用数量判断是否接入成功，不要修改 `Packages/manifest.json`、`Packages/packages-lock.json`、PackageCache、已解析 SDK/Kit 或 Framework 包、场景或 Prefab，也不要建议删除 `Library/` 作为默认诊断动作。
