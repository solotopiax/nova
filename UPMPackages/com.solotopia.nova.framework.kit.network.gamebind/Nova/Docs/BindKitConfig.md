# BindKitConfig

## 1. 简介

`BindKitConfig` 是账号绑定 Kit 的固有配置类，实现 `IKitConfig` 接口，在 ConfigWindow「Kit 配置」面板中全局静态配置。`Bind` 的所有接口方法在运行时通过 `Nova.Config.GetKitConfig<BindKitConfig>()` 按需拉取指令名。

**所在文件：** `Nova/Scripts/Runtime/BindKitConfig.cs`
**命名空间：** `NovaFramework.Kit.Network.GameBind.Runtime`
**类签名：** `[Serializable] public sealed class BindKitConfig : IKitConfig`

---

## 2. 配置字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `BindCmdName` | `string` | 绑定协议的 NetCmd 指令名（如 `GameAccountBind`），`BindAsync` 使用 |
| `BindConflictCmdName` | `string` | 冲突查询协议的 NetCmd 指令名（如 `GameAccountBindConflict`），`QueryConflictAsync` 使用 |
| `BindResolveCmdName` | `string` | 裁决协议的 NetCmd 指令名（如 `GameAccountBindResolve`），`ResolveAsync` 使用 |
| `DisplayName` | `string` | ConfigWindow 左树展示名称，固定为 `"Bind 账号绑定"` |

---

## 3. 使用说明

1. 在 ConfigWindow → Kit 配置 面板中找到 `Bind 账号绑定` 条目（由 KitConfigScanner 自动扫描注册）。
2. 填写三个 CmdName，与服务端约定的 NetCmd 指令名保持一致。
3. 启用后，`Nova.Network.Kit<Bind>()` 取得的实例即可直接调用三个入口，无需业务侧手传 cmdName。

---

## 4. 关联

- 实现类：[Bind.md](./Bind.md)
- 接口：`IKitConfig`（`NovaFramework.Runtime`）
- 异常：`KitConfigMissingException`（配置未启用时 fail-fast）
