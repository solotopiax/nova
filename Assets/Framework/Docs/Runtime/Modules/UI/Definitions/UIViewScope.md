# UIViewScope

把**本次打开**产生的订阅、取消令牌和可释放对象集中收尾。页面关闭、回收或销毁时结束，池化重开取得新作用域；暂停、隐藏、遮挡不结束。

此能力按需使用：`UIView.OpenScope` 惰性创建，只在本次打开期间可取。旧页面不访问该属性就不分配作用域；已有手工订阅、CTS、`OnClose` / `OnRecycle` 清理保持原归属，不被自动发现、迁移或接管。只对明确登记给作用域的内容执行收尾。

销毁收尾以原生销毁令牌为主。只有显式取得作用域、且该 UIView 尚未 `Awake` 时，额外登记**本作用域专属**的短期 Update 观察项，避免从未激活的对象销毁后遗漏清理；检测到销毁时结束原作用域。对象完成 `Awake`、作用域结束或重开身份变化后停止观察，不做全局页面扫描，也不给旧页面加组件。该兜底依赖后续 PlayerLoop 检查，不能解释为 `Destroy` 调用栈内同步完成；正常关闭仍由框架关闭链立即同步结束作用域。

## 公开能力

类型为 `NovaFramework.Runtime.UIViewScope : IDisposable`，由 [UIView](UIView.md) 提供当前 `OpenScope`。

| 成员 | 用法与责任 |
| --- | --- |
| `Token` | 本次打开的取消令牌，关闭即取消；不代表业务服务的独立任务应被放弃 |
| `IsDisposed` | 本次作用域是否已经结束，异步续体可以据此拒绝旧结果 |
| `Own(IDisposable)` | 登记明确转交的可释放对象，例如本次创建的 SpriteBinding |
| `AddCleanup(Action)` | 登记精确清理动作，例如只移除自己添加的按钮 listener |
| `Subscribe<TEvent>(EventHandler<EventData>)` | 为本作用域创建独立包装委托，沿原事件链调用业务 handler，结束只退订该包装 |
| `Dispose()` | 手动结束，仅处理自身登记工作，重复结束幂等 |

事件订阅内部捕获 `IEventManager` 接口，以便框架组件先销毁后仍能退订；这是框架内部接线，普通业务仍通过 `OpenScope.Subscribe` 或 `Nova.Event` 使用，不取得 Manager。本作用域用独立包装委托隔离所有权，旧代码即使用相同原 handler 订阅，作用域结束也不会注销它；同作用域重复登记相同事件与原 handler 会拒绝。外部不能用 `Unsubscribe(originalHandler)` 代替作用域收尾。事件仍按原分发和回池语义执行，作用域不允许跨 await 保留池化 `EventData`。

结束时先标记已结束并断开登记列表，然后取消令牌、逐项清理；一项失败不阻止其余项。手动 `Dispose()` 最后抛出聚合异常，框架生命周期收尾记录错误后继续。作用域只负责已登记对象，不保证对象内部清理一定成功。

创建、取用、登记与释放在 Unity 主线程执行。活动作用域重复 `Own` 同一对象只释放一次；结束后 `Own` / `AddCleanup` 立即清理新传入内容，不会重新打开作用域，`Subscribe` 则拒绝已结束作用域。`Token` 在结束后仍可读取取消状态。清理按登记的逆序执行。

## 选择接入，不迁移旧页

只在需要减少本次打开的重复清理时，在业务 `OnOpen` 内主动取得作用域：

```csharp
base.OnOpen(userData);
UIViewScope scope = OpenScope;
var binding = new SpriteBinding(image);
scope.Own(binding);
await binding.BindAsync(iconLocation, scope.Token);
if (scope.IsDisposed) return;
// 后续只处理仍属于本次打开的业务结果。
```

`image` 和地址来自该页面已确认引用；异步代码放在页面自身的 UniTask 方法中，不把 `OnOpen` 改成 `async void`。`SpriteBinding` 的完整图片和图集所有权见 [图片绑定](../../Asset/SpriteBinding.md)。不要再在旧清理代码中释放已经转交给作用域的同一对象；未转交的原有工作继续按原方式清理。

`AddCleanup` 只登记本页新增的具体 listener/子部件，不使用 `RemoveAllListeners`，不代替资源系统的场景卸载或未知 GameObject 生命周期。异步续体捕获本次 `scope` / `Token`，不要在 await 后重新取 `OpenScope` 判断旧任务：此时页面可能已重开，返回的是新作用域。

框架受控的关闭、回收和销毁路径负责结束作用域，不以业务子类记得调用 `base.OnClose` 作为这项收尾的唯一保障。业务仍应遵守既有基类生命周期调用要求，作用域不会补做它未拥有的手工收尾。

## 验收路径

分别验证未接入的旧页面与主动接入页面。旧页代码、序列化配置、事件/取消/释放时机及调用次数保持原行为；新页覆盖打开→关闭→归池→重开、旧响应晚到、重复结束、某项清理抛错、隐藏/暂停及 Unity Stop。另按 [Unity 测试前置](../../../../Onboarding/VALIDATION.md#unity-测试前置与证据) 取得真实 Play 的从未 Awake 非激活 UIView/父对象销毁、觉醒后交回原生令牌、关闭同步清理证据，并确认旧观察项不能结束重开后的新作用域。源码接线只能说明兼容设计，实际旧项目无需修改仍须目标项目运行验证。

来源：[UIViewScope.cs](../../../../../Scripts/Runtime/Modules/UI/Definitions/UIViewScope.cs)、[UIView.Scope.cs](../../../../../Scripts/Runtime/Modules/UI/Definitions/UIView.Scope.cs)。
