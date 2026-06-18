# SaveKitConfig

## 1. 简介

`SaveKitConfig` 是游戏存档 Kit 的固有配置类，实现 `IKitConfig` 接口，在 ConfigWindow「Kit 配置」面板中全局静态配置。`Save` 的所有接口方法在运行时通过 `Nova.Config.GetKitConfig<SaveKitConfig>()` 按需拉取指令名。

**所在文件：** `Nova/Scripts/Runtime/SaveKitConfig.cs`
**命名空间：** `NovaFramework.Kit.Network.GameSave.Runtime`
**类签名：** `[Serializable] public sealed class SaveKitConfig : IKitConfig`

---

## 2. 配置字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `GetCmdName` | `string` | 获取存档协议的 NetCmd 指令名（如 `GameSaveGet`），`GetAsync` / `GetFullAsync` 共用 |
| `SetCmdName` | `string` | 上传存档协议的 NetCmd 指令名（如 `GameSaveSet`），`SetAsync` / `SetFullAsync` 共用 |
| `DisplayName` | `string` | ConfigWindow 左树展示名称，固定为 `"Save 云存储"` |

---

## 3. 使用说明

1. 在 ConfigWindow → Kit 配置 面板中找到 `Save 云存储` 条目（由 KitConfigScanner 自动扫描注册）。
2. 填写 `GetCmdName` 和 `SetCmdName`，与服务端约定的 NetCmd 指令名保持一致。
3. 启用后，`Nova.Network.Kit<Save>()` 取得的实例即可直接调用 6 个极简入口，无需业务侧手传 cmdName。

---

## 4. 关联

- 实现类：[Save.md](./Save.md)
- 接口：`IKitConfig`（`NovaFramework.Runtime`）
- 异常：`KitConfigMissingException`（配置未启用时 fail-fast）
