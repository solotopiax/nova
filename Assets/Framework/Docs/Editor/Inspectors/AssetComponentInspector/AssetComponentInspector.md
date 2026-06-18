# AssetComponentInspector

**类签名**：`[CustomEditor(typeof(AssetComponent))] internal sealed partial class AssetComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.AssetComponent`

只负责 AssetManager 类型选择 + AssetManagerConfig 绘制，旧三套 Manager 接口与运行时 RuntimeDrawer 已全部删除。
所有字段上方会先显示一条只读 `DevelopMode` 场景快照标签，由 `BaseComponentInspector` 统一绘制。

---

## 文件

| 文件 | 说明 |
|------|------|
| `AssetComponentInspector.cs` | 主 Inspector：OnEnable 绑定属性 + OnInspectorGUI 调用 DrawConfigs |
| `AssetComponentInspector.Visitors.cs` | 属性与字段声明 |
| `AssetComponentInspector.Methods.cs` | DrawConfigs 与 DrawRuntimePlayModePopup 私有方法 |

---

## §4 序列化属性（Visitors.cs）

按语义段声明，顺序与 OnEnable 绑定及 DrawConfigs 绘制完全一致。

**① Manager 选择**

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_CurAssetManagerTypeName` | `SerializedProperty` | 绑定 `AssetComponent.m_CurAssetManagerTypeName` |
| `m_AssetManagerTypeNames` | `List<string>` | 程序集扫描得到的 `IAssetManager` 实现类全名列表 |

**② 加载模式**

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_EditorPlayMode` | `SerializedProperty` | 绑定 `AssetComponent.m_EditorPlayMode` |
| `m_RuntimePlayMode` | `SerializedProperty` | 绑定 `AssetComponent.m_RuntimePlayMode` |

**③ 资源包配置**

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_Packages` | `SerializedProperty` | 绑定 `AssetComponent.m_Packages`（资源包名列表） |
| `m_DefaultPackageName` | `SerializedProperty` | 绑定 `AssetComponent.m_DefaultPackageName` |
| `m_DecryptorType` | `SerializedProperty` | 绑定 `AssetComponent.m_DecryptorType` |
| `m_AutoCleanupOnSceneUnload` | `SerializedProperty` | 绑定 `AssetComponent.m_AutoCleanupOnSceneUnload` |

**④ 热更配置（开关 + 服务器分发 + 下载行为）**

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_EnableHotfix` | `SerializedProperty` | 绑定 `AssetComponent.m_EnableHotfix`（热更总开关） |
| `m_HostServerUrlDebug` | `SerializedProperty` | 绑定 `AssetComponent.m_HostServerUrlDebug` |
| `m_HostServerUrlFallbackDebug` | `SerializedProperty` | 绑定 `AssetComponent.m_HostServerUrlFallbackDebug` |
| `m_HostServerUrlRelease` | `SerializedProperty` | 绑定 `AssetComponent.m_HostServerUrlRelease` |
| `m_HostServerUrlFallbackRelease` | `SerializedProperty` | 绑定 `AssetComponent.m_HostServerUrlFallbackRelease` |
| `m_AutoHotfix` | `SerializedProperty` | 绑定 `AssetComponent.m_AutoHotfix` |
| `m_QuitOnFailedOrCancel` | `SerializedProperty` | 绑定 `AssetComponent.m_QuitOnFailedOrCancel` |
| `m_MaxDownloadConcurrency` | `SerializedProperty` | 绑定 `AssetComponent.m_MaxDownloadConcurrency` |
| `m_RetryDownloadCount` | `SerializedProperty` | 绑定 `AssetComponent.m_RetryDownloadCount` |
| `m_CheckTimeout` | `SerializedProperty` | 绑定 `AssetComponent.m_CheckTimeout` |
| `m_IdleTimeout` | `SerializedProperty` | 绑定 `AssetComponent.m_IdleTimeout` |
| `m_LaunchHotfixTags` | `SerializedProperty` | 绑定 `AssetComponent.m_LaunchHotfixTags`（启动期切片下载 tag 列表） |
| `m_AutoClearUnusedCacheOnHotfix` | `SerializedProperty` | 绑定 `AssetComponent.m_AutoClearUnusedCacheOnHotfix`（热更完成后是否自动清理旧缓存） |

---

## §5 DrawConfigs 行为说明

`DrawConfigs()` 按以下布局顺序绘制：

1. **Asset 加载管理器**（TypesSelector）— 枚举 `IAssetManager` 全部实现类，配合 HelpBox 说明可自定义扩展。
2. **分隔线**
3. **编辑器加载模式**（平铺，`EditorUtil.Draw.Property`）— 标准 enum Popup，4 选 1，永远可编辑。
4. **终端加载模式**（平铺，`DrawRuntimePlayModePopup`）— 自定义 IntPopup，3 选 1，禁 EditorSimulateMode；含 RuntimePlayMode→EnableHotfix 联动逻辑。
5. **分隔线**
6. **资源包名列表**（平铺，Unity 默认 List 控件，`DrawPackagesList`）— 对应 `AssetComponent.m_Packages`；通过 `EditorUtil.Draw.PropertyField(includeChildren:true)` 渲染，自带 Size 字段与默认增删按钮；`DrawPackagesList` 强制 `m_Packages.isExpanded = true` 默认展开。
7. **默认资源包名**（平铺，下拉，`DrawDefaultPackageNamePopup`）— 对应 `AssetComponent.m_DefaultPackageName`；选项严格 = 当前 `m_Packages` 全部条目，无占位；当前值不在选项内时自动归一为首项；`m_Packages` 为空时退化为提示 Label。
8. **资源解密器类型**（平铺）— 对应 `AssetComponent.m_DecryptorType`；AB 包加密方式，与打包时加密器保持一致，None 表示不加密。
9. **场景卸载时自动清理**（平铺）— 对应 `AssetComponent.m_AutoCleanupOnSceneUnload`。
10. **分隔线**
11. **热更配置 Foldout**（默认展开，key `"AssetHotfixConfigGroup"`）：
    - **EnableHotfix Toggle**（首位）— 热更新总开关；关闭后直跳 LoadDll，跳过版本检查 / 资源补丁 / 强更下载；含 EnableHotfix→RuntimePlayMode 联动逻辑（详见联动规则表）。
    - 以下字段通过 `EditorGUI.DisabledScope(!m_EnableHotfix.boolValue)` 联动灰度（EnableHotfix=false 时不可编辑），按显示顺序：
      - HostServerUrlDebug — Debug 主机服务器地址 URL
      - HostServerUrlFallbackDebug — Debug 备用主机服务器地址 URL
      - HostServerUrlRelease — Release 主机服务器地址 URL
      - HostServerUrlFallbackRelease — Release 备用主机服务器地址 URL
      - AutoHotfix — 补丁就绪后是否自动开始下载
      - QuitOnFailedOrCancel — 下载失败或取消时是否强制退出
      - MaxDownloadConcurrency — 最大并发数（推荐 3-8）
      - RetryDownloadCount — 单文件重试次数
      - LaunchHotfixTags — 启动期切片下载 tag 列表（空列表=整包更新，填入 tag=切片按需下载）
      - AutoClearUnusedCacheOnHotfix — 热更完成后是否自动清理旧缓存
      - CheckTimeout — 版本检查超时（秒）
      - IdleTimeout — 文件下载空闲超时（秒）
12. **分隔线**

所有 Foldout 内条目通过 `EditorUtil.Draw.Space(16f)` 缩进，形成父子层级视觉。
四个 HostServerUrl 字段之间不再插入额外横线，保持同组平铺；实际生效组由节点上的 `DevelopMode` 决定。

---

## §9 联动规则表

Inspector 编辑期双向联动，运行时不再二次覆盖：

| 触发字段 | 触发后值 | 连锁动作 |
|---|---|---|
| `EnableHotfix` | `false` | `RuntimePlayMode` → `OfflinePlayMode` |
| `EnableHotfix` | `true` | 若 `RuntimePlayMode == OfflinePlayMode` → `RuntimePlayMode = HostPlayMode`；否则保持原值 |
| `RuntimePlayMode` | `OfflinePlayMode` | `EnableHotfix` → `false` |
| `RuntimePlayMode` | `HostPlayMode` 或 `WebPlayMode` | `EnableHotfix` → `true` |

联动后均调用 `serializedObject.ApplyModifiedProperties()` + `serializedObject.Update()` 刷新 Inspector。

---

## §10 DrawRuntimePlayModePopup 说明

`DrawRuntimePlayModePopup()` 是 `AssetComponentInspector.Methods.cs` 中的私有方法，实现自定义 IntPopup，满足「终端模式禁 EditorSimulateMode」的约束：

- 合法选项固定为 `{ OfflinePlayMode, HostPlayMode, WebPlayMode }`；
- 入参校正：若当前值为 `EditorSimulateMode`（异常值），回落到 `OfflinePlayMode`；
- 用 `EditorUtil.Draw.Layout.Horizontal` 包裹 `Label + EditorGUILayout.IntPopup`，与同页其他 Property 视觉对齐；
- 值变更时触发 RuntimePlayMode→EnableHotfix 联动（见联动规则表）。

---

## 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [AssetComponent.md](../../../Runtime/Modules/Asset/AssetComponent.md)
- [AssetManagerConfig.md](../../../Runtime/Modules/Asset/AssetManager/Definitions/AssetManagerConfig.md)
- [AssetPlayMode.md](../../../Runtime/Modules/Asset/Definitions/AssetPlayMode.md)
