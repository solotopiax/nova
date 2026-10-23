# LoginKitConfig

## §1 文件头

**所在文件：** `Nova/Scripts/Runtime/LoginKitConfig.cs`
**命名空间：** `NovaFramework.Kit.Network.GameLogin.Runtime`
**类签名：** `[Serializable] public sealed class LoginKitConfig : IKitConfig`

登录 Kit 固有配置，持有登录协议指令名 `LoginCmdName` 和账号删除协议指令名 `DeleteCmdName`，分别由 `Login.Async` / `Login.DeleteAsync` 在运行时通过 `Nova.Config.GetKitConfig<LoginKitConfig>()` 拉取。账号绑定、冲突查询与裁决由 GameBind 的 `BindKitConfig` 和 `Bind` 负责，不属于本配置或 `Login` 的公开入口。

在 ConfigWindow「Kit 配置」面板中按 Platform×Channel×DevelopMode 坐标配置：实例存于 `PlatformChannelEntry.KitConfigsByMode`，`ConfigMasterSO.EnabledKits` 仅作类型白名单，Exporter 将当前格的已启用配置导出为单格 `ConfigRuntimeSO`；业务侧无需感知。

> **序列化迁移说明：** `LoginCmdName` 对应的序列化字段 `m_LoginCmdName` 上标注了 `[FormerlySerializedAs("m_CmdName")]`，存量 .asset 中旧字段值（`m_CmdName`）由 Unity 反序列化迁移至 `m_LoginCmdName`，无需手动重填。`DeleteCmdName` 没有初始化默认指令名；使用账号删除能力前需在 ConfigWindow 填写项目实际 NetCmd 名称并重新导出。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `LoginKitConfig.cs` | `LoginKitConfig` | 登录 Kit 固有配置，实现 `IKitConfig` |

---

## §5 公开 API

| 签名 | 说明 |
|---|---|
| `public string LoginCmdName { get; }` | 登录协议 NetCmd 指令名；由 Inspector 序列化字段 `m_LoginCmdName` 支撑（[FormerlySerializedAs("m_CmdName")] 保证存量值自动迁移）；`Login.Async` 内部传给 `Nova.Network.ResolveNetCmdRow(LoginCmdName)` |
| `public string DeleteCmdName { get; }` | 账号删除协议 NetCmd 指令名；由 Inspector 序列化字段 `m_DeleteCmdName` 支撑；`Login.DeleteAsync` 内部传给 `Nova.Network.ResolveNetCmdRow(DeleteCmdName)` |
| `public string DisplayName { get; }` | 返回 `"Login 登录"`；供 ConfigWindow 左树展示节点名称 |
| `public LoginKitConfig()` | 无参构造器；供 ConfigWindow KitConfigScanner 通过 `Activator.CreateInstance` 创建空实例 |

两个指令字段的无参初始值都未指定，不能视为已有路由。实际保存值保留；示例名仅说明用法，不替代项目表源与服务端契约。

---

## §11 使用示例

```csharp
// ConfigWindow 中配置 LoginKitConfig.LoginCmdName = "GameLogin"
// ConfigWindow 中配置 LoginKitConfig.DeleteCmdName = "DeleteAccount"

// 运行时（Login.Async 内部）
LoginKitConfig config = Nova.Config.GetKitConfig<LoginKitConfig>();
if (config == null)
{
    throw new KitConfigMissingException(typeof(LoginKitConfig).FullName);
}
// config.LoginCmdName => "GameLogin"
// config.DeleteCmdName => "DeleteAccount"
```

---

## §13 关联文档

- 同包：[Login.md](./Login.md) — 调用方，`Async` 内拉取 `LoginCmdName`，`DeleteAsync` 内拉取 `DeleteCmdName`
- 关联包：[GameBind · Bind.md](../../../com.solotopia.nova.framework.kit.network.gamebind/Nova/Docs/Bind.md) — 账号绑定与冲突裁决协议
- 同包：[INDEX.md](./INDEX.md) — 本包文档总索引
