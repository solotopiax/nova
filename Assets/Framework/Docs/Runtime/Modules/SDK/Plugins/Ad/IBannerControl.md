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

当前聚合把操作转交 active Banner 渠道；尚无成功请求或 active 渠道时，显隐/位置等调用可能无操作，不能据 void 返回证明原生 UI 已改变。宽度及 Vector2 位置使用逻辑像素，不能把 Inspector Range 当作所有实现的运行时校验。`GetAdaptiveBannerHeight` 的 width 默认 `-1f` 表示使用屏幕宽度，无 active/不支持时为 `-1f`。Hide 允许后续 Show，Destroy 才销毁并清 active 记录；刷新控制不等于销毁。

## 关联文档

- [IAdPlugin.md](IAdPlugin.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
