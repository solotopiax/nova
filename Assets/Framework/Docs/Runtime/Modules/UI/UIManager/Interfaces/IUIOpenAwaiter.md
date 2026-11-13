# IUIOpenAwaiter

等待页面真正准备完成后继续业务流程，同时得到明确的成功、失败、关闭或取消结果。普通业务使用 `Nova.UI` 的新门面；原有返回 `int serialID` 的全部打开入口和失败事件保持原用法，不要求改写旧项目调用。

## 可选入口

`IUIOpenAwaiter` 是独立可选能力，不向 [IUIManager](IUIManager.md) 添加强制成员。旧自定义 Manager 无需实现新接口；调用新门面而实现不支持时抛 `NotSupportedException`，不会改走轮询或按超时假定已打开。

```csharp
UniTask<UIViewOpenResult> OpenUIViewAwaitAsync<T>(
    object userData = null, CancellationToken cancellationToken = default) where T : UIView;
UniTask<UIViewOpenResult> OpenUIViewAwaitAsync(
    string uiViewName, object userData = null,
    CancellationToken cancellationToken = default);
UniTask<UIViewOpenResult> OpenUIViewByAssetAwaitAsync(
    string assetLocation, object userData = null,
    CancellationToken cancellationToken = default);
```

仍先加载 UI 注册表：泛型按类型短名、普通字符串按表内 `Name`、`ByAsset` 按唯一 `AssetLocation` 匹配。分组、暂停、池化仍来自注册行；不增加调用方覆盖配置的参数，不改变旧入口的默认流程。

## 结果含义

`UIViewOpenResult` 含 `Status`、`SerialID`、`View`、`ErrorMessage`、`Succeeded`。

| `Status` | 说明 |
| --- | --- |
| `Opened` | 本次初始化、`OnOpen` 与组刷新完成，`Succeeded=true`，`View` 为 `IUIView` |
| `Failed` | 注册、加载或打开链失败，读取 `ErrorMessage`；不能继续使用视图 |
| `Cancelled` | 调用方取消本请求，不影响其他页面/请求 |
| `Closed` | 本请求准备完成前被明确关闭 |
| `Shutdown` | UI 管理器关闭，等待结束 |

只有成功结果携带可使用的 `View`；以 `Succeeded` 判断成功。请求序号尚未分配时 `SerialID=-1`。结果是完成当时的快照，调用方后续仍须确认页面未关闭/复用；不能把池化 View 引用长期当身份凭据。不同请求各有自己的序号与结果；不得只按同类型名称把其他正在加载请求视作自己的成功。

内置实现复用原打开链，等待初始化、打开和组刷新完成后才结算一次。新 await 入口会识别 UIView 初始化钩子已记录的异常并返回失败；旧入口仍保留其原日志/异常与失败事件边界，新增结果不等于把旧调用改成抛错或强制等待。

本次打开尚未完成时，取消只结束这次请求并关闭其页面；底层加载即使晚到也走已有释放链。结果结算后会撤掉此取消注册，之后取消请求 token 不会关闭已经成功打开的页面；页面自身工作另用实际生命周期或 `OpenScope` 管理。新入口限 Unity 主线程调用，不承诺任意底层 I/O 已中止，也不取消独立业务服务。关闭、取消、失败和 Shutdown 是不同结果，业务按实际状态处理，不自动重试或继续使用失败结果。

`Cancelled`、`Closed`、`Shutdown` 可以先通知调用方，底层观察链仍保留原实例/Prefab 拥有者/实例池直到晚到收尾；结果不是“全部资源已经释放”的证明。尚在初始化、打开或刷新阶段终止时，新入口停止后续阶段并逐项收尾，未成功的候选不进入旧正常回收队列；已经成功打开的页面和旧序号入口继续原关闭后由 Update 回收的流程。终态通知后的实际清理异常单独记录，不自动重放结果未知的销毁。

## 业务示例与验收

```csharp
UIViewOpenResult result = await Nova.UI.OpenUIViewAwaitAsync(
    uiViewName, userData, cancellationToken);
if (!result.Succeeded)
{
    // 按 Status 区分失败、取消、关闭与 UI 关闭；失败时读取 ErrorMessage。
    return;
}
// 当前这一刻页面已经打开；后续异步操作仍绑定页面自己的生命周期。
```

名称、业务数据、取消拥有者从项目原入口读取。只在新业务明确需要等待时选择新 API，旧 serialID 编排可以保留。验收包含正常打开、同页并发请求、加载中关闭/取消、加载失败、`OnInit` / `OnOpen` / 刷新异常、Shutdown、池化重开与晚到释放；同时运行未修改的旧调用方。编译通过不能代替这些时序或真实旧项目验收。

来源：[可选接口](../../../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Interfaces/IUIOpenAwaiter.cs)、[结果与状态](../../../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Definitions/UIViewOpenResult.cs)、[门面](../../../../../../Scripts/Runtime/Modules/UI/UIComponent.Await.cs)、[完成实现](../../../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Implements/UIManager.Await.cs)。
