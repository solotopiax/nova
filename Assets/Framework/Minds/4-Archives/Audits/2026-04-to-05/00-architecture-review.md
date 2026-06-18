# Nova Framework 全局架构复盘

> 审计日期：2026-04-12 | 基于 develop 分支 HEAD 快照
> 以 12 年+ Unity 商业项目顶级架构师视角

---

## 1. 架构总评

### 1.1 评分矩阵

| 模块 | 继承链 | C+M 分离 | 接口解耦 | 目录结构 | 综合 |
|------|--------|----------|----------|----------|------|
| Nova 根节点 | 5 | 5 | 5 | 5 | **4.5** |
| Event | 5 | 5 | 5 | 5 | **4.5** |
| Network | 5 | 4 | 5 | 5 | **4.3** |
| Asset | 5 | 5 | 5 | 5 | **4.5** |
| UI | 5 | 5 | 5 | 5 | **4.5** |
| Hotfix | 5 | 5 | 5 | 5 | **4.3** |
| Localization | 5 | 5 | 5 | 5 | **4.5** |
| Persist | 5 | 5 | 5 | 5 | **4.5** |
| Procedure | 5 | 5 | 5 | 5 | **4.5** |
| ObjectPool | 5 | 5 | 5 | 5 | **4.0** |
| Table | 5 | 5 | 5 | 5 | **4.8** |
| Config | 5 | 5 | 5 | 5 | **4.8** |
| SDK | 5 | 5 | 5 | 5 | **4.8** |
| Sound | 5 | 5 | 5 | 5 | **4.8** |
| Vibrate | 5 | 5 | 5 | 5 | **5.0** |
| **Debug** | 4 | **2** | 3 | 3 | **2.8** |
| Bases 基础层 | N/A | N/A | 5 | 5 | **4.0** |
| Utils 工具层 | N/A | N/A | 5 | 5 | **4.0** |
| Editor 层 | N/A | 5 | 5 | 5 | **4.5** |

**框架整体评分：4.3 / 5**

### 1.2 架构亮点

1. **Component + Manager 分离一致性极高**：16 个业务模块中 15 个严格遵循三层继承链 `FrameworkManager → XxxManagerBase (internal abstract) → XxxManager (internal sealed partial)`。访问修饰符无一例外正确。

2. **接口解耦彻底**：所有 Component 通过 `IXxxManager` 接口持有 Manager，全局访问通过 `Nova.Xxx` 静态属性。平级模块间无直接依赖，跨模块通信走 Event。

3. **Priority 驱动的生命周期管理**：`FrameworkManagersGroup` 以 Priority 排序的 LinkedList 管理所有 Manager，Update 正序/Shutdown 逆序，设计优雅且可预测。

4. **Editor 三文件联动规范**：全部 16 个 ComponentInspector 严格遵循 `.cs / .Visitors.cs / .Methods.cs` 三文件结构，声明顺序 = 绑定顺序 = 绘制顺序，一致性极高。

5. **EditorUtil.Draw 封装体系**：绝大多数 Inspector 通过统一的 `EditorUtil.Draw` 封装层绘制 UI，仅发现 1 处不合理的直接使用（TextLocalizingInspector）。

6. **Helper 注入模式**：Log/Txt/Reference 三个基础服务通过 Helper 接口 + TypeCreator 反射注入，可替换性好。

### 1.3 架构痛点

#### 1.3.1 Debug 模块 — 架构严重失衡

**问题**：DebugComponent 承载了 35+ 个 partial 文件的业务逻辑（日志收集、IMGUI 绘制、FPS/RAM 计数器、磁盘检测、GM 工具、白名单请求、触摸状态机），而 DebugManager 几乎是空壳。

**影响**：严重违反 SRP 和 Component/Manager 分离原则。单个类文件数量是其他模块的 5-10 倍，维护成本极高。

**建议**：将业务逻辑下沉到 DebugManager，Component 仅保留 IMGUI OnGUI() 绘制分发和 Unity 生命周期桥接。

#### 1.3.2 async void 滥用

**问题**：`SDKComponent.Start()`、`SoundManager.LoadAndPlaySoundAsync`、`DebugComponent.GetDeviceWhiteDevices` 等使用 `async void` 而非 `async UniTask` / `async UniTaskVoid`。

**影响**：异常无法被调用方捕获；MonoBehaviour Destroy 后异步续体仍可执行；调用方无法知晓完成时机。

**建议**：统一改为 `async UniTaskVoid`（fire-and-forget 场景）或 `async UniTask`（需要 await 场景），在 `.Forget()` 处理未观察异常。

#### 1.3.3 Component 门面层参数校验不一致

**问题**：部分 Component（ObjectPool 17处、Table 4处）在门面层做了参数校验（throw ArgumentNullException），而其他 Component（Config、UI 优化后）纯透传不校验。

**影响**：与项目确立的"谁使用谁校验"原则冲突，增加维护认知负担。

**建议**：统一移除 Component 层校验，全部下沉到 Manager 层。

#### 1.3.4 Priority 文档系统性失真

**问题**：ObjectPool（文档 16 vs 代码 2）、Table（文档 4 vs 代码 14）、Config（文档 3 vs 代码 15）的 L2 文档中 Priority 值全部错误。ARCHITECTURE.md 中的 Shutdown 顺序表也存在排序错误。

**影响**：开发者依据文档理解模块初始化/销毁顺序时获得完全错误的信息。

**建议**：建立 Priority 单一真相源（ARCHITECTURE.md），L2 文档引用而非复制。

---

## 2. 继承体系分析

### 2.1 Runtime Manager 继承链

```
FrameworkManager (public abstract)
├── AssetManagerBase (internal abstract) : IAssetManager          Priority = 1
│   └── AssetManager (internal sealed partial)
├── ObjectPoolManagerBase (internal abstract) : IObjectPoolManager Priority = 2
│   └── ObjectPoolManager (internal sealed partial)
├── PersistManagerBase (internal abstract) : IPersistManager       Priority = 3
│   └── PersistManager (internal sealed partial)
├── EventManagerBase (internal abstract) : IEventManager           Priority = 4
│   └── EventManager (internal sealed partial)
├── ConfigManagerBase (internal abstract) : IConfigManager         Priority = 15
│   └── ConfigManager (internal sealed partial)
├── TableManagerBase (internal abstract) : ITableManager           Priority = 14
│   └── TableManager (internal sealed partial)
├── UIManagerBase (internal abstract) : IUIManager                 Priority = 5
│   └── UIManager (internal sealed partial)
├── NetworkManagerBase (internal abstract) : INetworkManager       Priority = 6
│   └── NetworkManager (internal sealed partial)
├── HotfixManagerBase (internal abstract) : IHotfixManager         Priority = 7
│   └── HotfixManager (internal sealed partial)
├── LocalizationManagerBase (internal abstract) : ILocalizationManager Priority = 8
│   └── LocalizationManager (internal sealed partial)
├── ProcedureManagerBase (internal abstract) : IProcedureManager   Priority = 9
│   └── ProcedureManager (internal sealed partial)
├── SDKManagerBase (internal abstract) : ISDKManager               Priority = 16
│   └── SDKManager (internal sealed partial)
├── DebugManagerBase (internal abstract) : IDebugManager           Priority = 17
│   └── DebugManager (internal sealed partial)
├── VibrateManagerBase (internal abstract) : IVibrateManager       Priority = 18
│   └── VibrateManager (internal sealed partial)
└── SoundManagerBase (internal abstract) : ISoundManager           Priority = 19
    └── SoundManager (internal sealed partial)
```

### 2.2 Update 执行顺序（Priority 升序）

```
Asset(1) → ObjectPool(2) → Persist(3) → Event(4) → UI(5) → Network(6) →
Hotfix(7) → Localization(8) → Procedure(9) → Table(14) → Config(15) →
SDK(16) → Debug(17) → Vibrate(18) → Sound(19)
```

### 2.3 Shutdown 执行顺序（Priority 降序）

```
Sound(19) → Vibrate(18) → Debug(17) → SDK(16) → Config(15) → Table(14) →
Procedure(9) → Localization(8) → Hotfix(7) → Network(6) → UI(5) →
Event(4) → Persist(3) → ObjectPool(2) → Asset(1)
```

---

## 3. 依赖关系图

### 3.1 模块间依赖方向

```
                    ┌──────────────────────────────┐
                    │        Nova (Root)            │
                    │   DontDestroyOnLoad 根节点    │
                    └──────────┬───────────────────┘
                               │ 获取所有 Component
                    ┌──────────▼───────────────────┐
                    │  FrameworkComponentsGroup     │
                    │  (静态注册表 Dictionary<Type>) │
                    └──────────┬───────────────────┘
                               │
          ┌────────────────────┼────────────────────┐
          ▼                    ▼                    ▼
    ┌──────────┐        ┌──────────┐        ┌──────────┐
    │  Asset   │◄───────│  Hotfix  │        │  Table   │
    │Component │        │Component │        │Component │
    └──────────┘        └──────────┘        └──────────┘
          ▲                                       ▲
          │                                       │
    ┌──────────┐        ┌──────────┐        ┌──────────┐
    │   UI     │        │  Event   │◄───ALL │  Config  │
    │Component │        │Component │        │Component │
    └──────────┘        └──────────┘        └──────────┘
          ▲                    ▲
          │                    │
    ┌──────────┐        ┌──────────┐
    │Procedure │───────►│   SDK    │
    │Component │        │Component │
    └──────────┘        └──────────┘
```

**关键原则**：
- 所有模块通过 `Nova.Xxx` 访问其他模块（间接依赖）
- 跨模块通信优先走 Event 系统
- Runtime → Editor 严格单向（零违规）
- Editor 只通过 Component 公开 API 读写数据

### 3.2 基础设施依赖层

```
┌─────────────────────────────────────────────┐
│              业务模块层 (16 Components)       │
├─────────────────────────────────────────────┤
│         FrameworkComponent / Manager         │
│      FrameworkComponentsGroup / ManagersGroup│
├─────────────────────────────────────────────┤
│    Bases: ReferencePool / FSM / Log / Txt    │
│    Bases: NovaLinkedList / MultiDictionary    │
│    Bases: DataReceiver / Extensions          │
├─────────────────────────────────────────────┤
│    Utils: TypeCreator / Assembly / Encrypt    │
│    Utils: Json / SQLite / SysIO / MD5        │
└─────────────────────────────────────────────┘
```

---

## 4. 数据流分析

### 4.1 启动流程

```
Nova.Awake() [DefaultExecutionOrder(-1000)]
  ├── base.Awake() → FrameworkComponentsGroup.RegisterComponent
  ├── TxtHelper = TypeCreator.Create<ITxtHelper>()
  ├── LogHelper = TypeCreator.Create<ILogHelper>()
  ├── RefHelper = TypeCreator.Create<IReferenceHelper>()
  ├── Application.targetFrameRate = m_TargetFrameRate
  └── Application.runInBackground = true

各 Component.Awake() [自然执行顺序]
  ├── base.Awake() → FrameworkComponentsGroup.RegisterComponent
  └── m_XxxManager = TypeCreator.Create<IXxxManager>(typeName)
       └── FrameworkManager 构造器 → FrameworkManagersGroup.RegisterManager(this)
           └── 按 Priority 排序插入 LinkedList

Nova.Start()
  ├── Self = this
  ├── Asset = GetComponent<AssetComponent>()  // 获取全部 16 个 Component
  └── ... (其他 Component)

各 Component.Start()
  └── m_XxxManager.Initialize(config)
       └── 配置注入、跨模块引用获取
```

### 4.2 帧循环

```
Nova.Update()
  └── FrameworkManagersGroup.Update()
       └── foreach manager in linkedList (Priority 升序)
            └── manager.Update()
```

### 4.3 销毁流程

```
Nova.OnDestroy()
  └── FrameworkManagersGroup.Shutdown()
       └── foreach manager in linkedList.Reverse (Priority 降序)
            └── manager.Shutdown()
```

---

## 5. Partial Class 拆分合规性

### 5.1 合规模块（四文件拆分完整）

- Event, UI, Network, Hotfix, Localization, Persist, Procedure, SDK, Sound, Vibrate
- 所有 ComponentInspector (16/16)

### 5.2 需改进模块

| 模块 | 问题 | 建议 |
|------|------|------|
| ObjectPoolManager | 声明 partial 但只有 1 个文件（660行） | 拆分为 .cs/.Visitors.cs/.Methods.cs |
| ObjectPool\<T\> | 未声明 partial，单文件 700行 | 拆分并声明 partial |
| Object\<T\> | 未声明 partial | 按规范拆分 |
| ObjectBase | 未声明 partial | 按规范拆分 |
| ObjectPoolBase | 未声明 partial | 按规范拆分 |
| TableManager | `.Method.cs` (应为 `.Methods.cs`) | 重命名 |
| ConfigManager | `.Method.cs` (应为 `.Methods.cs`) | 重命名 |
| Debug | 35+ partial 文件但逻辑全在 Component | 重构：逻辑下沉到 Manager |

---

## 6. 跨模块模式问题

### 6.1 Error Handling 不一致

| 场景 | Sound 做法 | Vibrate 做法 | 应统一为 |
|------|-----------|-------------|----------|
| 空路径加载 | Log.Debug + return | throw InvalidOperationException | Log.Debug + return |
| 异步异常 | try-catch + Log.Error | try-catch + Log.Error | 一致 (OK) |

### 6.2 ConfigComponent vs TableComponent 对称性缺失

| 维度 | Table | Config |
|------|-------|--------|
| OnDestroy | 有 | **缺失** |
| Initialize 校验 | Namespaces 非空 | **无校验** |
| UnitDataReceiver.OnParseDataAsset(string) | 无 try-catch | 有 try-catch |

### 6.3 文档 Priority 单一真相源问题

当前 Priority 值存在于 3 个位置：代码、ARCHITECTURE.md、各 L2 文档。三者之间同步靠人工维护，已出现系统性失真。

**建议**：L2 文档中的 Priority 应链接到 ARCHITECTURE.md 而非硬编码数值。或建立自动化校验脚本。
