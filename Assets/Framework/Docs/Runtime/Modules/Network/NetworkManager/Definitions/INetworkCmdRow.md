# INetworkCmdRow

**类签名**：`public interface INetworkCmdRow`
**命名空间**：`NovaFramework.Runtime`

网络指令数据行接口，Luban 生成的 NetCmd bean 类须实现此接口，框架侧通过接口直接访问字段，彻底消除反射和 JArray 直接解析。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Managers/NetworkManager/Definitions/INetworkCmdRow.cs` | `interface INetworkCmdRow` | 网络指令数据行接口定义 |

---

## §5 完整公开 API

```csharp
public interface INetworkCmdRow
{
    string Name    { get; }  // 指令名称（主键）
    string Way     { get; }  // 网络方式（"HTTP_GET" / "HTTP_POST" / "HTTP_URL" / "WS"）
    string HostKey { get; }  // Host 唯一标识，对应域名表的 Key
    string Path    { get; }  // 接口路径，与 HostKey URL 拼接成完整 URL
}
```

---

## §11 使用示例

```csharp
// Luban 生成的 bean 类实现接口
public partial class TbNetCmd : INetworkCmdRow
{
    public string Name    { get; set; }
    public string Way     { get; set; }
    public string HostKey { get; set; }
    public string Path    { get; set; }
}
```

---

## §13 关联文档

- [INetworkHostKeyRow.md](INetworkHostKeyRow.md)
- [NetworkManager.md](../NetworkManager.md)
