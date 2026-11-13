# Asset、ObjectPool、ReferencePool 与本地缓存

用户要接入资源持有/释放、可复用实例或纯数据池，或清理本地热更缓存时读取当前分支。本页复用业务工作区编辑与现有 API/菜单，不增加 Project Action；API 存在不能代替实际可调用的 Unity/MCP 入口。

## 从目标选入口

| 业务目标 | 当前可复用入口 | 权威产物与验收 |
|---|---|---|
| 加载并持有资源 | 工作区编辑明确消费方，使用 `Nova.Asset.LoadAsync<T>` / `LoadSync<T>` 与 `IAssetHandle<T>` | 消费源码、真实资源地址/Package，成功加载与生命周期末 Release；WebGL 不使用同步加载 |
| 复用业务 GameObject/资源对象 | 工作区编辑 `ObjectBase` 子类及调用方，使用 `Nova.ObjectPool.CreateSingleGettingObjectPool<T>` / `CreateMultiGettingObjectPool<T>` 与 `IObjectPool<T>` | 固定 `Type + poolName`、非空唯一 Target、注册/Get/Put/Release 闭包；实际运行统计与清理 |
| 高频纯数据 | 工作区编辑 `IReference` 实现，`ReferencePool.Get<T>` / `Put` | Clear 重置全部状态，成功/取消/异常归还且不再访问；实际池 UsingReferenceCount 回到基线 |
| 编辑器本地热更缓存 | 当前 menu-items 可见的 `Nova/Clean Hotfix Caches` | 清理前后真实沙盒与第一层 `.version` 状态、DeviceID 保留；不是运行时/浏览器/CDN purge |
| 运行时冗余磁盘 Bundle | 授权项目业务入口编译消费 `Nova.Asset.ClearUnusedCacheAsync(package, ct)` | 目标 Package 已激活清单，真实未引用缓存前后结果；手机目标须设备证据，不清用户存档或清单仍引用的文件 |

普通业务源码可在授权范围编辑；Unity 资产、Prefab、Scene 与序列化引用仍通过实际 Editor/MCP 保存。Collector/配置/生成物不能因“接入池”顺手重建；池参数、类型、容量、地址、持有期和销毁责任从项目读取，不照搬 Demo 数字。`ObjectPool` 管可复用资源对象，`ReferencePool` 管纯数据载体；不能把 MonoBehaviour 当 `new T()` 的纯引用，不能用 `IReference.Clear()` 释放未知 Unity 生命周期。

## Asset 句柄和异步归属

短期持有用 `try/finally` 释放；长期将完整 Handle 保存在明确持有方，卸载/关闭前先清消费引用再 `Release()`，一次释放后清字段。`ISceneHandle` 只能 `await UnloadAsync()`；SubAssets/AllAssets 为整批同生共死，释放整批后不能继续持有其中一个元素。RawFile 用 `GetBytes()` 读取原始内容副本；需要长期消费时持有已复制字节，不依赖 best-effort 的底层 `FilePath`。

异步资源任务使用自己持有的 CTS/代际或现有 owner 生命周期标记。旧任务在取消、替换、关闭/重开后仍可能完成；只允许当前 owner 接管 Handle，其余结果在任务本地归还。不要在 await 后读取已经属于新任务的共享 CTS，也不要让旧 finally 清空新任务字段。取消抛出时当前 AssetManager 已回收未交付底层句柄，调用方只释放自己实际拿到的 Handle，不能重复回收内部适配器。

## 显式选择内容包

只在业务确实需要选择内容包时使用 [IAssetPackageLoader 对应的 Component 门面](../../../../Docs/Runtime/Modules/Asset/AssetManager/Interfaces/IAssetPackageLoader.md)。从有效资源配置、Collector 和既有启动链查出精确包名、地址、加载类型及 Manifest 状态；`package` 不是 UPM 包名。保持现有 `BootstrapAsync` / `LoadManifestAsync(package, ct)` 的拥有者，不为一次加载重复准备整个系统。

业务源码通过 `Nova.Asset.LoadFromPackageAsync<T>(package, location, ct)` 或同族 Subs/All/Raw/Scene 门面调用；场景把 `mode` 放在 `ct` 前并按内容拓扑冻结。旧 `Load*` 仍读取默认包，不改全局默认值，不向普通业务传递 `IAssetPackageLoader` 或 Manager。旧自定义 Manager 无需实现新接口；不支持时保留明确的 `NotSupportedException`，不能捕获后静默回退默认包。

编译后验证两包同地址读到各自内容、旧入口仍读原默认包，以及空白/未注册包、清单未就绪、预取消、进行中取消和最终 Release/Unload。主/子/全资源与场景沿用原完成判定，不能把 `IsDone` 当加载成功；Raw 检查沿原通道。只释放实际收到的 Handle，Scene 仅 `UnloadAsync()`。UI、Prefab、图片绑定未提供选包参数时明确说明，不能把底层能力扩大为所有上层入口已支持。

## 图片绑定

用户要“列表复用时不被旧图覆盖”“页面关掉后释放图片”或“按图集子图名换图”时，先读 [SpriteBinding](../../../../Docs/Runtime/Modules/Asset/SpriteBinding.md)，选择普通 Sprite 或真正 SpriteAtlas 分支。冻结实际 Image/SpriteRenderer、默认内容包/地址、精确子图名、唯一绑定拥有者、打开/回池/销毁与取消责任；旧赋图逻辑满足目标时不自动迁移。

通过已授权的业务源码创建并保存一个 `SpriteBinding`，调用 `BindAsync` / `BindAtlasAsync`；需要改序列化目标引用时仍经 Unity Editor/MCP。助手内部走 `Nova.Asset`，普通业务不注入 Manager。页面可登记到 `UIView.OpenScope.Own(binding)`；旧页也可显式在关闭/回池清理。隐藏/暂停或仅取消已完成请求的 token 不会清掉当前图；需要清图用 `Clear`，结束拥有权用 `Dispose`，重开重新创建。

显式绑定的真实宿主尚未 Awake 时才有本绑定局部 Update 兜底，销毁后在下一次 Update 收尾；觉醒后交回原生令牌，不能承诺 Destroy 栈内立即释放。Image 以自身组件为宿主，Renderer 以同 GameObject 的辅助组件为宿主；已 Awake 后仅移除 Renderer 仍由业务显式 Dispose。原生时序按 [Unity 测试前置](../../../../Docs/Onboarding/VALIDATION.md#unity-测试前置与证据) 取得真实 Play 证据，不用普通 MonoBehaviour 的 EditMode 结果替代，也不向旧流程启用观察。

返回 `false` 是被替代/取消/清空/结束，实际加载或查图失败抛异常且交接前保旧图；清理异常可能发生在新租约已接管之后。不要重试旧请求、手动释放助手内部 Handle 或销毁其普通 Sprite/图集克隆。编译及当前项目 Play 验证 A/B反序、跨入口替换、缺图、单方取消、关闭/归池/重开和销毁；图集构建/Player/设备只按实际证据报告。助手没有包参数，不把选包加载分支套到此入口。

## 对象池和引用池

先核对具体重载：无参 `IObjectPool<T>.CanGet()` / `Get()` 只查询空名称；注册命名对象须 `CanGet(name)` / `Get(name)`。`Register(obj, got)` 的 `got=true` 表示已取用并需相应 Put；`SingleGetting` 同一对象只允许一个在用引用，`MultiGetting` 每次取用都增加引用计数，必须逐次归还。`ObjectBase.Target` 是池反查归还的身份，要求非空唯一，不用同一 Target 注册两个不同 wrapper；纯 C# 占位可用自身身份，真实 GameObject 用真实实例。

池拥有注册对象。`Put` 仅变为空闲并执行 `OnPut`，不立刻销毁 Target；`ReleaseObject` / `ReleaseAllUnused` / 自动释放和池销毁按各自保护规则执行 `ObjectBase.Release(bool)`，内部再归还 wrapper。业务不要在 Put 后再 `ReferencePool.Put(ObjectBase)`。持有单个对象的字段不能被下一次 Get 覆盖；先 Put、明确拒绝重复取用，或按业务需要维护完整持有集合。池销毁后旧字段失效，不能再 Put。

`ReferencePool.Get<T>` 只用于纯 C# `IReference` + 可用构造；`Clear()` 清全部业务字段/集合，不保留上次请求状态。跨 await 的池化结果用 finally；`HttpResponse` 由业务消费完后 Put，但 EventData、下载进度回调与 `PlaySoundParams` 有框架接管时点，必须查其专用文档，不套“任何输入/结果都自行 Put”。归还后复制值可保留，池化对象引用不能继续读。

验收对应本次对象与业务路径：注册/Get/Put、重复请求、取消/关闭和释放/销毁；确认 Count/CanReleaseCount 或 UsingReferenceCount 与基线符合预期。只编译不证明 Unity 资源、声音或池实际清理。复用当前 [Docs/Runtime/Modules/ObjectPool/IObjectPool.md](../../../../Docs/Runtime/Modules/ObjectPool/IObjectPool.md)、[ObjectBase.md](../../../../Docs/Runtime/Modules/ObjectPool/ObjectBase.md)、[Docs/Runtime/Core/Reference/ReferencePool.md](../../../../Docs/Runtime/Core/Reference/ReferencePool.md) 和对应 Handle 文档，不要求改 Runtime 管理器。

## 运行时冗余 Bundle 缓存

先读当前 [Docs/Runtime/Modules/Asset/AssetComponent.md](../../../../Docs/Runtime/Modules/Asset/AssetComponent.md) 与 [AssetManager/Implements/AssetManager.md](../../../../Docs/Runtime/Modules/Asset/AssetManager/Implements/AssetManager.md)，从实际项目冻结 Package、运行模式、当前激活清单与目标版本、业务入口/Owner、现有加载与下载任务、句柄归属和目标 Player/设备。`package=null` 会选当前默认包，先查明其身份再绑定计划；已有的 `AutoClearUnusedCacheOnHotfix` 或业务清理链满足目标时复用，不重复挂一条自动清理。运行时 API 是编译消费的公开门面，不能当作 MCP Action；在授权的项目业务源码内补必要调用入口，沿现有 Procedure/Service/UIView 生命周期接入，不使用临时任意代码、私有反射、直接 YooAsset 调用或第二套缓存管理器。

目标 Package 必须已完成既有 `BootstrapAsync(ct)` 和 `LoadManifestAsync(package, ct)`；不要因为清缓存重复初始化整个资源系统。若任务明确要求下载后切换新清单，复用原更新链的 `Nova.Asset.RefreshManifestAsync(package, ct)`，确认实际激活版本后再清理。刷新允许回退至可用旧清单，方法返回不证明已进入请求的新版本；未能确认版本时停止依赖新清单的清理。固定清理调用使用 `await Nova.Asset.ClearUnusedCacheAsync(package, ct)`，其范围仅为**未被当前清单引用的磁盘 Bundle**；不是清所有手机缓存，也不删除 [persistentDataPath/Asset（定位）](../../../../Docs/Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Cache.md) 的版本记录、DeviceID、首包或用户存档。WebGL 与不同运行模式的文件系统能力分别核实，不能据该 API 推断浏览器持久缓存支持相同删除。

`Nova.Asset.CleanupAsync(package, ct)` 回收引用为零的内存 Provider/Bundle，与磁盘清理分开。清缓存不转移已有 Handle 所有权；保持正在使用资源的有效引用，按本页既有关闭/卸载责任释放，场景仍 `await UnloadAsync()`。串行执行同 Package 的清单切换与清理，不与未确认的下载/加载任务竞争，不用已取消的 Owner token 跳过必要释放。内置实现先检查 `ct`：调用前已取消会抛 `OperationCanceledException`，不调度清理；开始后取消只停止等待，未中止底层 `ClearCacheOperation`。不能承诺磁盘清理回滚、原子恢复或立即停止，取消后也不能按“已停止删除”继续操作同一缓存；记录已知结果，不自动重放或扩大删除范围。

当前内置 `AssetManager` 在解析包名、取得 `ResourcePackage` 后检查实际 `PackageValid`；清单无效时抛 `InvalidOperationException`，不调度清理，也不自动加载或刷新清单。等待 `ClearCacheOperation.IsDone` 后仅接受 `EOperationStatus.Succeeded`，其他终态抛含解析包名、`Status` 与底层 `Error` 的 `InvalidOperationException`。调用方应处理这些失败，不能沿用旧实现“底层失败也正常返回”的判断；自定义 Manager 的同签名不自动保证相同异常语义，按其实际实现核验。

正常返回只证明底层操作报告成功。当前 YooAsset 沙盒删除个别文件失败可能只记录 Error，未进入操作失败状态；仍须核对既有可读取日志、允许读取的缓存文件/字节前后结果，确认当前 Manifest 引用的 Bundle 保留、目标资源仍可加载。没有可确认的冗余文件时如实报告无可观察删除，不能虚构释放空间。接入分支先编译并从真实入口完成本次 Play 验证；用户要手机更新/清理时，还须绑定设备与安装产物身份、时间窗、实际清单版本和设备缓存结果，再验证目标资源加载。EditorSimulate、Editor 菜单、CDN 上传或 purge 不能代替真机证据；缺设备读回仅保留对应 `partial`，不清应用数据、卸载、`adb rm` 或强制删除仍被当前清单引用的 Bundle 来补齐。

## 本地热更缓存菜单

先读取当前 Framework 的 [Docs/Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Cache.md](../../../../Docs/Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Cache.md)，发现并展示本次实际 `GetEditorSandboxRootPath()` 路径和 [Application.persistentDataPath/Asset（定位）](../../../../Docs/Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Cache.md) 第一层 `.version` 清单；不可读的真实目标需说明，不能猜 [Library/Yoo（定位）](../../../../Docs/Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Cache.md) 或开发机器目录。冻结当前项目、已退出 Play、精确删除范围和 DeviceID 状态，用户授权本次缓存清理后再通过已发现的 `Nova/Clean Hotfix Caches` 菜单执行。菜单点击立即删除，当前内部没有执行前确认；不能把该实现当作已获得用户授权。

菜单共用 `EditorUtil.Asset.Cache.ClearAllHotfixResources()`：清 YooAsset Editor 沙盒，删除框架 Asset 第一层 `.version`，保留 `asset-check-device-id.dat`、其它文件、StreamingAssets 和 Bundle 构建产物。它是破坏性本地操作，不清全部 persistentDataPath，不影响真机沙盒/浏览器缓存/Cloudflare。菜单会在错误时显示失败但不把异常抛给 Tool，因此 MCP 菜单调用返回成功不等于实际清理成功；必须检查结果提示/Console和文件状态。

确认范围与既有授权后执行一次；异常停止并说明已经清理/仍保留部分，不盲目重跑或删整个 Library。缓存清理任务保留授权删除结果；只有测试态目标/临时日志与本轮探针需要清场，不能恢复猜测的旧版本、覆盖用户存档。交付清理范围、DeviceID保留证据、真实验证及尚未验证平台。
