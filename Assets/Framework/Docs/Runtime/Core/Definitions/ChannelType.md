# ChannelType

**类签名**：`[Serializable] public enum ChannelType : byte`
**命名空间**：`NovaFramework.Runtime`

业务渠道类型枚举，定义应用分发所支持的各个渠道标识。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Runtime/Core/Definitions/ChannelType.cs` | `ChannelType` | 枚举定义 |

---

## §5 完整公开 API

```csharp
[Serializable]
public enum ChannelType : byte
{
    None     = 0,   // 无效渠道，兜底默认值
    Official = 1,   // 官网包渠道
    Google   = 2,   // 谷歌商店渠道
    Apple    = 3,   // 苹果商店渠道
    WeChat   = 4,   // 微信渠道
    TikTok   = 5,   // 抖音渠道
    Alipay   = 6,   // 支付宝渠道
}
```

---

## §11 使用示例

```csharp
// 运行时读取当前渠道
ChannelType runtimeChannel = Nova.Config.Channel;

// 按渠道与平台查找矩阵行
if (master.TryGetEntry(PlatformType.Android, ChannelType.Google, out var entry))
{
    List<ISDKPluginConfig> sdkConfigs = entry.GetSDKConfigs(DevelopMode.Debug);
}
```

---

## §13 关联文档

- [Definitions.md](Definitions.md) — 框架级枚举概览
- [PlatformType.md](PlatformType.md) — 平台类型枚举
- [DevelopMode.md](DevelopMode.md) — 开发模式枚举
- [../../Modules/Config/ConfigMasterSO.md](../../Modules/Config/ConfigMasterSO.md) — 使用渠道矩阵的主 SO
