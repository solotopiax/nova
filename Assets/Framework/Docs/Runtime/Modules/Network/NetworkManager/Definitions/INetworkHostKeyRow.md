# INetworkHostKeyRow

**类签名**：`public interface INetworkHostKeyRow`
**命名空间**：`NovaFramework.Runtime`

域名数据行接口，Luban 生成的 HostKey bean 类须实现此接口，框架侧通过接口直接访问字段。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Definitions/INetworkHostKeyRow.cs` | `INetworkHostKeyRow` | 域名数据行接口定义 |

---

## §5 完整公开 API

```csharp
public interface INetworkHostKeyRow
{
    string Name          { get; }   // 域名标识名称（主键）
    string Value         { get; }   // 主域名
    string FallbackValue { get; }   // 备用域名，可为空
}
```

---

## §11 使用示例

```csharp
// Luban 生成的 bean 类实现接口
public partial class TbHostKey : INetworkHostKeyRow
{
    public string Name  { get; set; }
    public string Value { get; set; }
    public string FallbackValue { get; set; }
}

// NetworkManager 通过 ITable<INetworkHostKeyRow> 协变访问
private void BuildHostKeyCacheFromTable(ITable table)
{
    if (table is ITable<INetworkHostKeyRow> typedTable)
    {
        foreach (INetworkHostKeyRow row in typedTable.DataList)
            // NetworkManager 会校验并缓存主、备域名。
    }
}
```

---

## §13 关联文档

- [INetworkCmdRow.md](INetworkCmdRow.md) — 配套指令行接口
- [NetworkManager.md](../NetworkManager.md) — 消费者
- [../../../../Core/Table/ITable.md](../../../../Core/Table/ITable.md) — 表容器接口
