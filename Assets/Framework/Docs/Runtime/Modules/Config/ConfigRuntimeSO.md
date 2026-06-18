# ConfigRuntimeSO

**类签名**：`public sealed class ConfigRuntimeSO : ScriptableObject`
**命名空间**：`NovaFramework.Runtime`

运行时配置导出物 SO，由 ConfigWindow 导出覆盖写入，运行时由 Config 模块加载后解析；同时承载 HybridCLR 所需的 DLL 列表与业务入口 Procedure 名称。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Runtime/Modules/Config/ConfigRuntimeSO.cs` | `ConfigRuntimeSO` | ScriptableObject 定义 |

---

## §5 完整公开 API

```csharp
public sealed class ConfigRuntimeSO : ScriptableObject
{
    // --- 三维索引 ---
    public DevelopMode DevelopMode;   // 本次导出的开发模式，与 Platform/Channel 构成三维索引
    public string Namespace;          // 全局业务命名空间，直接从 ConfigMasterSO.Namespace 导出，不随 DevelopMode 变化
    public CommonConfig Common;       // 全局公共配置（已按 DevelopMode 单值化）
    public PlatformType Platform;     // 本次导出目标平台
    public ChannelType Channel;       // 本次导出目标渠道

    [SerializeReference]
    public List<ISDKPluginConfig> EnabledSDKConfigs = new();  // 启用的 SDK Plugin 配置列表

    [SerializeReference]
    public List<IKitConfig> EnabledKitConfigs = new();        // 启用的 Kit 配置列表；未启用类型不写入

    // --- HybridCLR 配置（W1 新增，由 ConfigMasterSO 同名字段导出） ---
    public string GameEntranceProcedureName;  // 业务入口 Procedure 相对类名（不含 namespace），如 "ProcedurePreload"
    public List<DllAssetEntry> AotMetadataDlls = new();   // AOT 元数据 DLL 列表；ProcedureLoadDll 按序加载支持泛型共享
    public List<DllAssetEntry> GameDlls = new();          // 业务 DLL 列表；ProcedureLoadDll 加载后注册业务程序集

    // --- SDK Plugin 查询 ---
    public T GetSDKPluginConfig<T>() where T : class, ISDKPluginConfig;
    public ISDKPluginConfig GetSDKPluginConfig(Type type);

    // --- Kit 配置查询 ---
    public T GetKitConfig<T>() where T : class, IKitConfig;
    public IKitConfig GetKitConfig(Type type);
}
```

### HybridCLR 字段语义

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `GameEntranceProcedureName` | `string` | `null` | 不含 namespace 的入口 Procedure 类名；ProcedureLoadDll 与 `Namespace` 拼接后跳转，为空时抛 `InvalidOperationException` |
| `AotMetadataDlls` | `List<DllAssetEntry>` | `new()` | AOT 元数据 DLL 寻址列表；IL2CPP 下并行加载，Editor 下 no-op |
| `GameDlls` | `List<DllAssetEntry>` | `new()` | 业务 DLL 寻址列表；IL2CPP 下顺序加载，Editor 下 no-op |

> 这三个字段由 ConfigWindow → **HybridCLR 配置** 面板编辑 `ConfigMasterSO` 同名字段后，通过 `EditorUtil.Config.Exporter` 导出时写入本 SO。

---

## §11 使用示例

```csharp
// 读取（运行时，await LoadAsync 后）
await Nova.Config.LoadAsync();
DevelopMode mode = Nova.Config.DevelopMode;
string ns = Nova.Config.Namespace;

// HybridCLR 字段由 ProcedureLoadDll 在框架启动期内部读取，业务层无需直接访问
```

---

## §13 关联文档

- [DevelopMode.md](../../Core/Definitions/DevelopMode.md)
- [CommonConfig.md](CommonConfig.md)
- [ConfigMasterSO.md](ConfigMasterSO.md)
- [DllAssetEntry.md](Definitions/DllAssetEntry.md)
- [ProcedureLoadDll.md](../Procedure/Procedures/ProcedureLoadDll.md)（读取本 SO HybridCLR 字段的消费方）
- [EditorUtil.Config.Exporter.md](../../../Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.Exporter.md)
