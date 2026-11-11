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

    /// 更新 Banner 原生视图坐标；单位由渠道实现约定。
    void UpdateBannerPosition(Vector2 position);

    /// 开启 Banner 自动刷新（仅 MAX 有效）。
    void StartBannerAutoRefresh();

    /// 停止 Banner 自动刷新（仅 MAX 有效）。
    void StopBannerAutoRefresh();

    /// 设置 Banner 原生宽度（MAX：Android dp / iOS points）。
    void SetBannerWidth(float width);

    /// 获取原生自适应高度（MAX：Android dp / iOS points）；不支持时返回 -1。
    float GetAdaptiveBannerHeight(float width = -1f);

    /// 设置 Banner 背景色（仅 MAX 有效）。
    void SetBannerBackgroundColor(Color color);
}
```

> 先 `IAdPlugin.RequestAsync(AdFormat.Banner)` 创建并加载广告。首次显示重新选择当前就绪渠道，能发现后台重试或取消等待后成功加载的结果；无就绪渠道时记录 Warning。`AdPlugin.TryShowBanner()` 提供 bool 反馈，接口签名保持不变，true 不代表曝光。显示后控制固定作用于该实例，Hide 保留归属，Destroy 清除归属和预加载候选；首次显示前控制使用请求成功时的候选。没有目标时控制为空操作，自适应高度为 -1。所有控制方法由 Unity 主线程调用，详见 [Banner 归属](./AdPlugin.md#7-请求展示和渠道所有权)。

当前 MAX 的坐标与宽度直接转成整数传给原厂，Android 使用 dp，iOS 使用 points；高度 helper 也没有密度换算。`GetAdaptiveBannerHeight()` 的默认宽度直接取 Unity `Screen.width`，不能据此认为返回值是 Canvas 像素。需要指定宽度时传入原生单位，并在布局端核对转换。

---

## §11 使用示例

```csharp
// 通过 IAdPlugin 直接调用（IAdPlugin 继承 IBannerControl，无需额外转型）
var ad = Nova.SDK.Get<IAdPlugin>();

async UniTask ShowBanner(CancellationToken ct)
{
    // 未注册或加载失败返回 Success=false；仅成功后操作 Banner。
    var loaded = await ad.RequestAsync(AdFormat.Banner, ct: ct);
    if (!loaded.Success) return;

    // 设置位置后展示
    ad.UpdateBannerPosition(BannerPosition.Bottom);
    ad.ShowBanner();
}

// 隐藏与销毁
void HideBanner() => ad.HideBanner();
void DestroyBanner() => ad.DestroyBanner();

// 查询原生自适应高度；布局使用时按目标平台与 Canvas 进行单位换算。
float nativeHeight = ad.GetAdaptiveBannerHeight();
```

---

## §13 关联文档

- [IAdPlugin.md](./IAdPlugin.md) — 继承 IBannerControl 的聚合调度接口
- [AdChannelPluginBase.md](./AdChannelPluginBase.md) — Banner 方法 virtual 空实现，Banner 渠道按需重写
- [Data.md](../../../../Assets/Framework/Docs/Runtime/Modules/SDK/Definitions/Data.md) — BannerPosition 枚举
