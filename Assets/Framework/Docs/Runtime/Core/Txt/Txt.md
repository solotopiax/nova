# Txt

**类签名**：`public static class Txt`
**命名空间**：`NovaFramework.Runtime`

静态文本格式化门面，底层委托可替换的 `ITxtHelper`（默认封装 `string.Format`）。编辑器模式下由 `EditorUtil.Initializer` 注入 `TxtHelper`，运行时由 `Nova.Awake` 注入。

---

## 文件

```csharp
string result = Txt.Format("Hello {0}, score: {1}", name, score);
```

底层委托给可替换的 `ITxtHelper`，默认实现 `TxtHelper` 使用 `string.Format`。当 `s_TxtHelper` 尚未初始化时（框架启动最早期），所有 `Format` 重载自动回退到 `string.Format`，避免 NullReferenceException。`GetCachedFullString` 和 `FillGap` 同样支持 null 回退。

## 文件列表

| 文件 | 说明 |
|------|------|
| `Txt.cs` | 静态门面 |
| `Interfaces/ITxtHelper.cs` | 文本助手接口 |
| `Implements/TxtHelper.cs` | 默认实现（`string.Format` 封装） |
