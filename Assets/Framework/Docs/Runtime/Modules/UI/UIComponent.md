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

## 按需等待页面打开

旧 `OpenUIViewAsync*` 返回请求序号，原用法保留。新业务确需等待页面准备完成时，选择 `OpenUIViewAwaitAsync<T>`、按表内 Name 的 `OpenUIViewAwaitAsync(string, ...)` 或唯一地址的 `OpenUIViewByAssetAwaitAsync`，取得 `UIViewOpenResult`；成功必须完成初始化、`OnOpen` 与组刷新。状态、取消和旧自定义 Manager 的兼容边界见 [IUIOpenAwaiter](UIManager/Interfaces/IUIOpenAwaiter.md)。该能力不自动迁移旧页面，也不改变 UI 注册与分组来源。

## 屏幕适配与安全区

### 先确定方案，再制作布局

新项目在 UI/Prefab 制作前完成下表；已有项目创建页面或调整布局、画布、安全区时，先读取并继承当前方案，只修改本次授权内容。纯事件、文案或数据绑定且不影响布局时，无需重做全套适配。项目事实由 Agent 查明；用户未指定且工程无既有方案时，在目标范围内选择合理默认、说明理由并告知用户，不把所有参数反复转交用户填写。

| 必须明确 | 实施前记录的内容 |
| --- | --- |
| 平台与方向 | 目标设备、产品定位横屏/竖屏、是否自动旋转及允许方向；核对实际 Player Settings，不能仅从 Game View 截图判断。横屏只按横屏适配，竖屏只按竖屏适配；只有明确支持双方向才设计和验证双方向，不为适配自动扩大范围。 |
| 设计尺寸与比例 | 参考设计分辨率及其宽高比、支持的最窄/最宽屏幕比例。设计分辨率是画布参考尺寸，不能每台设备改成其物理像素，也不能套用 Demo 的固定尺寸。 |
| 布局策略 | 按宽/按高匹配与权重，玩法区域如何保持比例、留白或裁切，背景如何铺满；明确文字、按钮及底部操作区的锚点与最小可用空间。 |
| 缩放拥有者 | 实际 `UIComponent`、实例根/父 Canvas、`CanvasScaler` 与配置资产，记录当前值、目标值及生效时机；根缩放只由一处负责。 |
| 安全区拥有者 | 刘海、挖孔、圆角、系统栏和底部手势区域如何避让，每个内容区域由谁唯一负责，背景是否延伸到屏幕边缘；记录坐标换算与支持方向/尺寸变化、池化重开的刷新方式。单一拥有者指同一目标不重复扣边距，不要求所有页面和区域共用一个收缩容器。 |
| 验证范围 | 参考比例、最窄/最宽支持比例、无刘海与有安全区裁切的代表场景，以及实际支持的旋转方向；移动端交付还需目标 Player/设备验证，未取得时单列缺口。 |

方案可以直接写在本轮任务说明中，不强制新增长期方案文件。实施前必须已确定可执行方案，不能等页面做好后才补适配。

### 从启动到玩法的逐页适配

完整产品的适配范围从启动第一帧开始。按实际入口列出闪屏、版本检查/下载/Loading、启动确认或错误弹窗、主菜单、玩法 HUD/触控、暂停、结算及返回/重开；共用 Prefab 或面板的不同状态可以复用布局证据，但须核对内容长度和按钮变化。不存在的产品页面不为验收而新增。

启动 [LauncherUI](../Procedure/LauncherUI.md) 的 Resources Prefab 有独立 Canvas/CanvasScaler，不能因玩法 `UIComponent` 已设为横屏就认定它们也已适配。逐个核对其参考尺寸、匹配方式、Logo 比例、文本/进度条宽度、弹窗与触控尺寸。启动早于业务 DLL 加载；优先用项目自有 Prefab 的布局完成，确需早期适配组件时按 [NA-004 的 AOT 边界](../../../Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-004业务默认进入热更范围aot-例外须证明必要性)证明加载前职责，不能把尚未加载的业务程序集脚本挂到启动资源上。

### 按内容避让，保留可用空间

背景、世界画面和装饰按铺满策略显示；Logo、正文、进度、按钮、HUD 与摇杆分别选择有界居中、边缘锚定、重排或局部位移。系统栏/手势带等连续边界按区域用途处理；有精确刘海/挖孔信息时，只调整与遮挡范围相交的内容，未相交的顶部信息、底部操作区等保留原有空间。平台只提供矩形安全区时可以保守采用安全区内排版，但要如实说明数据边界，仍需检查字号、间距、触达和观感。

不把整张画面或所有内容统一缩入安全矩形，不用全局缩小、对称加黑边或固定大边距代替布局设计；也不为追求铺满把关键文字与交互留在遮挡区。排版本身已留足安全空间的中央内容无需再扣整屏安全边距。采用多个局部容器时，每个目标只有一个避让拥有者，注意父子层叠加、背景裁切和触控坐标。

### 复用 UIComponent 的根画布缩放

| Inspector / API | 当前实现 |
| --- | --- |
| “屏幕设计分辨率” `m_ScreenDesignedResolution` / `ScreenDesignedResolution` | 声明默认 `1366×768`；公开属性只读。项目的实际值来自自身配置，不以声明默认代替。 |
| “屏幕宽高适配比例阀值 (W<->H)” `m_ScreenWidthHeightMatchValue` / `ScreenWidthHeightMatchValue` | 声明默认 `1`；`0` 匹配宽、`1` 匹配高。公开 setter 会同步已缓存的缩放器。 |
| `InstanceRootCanvasScaler` | 只读访问实际缓存的根缩放器；不是另一套独立缩放配置。 |

`Awake()` 的 `ApplyInstanceRootCanvasScaler` 查找实例根自身或父 Canvas 的 `CanvasScaler`，写入 `uiScaleMode=ScaleWithScreenSize`、`referenceResolution` 与 `matchWidthOrHeight`；当前没有显式写入 `screenMatchMode`，必须核对实际模式。预设 `m_InstanceRoot` 不会自动补齐缺少的 Canvas/Scaler。调整设计值应通过 Unity 保存实际承载 `UIComponent` 的项目资产；消费任务按 `nova-project-edit-settings` 冻结目标、Prefab override 与写集，不修改包内 canonical Prefab 或手写 YAML。方向变更须核对当前宿主真实可用的 Unity Editor Player Settings 编辑通道，单独冻结原值、目标值、保存与读回；`edit-settings` 的普通资产 Action 不支持任意 ProjectSettings，缺少可用通道时明确缺口。

运行后再读回实际缩放器验证。被 UIComponent 选中的实例根/父 CanvasScaler 会在 `Awake()` 被写入，不能仅改该缩放器而忽略 UIComponent 的配置；不得每页再挂一套根尺寸/缩放脚本，使父画布和页面重复缩放。局部棋盘、图片等保持比例属于具体内容布局，应明确其局部职责，不再控制全局根画布。

### 安全区查询与内容接入

`Nova.UI.GetDeviceSafeArea(bool isForGUISystem = false)` 返回物理像素 `Rect`；默认原点在左下，`true` 用于左上原点的 GUI 坐标。默认 provider 读取 `Screen.safeArea`，查询不会自动修改任何 `RectTransform`。当前 UI 模块没有通用的自动安全区容器组件，`UIComponent` Inspector 也没有自定义 provider 注入项，不能宣称安装后已自动避让刘海。

先查项目已有安全区容器和平台适配；缺少内容布局时，只在业务范围补最小接线。业务通过 `Nova.UI` 查询，不直接取得 UIManager。针对实际画布、父 RectTransform、坐标原点及渲染模式换算：只有内容容器对应全屏且坐标一致时才可按屏幕尺寸归一化锚点；其他情况使用实际父级的坐标变换。不能把屏幕物理像素边距直接赋给已缩放画布的 `offsetMin/offsetMax`。

`UIView` 基类初始化会把视图根拉伸铺满父级；宜在独立内容子容器或局部区域避让，每个目标由一处应用安全约束，避免背景、视图根和内容层重复扣边距。首次 `OnInit` 仅对新实例执行，池化重开不会再次初始化；按支持的方向、窗口/安全区变化及重开时机刷新，避免每帧无差别重建全部布局。

存量示例边界：`DemoNavTreeView.ApplySafeArea` 当前直接把物理像素边距写入 offset，不能作为缩放画布下正确换算的通用模板；Debugger 的 `SafeAreaSizer` 是单边布局占位组件，也不等于 UI 模块通用安全容器。本节明确偏差，不在规则维护任务中迁移这些存量实现。

### 适配验收

在实际入口按页面清单核对产品方向、参考/边界比例和安全区：文字可读、按钮可触达、关键内容不被裁切或遮挡、背景铺满策略正确、未受遮挡的区域没有无谓收缩、交互坐标与显示一致、开关页及支持的旋转后不叠加缩放或边距。短暂的闪屏和 Loading 也须取得实际显示证据，可在受控验证中暂留页面观察，不能只检查静态 Prefab 或伪称它经过真实下载。只覆盖玩法界面时不得称启动全流程已适配；只完成横屏产品的横屏验收不算缺少竖屏验收。记录实际分辨率、比例、安全区和生效配置；只有 Inspector 保存或编译通过不足以证明布局正确。缺少必要 Play、目标 Player 或设备证据时保持对应 `partial`。测试尺寸、方向、临时停留、对象和截图按共同清场要求恢复或清理。

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

订阅需在组件初始化后进行。退出时 `UIComponent` 可能先于业务视图执行 `OnDestroy`；
此时 `OnOpenUIViewFail -= handler` 允许安全完成，重复退订也不会访问已清空的管理器引用。
这只保证事件退订的收尾安全，不表示已销毁组件仍可打开、查询或关闭视图。

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
