# IAdPlugin

**类签名**：`public interface IAdPlugin : ISDKPlugin, IBannerControl`  
**命名空间**：`NovaFramework.Runtime`

IAA 广告主接口。当前框架不再使用旧的 `Supports / LoadAsync / Hide` 契约，而是采用 `RequestAsync + IsReady + ShowAsync` 三段式，并把 Banner 控制收口到 `IBannerControl`。

## 当前公开 API

```csharp
public interface IAdPlugin : ISDKPlugin, IBannerControl
{
    bool IsAdPlaying(AdFormat format);
    bool IsUserConsentSet();
    bool HasUserConsent();
    UniTask WaitForPrivacyFlowAsync(CancellationToken ct = default);
    UniTask<string> GetCountryCodeAsync(CancellationToken ct = default);
    UniTask<AdLoadResult> RequestAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
    bool IsReady(AdFormat format);
    UniTask ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
}
```

## 关键语义

- `RequestAsync()` 的具体调度由实现负责；当前广告聚合实现并行请求已注册渠道，不支持该格式的渠道返回失败。渠道内广告组、重试和缓存策略由广告包文档维护。
- 返回值是统一的 `AdLoadResult`；当前聚合在首个渠道成功或全部渠道失败时完成。成功可能来自已有缓存，失败不表示已启动的后台重试全部停止；调用方取消按实现传播取消异常。
- `ShowAsync()` 只负责展示，展示前应先用 `IsReady()` 判断。
- Ready 是当前缓存状态快照，不保证稍后展示成功；当前聚合的 `IsAdPlaying(format)` 查询任意渠道是否占用，业务取消等待不会自行清除原生播放状态。
- Banner 的显示、隐藏、销毁与位置更新不走 `ShowAsync()`，而是走 `IBannerControl`。
- `IsUserConsentSet()` 表示用户是否已经作出广告隐私授权决定；只有它为 `true` 时，`HasUserConsent()` 的结果才表示明确同意或拒绝。
- 尚未设置、没有支持查询的渠道以及明确拒绝时，`HasUserConsent()` 都返回 `false`。
- `WaitForPrivacyFlowAsync()` 等待渠道初始化期间触发的隐私流程结束，不保证一定获得个性化同意，也不表示广告已就绪。调用前应先等待 `Nova.SDK.InitializeTask` 或确认广告插件已创建；等待与取消能力须按实际渠道实现核对。

`ShowAsync()` 返回 `UniTask`，不返回广告结果；展示、关闭和奖励通知由具体广告实现的事件 API 提供。业务取消只结束实现支持的本地等待，不能据此推断原生广告已关闭。

本接口继承的初始化与释放契约不统一规定对象可再次初始化。每个实现的复用条件、事件清理和取消边界由对应包声明；业务通过 SDKManager 获取和使用插件，整体释放交给其所有者。

`customProps` 默认 null，由广告实现传给渠道和统计入口，不表示所有厂商会接收同一类型全集。当前聚合国家码只发布非空、非 `IV` 的大写值；等待使用广告配置的超时，超时返回空，调用方 ct 取消继续传播。它不是网络地理定位或国家真实性验证。

## 关联文档

- [IBannerControl.md](IBannerControl.md)
- [AdLoadEvent.md](AdLoadEvent.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
