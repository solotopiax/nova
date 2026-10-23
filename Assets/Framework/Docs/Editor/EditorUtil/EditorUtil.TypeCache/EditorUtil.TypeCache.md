# EditorUtil.TypeCache

**类签名**：`public static class EditorUtil.TypeCache`（`EditorUtil` 的嵌套静态类）
**命名空间**：`NovaFramework.Editor`
**一行描述**：编辑器类型缓存 — 按需反射收集指定基类/接口的所有实现类名称，编译后自动刷新。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|------|------|
| `EditorUtil.TypeCache.cs` | `EditorUtil.TypeCache` | 类型缓存核心（缓存字典 + 编译回调 + 延迟加载） |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── TypeCache (public static class)
```

---

## §4 关键字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `s_CachedTypeNames` | `Dictionary<string, string[]>` | 缓存：`Type.FullName` → 排序后的实现类全名数组 |
| `s_IsCacheValid` | `bool` | 缓存有效标记，编译后置 `false` |

---

## §5 公开 API

| 方法 | 签名 | 说明 |
|------|------|------|
| `GetTypeNames` | `string[] GetTypeNames(Type typeBase)` | 获取指定基类/接口的所有实现类名称（带缓存，首次访问时反射收集） |
| `InvalidateCache` | `void InvalidateCache()` | 手动使缓存失效并清空 |

---

## §7 线程模型

- `[InitializeOnLoadMethod]` 在编辑器启动时注册 `CompilationPipeline.compilationFinished` 回调
- 编译完成后自动调用 `InvalidateCache()`，下次 `GetTypeNames` 时重新反射收集
- 仅在主线程调用，无跨线程问题

---

## §11 使用示例

```csharp
// Inspector 中获取某接口的所有实现类名称（用于下拉框）
var managerTypeNames = new List<string>(EditorUtil.TypeCache.GetTypeNames(typeof(IConfigManager)));
```

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md)
- [BaseComponentInspector.md](../../Inspectors/BaseComponentInspector.md)
- [Editor.md](../../Editor.md)

## 缓存身份与失效

缓存键只有基类/接口的 `Type.FullName`，没有程序集身份；不同程序集同名类型可能碰撞。返回值是缓存数组本身，调用方改动数组会影响后续读取；不应把它作为可写工作列表。typeBase 不可为 null，工具也不提供后台线程同步。

Reset 仅清空并把缓存标成有效，不预热。加载回调将有效标记置 false 并订阅 compilationFinished；完成回调忽略上下文和编译错误状态，任何完成通知均清空缓存，不能据此证明新类型已成功编译、加载。类型名单的实际发现与排序由 `Util.Assembly.GetTypeNames` 决定。
