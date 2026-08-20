# Nova Framework - MCP Adapter

> 包名：`com.solotopia.nova.framework.mcp`
>
> 当前版本：`0.1.0`
>
> 当前默认 Provider：`com.coplaydev.unity-mcp@10.1.2`

Nova Framework 的必需 Editor 配套包，将默认 Unity MCP Provider 的 `nova_project_action` Tool 连接到受控 `EditorUtil.AgentActions`。本包只负责中立契约、传输准入与结果裁剪，不复制 Config、HybridCLR、YooAsset、Build 或 UPM 的领域实现。

## 职责边界

- 只暴露 Adapter 中显式允许且通过副作用门的 Nova Project Action。
- 不提供任意 C#、CLR 类型名、方法名或反射执行入口。
- Nova Framework 通过 `package.json` 显性依赖本包；中立契约程序集不依赖具体 Provider，默认 Unity MCP Adapter 放在独立程序集。
- Action 的当前 Schema、证据与副作用以运行中的 Registry `describe` 为准；README 不充当第二份静态 Action Catalog。

## 安装与自动衔接

消费项目只安装或升级 `com.solotopia.nova.framework`；UPM 应沿 Framework 的显性依赖自动解析本包，再解析：

- `com.coplaydev.unity-mcp@10.1.2`
- `com.unity.nuget.newtonsoft-json@3.2.2`

`com.coplaydev.unity-mcp@10.1.2` 由 OpenUPM 提供。NovaSpark 会先在消费工程 `Packages/manifest.json` 的 OpenUPM scoped registry 中补齐精确 scope `com.coplaydev.unity-mcp`，再写入 Framework；本包的 semver 依赖随后由 UPM 自动解析，不需要把 Unity MCP 作为项目顶层 Git 依赖。绕过 NovaSpark 直接安装 Framework 时，工程必须已经配置可解析该包的 registry。Nova 开发工程可另外安装其他 Provider 做开发验证；它不会改变对外 UPM 包当前默认适配 Unity MCP 的事实。

`nova_project_action` 声明 `AutoRegister=true`。本包不会自动启动 Server、占用端口或修改外部 Agent 配置。已有 Agent 会话如果缓存了 Tool 列表，安装或升级后需要重连 MCP 或开启新会话。

## 调用协议

调用协议固定为：

1. `describe` 获取当前开放 ID 与 Registry Request Schema；
2. `plan` 传 `action_id + request`；
3. `execute` 必须传 `action_id + plan_id`，确认型 Action 再传 `confirmation_token=plan_id`；
4. `verify` 的参数名为 `receipt`，值优先使用 Core 返回的 `recovery_token`。

ready Plan 会在项目 `Library/Nova/AgentActions/Operations/` 写最小恢复元数据；domain reload 后只允许 Verify，不恢复或重放 Execute。当前桥拒绝 `Delivery` 以及带 `Destructive`、`ExternalWrite` 或 `Credential` 的 Action，因为 `confirmation_token` 只能绑定计划，不能证明可信的人类审批。

当前开放 UPM 安装/升级、Config Inspect/Ensure/Export、业务热更 DLL 刷新与只读构建前检共 8 个 Action。UPM direct dependency 卸载已拆为独立 `Destructive` Action，当前不开放。实际列表与 Schema 以运行中的 `describe` 为准。

完整协议、开放边界与排障入口见 [Nova/Docs/INDEX.md](./Nova/Docs/INDEX.md)。

## 目录结构

- `Nova/Editor/`：Provider 中立的契约、Registry 与受控 Gateway。
- `Nova/UnityMcp/Editor/`：当前默认 Unity MCP Provider 的薄 Tool Adapter。
- `Nova/Docs/`：安装后可直接查阅的包内文档入口。
- `Core/`：第三方源码槽位；当前通过 UPM 依赖使用 Unity MCP，不复制其源码，因此为空。

## 维护

- 修改公开 Tool envelope、Action 暴露范围或恢复语义时，必须同步包内文档和测试。
- 默认 Unity MCP 依赖变化时，同步更新 `dependencies`、安装说明与 CHANGELOG；`coreVersion` 表示 Nova MCP 自身的内核契约基线，不跟随第三方 Provider 版本。
- 变更记录见 [CHANGELOG.md](./CHANGELOG.md)，许可见 [LICENSE.md](./LICENSE.md)。
