# UIManager

`UIManager` 是 UI 模块真正的运行时核心。  
它同时管理两层东西：

- UI 注册表：把 `IUIViewRow` 数据装进内存，支撑泛型打开
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

### 3. LoadSync / LoadAsync：只让“泛型打开所需注册表”可用

这两个方法都只处理 UI 表数据：

1. 遍历 `m_UnitSettings`
2. 用 `LubanDataReceiver` 把 JSON 拆到 `LubanDataCache`
3. 通过 `IConfigManager.Namespace` 调 `LubanTablesLoader.Load("UITables", namespace, loader)`
4. 从各表里抽出 `IUIViewRow`
5. 填充 `m_UIViewRegistry`

关键边界：

- 这里不会创建任何 UI 实例
- 这里解决的是“泛型打开时能不能找到配置”
- `UIManager` 本身没有 `IsLoadOver`，加载幂等和并发合并在 `UIComponent`

### 4. OpenUIView：先准备，再决定走对象池还是加载

同步和异步打开都先走 `PrepareOpenUIView(...)`：

1. 校验 `assetLocation` 和 `uiGroupName`
2. 校验目标分组存在
3. 分配 `serialID`
4. 如果启用对象池，则尝试 `m_InstancePool.Get(instanceName)`

之后分两条路：

- 命中对象池：直接 `InternalOpenUIView(...)`
- 未命中对象池：
  - 同步版：立刻实例化 Prefab
  - 异步版：记入 `m_UIViewsBeingLoaded`，等待回调成功后再真正打开

### 5. CloseUIView：先脱离分组，再进入回收队列

已加载视图关闭时：

1. 从 `UIGroup` 链表移除
2. 从 `m_UIViewIndex` 移除
3. 调 `uiView.OnClose(...)`
4. `uiGroup.Refresh()`
5. 进入 `m_RecycleQueue`

真正的实例归还 / 销毁，不在 `CloseUIView` 里立刻做，而是在 `Update()` 里按 `DestroyMaxNumPerFrame` 节流处理。

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

## 高价值 API 面

### 1. 注册表入口

- `LoadSync()`
- `LoadAsync()`

### 2. 打开视图

- `OpenUIViewSync<T>()`
- `OpenUIViewAsync<T>()`
- `OpenUIViewSync(assetLocation, uiGroupName, ...)`
- `OpenUIViewAsync(assetLocation, uiGroupName, ...)`

语义差异：

- 泛型入口依赖 `m_UIViewRegistry`
- 字符串入口直接显式指定资源与分组

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

- `m_UIViewRegistry`：泛型打开的配置源，键是 `typeof(T).Name`
- `m_UIViewIndex`：`serialID -> IUIView`
- `m_UIGroups`：分组容器
- `m_UIViewsBeingLoaded`：还没真正打开完成的异步视图
- `m_UIViewsToReleaseOnLoad`：异步加载完成后立刻释放的取消集合
- `m_RecycleQueue`：关闭后等待统一回收的视图
- `m_InstancePool`：视图实例对象池
- `m_IsShutdown`：关闭期守卫，避免异步回调继续进入打开逻辑

## 风险点 / 易错点

- 泛型打开依赖注册表；如果 `LoadSync / LoadAsync` 还没跑，`GetUIViewRow<T>()` 会报错并返回 `-1`。
- 字符串打开不依赖注册表，但依赖目标分组已经存在；分组不存在会直接抛异常。
- `inObjectPools == true` 的路径默认会访问 `m_InstancePool`；如果对象池还没创建，时序就不成立。
- `CloseUIView(serialID)` 对“正在加载中的视图”不是立刻 `OnClose`，而是标记到 `m_UIViewsToReleaseOnLoad`，等资源返回后直接释放实例。
- `UIManager` 自己不做加载幂等；如果外层重复触发注册表加载，去重逻辑不在这里。
- `GetDeviceSafeArea()` 返回的是物理像素矩形；`isForGUISystem=true` 时会切换到 GUI 坐标系的 Y 方向。
- `Refresh()` 会触发 `OnReveal / OnCover / OnResume / OnPause`，这些回调里再开关 UI 会递归进入刷新逻辑，排查问题时必须按真实调用链看。

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
