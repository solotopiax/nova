# SpriteBinding

给 `Image` 或 `SpriteRenderer` 异步换图时，自动淘汰过期请求并持有当前图片的资源句柄。它是按需创建的便利对象，旧资源加载和现有赋图代码保持原用法；不创建第二套资源缓存。

没有全局布尔开关：**创建助手并通过它换图就是接入，不创建就不启用**。没有接入的旧代码无需增加开关、组件或清理逻辑。主动采用时，才局部替换目标原来的加载、赋图与该资源的释放代码。

## 入口与来源

类型为 `NovaFramework.Runtime.SpriteBinding : IDisposable`，实现见 [SpriteBinding.cs](../../../../Scripts/Runtime/Modules/Asset/Definitions/SpriteBinding.cs)。普通业务使用 `new SpriteBinding(image)` 或 `new SpriteBinding(spriteRenderer)`；带 `IAssetManager` 的构造仅供框架扩展和隔离测试，业务不为注入而取得 Manager。

| API | 作用 |
| --- | --- |
| `BindAsync(string location, CancellationToken ct = default)` | 加载默认包中的普通 Sprite，成功后替换 |
| `BindAtlasAsync(string atlasLocation, string spriteName, CancellationToken ct = default)` | 加载真正的 SpriteAtlas，再按精确名称取子图 |
| `Clear()` | 结束未完成请求，清图并释放当前资源；此对象仍可重用 |
| `Dispose()` | 清理并永久结束拥有权，重复调用幂等 |
| `IsDisposed` | 是否已经永久结束，结束后 Bind 抛 `ObjectDisposedException` |

创建和上述 API 限 Unity 主线程；调用方可以从其他线程取消 token。每个目标同时只允许一个 `SpriteBinding`，重复创建抛 `InvalidOperationException`。使用期间统一由它写入目标 `sprite`，其他代码不能再并行替换同一引用。目标原先的外部图片/Handle 不被接管，原拥有者须自行处理。

## 返回、替换与失败

`Bind*Async` 返回 `true` 表示这次请求仍是当前请求且已完成赋图；被新请求替代、取消、清空或结束时返回 `false`。返回 `false` 不等于资源错误，也不应自动重试旧请求。空地址/空图集名称属于参数错误；资源为空、句柄无效、图集缺子图等实际失败抛异常。

加载中保留旧图，新图可用且请求仍有效时才交接。加载或查图失败发生在交接前，旧图及其租约仍保留；业务应处理异常。赋图或旧租约释放失败也会向调用方报告，已交接的新图不保证回滚，不能把任何异常都解释成“画面没有改变”。

淘汰旧请求时，其取消回调抛错也会使新请求失败；新请求尚未调度加载时会撤掉自身登记。取消回调若重入产生更新请求，原请求不再覆盖它；已被替代请求的清理错误会被记录，不能据返回 `false` 推断底层所有清理均未报错。

每次请求独立取得 Handle，淘汰只取消自己的请求，不取消其他目标的租约。自定义资源实现即使忽略取消，晚到结果也会被观察并释放，不能回写新目标。底层可以共享 Provider/Bundle，但该助手没有增加跨目标缓存或共享图集管理器。

内置加载仍走 `Nova.Asset → AssetManager → YooAsset` 的原句柄链，底层引用计数由 YooAsset 管理。助手只释放自己取得的 `IAssetHandle`，不另建计数规则；业务不要再次释放它内部的 Handle。接入前已经由旧代码持有的 Handle 仍由旧拥有者处理，不能因换成助手就漏掉或重复释放。最后一份租约归还后，是否尝试卸载由底层实际实现决定。

## 普通图、图集与所有权

普通图直接由其 Handle 管理，助手不 `Destroy` 原始 Sprite。图集入口只接受真正的 `SpriteAtlas`，不把普通多切图纹理当图集；`SpriteAtlas.GetSprite` 返回的克隆归本助手拥有。交接后释放旧租约，清空时先断开目标引用，再销毁克隆并释放图集 Handle。Play 中 `Destroy` 按 Unity 的帧末时序生效，不能用当帧对象数断言已经销毁完成。

两个绑定入口都使用现有默认包加载路径，不提供包参数。需要普通切图子资源时仍沿 [ISubAssetsHandle](AssetManager/Interfaces/ISubAssetsHandle.md) 处理整批所有权；需要指定内容包时使用 [显式包门面](AssetManager/Interfaces/IAssetPackageLoader.md)，不会因为选包能力存在就自动改变图片绑定的寻址。

## 页面与池化接入

下面展示一个页面的接法：打开时创建一次，原有刷新入口调用 `SetIconAsync`，关闭时释放；重开再创建。`m_Icon` 仍用原有 Inspector 引用，不要求同时采用 `UIViewScope`。

```csharp
using Cysharp.Threading.Tasks;
using NovaFramework.Runtime;
using UnityEngine;
using UnityEngine.UI;

public sealed class IconView : UIView
{
    [SerializeField] private Image m_Icon;
    private SpriteBinding m_IconBinding;

    /// <summary>本次打开开始持有图片。</summary>
    public override void OnOpen(object userData)
    {
        base.OnOpen(userData);
        m_IconBinding = new SpriteBinding(m_Icon);
    }

    /// <summary>由原刷新入口等待；过期或已关闭时返回 false。</summary>
    public async UniTask<bool> SetIconAsync(string location)
    {
        var binding = m_IconBinding;
        if (binding == null) return false;
        return await binding.BindAsync(location);
    }

    /// <summary>关闭时结束请求并归还当前图片，保留基类收尾。</summary>
    public override void OnClose(bool isShutdown, object userData)
    {
        var binding = m_IconBinding;
        m_IconBinding = null;
        try { binding?.Dispose(); }
        finally { base.OnClose(isShutdown, userData); }
    }
}
```

刷新入口用 `await SetIconAsync(实际资源地址)`，按前述 `bool` 与异常处理业务结果。图集换图可在同一方法中改调 `binding.BindAtlasAsync(实际图集地址, 精确子图名)`；需要更短的任务生命周期时传入该任务的取消令牌。地址是默认资源包中的地址，不是 HTTP 图片 URL。

接入时，将这张图的加载、赋图和句柄释放交给助手，保留业务错误处理；其他业务异步仍需自己的过期判断和生命周期清理。不要再并行写同一 `sprite`、重复释放助手内部句柄，也不能每次刷新重新创建助手。`Dispose` 放在真实关闭/回池边界，不能赋图后立即调用。也可以主动把助手登记给 [UIView 的本次打开作用域](../UI/Definitions/UIViewScope.md)，转交后不再手动重复清理同一助手。

禁用、隐藏或暂停不会释放绑定，页面关闭、临时控件回池必须显式 `Clear` / `Dispose` 或由已登记作用域执行。`Image` 以自身组件为销毁令牌宿主；`SpriteRenderer` 以自身 GameObject 上的 [生命周期辅助组件](../../../../Scripts/Runtime/Modules/Asset/Definitions/SpriteBindingLifetime.cs) 为宿主，沿 GameObject 销毁链结束。

仅在显式构造绑定、且真实宿主尚未 Awake 时，为**本绑定**添加短期 Update 观察，检查本目标/宿主销毁；Image 单独移除而 GameObject 仍在也属于该目标销毁。此分支在销毁后的下一次 Update 走原 `Dispose` 收尾，不能承诺 `Destroy` 调用内已释放。手动 Dispose 后停止观察；宿主 Awake 后停止观察并交回原生令牌时序。它不扫描其他对象，不使用 ExecuteAlways，未创建绑定的旧流程不启用。

Renderer 宿主已经 Awake 后，只移除 SpriteRenderer 而保留其 GameObject 不触发该宿主的原生销毁令牌，仍须由业务作用域显式 Dispose；不能把未 Awake 阶段的局部观察推广成所有阶段的组件删除保障。

## 验收路径

先编译，再用真实 Image/SpriteRenderer 与可控加载顺序覆盖 A/B 反序完成、普通图与图集互换、同图集两个目标单方取消、缺图保留旧图、清空、关闭/归池/重开和目标销毁。原生销毁按 [Unity 测试前置](../../../Onboarding/VALIDATION.md#unity-测试前置与证据) 在真实 Play 中验证，分别覆盖已 Awake、从未 Awake、Image 单独移除、觉醒交接和显式 Dispose 后观察停止；EditMode 不代替这些时序证据。核对每笔 Handle 只释放一次、图集克隆最终清零、旧请求不改新图。真实图集还需构建与消费子图验证；替身图集或 Editor 通过不等于 Bundle、Player 或设备已通过。
