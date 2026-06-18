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
