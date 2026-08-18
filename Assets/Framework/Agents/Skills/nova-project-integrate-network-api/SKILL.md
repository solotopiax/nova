---
name: nova-project-integrate-network-api
description: Use when 项目组要在现有 Nova 项目中接入或修改一个已确认协议的业务 HTTP API，并需完成 HostKey/NetCmd 路由、客户端调用和运行时响应验证时使用。
---

# Nova 接入业务网络 API

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在当前决策分支读取下列页面：检查 HostKey、NetCmd 源和导出边界时读 `Docs/Editor/DataPipeline/Implements/Networks/NetworkExporter.md`；更新 Inspector 设置时读 `Docs/Editor/Inspectors/NetworkComponentInspector/NetworkComponentInspector.md`；执行导出时读 `Docs/Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.HostKeyExporter.md`、`Docs/Editor/EditorUtil/EditorUtil.Network/EditorUtil.Network.NetCmdExporter.md` 与 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md`；运行时加载、路由和 HTTP 验证时读 `Docs/Runtime/Modules/Network/NetworkComponent.md` 与 `Docs/Runtime/Modules/Network/NetworkManager/NetworkManager.md`。不要递归加载全部 Network、Proto 或 Pipify 文档。

## 冻结输入与阻断门

先冻结当前 `ConfigRuntimeSO`、`NetworkComponent`、HostKey / NetCmd 的唯一源和行、HTTP 方法、Path、主备 HostKey、协议、请求字段、认证头、响应成功与业务失败语义、重放/幂等语义、允许修改的业务调用入口、测试端点和可用测试账号。没有这些输入时返回 `blocked`；仅有域名与 Path 时不能声称已接入。

- 不猜测协议：Proto + AES、JSON、form、raw body 与既有业务 Kit 是不同路径。Proto Schema、生成范围或服务端字段不明确时不导出、不改写业务代码。
- 业务代码只能使用已确认的公开项目 Service 或适用的 `Nova.Network` API；不得直接调用内部 `NetService`，也不得为取得主备切换而绕开既有业务链。
- `Nova.Network.PostAsync` 只适用于已冻结、无需请求级主备候选链的 JSON / form / raw 请求。若项目要求请求级主备、协议重试或签名续期，必须有已确认的项目公开 Service；没有时返回 `blocked`，不临时设计 Framework API。
- 主备验证只以受控的通信失败为证据。正式 HTTP `4xx / 5xx` 是业务响应，不得被包装成“备域接管成功”。会产生服务端状态的登录、领奖或下单请求还必须确认重放语义和测试环境。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的 HostKey / NetCmd 源、行与路径 | 项目已确认的源编辑入口；仅定向修改允许的行 | 指定的主备域名与指令路由 | 源、行、HTTP 方法、HostKey、Path 与确认记录一致 |
| 已确认的 NetworkComponent、数据格式和导出范围 | Unity Editor / MCP 的 `NetworkComponentInspector`；`EditorUtil.Network.HostKeyExporter` / `NetCmdExporter` | 正式 HostKey / NetCmd 数据与需要时的类型产物 | 暂存发布成功，且 ConfigRuntime 的 DevelopMode 与导出事实一致 |
| 已确认且范围为全部启用描述的 Batch | `export.network.hostkey.data` / `export.network.hostkey.code` / `export.network.netcmd.data` / `export.network.netcmd.code` / `export.network.proto`；仅有明确 Proto Schema 时才用 `export.network.proto` | 当前项目声明的网络生成物 | Pipify 成功且没有扩大到无关网络源 |
| 已确认协议与业务调用入口 | 已确认的公开项目 Service，或适用时 `Nova.Network.PostAsync` | 可调用的目标业务请求 | 业务代码不直接依赖内部 Network 实现 |
| 已确认测试端点、账号与成功探针 | `Nova.Network.LoadAsync`、`GetNetCmdUrl()`、`ResolveNetCmdRow()` 与适用的请求 API | 运行时路由与目标响应 | Play Mode 中加载、路由、请求和响应符合冻结的成功定义 |

## 实施与验证边界

1. 先检查 Config 和 Network 是否已可加载；HostKey、NetCmd、协议、认证、响应、重放语义、测试端点、账号或成功探针不唯一或缺失时停止并返回 `blocked`。
2. 只修改冻结的源行和业务调用入口。NetworkComponent、配置资产、Prefab 与 Scene 引用只能经 Unity Editor / MCP 修改；禁止手写 YAML，禁止手改导出产物。
3. 按需要定向导出 HostKey / NetCmd；当前 ConfigRuntime 不存在、导出失败或暂存产物校验失败时停止，不清理其他网络输出。
4. 在 Play Mode 先完成 `Nova.Network.LoadAsync()`，再以冻结的表名与指令名验证 URL；最后向测试端点发起一次可审计请求，并按已确认的响应契约判断成功。
5. 只有路由、请求与预期响应都成立才报告 `success`。无测试端点、账号或成功探针时返回 `blocked`；输入已确认且已执行允许步骤，但因当前运行环境无法完成 Play 证据（包括仅能取得导出或编译证据）时报告 `partial`；未执行允许步骤时不得报告 `partial`；协议或主备语义不明确时同样报告 `blocked`。

不默认修改服务端、凭据、生产数据、Framework 包、其他 NetCmd、其他 HostKey 或 Git。删除、外部写入、凭据使用、生产请求、Git commit / push 均需要本 Skill 之外的精确确认。
