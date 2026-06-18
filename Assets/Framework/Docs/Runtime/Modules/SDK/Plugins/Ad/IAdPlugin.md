# IAdPlugin

**类签名**：`public interface IAdPlugin : ISDKPlugin, IBannerControl`  
**命名空间**：`NovaFramework.Runtime`

IAA 广告主接口。当前框架不再使用旧的 `Supports / LoadAsync / Hide` 契约，而是采用 `RequestAsync + IsReady + ShowAsync` 三段式，并把 Banner 控制收口到 `IBannerControl`。

## 当前公开 API

```csharp
public interface IAdPlugin : ISDKPlugin, IBannerControl
{
    bool IsAdPlaying(AdFormat format);
    UniTask<AdLoadResult> RequestAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
    bool IsReady(AdFormat format);
    UniTask ShowAsync(AdFormat format, Dictionary<string, object> customProps = null, CancellationToken ct = default);
}
```

## 关键语义

- `RequestAsync()` 向所有支持该格式的渠道并行发起预加载。
- 返回值是统一的 `AdLoadResult`；`Success=true` 表示加载成功，`Success=false` 时可直接读取 `ErrorCode` 和 `ErrorMessage`。
- `ShowAsync()` 只负责展示，展示前应先用 `IsReady()` 判断。
- Banner 的显示、隐藏、销毁与位置更新不走 `ShowAsync()`，而是走 `IBannerControl`。

## 关联文档

- [IBannerControl.md](IBannerControl.md)
- [AdLoadEvent.md](AdLoadEvent.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
