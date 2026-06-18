# IDoHManager

**类签名**：`public interface IDoHManager`
**命名空间**：`NovaFramework.Runtime`

DoH 管理器公开接口，定义 DNS-over-HTTPS 查询与 IP 地址收集的全部契约。外部通过此接口与 DoH 模块交互，避免直接依赖内部实现类。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `IDoHManager.cs` | DoH 管理器接口定义 |

## 公开 API

```csharp
// 初始化
void Initialize(DoHManagerConfig config)

// 遍历所有 URL，异步收集各域名的 IP 地址
UniTask CollectAllIPAddresses(IEnumerable<string> urls)

// 对指定 URL 进行 DoH DNS 查询，结果写入内部缓存
UniTask DNSQuery(string url)

// 从 URL 中提取主机名（域名部分）
string GetHostName(string url)

// 通过主机名获取已收集的 IP 地址数组，未收集时返回 null
IPAddress[] GetIPAddresses(string hostName)

// 清空所有已收集的 IP 地址与 DNS 缓存
void Clear()

// 所有已收集的 IP 地址，<原始 URL, 替换 IP 后的 URL 列表>
IReadOnlyDictionary<string, List<string>> AllCollectedIPAddresses { get; }

// 所有域名对应的 IP 地址，<主机名, IPAddress 列表>
IReadOnlyDictionary<string, List<IPAddress>> AllDomainIPAddresses { get; }

// 最近一次 DNSQuery 返回的 DNS 应答集合
DNSAnswer[] DNSAnswers { get; }
```

## 关联文档

- [DoHManager](DoHManager.md)
- [DoHManagerBase](DoHManagerBase.md)
- [DoHManagerConfig](Definitions/DoHManagerConfig.md)
