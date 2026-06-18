# EditorUtil.Config.SDKPluginScanner

**类签名**：`public static class SDKPluginScanner`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

反射扫描全部已加载程序集中符合条件的 `ISDKPluginConfig` 实现类型，并提供对 `PlatformChannelEntry` 指定 `DevelopMode` 的 SDKConfigs 列表的实例补全与移除操作。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.SDKPluginScanner.cs` | `EditorUtil.Config.SDKPluginScanner` | 扫描器工具类 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Config (public static partial class)
        └── SDKPluginScanner (public static class)
```

---

## §4 关键字段表

无字段（静态工具类）。

---

## §5 完整公开 API

```csharp
// 扫描全程序集中满足条件的 Plugin Config 类型
// 条件：非抽象、非接口、实现 ISDKPluginConfig、标注 [Serializable]
// 无匹配时返回空列表
public static List<Type> ScanAll();

// 若 Entry 指定 DevelopMode 的 SDKConfigs 缺少指定类型实例，反射补空实例并追加；已存在返回 false
// entry 或 configType 为 null 时返回 false
public static bool EnsureInstance(PlatformChannelEntry entry, DevelopMode mode, Type configType);

// 从 Entry 指定 DevelopMode 的 SDKConfigs 移除所有指定类型的实例
// entry 或 configType 为 null 时提前返回
public static void RemoveInstance(PlatformChannelEntry entry, DevelopMode mode, Type configType);
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
      typeof(ISDKPluginConfig).IsAssignableFrom(t) &&
      t.GetCustomAttribute<SerializableAttribute>() != null)
    .ToList()
```

`SafeGetTypes` 在程序集包含加载失败类型时捕获 `ReflectionTypeLoadException` 并返回 `e.Types` 非 null 部分，避免中断整个扫描。

---

## §11 使用示例

```csharp
// ConfigWindow.OnEnable 中缓存扫描结果
List<Type> pluginTypes = EditorUtil.Config.SDKPluginScanner.ScanAll();

// SDK 左树勾选时向所有 Entry 所有 DevelopMode 补实例
DevelopMode[] allModes = (DevelopMode[])System.Enum.GetValues(typeof(DevelopMode));
for (int i = 0; i < allEntries.Count; i++)
{
    for (int m = 0; m < allModes.Length; m++)
    {
        EditorUtil.Config.SDKPluginScanner.EnsureInstance(allEntries[i], allModes[m], pluginType);
    }
}

// SDK 左树取消勾选时（当前 ConfigWindow 实现中不移除实例，仅从 EnabledSDKs 移除类型名）
EditorUtil.Config.SDKPluginScanner.RemoveInstance(entry, mode, pluginType);
```

---

## §12 注意事项

- `ScanAll` 每次调用都会遍历全程序集，建议结果缓存（ConfigWindow 在 `RefreshPluginCache` 中缓存到 `m_PluginTypeCache`）
- `EnsureInstance` 按类型判等（`c.GetType() == configType`），同类型最多一个实例

---

## §13 关联文档

- [PlatformChannelEntry.md](../../../Runtime/Modules/Config/PlatformChannelEntry.md)
- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
