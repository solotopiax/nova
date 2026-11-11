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
| `m_ChannelConfigs` | `AdChannelConfigList` | `new AdChannelConfigList()` | Inspector 渠道配置列表，由 `AdChannelConfigListDrawer` 绘制；同时承载广告全局参数 |
| `m_CountryCodeWaitTimeoutSeconds` | `float` | `5f` | `IAdPlugin.GetCountryCodeAsync` 等待广告 SDK 返回国家码的超时时间（秒）；超时后返回空字符串 |
| `m_RevenueReportCmdName` | `string` | `string.Empty` | 广告收益上报使用的 NetworkCmd 名称；建议填写 `AdRevenueReport`，为空时兼容旧资产并跳过自动上报 |

面板显示顺序上，m_CountryCodeWaitTimeoutSeconds 与 m_RevenueReportCmdName 仍序列化在 AdPluginConfig，但通过 AdChannelConfigListDrawer 绘制在“广告组重试间隔（秒）”下方。绘制器按数组展开状态计算高度并显示 Tooltip，Banner ILRD 间隔用 0～10 的整数滑条；运行时并未将外部配置值强制限于该范围。

展开 MAX、AdMob 等渠道项时，通用列表绘制器按该项同层可见字段的最长显示名统一标签列，并为右侧输入框保留至少 80 像素；窄窗口下标签仍可能截断，可拉宽配置窗口查看。这只是编辑界面的排版调整，不改变序列化字段、广告位解析、默认值或正式导出内容；不要与广告的自适应 Banner 高度混淆。

`AdChannelConfigList` 内含字段；`m_Items` 使用 SerializeReference，其余使用 SerializeField：

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `m_Items` | `List<IAdChannelConfig>` | `[]` | 渠道配置实例，SerializeReference 多态存储 |
| `m_EnableBidding` | `bool` | `true` | 是否启用多渠道比价 |
| `m_BannerIlrdInterval` | `int` | `5` | 面板显示“Banner收益累计次数”；MAX 的 ad_ilrd 与后台收益协议共用一份渠道累计及存档。1 为逐次，0 或负数停止并清理对应批次；非 Banner 不受该阈值影响，ad_impression 仍逐次派发。MAX 后台请求随 ad_ilrd 派发；AdMob 尚无分析 ad_ilrd，显式复用渠道累计入口 |
| `m_MuteAd` | `bool` | `false` | 是否全局静音广告 |
| `m_GroupRetryIntervalsSeconds` | `float[]` | `[2, 4, 8, 16, 32]` | 所有渠道和广告组共用的重试间隔序列（秒）；仅整组失败推进，下标按组独立，末尾循环，成功归零；空或任意非法项整体回退默认序列 |

---

国家码等待配置小于等于 0、NaN 或无穷大时，运行时不等待并返回空字符串；有效超时发生时返回空字符串，调用方取消继续抛取消异常。

## §5 完整公开 API

```csharp
[Serializable]
public sealed class AdPluginConfig : ISDKPluginConfig
{
    /// 配置项在 ConfigWindow 左树中展示的中文名称。
    public string DisplayName => "广告聚合";

    /// 渠道配置列表包装；通过 .Items 遍历渠道实例，通过 .EnableBidding / .BannerIlrdInterval /
    /// .MuteAd / .GroupRetryIntervalsSeconds 读取全局开关。
    /// AdPlugin 调用 ApplyGlobalConfig(ChannelConfigs, RevenueReportCmdName) 将全局开关与 收益上报路由一次性写入各渠道实例。
    public AdChannelConfigList ChannelConfigs { get; }

    /// 等待广告 SDK 返回国家码的超时时间，单位秒。
    public float CountryCodeWaitTimeoutSeconds { get; }

    /// 广告收益上报使用的 NetworkCmd 名称；为空时不自动发送收益协议。
    public string RevenueReportCmdName { get; }

    /// 无参构造器；供 ConfigWindow SDKPluginScanner 通过 Activator 创建空实例使用。
    public AdPluginConfig();
}
```

---

## §11 使用示例

以下为 Inspector 配置内容示意，访问器为只读，不能用 C# 赋值：

~~~text
AdPluginConfig.ChannelConfigs.GroupRetryIntervalsSeconds: [2, 4, 8, 16, 32]
MAX.RVPlacementGroups: ["A1,A2,A3", "B1|B2"]
MAX.InterPlacementGroups: ["I1|I2", "J1"]
MAX.AppOpenPlacementGroups: ["O1,O2"]
~~~

广告收益上报建议将 `RevenueReportCmdName` 配置为 `AdRevenueReport`。渠道在自身收益派发入口调用 `StartRevenueReport`：MAX 放在 `TrackMaxAdIlrd` 已派发分支，Banner 直接传入同一批累计金额；AdMob 在 `Paid` 中显式接入。基类在 Unity 主线程交由 `AdRevenueReportNetService` 构造 `PbNetIAAReportReq`（`ad_channel` + `iaa`），并调用 `NetService.SendAsync`。`RaiseRevenue` / `RaiseRevenueImmediately` 只通知业务，不自动发送协议。旧资产未配置该字段时不上报，不会自动猜测 URL。

序列化字段改为 `m_RevenueReportCmdName`，使用 `FormerlySerializedAs("m_IaaReportCmdName")` 读取已有资产，Unity 保存后写入新名称。旧 public 属性 `IaaReportCmdName` 已移除，业务引用应改为 `RevenueReportCmdName`。初始化会记录实际生效的路由或未启用状态；修改配置后须正式导出 Runtime 并更新对应配置 Bundle，不能仅以本地资产有值判断 APK 已获得新配置。发送还依赖应用配置中的 `AppAesKey / AppAesIV`（UTF-8 各 16 字节）、有效 NetCmd / Host 和业务登录身份；不得拿隐私 AES 配置代替。协议与日志见[收益上报入口](./INDEX.md#广告收益上报-proto-协议层)。

List每项是一组，英文逗号和竖线是同义组内分隔符，可混用。每组从首ID开始，失败立即尝试下一项，成功停止本组并将间隔下标归零。仅末项失败时取本组当前间隔，推进一次下标并等待后回组首；默认连续整组失败等待2、4、8、16、32、2秒，持续循环。旧单ID列表自然为多个单ID组，首次并发保持。Banner 每项填写单个真实 ID，多项并行；主动加载失败使用组间隔，原厂自动刷新间隔独立。

广告组字段改名仍用 FormerlySerializedAs 保留 Unity 旧资产，MAX / AdMob 的旧 *PlacementIds 属性返回解析后的真实 ID 快照。MAX 初始化按表达式注册有序组；当前 AdMob 已实现官方 SDK 的四格式与 UMP，但初始化通过这些真实 ID 访问器展开后调用 RegisterAdUnits，因此每个 ID 是独立单 ID 组，不保留源表达式的组内串行拓扑。AdMob 还需官方依赖、平台 App ID 与 Android / iOS Player，详情见 [AdMobAdPlugin.md](../../../com.solotopia.nova.framework.sdk.admob/Nova/Doc/AdMobAdPlugin.md)。

旧逐 ID 重试次数、单值间隔、兼容访问器与槽位重试字段已删除，不将旧单值迁移为新序列；旧配置缺少新数组时使用默认序列。外部渠道若引用被删除成员，需要同步源码。配置编辑不缓存解析，MAX 初始化物化运行计划；ApplyGlobalConfig 复制有效间隔序列，后续配置数组修改不会改变渠道已注入值。

正式配置仍通过ConfigMaster.EnabledSDKs→导出ConfigRuntime.EnabledSDKConfigs→SDKManager注入。MAX 初始化检查组语法及 ID 唯一性；当前构建处理器未接入 `MaxAdGroupConfigValidator`，通用 Exporter 也没有广告专属校验 hook，不能将构建或导出成功当作广告组配置已校验。保存/导出后需在Editor重载检查组串与间隔数组，并清除旧序列化重试字段。


---

## §13 关联文档

- [IAdPlugin.md](./IAdPlugin.md) — 配置注入的目标插件
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — 渠道实例基类
- [ISDKPluginConfig.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Definitions/ISDKPluginConfig.md) — 配置 marker 接口
