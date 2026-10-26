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

`LoadSync()` 和 `LoadAsync()` 的目标都不是打开 UI，而是让 UI 注册表进入可供所有打开入口查找配置的状态。泛型、表内名称和资源地址入口都要先加载注册表。

`LoadSync()` 调用返回 `void` 的 Manager 加载入口后直接设置 `IsLoadOver=true`。Manager 遇到读取失败或建表失败可以只记日志并返回，因此同步调用结束和该标志不能证明目标注册项可用；还须查询对应 `UIView` 注册行。空配置也可以正常跳过加载。

`LoadAsync()` 有两个关键语义：

- 已成功加载后直接复用结果
- 并发调用会合并到同一个 `m_LoadTcs`

这意味着：

- 可以把它放到启动流程里集中加载
- 不应该自己再在业务层做额外的并发去重

失败或异常转 false 后允许重试；OnDestroy 清任务引用/标志/Manager 引用，不取消正在执行的底层注册表加载。IsLoadOver 与目标行、Prefab 和页面打开分别验证。

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
- `OpenUIViewSync(uiViewName, userData = null)`
- `OpenUIViewAsync(uiViewName, userData = null)`
- `OpenUIViewByAssetSync(assetLocation, userData = null)`
- `OpenUIViewByAssetAsync(assetLocation, userData = null)`
- `OnOpenUIViewFail`

所有入口都先查找已加载的 UI 配置行，再从该行读取资源地址、分组、是否暂停下层视图和是否缓存到对象池。`userData` 只透传给视图，不改变这些配置。

| 打开方式 | 配置匹配规则 |
|---|---|
| 泛型 `OpenUIViewSync<T>` / `OpenUIViewAsync<T>` | 按 `typeof(T).Name` 匹配 UI 表的 `Name`，仍使用短类名 |
| 字符串 `OpenUIViewSync` / `OpenUIViewAsync` | 首参 `uiViewName` 匹配 UI 表某一行的 `Name`；这里的名称不是 xlsx 文件名 |
| `OpenUIViewByAssetSync` / `OpenUIViewByAssetAsync` | 首参 `assetLocation` 按 `AssetLocation` 在整个已加载注册表中查找唯一行；共享地址有多行时改按 `Name` 打开 |

接口名称决定首参含义：无 `ByAsset` 的字符串入口始终按 `Name` 查找，带 `ByAsset` 的入口始终按资源地址查找，不自动猜测或在两者之间回退。第二个参数始终是 `userData`，即使它是字符串也会原样交给视图。

名称和资源地址都按原字符串精确匹配。缺少配置、注册表尚未加载，或地址匹配到多行时返回 `-1`，不创建打开请求，也不以默认开关绕过表格。分组统一取自匹配行的 `UIGroupName`。

`OpenUIViewAsync` 和 `OpenUIViewByAssetAsync` 返回的非负 `serialID` 只表示打开请求已创建；后续资源加载或实例化失败时，
通过 `OnOpenUIViewFail(serialID, assetLocation, errorMessage)` 通知业务层。业务订阅者应按
自己保存的 `serialID` 过滤事件，并在生命周期结束时注销。

```csharp
// Name 列值为 DemoToastView 的配置行。
// 第二个字符串是业务数据。
int byName = Nova.UI.OpenUIViewAsync("DemoToastView", "Hello");

// AssetLocation 列值为 DemoToastView，且只对应一行配置。
int byAsset = Nova.UI.OpenUIViewByAssetAsync("DemoToastView");
```

#### Breaking：旧调用迁移

- 原有泛型 bool 重载保留签名并标记 `[Obsolete]`，传入的开关不再生效；将行为写回 UI 表，并改用不带开关的泛型入口。
- 原有非泛型“资源地址 + 分组”重载全部移除。按地址打开改为 `OpenUIViewByAssetSync/Async(assetLocation, userData)`；地址对应多行时改为 `OpenUIViewSync/Async(uiViewName, userData)`，分组从该行读取。
- 必须检查旧的两个字符串调用：`OpenUIViewAsync(assetLocation, uiGroupName)` 现在会按第一项作为 `Name`、第二项作为业务数据绑定，不能继续把第二项当分组使用。
- 原先未在 UI 表登记的资源地址现在不能直接打开，需要先登记并正式导出、加载注册表。
- 项目自定义 `IUIManager` 需要实现新增的两个 `OpenUIViewSync/Async(string uiViewName, object userData = null)` 名称重载和两个 `OpenUIViewByAssetSync/Async` 方法，详见 [IUIManager.md](UIManager/Interfaces/IUIManager.md)。

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
- `IsLoadOver`：组件的加载完成标志；同步路径不核对 Manager 的失败结果，不能单凭此标志确认目标注册项存在或具体 UI 已打开。

## 风险点 / 易错点

- `LoadAsync()` 成功前，不要假设任何打开入口已有所需配置；即使 `IsLoadOver=true`，目标行也可能缺失。
- 所有打开入口返回 `-1` 都表示未创建打开请求；请求创建后的异步失败通过 `OnOpenUIViewFail` 通知。
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
