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

当前聚合首次 `ShowBanner()` 会重新选择当前就绪渠道，支持先前请求失败后后台加载成功、或业务取消等待后才完成的加载；已有显示实例则继续使用原渠道，不因新的预加载结果或刷新期间 Ready 暂时变化而切换归属。预加载成功只建立候选，不表示已经显示。

隐藏、销毁、位置、刷新、尺寸和背景色控制优先投递给已显示实例所属渠道，首次显示前才回退到有效的预加载候选；两者都没有时可能无操作。`HideBanner()` 保留归属，`DestroyBanner()` 清除归属和候选，旧请求返回也不会恢复本轮候选。不能据 void 返回证明原生 UI 已改变；具体 `AdPlugin` 另有 `TryShowBanner()`，其 true 仅代表显示调用正常发出且归属仍有效，不能当作曝光回调，它不是 `IBannerControl` 的新增成员。

宽度及 Vector2 位置使用逻辑像素，不能把 Inspector Range 当作所有实现的运行时校验。`GetAdaptiveBannerHeight` 的 width 默认 `-1f` 表示使用屏幕宽度，无有效控制目标或渠道不支持时为 `-1f`。刷新、宽度、自适应高度和背景色扩展当前由 MAX 实现；独立 AdMob 沿用这些扩展的空实现或 `-1f` 返回，显示、隐藏、销毁及位置更新另行支持。

## 关联文档

- [IAdPlugin.md](IAdPlugin.md)
- [../../Definitions/Data.md](../../Definitions/Data.md)
