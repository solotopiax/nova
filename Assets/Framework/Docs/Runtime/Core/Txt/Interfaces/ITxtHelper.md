# ITxtHelper

**类签名**：`public interface ITxtHelper`
**命名空间**：`NovaFramework.Runtime`

文本工具接口，定义了字符串格式化及缓存拼接的统一契约。`Txt` 静态门面通过该接口委托实际的文本处理逻辑，支持替换底层实现。提供 1~16 个泛型参数的 `Format` 重载以减少装箱开销。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `Interfaces/ITxtHelper.cs` | 接口定义 |

## 公开 API

```csharp
void Initialize();

// 格式化（支持 params 及 1~16 个泛型参数的重载）
string Format(string format, params object[] args);
string Format<T>(string format, T arg);
string Format<T1, T2>(string format, T1 arg1, T2 arg2);
// ... 最多支持 16 个泛型参数

// 缓存拼接
string GetCachedFullString(string keyWords, params string[] prefixes);

// 字符串填充
string FillGap(string word, int length = 15);
```

## 关联文档

- [Txt](../Txt.md)
- [TxtHelper](../Implements/TxtHelper.md)
