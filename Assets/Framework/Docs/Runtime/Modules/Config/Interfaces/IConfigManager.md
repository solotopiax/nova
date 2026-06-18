# IConfigManager

**类签名**：`public interface IConfigManager`
**命名空间**：`NovaFramework.Runtime`

Config 管理器对外契约；声明 AB 异步加载（幂等）、运行期配置读取、HybridCLR 参数读取、SDK PluginConfig 查询与 Kit 配置查询的完整生命周期 API。`ConfigComponent.Start()` 只做 `Initialize(...)`，不会自动等待或触发 `LoadAsync()`。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Managers/Interfaces/IConfigManager.cs` | `IConfigManager` | 接口定义 |

---

## §3 继承关系

```
IConfigManager（public interface）
  └── ConfigManagerBase (internal abstract) : IConfigManager
        └── ConfigManager (internal sealed partial)
```

---

## §4 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| `IsLoadOver` | `bool` | 是否已完成加载；`LoadAsync` 完成后为 true |
| `DevelopMode` | `DevelopMode` | 当前运行时开发模式；未加载时返回 `DevelopMode.Debug` |
| `Common` | `CommonConfig` | 全局公共配置整块；未加载时返回 null |
| `Namespace` | `string` | 全局业务命名空间；未加载时返回空字符串 |
| `GameEntranceProcedureName` | `string` | 业务入口 Procedure 相对类型名；LoadAsync 完成后可读 |
| `AotMetadataDlls` | `IReadOnlyList<DllAssetEntry>` | AOT 元数据 DLL 列表；未加载时返回空集合 |
| `GameDlls` | `IReadOnlyList<DllAssetEntry>` | 业务 DLL 列表；未加载时返回空集合 |
| `Platform` | `PlatformType` | 导出时写入的目标平台；未加载时返回 `PlatformType.None` |
| `Channel` | `ChannelType` | 导出时写入的目标渠道；未加载时返回 `ChannelType.None` |

---

## §5 完整公开 API

```csharp
public interface IConfigManager
{
    // --- 状态属性 ---
    bool IsLoadOver { get; }
    DevelopMode DevelopMode { get; }
    CommonConfig Common { get; }
    string Namespace { get; }
    string GameEntranceProcedureName { get; }
    IReadOnlyList<DllAssetEntry> AotMetadataDlls { get; }
    IReadOnlyList<DllAssetEntry> GameDlls { get; }
    PlatformType Platform { get; }
    ChannelType Channel { get; }

    // --- 初始化与加载 ---
    /// 初始化；由 ConfigComponent 在 Start 阶段构造 ConfigManagerConfig 后调用。
    /// 校验不通过时抛 ArgumentNullException / ArgumentException。
    void Initialize(ConfigManagerConfig config);

    /// 异步加载并持有 ConfigRuntimeSO；幂等（m_IsLoadOver 为 true 时直接返回）。
    /// 失败直接 Log.Error + throw，由 Procedure 层决定是否中断启动流程。
    UniTask LoadAsync();

    // --- PluginConfig 查询 ---
    /// 按泛型类型取 SDK Plugin 配置实例；未启用或类型不匹配返回 null。
    T GetSDKPluginConfig<T>() where T : class, ISDKPluginConfig;

    /// 按类型对象取 SDK Plugin 配置实例（非泛型版，供反射调用）；未启用或 type 为 null 返回 null。
    ISDKPluginConfig GetSDKPluginConfig(Type type);

    // --- KitConfig 查询 ---
    /// 按泛型类型取 Kit 配置实例；未启用或类型不匹配返回 null。
    T GetKitConfig<T>() where T : class, IKitConfig;

    /// 按类型对象取 Kit 配置实例（非泛型版，供反射调用）；未启用或 type 为 null 返回 null。
    IKitConfig GetKitConfig(Type type);

    /// 当前已加载的所有启用 SDK Plugin 配置集合；未加载时返回空集合。
    IReadOnlyCollection<ISDKPluginConfig> GetAllPluginConfigs();
}
```

---

## §11 使用示例

```csharp
// 游戏启动流程 Procedure 中（LoadAsync 幂等，已加载则直接返回）
await Nova.Config.LoadAsync();

// LoadAsync 完成后，IsLoadOver = true
string ns        = Nova.Config.Namespace;
DevelopMode mode = Nova.Config.DevelopMode;

// 其他模块在 Initialize 阶段通过 FrameworkManagersGroup 获取接口
IConfigManager cm = FrameworkManagersGroup.GetManager<IConfigManager>();
string ns2 = cm?.Namespace;

// SDKManager 内部按类型拉取 PluginConfig
ISDKPluginConfig config = cm.GetSDKPluginConfig(requiredConfigType);
MyPluginConfig typed    = cm.GetSDKPluginConfig<MyPluginConfig>();
```

---

## §13 关联文档

- [../Implements/ConfigManagerBase.md](../Implements/ConfigManagerBase.md)
- [../ConfigManager.md](../ConfigManager.md)
- [../ConfigComponent.md](../ConfigComponent.md)
- [../ConfigRuntimeSO.md](../ConfigRuntimeSO.md)
