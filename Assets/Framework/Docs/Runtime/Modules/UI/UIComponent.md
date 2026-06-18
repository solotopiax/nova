# UIComponent

`UIComponent` 是 Nova UI 模块的场景入口。它本身不承载复杂业务逻辑，主要负责三件事：

- 创建并持有 `IUIManager`
- 准备实例根节点与分组辅助器
- 对业务层暴露统一的“打开 / 关闭 / 查询 / 加载注册表”门面

## 什么时候先看这页

优先看这页的场景：

- 你要判断 UI 模块的入口在哪。
- 你要搞清 `Awake / Start / LoadAsync` 各负责什么。
- 你要判断某个 UI 能力是在 `UIComponent` 还是在 `UIManager`。
- 你要接入新分组、看注册表加载、或排查 UI 为什么还不能查询。

如果你已经确认问题在实例池、视图注册表、打开关闭细节里，直接继续看 [UIManager.md](UIManager/UIManager.md)。

## 依赖与边界

### 它依赖什么

- `IUIManager`
- `UIGroupHelperBase`
- `UISettings`
- `IObjectPoolManager`（通过 `UIManager.CreateInstancePool()` 间接依赖）

### 它对外暴露什么

- UI 注册表加载入口
- 视图打开 / 关闭 / 查询门面
- 分组添加与查询门面
- 安全区域与少量运行时配置入口

### 它不负责什么

- 不负责视图注册表的实际加载细节
- 不负责视图实例生命周期细节
- 不负责对象池实现
- 不负责具体分组排序、打开策略、覆盖暂停策略的底层规则

这些都在 `UIManager` 和 `UIGroupHelper`。

## 核心流程

### Awake：把 UI 运行时骨架搭起来

`Awake()` 做的是“把壳搭好”，而不是加载 UI 数据：

1. `base.Awake()` 注册框架组件
2. 通过 `Util.TypeCreator.Create<IUIManager>` 创建 `m_UIManager`
3. 若 `m_InstanceRoot` 未配置，则自动创建 `UIViewInstancesRoot`
4. 只有“自动创建根节点”这条分支才会补挂 `Canvas / CanvasScaler / GraphicRaycaster`
5. 无论根节点是自动创建还是 Inspector 预设，都会把实例根节点层级切到 `UI`
6. 对可用的 `CanvasScaler` 应用设计分辨率和宽高适配参数

这一步的重点是：**先把运行时容器和显示根节点准备好。**

如果 Inspector 已经指定了 `m_InstanceRoot`，当前实现只会复用现有节点并尝试应用 `CanvasScaler`，不会自动补组件。

### Start：把配置下发给 UIManager，再注册分组

`Start()` 做的是“初始化”和“分组注册”：

1. `m_UIManager.Initialize(...)`
2. 遍历 `m_UIGroups`，逐个 `AddUIGroup(name, depth)`
3. 调用 `m_UIManager.CreateInstancePool()`

这里有一个关键拆分：

- `Initialize` 只下发配置
- `CreateInstancePool` 才真正依赖对象池管理器

所以 `UIComponent` 把“配置阶段”和“对象池接入阶段”拆成两段，避免在过早阶段触发跨组件时序问题。

### LoadSync / LoadAsync：只负责“注册表可用”

`LoadSync()` 和 `LoadAsync()` 的目标都不是打开 UI，而是让 UI 注册表进入“可查询、可按泛型打开”的状态。

`LoadAsync()` 有两个关键语义：

- 已成功加载后直接复用结果
- 并发调用会合并到同一个 `m_LoadTcs`

这意味着：

- 可以把它放到启动流程里集中加载
- 不应该自己再在业务层做额外的并发去重

## 高价值 API 面

这里不展开所有重载，只保留真正值得先记住的能力分组。

### 1. 注册表加载

- `LoadSync()`
- `LoadAsync()`
- `IsLoadOver`

用途：

- 把 UI 模块切到“可按类型 / 配置查询”的状态

### 2. 打开视图

- `OpenUIViewSync<T>()`
- `OpenUIViewAsync<T>()`
- `OpenUIViewSync(assetLocation, uiGroupName, ...)`
- `OpenUIViewAsync(assetLocation, uiGroupName, ...)`

语义：

- 泛型入口走注册表
- 字符串入口走显式资源地址 + 分组
- 是否暂停被覆盖视图、是否带 `userData` 都属于打开策略层，而不是入口层差异

### 3. 关闭与重聚焦

- `CloseUIView(...)`
- `CloseUIViews(...)`
- `CloseAllLoadedUIViews(...)`
- `CloseAllLoadingUIViews()`
- `RefocusUIView(...)`

用途：

- 已加载视图关闭
- 仍在加载中的视图取消
- 重新提升焦点

### 4. 查询

- `GetUIGroup(...)`
- `GetAllUIGroups(...)`
- `GetUIView(...)`
- `GetUIViews(...)`
- `HasUIGroup(...)`
- `HasUIView(...)`
- `IsLoadingUIView(...)`
- `IsValidUIView(...)`

用途：

- 看分组
- 看实例
- 看加载态

### 5. 运行时显示行为

- `SetUIViewTargetLocked(...)`
- `SetUIViewTargetPriority(...)`
- `GetDeviceSafeArea(...)`

用途：

- 调对象池释放行为
- 取物理安全区域

## 关键状态

- `m_UIManager`：真正的 UI 运行时核心实现，`UIComponent` 基本都在透传它。
- `m_UISettings`：决定注册表和运行时加载来源，是理解 UI 数据来自哪里的第一入口。
- `m_LoadTcs`：用于合并并发 `LoadAsync`。
- `m_InstanceRoot`：所有运行时 UI 实例挂载根节点。
- `m_UIGroups`：Inspector 中声明的分组配置，会在 `Start()` 转成真实分组。
- `IsLoadOver`：只表示“注册表是否已经成功加载”，不表示某个具体 UI 是否已打开。

## 风险点 / 易错点

- `LoadAsync()` 成功前，不要假设泛型打开和基于注册表的查询一定可用。
- `UIComponent` 的很多 public API 只是门面；如果要排查打开失败、排序异常、实例回收异常，直接看 `UIManager`。
- `CreateInstancePool()` 被放在 `Start()` 末尾，不要把对象池相关假设提前到 `Awake()`。
- `m_InstanceRoot` 为空时会自动创建根节点；如果你依赖自定义层级或现成 Canvas，需要确认 Inspector 配置是否覆盖了默认行为。
- `InstanceAutoReleaseInterval / Capacity / ExpireTime / Priority` 这些值既影响 Inspector 语义，也会同步影响 `UIManager`。

## 继续阅读

关键源码：

- [UIComponent.cs](../../../../Scripts/Runtime/Modules/UI/UIComponent.cs)
- [UIComponent.Visitors.cs](../../../../Scripts/Runtime/Modules/UI/UIComponent.Visitors.cs)
- [UIManager.cs](../../../../Scripts/Runtime/Modules/UI/Managers/UIManager/Implements/UIManager.cs)

相关文档：

- [UIManager.md](UIManager/UIManager.md)
- [UIGroupHelper.md](UIGroupHelper/UIGroupHelper.md)
- [UISettings.md](UIManager/Definitions/UISettings.md)
- [IUIViewRow.md](UIManager/Definitions/IUIViewRow.md)
- [UIView.md](Definitions/UIView.md)
