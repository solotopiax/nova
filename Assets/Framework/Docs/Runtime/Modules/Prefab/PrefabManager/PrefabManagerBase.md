# PrefabManagerBase

**类签名**：`internal abstract class PrefabManagerBase : FrameworkManager, IPrefabManager`
**命名空间**：`NovaFramework.Runtime`

PrefabManager 抽象基类，声明 IPrefabManager 全部成员的 abstract 形式。Priority = 10。

---

## 文件

| 文件 | 类 | 说明 |
|------|-----|------|
| `Managers/PrefabManager/Implements/PrefabManagerBase.cs` | `PrefabManagerBase` | 基类定义 |

---

## 继承关系

```
FrameworkManager
  └── PrefabManagerBase (internal abstract) : IPrefabManager
        └── PrefabManager (internal sealed partial)
```

---

## 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `Priority` | `int` | `10` | 调度优先级（override），高于 AssetManager（4）确保后初始化先关闭 |

---

## 完整公开 API

```csharp
public override int Priority => 10;

public abstract void Initialize(PrefabManagerConfig config);
public abstract override void Update();
public abstract override void Shutdown();
public abstract GameObject InstantiateSync(string location, Transform parent = null);
public abstract T InstantiateSync<T>(string location, Transform parent = null) where T : Component;
public abstract UniTask<GameObject> InstantiateAsync(string location, Transform parent = null, CancellationToken ct = default);
public abstract UniTask<T> InstantiateAsync<T>(string location, Transform parent = null, CancellationToken ct = default) where T : Component;
public abstract void Destroy(GameObject instance);
public abstract int RecordedInstanceCount { get; }
public abstract IReadOnlyList<PrefabRecordedInstance> RecordedInstances { get; }
```

---

## 使用示例

```csharp
// 不直接使用，由 PrefabManager 继承实现
```

---

## 关联文档

- [IPrefabManager.md](IPrefabManager.md)
- [PrefabManager.md](PrefabManager.md)
