---
name: nova-project-upgrade-framework
description: Use when Nova 项目组要把当前 direct Nova Framework 从既有 registry 升级到明确最新或指定更高版本，并在 Unity 重载后验证 Skills 与 MCP 恢复时使用。
---

# Nova 升级消费项目 Framework

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 `references/contract.json`，冻结目标、来源、依赖闭包、确认门和恢复语义。仅在当前外部 UPM 宿主的包管理参数不明确时，读取它的 live 能力清单与当前 Schema；仅在重载后核验实际 Framework 与受管投影时，使用已解析 Framework 包内的 `Agents/Tools/nova_skills.py`。不要读取主仓 `.nova/`、`Minds/`、绝对路径或用 Framework 内的 C# Action 代替包外宿主。

## 范围与前置门

本 Operation 只升级当前消费项目 `Packages/manifest.json` 中的 direct `com.solotopia.nova.framework`：目标只能是同一已配置 registry 的远端 `latest`，或同一来源可解析的明确、更高版本。指定历史版本、降级、Git/file/local 来源、registry 切换、批量包升级或 Framework 发布均为 `not_applicable`。

升级必须由 Framework 外部、跨 reload 仍独立的受控 UPM 宿主完成。先根据它当前公开的 live Schema 确认：只修改这个 direct dependency、触发并等待 Unity Resolve，且能在 domain reload 后重新连接核验。缺少任一能力，或无法证明目标 Framework 的 Nova MCP 默认 Adapter 依赖闭包可解析时，执行前返回 `blocked`。绝不调用 `nova_project_action`、Framework C# AgentAction、任意 C#、反射、菜单模拟或手写 `manifest.json` 来升级 Framework。

## Freeze → Confirm → Upgrade → Recover

1. 只读冻结唯一项目根、当前 manifest direct entry、当前 lock 解析版本与来源、脱敏后的 registry 身份和可达性、`latest` 或精确目标版本、目标元数据，以及目标 Framework → Nova MCP 包 → 默认 Provider Adapter 的可解析闭包。当前版本已等于或高于目标为 `not_applicable`；任何来源、版本、依赖或宿主不唯一为 `blocked`。
2. 展示唯一的 Framework 版本 diff、目标来源、预期 Resolve 影响、后续重载风险和验证清单。只有用户确认该冻结计划后，才调用外部宿主的一次升级；若宿主要求自己的确认，仍须按其精确 Schema 单独完成。
3. 外部宿主提交 manifest 变更并触发 Resolve 后，连接中断、domain reload 或编译尚未稳定时只能返回 `partial`，保留已知的目标与宿主 receipt（如有）。不得自动重试、复用旧请求、清缓存、回滚或改写其它包。
4. 重连到同一项目且 Unity 稳定后，先由包外宿主只读核验 direct manifest、packages-lock、目标 Framework 版本和同源解析。再从新解析的 Framework 读取其 `Agents/catalog.json`，以 Catalog 实际项数（当前为 29，未来以 N 为准）运行 `nova_skills.py doctor --project-root <projectRoot>`，确认投影没有 missing、modified、sourceChanged 或未完成事务。最后重新执行 MCP `tools/list`，确认 `nova_project_action` 已重新出现；仅检查可见性，不用它补做 Framework 升级。

## 结果与不可越界项

所有版本、来源、编译、Catalog 投影与 `nova_project_action` 可见性均符合冻结目标时才为 `success`。写入前缺少外部 UPM 宿主、当前默认 Provider Adapter 或可解析闭包时为 `blocked`；写入已提交后因 Resolve、重载、编译、投影或 MCP 重连未完成时为 `partial`，只给出恢复核验路径，不回滚。

本 Skill 不修改 ConfigMaster 或任何业务配置字段，不清 `Library` / PackageCache，不编辑已解析包，不处理 Samples、Scene、Prefab、Bundle、Player、SDK/Kit 业务配置、Git、发布或外部部署。
