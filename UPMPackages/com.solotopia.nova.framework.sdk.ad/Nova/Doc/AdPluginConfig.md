# AdPluginConfig

**类签名**：`[Serializable] public sealed class AdPluginConfig : ISDKPluginConfig`
**命名空间**：`NovaFramework.SDK.AdPlugin.Runtime`

AdPlugin 配置，由 SDKManager 注入 AdPlugin 初始化；持有所有渠道的配置实例列表。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Nova/Scripts/Runtime/AdPluginConfig.cs` | `AdPluginConfig` | 全部定义 |

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_ChannelConfigs` | `AdChannelConfigList` | `new AdChannelConfigList()` | Inspector 渠道配置列表，由 `AdChannelConfigListDrawer` 绘制；同时承载 5 个全局开关 |

`AdChannelConfigList` 内含字段（均有 SerializeField）：

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_Items` | `List<IAdChannelConfig>` | `[]` | 渠道配置实例，SerializeReference 多态存储 |
| `m_EnableBidding` | `bool` | `true` | 是否启用多渠道比价 |
| `m_BannerIlrdInterval` | `int` | `5` | Banner ILRD 上报间隔次数 |
| `m_MuteAd` | `bool` | `false` | 是否全局静音广告 |
| `m_RetryLoadAdMaxNum` | `int` | `3` | 广告加载最大重试次数 |
| `m_RetryLoadAdInterv` | `float` | `30f` | 重试达到上限后再次加载的间隔时间（秒） |

---

## §5 完整公开 API

```csharp
[Serializable]
public sealed class AdPluginConfig : ISDKPluginConfig
{
    /// 配置项在 ConfigWindow 左树中展示的中文名称。
    public string DisplayName => "广告聚合";

    /// 渠道配置列表包装；通过 .Items 遍历渠道实例，通过 .EnableBidding / .BannerIlrdInterval /
    /// .MuteAd / .RetryLoadAdMaxNum / .RetryLoadAdInterv 读取全局开关。
    /// AdPlugin 调用 ApplyGlobalConfig(ChannelConfigs) 将全局开关一次性写入各渠道实例。
    public AdChannelConfigList ChannelConfigs { get; }

    /// 无参构造器；供 ConfigWindow SDKPluginScanner 通过 Activator 创建空实例使用。
    public AdPluginConfig();
}
```

---

## §11 使用示例

```csharp
// SDKManager 注入配置时访问（框架内部，业务层无需直接操作）
var adConfig = sdkManagerConfig.PluginEntries
    .Select(e => e.Config)
    .OfType<AdPluginConfig>()
    .FirstOrDefault();

if (adConfig != null)
{
    foreach (var channelCfg in adConfig.ChannelConfigs)
    {
        // 按 PluginType 反射创建渠道实例
        var channel = (AdChannelPluginBase)Activator.CreateInstance(channelCfg.PluginType);
        await channel.InitializeAsync(channelCfg, ct);
    }
}
```

---

## §13 关联文档

- [IAdPlugin.md](./IAdPlugin.md) — 配置注入的目标插件
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 渠道实例基类
- [../../Definitions/ISDKPluginConfig.md](../../Definitions/ISDKPluginConfig.md) — 配置 marker 接口
