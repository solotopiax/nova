# IBannerControl

**类签名**：`public interface IBannerControl`
**命名空间**：`NovaFramework.Runtime`

Banner 广告专属控制接口，聚合 Banner 超出三段式之外的额外控制方法；`IAdPlugin` 继承此接口。

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `Assets/Framework/Scripts/Runtime/Modules/SDK/Plugins/Ad/IBannerControl.cs` | `IBannerControl` | 接口全部定义 |

---

## §5 完整公开 API

```csharp
public interface IBannerControl
{
    /// 显示已加载的 Banner 广告。
    void ShowBanner();

    /// 隐藏 Banner 广告（不销毁，可再次 ShowBanner 恢复）。
    void HideBanner();

    /// 销毁 Banner 广告实例并释放资源。
    void DestroyBanner();

    /// 通过枚举更新 Banner 停靠位置。
    void UpdateBannerPosition(BannerPosition position);

    /// 通过像素坐标更新 Banner 位置（逻辑像素）。
    void UpdateBannerPosition(Vector2 position);

    /// 开启 Banner 自动刷新（仅 MAX 有效）。
    void StartBannerAutoRefresh();

    /// 停止 Banner 自动刷新（仅 MAX 有效）。
    void StopBannerAutoRefresh();

    /// 设置 Banner 宽度（逻辑像素，仅 MAX 有效）。
    void SetBannerWidth(float width);

    /// 获取 Banner 自适应高度（逻辑像素，仅 MAX 有效）；不支持时返回 -1。
    float GetAdaptiveBannerHeight(float width = -1f);

    /// 设置 Banner 背景色（仅 MAX 有效）。
    void SetBannerBackgroundColor(Color color);
}
```

> 调用前须先 `IAdPlugin.RequestAsync(AdFormat.Banner)` 完成预加载，否则各方法行为由渠道 SDK 定义（通常为无操作或 Log.Warning）。

---

## §11 使用示例

```csharp
// 通过 IAdPlugin 直接调用（IAdPlugin 继承 IBannerControl，无需额外转型）
var ad = Nova.SDK.Get<IAdPlugin>();

async UniTask ShowBanner(CancellationToken ct)
{
    // 无需 Supports 检查；业务层直接预加载，未注册 Banner 的渠道 fail-soft 返回 default
    await ad.RequestAsync(AdFormat.Banner, ct: ct);

    // 设置位置后展示
    ad.UpdateBannerPosition(BannerPosition.Bottom);
    ad.ShowBanner();
}

// 隐藏与销毁
void HideBanner() => ad.HideBanner();
void DestroyBanner() => ad.DestroyBanner();

// 自适应高度（用于布局计算）
float height = ad.GetAdaptiveBannerHeight(Screen.width);
```

---

## §13 关联文档

- [IAdPlugin.md](./IAdPlugin.md) — 继承 IBannerControl 的聚合调度接口
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — Banner 方法 virtual 空实现，Banner 渠道按需重写
- [../../Definitions/Data.md](../../Definitions/Data.md) — BannerPosition 枚举
