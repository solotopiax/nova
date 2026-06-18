# IBannerControl

**类签名**：`public interface IBannerControl`  
**命名空间**：`NovaFramework.Runtime`

Banner 广告专属控制接口，供 `IAdPlugin` 聚合使用。

## 当前公开 API

```csharp
public interface IBannerControl
{
    void ShowBanner();
    void HideBanner();
    void DestroyBanner();
    void UpdateBannerPosition(BannerPosition position);
    void UpdateBannerPosition(Vector2 position);
    void StartBannerAutoRefresh();
    void StopBannerAutoRefresh();
    void SetBannerWidth(float width);
    float GetAdaptiveBannerHeight(float width = -1f);
    void SetBannerBackgroundColor(Color color);
}
```

## 关键语义

- `ShowBanner()` / `HideBanner()` 只控制已加载 Banner 的显隐。
- `DestroyBanner()` 会释放 Banner 实例。
- 枚举位置与像素位置两套定位 API 并存。
- 自动刷新、宽度、自适应高度、背景色只对支持这些能力的渠道实现生效。

## 关联文档

- [IAdPlugin.md](IAdPlugin.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
