# LoginKitConfig

## §1 文件头

**所在文件：** `Nova/Scripts/Runtime/LoginKitConfig.cs`
**命名空间：** `NovaFramework.Kit.Network.GameLogin.Runtime`
**类签名：** `[Serializable] public sealed class LoginKitConfig : IKitConfig`

登录 Kit 固有配置，持有登录协议指令名（LoginCmdName）与账号删除协议指令名（DeleteCmdName），分别由 `Login.Async` / `Login.DeleteAsync` 在运行时通过 `Nova.Config.GetKitConfig<LoginKitConfig>()` 拉取。在 ConfigWindow「Kit 配置」面板中全局静态配置一次，业务侧无需感知。

> **序列化迁移说明：** `LoginCmdName` 对应的序列化字段 `m_LoginCmdName` 上标注了 `[FormerlySerializedAs("m_CmdName")]`，存量 .asset 中旧字段值（`m_CmdName`）在 Unity 域重载后自动迁移至 `m_LoginCmdName`，无需手动重填登录指令名。`DeleteCmdName` 为新增字段，存量 .asset 中初始为空，需在 ConfigWindow 补填后重导出。

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
- 同包：[INDEX.md](./INDEX.md) — 本包文档总索引
