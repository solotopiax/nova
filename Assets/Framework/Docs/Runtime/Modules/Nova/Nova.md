# Nova

**类签名**：`public sealed partial class Nova : FrameworkComponent`
**命名空间**：`NovaFramework.Runtime`
**执行顺序**：`[DefaultExecutionOrder(-1000)]`

框架全局入口组件，挂载于场景根节点 GameObject。作为 **Facade（外观模式）**，聚合所有子 Component 为静态属性，任何代码可通过 `Nova.UI`、`Nova.Asset` 等直接访问全局服务。

---

## 文件

| 文件 | 说明 |
|------|------|
| `Nova.cs` | 主体：`Awake`（单例保护 + 异常隔离注入助手）、`Start`（聚合组件）、`Update`（驱动 ManagersGroup）、`OnDestroy`（清理 + 静态引用置空）、游戏控制方法 |
| `Nova.Visitors.cs` | 所有静态 Component 属性 + `ResetStatics`（Domain Reload 安全）+ `ClearStaticReferences` + 运行时配置 `[SerializeField]` 字段 |
| `Nova.Methods.cs` | `SetCultureInfo` / `IsStrictCheck` / `OnLowMemory` / `ValidateComponent` 等私有辅助方法 |

---

## 继承关系

```
MonoBehaviour
  └── FrameworkComponent (abstract) : ICoroutineRunner
        └── Nova (sealed partial)
```

> Nova 是所有 FrameworkComponent 中唯一在 `Awake` 中注入全局助手（TxtHelper / LogHelper / ReferenceHelper）的组件，其他 Component 仅做注册。

---

## Domain Reload 安全

Nova 使用 `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]` 标记的 `ResetStatics()` 方法，在 Enter Play Mode Settings 关闭 Domain Reload 时自动重置所有静态 Component 属性为 `null`，防止残留脏引用。

```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
private static void ResetStatics()
```

`OnDestroy()` 中同时调用 `ClearStaticReferences()` 确保运行期退出时也不持有已销毁对象。

---

## 单例保护

`Awake()` 中检测 `Self != null && Self != this`，若已存在实例则销毁自身并 `return`，避免多实例并存导致的状态混乱。

---

## 异常隔离

`Awake()` 中 TxtHelper、LogHelper、ReferenceHelper 三个助手的初始化各自独立 `try-catch`，某个助手创建失败不会阻断后续助手的初始化。

---

## 静态 Component 属性（Nova.Visitors.cs）

> **线程可见性**：所有静态属性仅在主线程 `Start()` 中赋值，无 volatile / 内存屏障保护。后台线程不应直接读取这些属性，否则可能读到未初始化的 `null` 值。

| 属性 | 类型 | 说明 |
|------|------|------|
| `Nova.Self` | `Nova` | Nova 自身引用（Start 赋值） |
| `Nova.Asset` | `AssetComponent` | 资源加载/卸载；`Nova.Asset.EnableHotfix: bool`（只读，委托 AssetComponent.m_EnableHotfix） |
| `Nova.Config` | `ConfigComponent` | 配置数据（异步加载，LoadConfigAsync） |
| `Nova.Prefab` | `PrefabComponent` | 预制体加载/缓存/实例化 |
| `Nova.Event` | `EventComponent` | 全局事件订阅/发布 |
| `Nova.App` | `AppComponent` | 大版本检查/APK 下载/商店跳转 |
| `Nova.Table` | `TableComponent` | 表格数据（异步加载，LoadTablesAsync） |
| `Nova.Localization` | `LocalizationComponent` | 多语言本地化（文本/字体适配） |
| `Nova.UI` | `UIComponent` | UI 视图管理 |
| `Nova.Network` | `NetworkComponent` | 网络通信 |
| `Nova.Procedure` | `ProcedureComponent` | 流程管理 |
| `Nova.ObjectPool` | `ObjectPoolComponent` | 对象池（GameObject/Resource 级别） |
| `Nova.Persist` | `PersistComponent` | 持久化存储 |
| `Nova.Sound` | `SoundComponent` | 声音播放/声音组管理 |
| `Nova.Vibrate` | `VibrateComponent` | 触觉振动反馈 |
| `Nova.SDK` | `SDKComponent` | SDK 集成 |
| `Nova.Debug` | `DebugComponent` | 调试工具 |

---

## 运行时配置字段（[SerializeField]，Inspector 可编辑）

| 字段 | 默认值 | 属性 | 说明 |
|------|--------|------|------|
| `m_FrameRate` | `60` | `FrameRate`（可读写） | `Application.targetFrameRate` |
| `m_GameSpeed` | `1f` | `GameSpeed`（可读写） | `Time.timeScale`，低于 0 自动截断为 0 |
| `m_RunInBackground` | `true` | `RunInBackground`（可读写） | `Application.runInBackground` |
| `m_NeverSleep` | `true` | `NeverSleep`（可读写） | `Screen.sleepTimeout` |
| `m_ReferenceStrictCheckType` | `AlwaysEnable` | — | 引用池严格检查模式（见下表） |
| `m_CurTxtHelperTypeName` | `""` | `CurTxtHelperTypeName` | ITxtHelper 实现类型全名 |
| `m_CurLogHelperTypeName` | `""` | `CurLogHelperTypeName` | ILogHelper 实现类型全名 |
| `m_CurReferenceHelperTypeName` | `""` | `CurReferenceHelperTypeName` | IReferenceHelper 实现类型全名 |

**计算属性：**

| 属性 | 说明 |
|------|------|
| `IsGamePaused` | `m_GameSpeed <= 0f` |
| `IsNormalGameSpeed` | `m_GameSpeed == 1f` |

**ReferenceStrictCheckType 枚举：**

| 值 | 行为 |
|----|------|
| `AlwaysEnable` | 始终严格检查（捕获池引用错误，性能稍差） |
| `OnlyEnableWhenDevelopment` | 仅 `Debug.isDebugBuild` 时启用 |
| `OnlyEnableInEditor` | 仅编辑器内启用 |
| `AlwaysDisable` | 始终关闭（正式发布包使用） |

---

## Unity 脚本执行顺序保证

Nova 使用 `[DefaultExecutionOrder(-1000)]` 确保其 `Awake()` 和 `Start()` 在其他 FrameworkComponent 之前执行。同时依赖 Unity 平台保证：

| 保证 | 含义 |
|------|------|
| **所有 Awake 先于所有 Start** | 所有 FrameworkComponent（含 Nova）的 `Awake()` 完成后，任何 `Start()` 才开始执行。因此 Nova.Start() 读取 `FrameworkComponentsGroup` 时，所有子组件已注册完毕 |
| **同帧内 Start 不依赖顺序** | Nova.Start() 只做引用赋值，不调用任何 Manager；Manager 在各 Component.Start() 中独立创建。因此各 Component.Start() 执行顺序不影响结果 |

> **结论：所有 FrameworkComponent 必须挂载在场景中且 GameObject 处于 Active 状态**，否则其 `Awake()` 不执行，`Nova.Start()` 取到 `null`。

---

## 生命周期详解

### Awake()
1. **单例保护**：若 `Self` 已存在且不等于 `this`，销毁自身并提前返回（在 `base.Awake()` 之前，避免重复注册）
2. `base.Awake()` → `FrameworkComponentsGroup.RegisterComponent(this)`
3. `Self = this`
4. `Util.TypeCreator.Create<ITxtHelper>()` → `Txt.SetHelper()`（独立 try-catch）
5. `Util.TypeCreator.Create<ILogHelper>()` → `Log.SetHelper()`（独立 try-catch）
6. `Util.TypeCreator.Create<IReferenceHelper>()` → `ReferencePool.SetHelper()`（独立 try-catch）
7. `SetCultureInfo(CultureInfo.InvariantCulture)` — 消除地区格式化差异
8. 设置 `Application.targetFrameRate / Time.timeScale / runInBackground / sleepTimeout`
9. 注册 `Application.lowMemory += OnLowMemory`

### Start()
1. 从 `FrameworkComponentsGroup` 查找并赋值 App / Asset / Config / Prefab / Event / Table / Localization / UI / Network / Procedure / ObjectPool / Persist / Sound / Vibrate / SDK / Debug
2. 通过 `ValidateComponent` 统一校验每个组件，若为 null 则输出标准化错误日志
3. 打印 Unity 版本、Nova 版本、APP 版本日志

### Update()
```csharp
FrameworkManagersGroup.Update();
```

### OnDestroy()
```csharp
FrameworkManagersGroup.Shutdown();     // 逆序 Shutdown 所有 Manager
Application.lowMemory -= OnLowMemory;  // 注销内存预警回调
ClearStaticReferences();               // 清空所有静态 Component 属性
```

---

## 游戏控制公开方法

| 方法签名 | 说明 |
|---------|------|
| `void PauseGame()` | `GameSpeed = 0`，缓存原速度到 `m_GameSpeedCache` |
| `void ResumeGame()` | 恢复 `GameSpeed = m_GameSpeedCache` |
| `void ResetNormalGameSpeed()` | `GameSpeed = 1f` |
| `void QuitApplication()` | `Application.Quit()`，编辑器下 `isPlaying = false` |

---

## 私有辅助方法（Nova.Methods.cs）

| 方法签名 | 说明 |
|---------|------|
| `void SetCultureInfo(CultureInfo)` | 设置主线程 + 默认线程文化信息，内置 try-catch |
| `bool IsStrictCheck()` | 根据 `m_ReferenceStrictCheckType` 返回是否启用严格引用检查 |
| `void OnLowMemory()` | 内存不足预警回调，依次执行 `ReferencePool.ClearAll()` + `Resources.UnloadUnusedAssets()` + `GC.Collect()` |
| `static void ValidateComponent(FrameworkComponent, string)` | 校验组件非空，空时输出标准化错误日志 |

---

## 关联文档

- [FrameworkComponent.md](../FrameworkComponent.md)
- [FrameworkManager.md](../FrameworkManager.md)
- [ARCHITECTURE.md](../../../ARCHITECTURE.md)
