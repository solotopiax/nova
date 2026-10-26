# UIManager

`UIManager` 是 UI 模块真正的运行时核心。  
它同时管理两层东西：

- UI 注册表：把 `IUIViewRow` 数据装进内存，支撑泛型、名称和资源地址打开
- UI 实例系统：分组、打开、关闭、回收、对象池、安全区

`UIComponent` 负责场景入口和生命周期编排，`UIManager` 负责具体运行。

## 什么时候先看这页

优先看这页的场景：

- 你要排查 `OpenUIView*` 为什么失败。
- 你要区分“注册表加载”和“视图实例打开”是不是一回事。
- 你要看 `PauseCoveredUIView`、分组层级、回收队列到底怎么生效。
- 你要排查对象池、异步加载中关闭、SafeArea 这些运行时细节。

如果你还没搞清场景入口和 `Awake / Start / LoadAsync` 的分工，先看 [UIComponent.md](../UIComponent.md)。

## 依赖与边界

### 它依赖什么

- `IAssetManager`
- `IPrefabManager`
- `IObjectPoolManager`
- `IConfigManager`
- `UIManagerConfig`
- `IUIGroupHelper`

### 它对外负责什么

- 维护 `m_UIViewRegistry`
- 创建和管理 `UIGroup`
- 同步 / 异步打开视图
- 关闭视图并在 `Update()` 中统一回收
- 维护实例对象池
- 提供查询、焦点提升、安全区读取

### 它不负责什么

- 不负责 Unity 场景组件生命周期编排
- 不负责并发 `LoadAsync` 合并与“是否已加载完成”的外观语义
- 不负责 UI 根节点和分组辅助器的场景搭建
- 不负责导出 UI 表数据

这些分别在 `UIComponent`、Inspector 配置和导出链路里。

## 核心流程

### 1. Initialize：只缓存配置和依赖

`Initialize(UIManagerConfig config)` 做的事很克制：

1. 记录对象池参数、销毁节流参数、深度系数
2. 记录 `UIUnitSetting`
3. 准备 `SafeAreaProvider`
4. 从 `FrameworkManagersGroup` 获取 `IAssetManager` 与 `IPrefabManager`

这里不会创建对象池，也不会加载注册表。

### 2. CreateInstancePool：等对象池管理器就绪后再接入

`CreateInstancePool()` 会：

1. 获取 `IObjectPoolManager`
2. 创建 `UIViewInstancesPool`

这是一个明确的时序切分点。  
UI 视图如果要走对象池，前提是这一步已经完成。

### 3. LoadSync / LoadAsync：准备所有打开入口所需的注册表

这两个方法都只处理 UI 表数据：

1. 遍历 `m_UnitSettings`
2. 用 `LubanRuntimeData.CreateReceiver` 按 DataFormat 读取 JSON/Binary 到 `LubanDataCache`
3. 通过 `IConfigManager.Namespace` 调 `LubanRuntimeData.LoadTables` 构造 `UITables`
4. 从各表里抽出 `IUIViewRow`
5. 填充 `m_UIViewRegistry`

关键边界：

- 这里不会创建任何 UI 实例
- 这里解决的是“按类型、名称或资源地址打开时能不能找到配置”
- `UIManager` 本身没有 `IsLoadOver`，加载幂等和并发合并在 `UIComponent`

空 Unit/空缓存可能返回 true，不证明有目标注册项；类型不匹配跳过并警告，空行/空 Name 跳过，重复 Name 保留首项并警告。注册表不在每次构建前自动清空，重载不是替换整份注册表的事务。泛型键是 `typeof(T).Name` 短名，跨 namespace 同名类型不会自动隔离。

注册表资源委托取 TextAsset 后立即 Release 句柄再返回给解析器，后续 release 委托为空，未复制 bytes；因此不具备“解析完成才释放”的所有权保证，异常也没有统一 finally 补偿。

### 4. OpenUIView：先查配置，再决定走对象池还是加载

所有打开入口先从 `m_UIViewRegistry` 选择配置行：

| 入口 | 选择规则 |
|---|---|
| `OpenUIViewSync<T>` / `OpenUIViewAsync<T>` | `Name == typeof(T).Name`，保持短类名匹配 |
| 字符串 `OpenUIViewSync` / `OpenUIViewAsync` | `Name == uiViewName`；名称是表内某行的 `Name` 值，不是 xlsx 文件名 |
| `OpenUIViewByAssetSync` / `OpenUIViewByAssetAsync` | `AssetLocation == assetLocation`，整个已加载注册表内必须只有一行 |

接口后缀决定字符串首参含义：`OpenUIViewSync/Async` 只按 `Name`，`OpenUIViewByAssetSync/Async` 只按 `AssetLocation`，不自动检测或尝试另一种身份。第二个参数是 `userData`，字符串业务数据也原样透传。

字符串按原值精确匹配，不做路径规范化。缺失配置、输入为空或地址匹配不唯一时返回 `-1`，不分配 `serialID`，也不开始资源加载。多行共享资源地址时须按各行 `Name` 打开；地址入口不会自行选第一行，也不能用分组参数消歧。

匹配成功后，`OpenRegisteredUIViewSync` / `OpenRegisteredUIViewAsync` 从该行读取 `AssetLocation`、`UIGroupName`、`PauseCoveredUIView`、`InObjectPools`，再调用 `PrepareOpenUIView(...)`：

1. 校验 `assetLocation` 和 `uiGroupName`
2. 校验目标分组存在
3. 分配 `serialID`
4. 如果启用对象池，则尝试 `m_InstancePool.Get(instanceName)`

之后分两条路：

- 命中对象池：直接 `InternalOpenUIView(...)`
- 未命中对象池：
  - 同步版：立刻实例化 Prefab
  - 异步版：记入 `m_UIViewsBeingLoaded`，等待回调成功后再真正打开

所有入口的分组、暂停和池化开关都由所选表格行决定；旧版泛型 bool 重载保留签名并标记 `[Obsolete]`，仅转发到按类型查表入口，传入的开关不再生效。非泛型“资源地址 + 分组”重载已移除。`userData` 原样传递，省略时为 null。池名仍由原资源地址得到；编号分配跳过已打开索引，但不同时检查 loading 编号。

创建实例后会按分组父节点重新归零 localPosition/rotation/scale，并要求根上已有 UIView 子类，不能用任意 IUIView 实现替代这条 Prefab 入口。InternalOpen 按 OnInit→加入组/索引→OnOpen→Refresh 顺序执行，异常包装后传播，但已建立索引、池登记、实例没有完整回滚。UIView 的新实例初始化钩子自身还可能记录并吞异常，继续进入打开链。

异步路径捕获资源/打开异常并转失败事件，事件订阅者异常会记录；主动关闭加载请求或 Shutdown 后的迟到实例走释放分支，不报打开失败。OpenUIViewInfo 来自引用池，Clear 清 group/userData 引用，不销毁业务对象。userData 跨加载等待时的有效性由调用者管理。

### 5. CloseUIView：先脱离分组，再进入回收队列

已加载视图关闭时：

1. 从 `UIGroup` 链表移除
2. 从 `m_UIViewIndex` 移除
3. 调 `uiView.OnClose(...)`
4. `uiGroup.Refresh()`
5. 进入 `m_RecycleQueue`

真正的实例归还 / 销毁，不在 `CloseUIView` 里立刻做，而是在 `Update()` 里按 `DestroyMaxNumPerFrame` 节流处理。

`RemoveUIView` 内部可能先触发该视图的遮挡、暂停回调，随后才从分组移除；`OnClose`、`Refresh` 与入队也不是事务。任一回调抛异常会中断后续步骤，已经移除的分组或索引不会自动回滚。未知 `serialID`、空视图或无有效分组会抛异常，重复关闭不应当作幂等操作。批量关闭先收集已加载视图再逆序关闭，并检查其索引仍存在；它不包含正在加载的视图。关闭加载中视图只标记迟到释放，不取消底层 Prefab 加载。

`Shutdown()` 先标记关闭，处理加载中和已加载视图，再直接清回收队列与内部引用；不能把清空队列解释为逐项执行了 `OnRecycle`、归池或销毁。实例池与 Prefab 的最终清理由各自 Manager 生命周期继续承担；关闭回调异常仍可能使本管理器后续清理中断。

### 6. UIGroup.Refresh：决定深度、遮挡、暂停

`UIGroup` 用链表维护视图，链表头就是当前顶层视图。  
`Refresh()` 每次都会从头到尾重新计算三件事：

- 深度
- 是否被遮挡
- 是否被暂停

当前实现的稳定语义：

- 只有最顶层视图会进入 `OnReveal()`
- 下层视图会进入 `OnCover()`
- 一旦某个视图 `PauseCoveredUIView == true`，它下面的视图都会进入暂停态
- `Update()` 只驱动链表头开始那一段“连续未暂停”的视图

组中每视图信息持有 Paused/Covered 标记，标记不等同 GameObject 当前 active 状态。Refresh 复制当前链表到共享快照再调用深度和状态回调，检查重入或视图移除；没有逐回调隔离或 finally，异常可留下快照与部分标记。组 Pause/Depth setter 先写字段再刷新，失败不回滚。

AddUIView 不拒绝重复实例，链表与实例索引可能不一致；组 Refocus 只把节点移到头，Manager 再 Refresh 并调用视图 OnRefocus。Update 缓存下一节点并处理移除，但回调抛异常会阻断本帧后续视图。不要从回调调用顺序推导业务回调均已完成。

### 7. 查询、回收与调优

公开 GetAll/GetUIViews 的 List 重载校验后先 Clear 再填本次结果，数组重载为副本；组内部收集 helper 才是追加。结果只含当前已加载组/索引，不包括 loading、回收队列或闲置池；地址严格比较，不统一规范化。共享收集缓存不是线程安全/可重入快照。

IsValidUIView 只检查非 null 和其 SerialID 是否存在，不比较索引中的实例引用；不能当强身份验证。Component 的 UIView 查询还会把 IUIView 强转 UIView，自定义接口实现可能失败。

Update 出队后先 OnRecycle，再按 InObjectPools 归池或销毁；DestroyMaxNumPerFrame<=0 会让回收队列不推进，OnRecycle 异常中断后续处理。UIViewInstanceObject.Clear 只清包装，Release 才经 IPrefabManager.Destroy；仅 Clear 不能证明目标已销毁。

实例池四项属性在池未创建时 getter 读初始化缓存，Manager setter 警告并丢弃该次修改；Component setter 会先保留自身字段再转发，二者不能混为同一保存层。SetUIViewTargetLocked 只锁对象池回收资格，不锁输入/遮挡；SetUIViewTargetPriority 只改该 target 的池优先级，不改 Canvas 深度。

DefaultSafeAreaProvider 直接从 Screen.safeArea 给出 Left=xMin、Right=xMax、Top=yMin、Bottom=yMax、PixelRatio=1。Manager 计算宽=Right-Left、高=Bottom-Top，非 GUI y=Top，GUI y=ScreenHeight-height-Top，再乘 PixelRatio 并向上取整。返回物理像素，不自动夹屏幕范围、负尺寸或非有限数；外部 provider 的原点与比例必须按这套字段消费核对。

## 高价值 API 面

### 1. 注册表入口

- `LoadSync()`
- `LoadAsync()`

### 2. 打开视图

- `OpenUIViewSync<T>()`
- `OpenUIViewAsync<T>()`
- `OpenUIViewSync(uiViewName, userData = null)`
- `OpenUIViewAsync(uiViewName, userData = null)`
- `OpenUIViewByAssetSync(assetLocation, userData = null)`
- `OpenUIViewByAssetAsync(assetLocation, userData = null)`
- `OnOpenUIViewFail(serialID, assetLocation, errorMessage)`

共同语义：

- 全部入口依赖 `m_UIViewRegistry`，只按类型、Name 或资源地址选择配置的方式不同
- 资源地址、分组、暂停与池化全部从匹配行读取，没有缺配时的默认值旁路
- 异步打开的非负 `serialID` 只表示请求已创建；资源加载或实例化失败通过 `OnOpenUIViewFail` 通知
- 关闭仍在加载的请求属于主动取消，不触发 `OnOpenUIViewFail`

### 3. 关闭与清理

- `CloseUIView(...)`
- `CloseAllLoadedUIViews(...)`
- `CloseAllLoadingUIViews()`

### 4. 查询与状态

- `GetUIGroup(...)`
- `GetUIView(...)`
- `GetUIViews(...)`
- `GetAllLoadedUIViews()`
- `GetAllLoadingUIViewSerialIDs()`
- `HasUIGroup(...)`
- `HasUIView(...)`
- `IsLoadingUIView(...)`
- `IsValidUIView(...)`

### 5. 运行时调优

- `InstanceAutoReleaseInterval`
- `InstanceCapacity`
- `InstanceExpireTime`
- `InstancePriority`
- `DestroyMaxNumPerFrame`
- `SetUIViewTargetLocked(...)`
- `SetUIViewTargetPriority(...)`
- `GetDeviceSafeArea(...)`

## 关键状态

- `m_UIViewRegistry`：所有打开入口共用的配置源，键是表内 `Name`；泛型入口用 `typeof(T).Name` 查询
- `m_UIViewIndex`：`serialID -> IUIView`
- `m_UIGroups`：分组容器
- `m_UIViewsBeingLoaded`：还没真正打开完成的异步视图
- `m_UIViewsToReleaseOnLoad`：异步加载完成后立刻释放的取消集合
- `m_RecycleQueue`：关闭后等待统一回收的视图
- `m_InstancePool`：视图实例对象池
- `m_IsShutdown`：关闭期守卫，避免异步回调继续进入打开逻辑

## 风险点 / 易错点

- 所有打开入口都依赖注册表；如果 `LoadSync / LoadAsync` 还没跑或目标行缺失，打开会报错并返回 `-1`。
- 按地址打开要求唯一配置；多个行共享地址时按 `Name` 打开，不接受额外分组筛选。跨 Unit 或 Sheet 也使用同一个注册表匹配。
- 配置行存在后，目标分组仍须先注册；表内组名没有对应运行时分组时，实例准备阶段仍会抛异常。
- `inObjectPools == true` 的路径默认会访问 `m_InstancePool`；如果对象池还没创建，时序就不成立。
- `CloseUIView(serialID)` 对“正在加载中的视图”不是立刻 `OnClose`，而是标记到 `m_UIViewsToReleaseOnLoad`，等资源返回后直接释放实例。
- `UIManager` 自己不做加载幂等；如果外层重复触发注册表加载，去重逻辑不在这里。
- `GetDeviceSafeArea()` 返回的是物理像素矩形；`isForGUISystem=true` 时会切换到 GUI 坐标系的 Y 方向。
- `Refresh()` 会触发 `OnReveal / OnCover / OnResume / OnPause`，这些回调里再开关 UI 会递归进入刷新逻辑，排查问题时必须按真实调用链看。

## Breaking：升级迁移

- 原“资源地址 + 分组”重载全部移除。按地址打开改为 `OpenUIViewByAssetSync/Async(assetLocation, userData)`；共享地址则用 `OpenUIViewSync/Async(uiViewName, userData)`，不再由调用方指定分组。
- 旧两个字符串调用必须逐处迁移：新字符串入口将第一项当 `Name`、第二项当 `userData`，不会继续按地址和组名解释，也不会自动猜测。
- 旧泛型 bool 开关参数不再覆盖表格；将暂停和池化行为维护在 UI 表，调用改为不带开关的泛型重载。所有入口都需要先完成权威表导出和注册表加载。
- 外部自定义 `IUIManager` 必须补齐两个名称字符串重载及两个 `ByAsset` 方法，并移除旧接口依赖，详见 [IUIManager.md](Interfaces/IUIManager.md)。

## 继续阅读

关键源码：

- [UIManager.cs](../../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Implements/UIManager.cs)
- [UIManager.Methods.cs](../../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Implements/UIManager.Methods.cs)
- [UIManager.UIGroup.cs](../../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Implements/UIManager.UIGroup.cs)
- [UIManager.Visitors.cs](../../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Implements/UIManager.Visitors.cs)

相关文档：

- [IUIManager.md](Interfaces/IUIManager.md)
- [UIComponent.md](../UIComponent.md)
- [UIGroupHelper.md](../UIGroupHelper/UIGroupHelper.md)
- [UIManagerConfig.md](Definitions/UIManagerConfig.md)
- [UISettings.md](Definitions/UISettings.md)
- [IUIViewRow.md](Definitions/IUIViewRow.md)
