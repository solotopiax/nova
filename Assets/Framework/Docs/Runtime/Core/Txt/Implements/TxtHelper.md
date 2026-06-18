# TxtHelper

**类签名**：`internal sealed class TxtHelper : ITxtHelper`
**命名空间**：`NovaFramework.Runtime`

Unity 平台文本工具实现，实现 `ITxtHelper` 接口。使用 `[ThreadStatic]` 修饰的 `StringBuilder` 作为线程静态缓存，避免频繁内存分配。提供 1~16 个泛型参数的 `Format` 重载以减少装箱。支持前缀拼接缓存（`GetCachedFullString`）以避免重复字符串拼接。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Implements/TxtHelper.cs` | 文本工具实现 |

## 关键字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `s_CachedStringBuilder` | `StringBuilder` | 线程静态缓存，每个线程独立 |
| `c_StringBuilderCapacity` | `int` | StringBuilder 初始容量（1024） |
| `s_PrefixCache` | `ConcurrentDictionary<string, string>` | 前缀拼接字符串的线程安全缓存 |
| `s_FullPrefixToKeyWordsMap` | `ConcurrentDictionary<string, ConcurrentDictionary<string, string>>` | 完整前缀到关键字映射的线程安全缓存 |

## 公开 API

```csharp
void Initialize();

// 格式化（1~16 个泛型参数的重载）
string Format(string format, params object[] args);
string Format<T>(string format, T arg);
// ... 最多支持 16 个泛型参数

// 缓存拼接
string GetCachedFullString(string keyWords, params string[] prefixes);

// 字符串填充
string FillGap(string word, int length = 15);
```

## 关联文档

- [Txt](../Txt.md)
- [ITxtHelper](../Interfaces/ITxtHelper.md)
