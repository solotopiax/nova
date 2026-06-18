# EditorUtil.Config.KitConfigScanner

**类签名**：`public static class KitConfigScanner`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

反射扫描全部已加载程序集中符合条件的 `IKitConfig` 实现类型，并提供对 `ConfigMasterSO.KitConfigs` 全局列表的实例补全与移除操作。

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

// 若全局 KitConfigs 列表中缺少指定类型实例，反射补空实例并追加；已存在返回 false
// configs 或 configType 为 null 时返回 false
public static bool EnsureInstance(List<IKitConfig> configs, Type configType);

// 从全局 KitConfigs 列表移除所有指定类型的实例
// configs 或 configType 为 null 时提前返回
public static void RemoveInstance(List<IKitConfig> configs, Type configType);
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

与 `SDKPluginScanner.ScanAll` 的区别：返回 `List<KitConfigEntry>`（含 DisplayName），而非 `List<Type>`。

---

## §10 常见误区

- `EnsureInstance` / `RemoveInstance` 直接接受 `List<IKitConfig>`（全局单份），无 PlatformChannelEntry / DevelopMode 参数——与 SDKPluginScanner 不同，不要按 SDK 模式调用。
- 扫描结果每次调用都遍历全程序集，建议调用方缓存结果。

---

## §11 使用示例

```csharp
// ConfigWindow.OnEnable 中缓存扫描结果
List<KitConfigEntry> kitEntries = EditorUtil.Config.KitConfigScanner.ScanAll();

// Kit 左树勾选时向全局 KitConfigs 补实例
EditorUtil.Config.KitConfigScanner.EnsureInstance(masterSO.KitConfigs, kitEntry.ConfigType);

// Kit 左树取消勾选时移除实例
EditorUtil.Config.KitConfigScanner.RemoveInstance(masterSO.KitConfigs, kitEntry.ConfigType);
```

---

## §12 注意事项

- `EnsureInstance` 按类型判等（`c.GetType() == configType`），同类型最多一个实例。
- 禁止 typeof 硬引用任何 Kit 子包具体类型（如 LoginKitConfig），框架 Editor 层不编译期依赖 Kit 包。

---

## §13 关联文档

- [IKitConfig.md](../../../Runtime/Modules/Config/Definitions/IKitConfig.md)
- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)
- [EditorUtil.Config.SDKPluginScanner.md](./EditorUtil.Config.SDKPluginScanner.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
