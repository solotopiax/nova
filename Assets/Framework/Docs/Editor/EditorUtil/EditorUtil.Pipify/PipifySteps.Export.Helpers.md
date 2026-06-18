# PipifySteps.Export.Helpers

**类签名**：`internal static class Helpers`（嵌套于 `PipifySteps`）
**命名空间**：`NovaFramework.Editor`

Pipify 导出 Step 共用辅助方法：定位 ConfigMasterSO 和从当前活动场景的 Nova 根节点获取 XxxComponent，避免各导出 Step 重复实现定位逻辑。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Pipify/Steps/PipifySteps.Export.Helpers.cs` | `PipifySteps.Helpers` | 导出辅助方法（嵌套于 PipifySteps） |

---

## §3 继承关系

```
PipifySteps (internal static partial class)
  └── Helpers (internal static class)
```

---

## §4 关键字段表

无字段（静态工具类）。

---

## §5 完整公开 API

```csharp
// 定位工程内唯一的 ConfigMasterSO 资产
// 未找到时抛出 InvalidOperationException（消息：未找到 ConfigMasterSO）
internal static ConfigMasterSO ResolveConfigMaster();

// 从当前活动场景的 Nova 根节点上获取指定类型的 Component
// 场景中未找到 Nova 根节点时抛出 InvalidOperationException
//   消息：未在当前场景找到 Nova 根节点，请打开挂载 Nova 组件的场景后重跑 Pipify
// Nova 根节点未挂载 T 时抛出 InvalidOperationException
//   消息：Nova 根节点未挂载 {T.Name}，请先在 Nova 根节点添加该组件
internal static T ResolveComponentOnNova<T>() where T : Component;
```

---

## §9 关键算法

### ResolveConfigMaster

```
ResolveConfigMaster():
  master = EditorUtil.Asset.Operator.Find<ConfigMasterSO>()
  master == null → throw InvalidOperationException("[Pipify] 未找到 ConfigMasterSO...")
  return master
```

### ResolveComponentOnNova\<T\>

```
ResolveComponentOnNova<T>():
  nova = Object.FindAnyObjectByType<Nova>(FindObjectsInactive.Include)
  nova == null → throw InvalidOperationException(
    "[Pipify] 未在当前场景找到 Nova 根节点，请打开挂载 Nova 组件的场景后重跑 Pipify")
  component = nova.GetComponent<T>()
  component == null → throw InvalidOperationException(
    "[Pipify] Nova 根节点未挂载 {T.Name}，请先在 Nova 根节点添加该组件")
  return component
```

> **为何选场景实例而非 Prefab 磁盘文件**：Prefab 磁盘序列化可能滞后于开发者在场景中对组件的编辑态改动；而场景实例（包含非激活对象）始终反映最新状态，确保 Pipify 读取的 Settings 与运行时一致。

---

## §10 常见误区

| 误区 | 正确理解 |
|------|---------|
| ConfigMasterSO 未找到 | 工程内必须有且只有一个 ConfigMasterSO 资产，通过 `EditorUtil.Asset.Operator.Find<ConfigMasterSO>()` 按类型搜索 |
| 当前场景无 Nova 根节点 | 需打开挂载 `Nova` 组件的场景后再跑 Pipify；`ResolveComponentOnNova<T>()` 使用 `FindObjectsInactive.Include` 查找，非激活的 Nova 也能找到 |
| Nova 根节点无 Component | 对应模块的 Component（如 `TableComponent`）须预先挂载在场景中的 Nova 根节点上，Pipify Step 才能通过 `GetComponent<T>()` 取得 |

---

## §11 使用示例

```csharp
// 在导出 Step 中定位 ConfigMasterSO
ConfigMasterSO master = PipifySteps.Helpers.ResolveConfigMaster();

// 在导出 Step 中定位 TableComponent（从当前场景 Nova 根节点获取）
TableComponent tableComponent = PipifySteps.Helpers.ResolveComponentOnNova<TableComponent>();

// 在导出 Step 中定位 SoundComponent（从当前场景 Nova 根节点获取）
SoundComponent soundComponent = PipifySteps.Helpers.ResolveComponentOnNova<SoundComponent>();
```

---

## §12 注意事项

| 场景 | 说明 |
|------|------|
| 前置条件 | 当前活动场景已挂载 Nova 根节点；`FindAnyObjectByType` 含 `FindObjectsInactive.Include`，非激活的 Nova 对象亦可被发现 |
| 多场景叠加加载 | `FindAnyObjectByType` 在所有已加载场景中搜索，返回第一个匹配的 Nova；如有多个场景同时加载且各含 Nova 对象，返回结果不确定，应确保 Pipify 运行时只有一个 Nova 实例 |

---

## §13 关联文档

- [PipifySteps.md](./PipifySteps.md)
- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)
- [EditorUtil.Asset.Operator.md](../EditorUtil.Asset/EditorUtil.Asset.Operator.md)
