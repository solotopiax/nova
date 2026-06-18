# IKitConfig

**类签名**：`public interface IKitConfig`
**命名空间**：`NovaFramework.Runtime`
**程序集**：`NovaFramework.Runtime`

Kit 固有配置 marker 接口；实现类按 Platform×Channel×DevelopMode 三维存储于 `PlatformChannelEntry.KitConfigsByMode`，由 Exporter 按坐标导出到 `ConfigRuntimeSO.EnabledKitConfigs`，Kit Service 方法内通过 `Nova.Config.GetKitConfig<T>()` 拉取使用。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/Definitions/IKitConfig.cs` | `IKitConfig` | marker 接口定义 |

---

## §5 完整公开 API

```csharp
public interface IKitConfig
{
    /// <summary>
    /// 配置项在 ConfigWindow 左树中展示的名称，不可为空。
    /// </summary>
    string DisplayName { get; }
}
```

---

## §11 使用示例

```csharp
// Kit 子包定义实现类（AOT 程序集，非热更 DLL）
[Serializable]
public sealed class LoginKitConfig : IKitConfig
{
    public string DisplayName => "Login 登录";

    [SerializeField, Tooltip("Luban NetCmd 表指令名，如 GameLogin")]
    private string m_CmdName;
    public string CmdName => m_CmdName;
}

// Kit Service 内部拉取配置
LoginKitConfig cfg = Nova.Config.GetKitConfig<LoginKitConfig>();
string cmd = cfg.CmdName;
```

---

## §12 注意事项

- 实现类必须落 AOT 程序集（热更红线）：`[SerializeReference]` 类型需在 AOT 层才可序列化
- 实现类按 Platform×Channel×DevelopMode 三维格存储（ADR-054 反转 ADR-053 决策 3）；`ConfigMasterSO.EnabledKits` 为全局白名单，控制哪些 Kit 类型参与导出
- 取不到配置时抛 `KitConfigMissingException`（fail-fast，参见同目录 `KitConfigMissingException.md`）

---

## §13 关联文档

- [KitConfigMissingException.md](KitConfigMissingException.md)
- [ConfigRuntimeSO.md](../ConfigRuntimeSO.md)
- [ConfigMasterSO.md](../ConfigMasterSO.md)
