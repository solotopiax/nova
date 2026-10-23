# EditorUtil.Config.KitConfigScanner

**类签名**：`public static class KitConfigScanner`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

反射扫描已加载的非测试程序集中符合条件的 `IKitConfig` 实现类型，并管理 `PlatformChannelEntry` 指定 `DevelopMode` 格的 Kit 配置实例。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.KitConfigScanner.cs` | `EditorUtil.Config.KitConfigScanner` | 扫描器工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Config (public static partial class)
        └── KitConfigScanner (public static class)
```

---

## §4 关键字段表

无字段（静态工具类）。

---

## §5 完整公开 API

```csharp
// 扫描全程序集中满足条件的 Kit Config 类型，并读取实例 DisplayName
// 条件：非抽象、非接口、实现 IKitConfig、标注 [Serializable]
// 无匹配时返回空列表
public static List<KitConfigEntry> ScanAll();

// 若指定格缺少同类型实例，反射构造并追加；已存在返回 false
// entry 或 configType 为 null 时返回 false
public static bool EnsureInstance(PlatformChannelEntry entry, DevelopMode mode, Type configType);

// 从指定格移除所有同类型的非 null 实例
// entry 或 configType 为 null 时提前返回
public static void RemoveInstance(PlatformChannelEntry entry, DevelopMode mode, Type configType);
```

### KitConfigEntry（嵌套结构体）

```csharp
public readonly struct KitConfigEntry
{
    public readonly Type ConfigType;    // Kit Config 类型
    public readonly string DisplayName; // 左树展示名称（实例 DisplayName 或类型名回退）
}
```

---

## §9 关键算法

### ScanAll — 全程序集扫描

```
ScanAll():
  AppDomain.CurrentDomain.GetAssemblies()
    .SelectMany(SafeGetTypes)     ← ReflectionTypeLoadException 安全包装
    .Where(t =>
      !t.IsAbstract &&
      !t.IsInterface &&
      typeof(IKitConfig).IsAssignableFrom(t) &&
      t.GetCustomAttribute<SerializableAttribute>() != null)
  foreach type:
    尝试 Activator.CreateInstance(type) 读 DisplayName → 失败时回退类型名
```

`SafeGetTypes` 在程序集包含加载失败类型时捕获 `ReflectionTypeLoadException` 并返回 `e.Types` 非 null 部分，避免中断整个扫描。

`ScanAll` 排除引用 `nunit.framework` 的程序集。与 `SDKPluginScanner.ScanAll` 一样，结果包含 `ConfigType` 与 `DisplayName`；SDK 返回的包装类型是 `PluginConfigEntry`。构造失败的候选仍以类型名加入扫描结果，因此出现在列表中不代表能够成功实例化。

---

## §10 常见误区

- `EnsureInstance` / `RemoveInstance` 接受 `PlatformChannelEntry` 与 `DevelopMode`，只操作该三维格的 `List<IKitConfig>`；`EnabledKits` 仅为导出白名单，Exporter 将当前格导出为单格 `ConfigRuntimeSO`。
- 扫描结果每次调用都遍历全程序集，建议调用方缓存结果。

---

## §11 使用示例

```csharp
// ConfigWindow.OnEnable 中缓存扫描结果
var kitEntries = EditorUtil.Config.KitConfigScanner.ScanAll();

if (kitEntries.Count > 0 &&
    master.TryGetEntry(PlatformType.Android, ChannelType.Google, out PlatformChannelEntry entry))
{
    Type kitType = kitEntries[0].ConfigType;
    EditorUtil.Config.KitConfigScanner.EnsureInstance(entry, DevelopMode.Debug, kitType);

    // 如需显式删除该格实例，可调用下列方法；ConfigWindow 取消勾选不会调用它。
    // EditorUtil.Config.KitConfigScanner.RemoveInstance(entry, DevelopMode.Debug, kitType);
}
```

ConfigWindow 勾选 Kit 时遍历已有矩阵行与全部开发模式补实例；取消勾选只移除 `EnabledKits` 的类型名，保留已填配置。Scanner 本身不修改启用列表，也不负责标脏或保存资产。

---

## §12 注意事项

- `EnsureInstance` 按类型判等（`c.GetType() == configType`）；已有同类型实例时不再追加，但不会清理旧重复项。
- `EnsureInstance` 不捕获构造或类型转换异常，也不单独验证类型和模式；`RemoveInstance` 通过 `GetKitConfigs(mode)` 取列表，缺模式时仍可能先追加空条目。
- 禁止 typeof 硬引用任何 Kit 子包具体类型（如 LoginKitConfig），框架 Editor 层不编译期依赖 Kit 包。

---

## §13 关联文档

- [IKitConfig.md](../../../Runtime/Modules/Config/Definitions/IKitConfig.md)
- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)
- [EditorUtil.Config.SDKPluginScanner.md](./EditorUtil.Config.SDKPluginScanner.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
