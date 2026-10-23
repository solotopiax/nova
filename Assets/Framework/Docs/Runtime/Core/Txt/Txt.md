# Txt

**类签名**：`public static class Txt`
**命名空间**：`NovaFramework.Runtime`

静态文本格式化门面，底层委托可替换的 `ITxtHelper`。默认 TxtHelper 复用每线程 StringBuilder；无 Helper 时回退 string.Format。编辑器模式下由 `EditorUtil.Initializer` 注入 `TxtHelper`，运行时由 `Nova.Awake` 注入。

---

## 文件

```csharp
string result = Txt.Format("Hello {0}, score: {1}", name, score);
```

底层委托给可替换的 `ITxtHelper`，默认实现 `TxtHelper` 使用 StringBuilder.AppendFormat。当 `s_TxtHelper` 尚未初始化时（框架启动最早期），所有 `Format` 重载自动回退到 `string.Format`。`GetCachedFullString` 和 `FillGap` 同样有无 Helper 的回退路径。

## 重载、缓存与失败边界

Txt 只公开 1～16 个泛型格式值重载，ITxtHelper 与默认实现还提供 params object[] 重载；不要把 object[] 作为单个泛型值时误认为数组会自动展开。所有参数按声明顺序对应从零开始的模板位置，参数无可省略默认值；模板拒绝 null、允许空串，非法格式、越界占位符和参数格式化异常向外传播。默认单参泛型先执行 `arg?.ToString() ?? string.Empty` 再 AppendFormat，所以数字/日期的 `{0:N2}`、`{0:yyyy}` 等接收到字符串，可能与无 Helper 时保留原对象的 string.Format 不同；即便模板不引用该参数，也已执行 ToString。多参默认路径保留原参数，4 参及以上可能使用 params 数组，值类型可能装箱。没有显式 IFormatProvider，格式文化取决于执行线程。

默认 Builder 每线程首次格式化时以容量 1024 懒建，每次清 Length 再 AppendFormat/ToString，返回新字符串，容量不自动收缩。ThreadStatic 只隔离不同线程，同线程 ToString/IFormattable 重入可能清掉同一个 Builder；异常没有 finally 恢复。Initialize 为空，ClearCache 与 SubsystemRegistration 只清前缀字典，不清 Builder，也不重置 Txt 的 Helper。SetHelper 允许 null，只绑定实现，不初始化、清缓存或释放旧实现；接口声明本身不保证这些内部策略。

GetCachedFullString 按 `string.Concat(prefixes)` 的结果、再按 keyWords 两级缓存，前缀不加分隔符、不 trim 或正规化，`["ab","c"]` 与 `["a","bc"]` 共用前缀键。省略 params 得空数组，null 元素拼为空文本，显式 null 数组会失败。Txt 门面拒绝 null/空关键字；直接默认 Helper 允许空关键字而 null 由字典拒绝。缓存无容量或期限淘汰；ConcurrentDictionary 工厂可能多次执行，ClearCache 不与在途调用形成全局原子清空，无 Helper 时仅直接拼接。

FillGap 默认长度为 15，按 UTF-16 长度截取前 length 个单元或右补空格，不按字素或显示宽度，可能截断代理对。负长度会抛异常；Txt 门面 null word 抛 ArgumentNullException，直接默认 Helper 则在读取 Length 时抛空引用异常。

## 文件列表

| 文件 | 说明 |
|------|------|
| `Txt.cs` | 静态门面 |
| `Interfaces/ITxtHelper.cs` | 文本助手接口 |
| `Implements/TxtHelper.cs` | 默认实现（`string.Format` 封装） |
