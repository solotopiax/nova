# ConfigManagerBase

**类签名**：`internal abstract class ConfigManagerBase : FrameworkManager, IConfigManager`
**命名空间**：`NovaFramework.Runtime`

配置管理器抽象基类；继承 `FrameworkManager` 并声明 `IConfigManager` 全部抽象成员。派生类 `ConfigManager` 提供 AB 异步加载 + 预加载短路 + SDK PluginConfig 透传的具体实现。`Priority = 10`。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Managers/Implements/ConfigManagerBase.cs` | `ConfigManagerBase` | 抽象基类定义 |

---

## §3 继承关系

```
FrameworkManager
  └── ConfigManagerBase (internal abstract) : IConfigManager   Priority = 10
        └── ConfigManager (internal sealed partial)
```

---

## §4 关键字段表

无实例字段（所有状态由子类 `ConfigManager` 声明）。

| 属性/方法 | 修饰符 | 说明 |
|---|---|---|
| `Priority` | `public override int` | 固定返回 `10` |

---

## §5 完整公开 API

```csharp
// --- 优先级 ---
public override int Priority => 10;

// --- abstract 声明（子类必须实现）---
public abstract void Initialize(ConfigManagerConfig config);
public abstract override void Update();
public abstract override void Shutdown();
public abstract UniTask LoadAsync();

// --- 状态属性（abstract）---
public abstract bool IsLoadOver { get; }
public abstract DevelopMode DevelopMode { get; }
public abstract CommonConfig Common { get; }
public abstract string Namespace { get; }
public abstract PlatformType Platform { get; }
public abstract ChannelType Channel { get; }

// --- SDK PluginConfig 查询（abstract）---
public abstract T GetSDKPluginConfig<T>() where T : class, ISDKPluginConfig;
public abstract ISDKPluginConfig GetSDKPluginConfig(Type type);
public abstract IReadOnlyCollection<ISDKPluginConfig> GetAllPluginConfigs();
```

---

## §11 使用示例

ConfigManagerBase 不直接使用，由框架通过 `Util.TypeCreator.Create<IConfigManager>(typeName)` 反射创建 `ConfigManager` 实例。`ConfigComponent.Start()` 只负责调用 `Initialize(...)` 注入 `AssetLocation`，真正的配置加载仍要由上层显式调用 `LoadAsync()`。

```csharp
// ConfigComponent.Awake 中（框架内部）
IConfigManager manager = Util.TypeCreator.Create<IConfigManager>("NovaFramework.Runtime.ConfigManager");
// 运行时 manager 实际为 ConfigManager 实例，通过接口访问
```

---

## §13 关联文档

- [IConfigManager.md](../Interfaces/IConfigManager.md)
- [ConfigManager.md](../ConfigManager.md)
- [ConfigComponent.md](../ConfigComponent.md)
