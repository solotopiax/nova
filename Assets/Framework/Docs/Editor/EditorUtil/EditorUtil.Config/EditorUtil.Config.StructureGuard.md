# EditorUtil.Config.StructureGuard

**类签名**：`public static class StructureGuard`（嵌套于 `EditorUtil.Config`）
**命名空间**：`NovaFramework.Editor`

`ConfigMasterSO` 结构巡检工具，负责 Platform×Channel 枚举网格补齐与清理、`SerializeReference` 丢失引用检测与清理。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.StructureGuard.cs` | `EditorUtil.Config.StructureGuard` | 工具类定义（含 MissingRef 嵌套结构体） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Config (public static partial class)
        └── StructureGuard (public static class)
              └── MissingRef (public readonly struct，嵌套类型)
```

---

## §4 关键字段表

`MissingRef` 嵌套结构体字段：

| 字段 | 类型 | 说明 |
|------|------|------|
| `EntryIndex` | `readonly int` | 该条目在 `ConfigMasterSO.EditorEntries` 中的索引 |
| `MissingCount` | `readonly int` | 该 Entry 的 `SDKConfigs` / `KitConfigs` 列表中 null 占位的总数量 |

---

## §5 完整公开 API

```csharp
// 同步 ConfigMasterSO 矩阵与当前枚举成员：新增补空行，废弃移除；忽略 None；完成后 SetDirty
// master 为 null 时直接返回
public static void SyncEnumGrid(ConfigMasterSO master);

// 检测矩阵中 SDKConfigs / KitConfigs 因脚本删除导致的 null 占位
// 返回含缺失引用的 Entry 索引与 null 数量列表；master 为 null 时返回空列表
public static List<MissingRef> DetectMissingPluginRefs(ConfigMasterSO master);

// 清理所有 Entry 的 SDKConfigs / KitConfigs 中的 null 占位；完成后 SetDirty
// master 为 null 时直接返回
public static void CleanMissingPluginRefs(ConfigMasterSO master);
```

---

## §9 关键算法

### SyncEnumGrid — 枚举网格补齐

```
SyncEnumGrid(master):
  1. wanted = PlatformType × ChannelType 所有非 None 组合的 HashSet
  2. 从末尾向前遍历 master.EditorEntries：
     - key 不在 wanted 中 → EditorRemoveEntryAt(i)（废弃成员）
     - key 已在 present 中 → EditorRemoveEntryAt(i)（重复行）
     - 否则 → present.Add(key)
  3. 遍历 wanted：不在 present 中 → EditorAddEntry(new PlatformChannelEntry{Platform, Channel})
  4. EditorUtility.SetDirty(master)
```

---

## §11 使用示例

```csharp
// ConfigWindow.OnEnable 中自动调用
EditorUtil.Config.StructureGuard.SyncEnumGrid(master);

// 检测并清理缺失引用
var missing = EditorUtil.Config.StructureGuard.DetectMissingPluginRefs(master);
if (missing.Count > 0)
{
    EditorUtil.Config.StructureGuard.CleanMissingPluginRefs(master);
}
```

---

## §12 注意事项

- `SyncEnumGrid` 在 `PlatformType` 或 `ChannelType` 枚举新增成员时会自动补空行；移除枚举成员时会清除对应行（该行下 SDK / Kit / Common 数据都会随矩阵行移除）
- `DetectMissingPluginRefs` 仅检测，不修改；需要清理时单独调用 `CleanMissingPluginRefs`

---

## §13 关联文档

- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)
- [PlatformChannelEntry.md](../../../Runtime/Modules/Config/PlatformChannelEntry.md)
- [ConfigWindow.md](../../Windows/ConfigWindow.md)
