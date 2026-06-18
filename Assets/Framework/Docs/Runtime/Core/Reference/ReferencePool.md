# ReferencePool

**类签名**：`public static class ReferencePool`
**命名空间**：`NovaFramework.Runtime`

零 GC 纯数据对象复用池（静态类，线程安全）。适用于高频创建/销毁的纯 C# 数据对象（EventData、OpenUIViewInfo、UIViewInfo 等）。对象必须实现 `IReference` 接口。

> 与 `IObjectPool<T>` 的区别：ReferencePool 用于无资源依赖的纯数据对象；ObjectPool 用于绑定了 GameObject/Audio 等资源的对象。

---

## 文件

| 文件 | 说明 |
|------|------|
| `IReference.cs` | 可归还接口，含 `Clear()` 方法 |
| `ReferencePool.cs` | 静态引用池（`Get<T>` / `Put<T>`） |
| `ReferencePoolInfo.cs` | 单类型池信息快照（struct，用于调试展示） |
| `ReferenceStrictCheckType.cs` | 枚举：严格检查模式（参见 Nova 配置） |
| `Interfaces/IReferenceHelper.cs` | 引用助手接口（可替换底层实现） |
| `Implements/ReferenceHelper.cs` | 默认实现，持有 `Dictionary<Type, ReferenceCollection>` |
| `Implements/ReferenceHelper.ReferenceCollection.cs` | 单类型集合（`Queue<IReference>` 存储空闲对象，StrictCheck 模式下辅以 `HashSet` O(1) 去重） |

---

## IReference 接口

```csharp
public interface IReference
{
    // 归还池时被自动调用，必须重置所有字段到初始状态
    void Clear();
}
```

**实现规范：**
- `Clear()` 中必须将所有字段还原为默认值（`null`、`0`、`false` 等）
- 不可在 `Clear()` 中访问已被释放的资源
- 不可继承 `MonoBehaviour`（ReferencePool 仅适用于纯 C# 对象）

---

## ReferencePool API

```csharp
// 从池中取出对象（池空时 new T()）
T obj = ReferencePool.Get<T>() where T : class, IReference, new()

// 归还对象（内部调用 obj.Clear() 后入池）
ReferencePool.Put<T>(T obj) where T : class, IReference

// 预热：预先创建指定数量的对象
ReferencePool.Add<T>(int count) where T : class, IReference, new()

// 清空指定类型的池
ReferencePool.Remove<T>(int count) where T : class, IReference

// 清空所有类型的池（FrameworkManagersGroup.Shutdown 结束时调用）
ReferencePool.ClearAll()

// 获取所有类型的池信息快照（调试用）
ReferencePoolInfo[] infos = ReferencePool.GetAllReferencePoolInfos()
```

---

## ReferencePoolInfo 字段表

```csharp
[StructLayout(LayoutKind.Auto)]
public struct ReferencePoolInfo
{
    Type   Type                   // 对应的 IReference 类型
    int    UnusedReferenceCount   // 当前空闲（池中未使用）的对象数量
    int    UsingReferenceCount    // 正在使用中的对象数量
    int    GetReferenceCount      // 历史 Get 总次数
    int    PutReferenceCount      // 历史 Put 总次数
    int    AddReferenceCount      // 历史 Add（预热/new）总次数
    int    RemoveReferenceCount   // 历史 Remove 总次数
}
```

---

## ReferenceStrictCheckType 枚举

| 值 | 行为 | 推荐场景 |
|----|------|---------|
| `AlwaysEnable` | 始终检查（Put 时验证对象是否来自此池） | 开发全阶段 |
| `OnlyEnableWhenDevelopment` | 仅 Debug Build 启用 | CI/测试包 |
| `OnlyEnableInEditor` | 仅编辑器启用 | 本地开发 |
| `AlwaysDisable` | 关闭检查（性能最优） | Release 包 |

通过 `Nova.m_ReferenceStrictCheckType`（Inspector）配置，`Awake` 时传入 `ReferencePool.SetHelper(helper.Initialize(strictCheck))`。

---

## 在框架中的使用方

| 类 | Get 时机 | Put 时机 |
|----|---------|---------|
| `UIViewInfo` | `UIGroup.AddUIView()` | `UIGroup.RemoveUIView()` |
| `OpenUIViewInfo` | `UIManager.OpenUIViewAsync()` | 回调完成后 |
| `EventData` 子类 | `XXXEvent.Create()` | `EventPool` 分发完毕后 |
| `UIViewInstanceObject` | `UIManager.InternalOpenUIView()` 注册时 | `ObjectPool` 回收时 |

---

## 常见误区（§10）

| 误区 | 正确理解 |
|------|---------|
| `Clear()` 中未重置所有字段 | 若字段未清空，下次 `Get()` 拿到的对象可能带有上次的脏数据 |
| `Put()` 后仍然持有对象引用并访问字段 | `Put()` 内部调用 `Clear()`，字段已重置，访问结果不可预期 |
| 在 handler 外持有 `EventData` 引用 | `EventData` 在 handler 返回后立即 `Put()` 归还，字段已被 `Clear()` |
| 对 MonoBehaviour 子类用 ReferencePool | ReferencePool 仅适用于纯 C# 对象（`new T()`）；MonoBehaviour 不能直接 new |
| 以为 `Get()` 返回的对象字段是初始值 | 若 `Clear()` 实现不完整，字段可能有上次使用的残留值 |
| 忘记在 `Clear()` 中重置集合类型 | `List`/`Dictionary` 字段需 `Clear()` 而非 `= null`（避免频繁 GC） |
| 在 Helper 未初始化时调用 API | 所有公开方法在 `s_ReferenceHelper == null` 时抛出 `InvalidOperationException`，确保初始化错误尽早暴露 |

---

## 自定义示例

```csharp
public class DamageData : IReference
{
    public int Damage { get; private set; }
    public int TargetID { get; private set; }

    public static DamageData Create(int damage, int targetID)
    {
        DamageData d = ReferencePool.Get<DamageData>();
        d.Damage = damage;
        d.TargetID = targetID;
        return d;
    }

    public void Clear()
    {
        Damage = 0;
        TargetID = 0;
    }
}

// 使用
DamageData d = DamageData.Create(100, heroID);
// ... 使用 d ...
ReferencePool.Put(d);   // d 归还后不可再访问
```

---

## 关联文档

- [ObjectPoolManager.md](../../Modules/ObjectPool/ObjectPoolManager.md)（资源对象池）
- [EventManager.md](../../Modules/Event/EventManager.md)（EventData 使用 ReferencePool）
- [UIManager.md](../../Modules/UI/UIManager/UIManager.md)（UIViewInfo / OpenUIViewInfo 使用 ReferencePool）
