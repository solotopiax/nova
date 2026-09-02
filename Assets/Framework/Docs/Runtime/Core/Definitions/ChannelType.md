# ChannelType

**类签名**：`[Serializable] public enum ChannelType : byte`
**命名空间**：`NovaFramework.Runtime`

游戏运营渠道类型枚举，定义应用分发与运营来源所使用的渠道标识；它与 Facebook、Google、Apple、Wechat 等第三方登录提供方无关。

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
    None     = 0,   // 无特定运营渠道，是合法的跨平台配置坐标
    Official = 1,   // 官网包渠道
    Google   = 2,   // 谷歌商店渠道
    Apple    = 3,   // 苹果商店渠道
    WeChat   = 4,   // 微信渠道
    TikTok   = 5,   // 抖音渠道
    Alipay   = 6,   // 支付宝渠道
}
```

`ChannelType.None` 适用于不区分包体分发渠道的项目。它可在 Android、iOS 和 WebGL 等所有有效 `PlatformType` 下用于 Config 编辑、导出、Agent Action 与构建预检；`PlatformType.None` 仍不是可执行坐标。

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
