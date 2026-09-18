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
| `m_WebGLAssetStrategy` | `SerializedProperty` | 绑定 `AssetComponent.m_WebGLAssetStrategy`；只在当前 BuildTarget 为 WebGL 时可编辑，提供 `TagsOnLaunch` / `OnDemand` / `AllOnLaunch` |

**③ 资源包配置**

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_Packages` | `SerializedProperty` | 绑定 `AssetComponent.m_Packages`（资源包名列表） |
| `m_DefaultPackageName` | `SerializedProperty` | 绑定 `AssetComponent.m_DefaultPackageName` |
| `m_AutoCleanupOnSceneUnload` | `SerializedProperty` | 绑定 `AssetComponent.m_AutoCleanupOnSceneUnload` |

**④ 热更配置（开关 + 服务器分发 + 下载行为）**

| 字段 | 类型 | 说明 |
|------|------|------|
| `m_EnableHotfix` | `SerializedProperty` | 绑定 `AssetComponent.m_EnableHotfix`（热更总开关） |
| `m_EnableStartupWhitelist` | `SerializedProperty` | 启动设备白名单开关 |
| `m_StartupWhitelistUrlDebug` / `m_StartupWhitelistUrlFallbackDebug` | `SerializedProperty` | Debug 配置文件主备 URL |
| `m_StartupWhitelistUrlRelease` / `m_StartupWhitelistUrlFallbackRelease` | `SerializedProperty` | Release 配置文件主备 URL |
| `m_StartupWhitelistMetadataRootUrlDebug` / `m_StartupWhitelistMetadataRootUrlFallbackDebug` | `SerializedProperty` | Debug 版本元数据根主备 URL |
| `m_StartupWhitelistMetadataRootUrlRelease` / `m_StartupWhitelistMetadataRootUrlFallbackRelease` | `SerializedProperty` | Release 版本元数据根主备 URL |
| `m_StartupWhitelistFallbackRoundCount` | `SerializedProperty` | 白名单文件主备完整轮数，默认 1 |
| `m_StartupWhitelistRetryRequestCount` | `SerializedProperty` | 白名单文件请求重试次数，默认 1 |
| `m_StartupWhitelistPreferLastSuccessfulHost` | `SerializedProperty` | 白名单文件是否优先最近成功域名，默认开启 |
| `m_StartupWhitelistEnableUWRTracks` | `SerializedProperty` | 白名单文件 UWR 埋点开关，默认开启 |
| `m_StartupWhitelistCheckTimeout` | `SerializedProperty` | 白名单文件单次请求超时，默认 5 秒 |
| `m_HostServerUrlDebug` | `SerializedProperty` | 绑定 `AssetComponent.m_HostServerUrlDebug` |
| `m_HostServerUrlFallbackDebug` | `SerializedProperty` | 绑定 `AssetComponent.m_HostServerUrlFallbackDebug` |
| `m_HostServerUrlRelease` | `SerializedProperty` | 绑定 `AssetComponent.m_HostServerUrlRelease` |
| `m_HostServerUrlFallbackRelease` | `SerializedProperty` | 绑定 `AssetComponent.m_HostServerUrlFallbackRelease` |
| `m_QuitOnFailedOrCancel` | `SerializedProperty` | 绑定 `AssetComponent.m_QuitOnFailedOrCancel` |
| `m_MaxDownloadConcurrency` | `SerializedProperty` | 绑定 `AssetComponent.m_MaxDownloadConcurrency` |
| `m_FallbackRoundCount` | `SerializedProperty` | 绑定主备候选完整轮数 |
| `m_RetryDownloadCount` | `SerializedProperty` | 绑定下载重试次数；每次重试重新执行完整轮次组合 |
| `m_PreferLastSuccessfulHost` | `SerializedProperty` | 绑定最近成功域名优先开关 |
| `m_EnableUWRTracks` | `SerializedProperty` | 绑定 Asset UWR 埋点开关 |
| `m_CheckTimeout` | `SerializedProperty` | 绑定 `AssetComponent.m_CheckTimeout` |
| `m_ManifestRequestTimeout` | `SerializedProperty` | 绑定 `.hash/.bytes` 各自的单次请求超时，默认 60 秒 |
| `m_WebGLBundleRequestTimeout` | `SerializedProperty` | 绑定 WebGL Bundle 单次请求超时，默认 300 秒；覆盖 StreamingAssets 与 CDN 来源 |
| `m_IdleTimeout` | `SerializedProperty` | 绑定非 WebGL 单文件字节流入超时 |
| `m_LaunchHotfixTags` | `SerializedProperty` | 绑定启动期 Tag：非 WebGL 为补丁下载范围，WebGL 仅为 `TagsOnLaunch` 预热范围 |
| `m_AutoClearUnusedCacheOnHotfix` | `SerializedProperty` | 绑定 `AssetComponent.m_AutoClearUnusedCacheOnHotfix`（热更完成后是否自动清理旧缓存） |

---

## §5 DrawConfigs 行为说明

`DrawConfigs()` 按以下布局顺序绘制：

1. **Asset 加载管理器**（TypesSelector）— 枚举 `IAssetManager` 全部实现类，配合 HelpBox 说明可自定义扩展。
2. **分隔线**
3. **编辑器加载模式**（平铺，`DrawEditorPlayModePopup`）— 自定义 IntPopup，3 选 1，永远可编辑。
4. **终端加载模式**（平铺，`DrawRuntimePlayModePopup`）— 自定义 IntPopup，2 选 1，禁 EditorSimulateMode；含 RuntimePlayMode→EnableHotfix 联动逻辑。下方只显示一个 HelpBox，并随 Unity Active BuildTarget 切换内容：WebGL 说明 Offline 必须全量随网页发布、Host 可选三种首包布局及选错影响；其他平台显示普通安装包与远端更新说明。
5. **WebGL 资源策略**（平铺，`DrawWebGLAssetStrategyPopup`）— 当前 BuildTarget 为 WebGL 时可编辑；其余平台保留显示但禁用。HelpBox 只说明三种策略对启动速度、首次加载等待和内存占用的直接影响，不展示内部 Handle 或加载链路。
6. **分隔线**
7. **资源包名列表**（平铺，Unity 默认 List 控件，`DrawPackagesList`）— 对应 `AssetComponent.m_Packages`；通过 `EditorUtil.Draw.PropertyField(includeChildren:true)` 渲染，自带 Size 字段与默认增删按钮；`DrawPackagesList` 强制 `m_Packages.isExpanded = true` 默认展开。
8. **默认资源包名**（平铺，下拉，`DrawDefaultPackageNamePopup`）— 对应 `AssetComponent.m_DefaultPackageName`；选项严格 = 当前 `m_Packages` 全部条目，无占位；当前值不在选项内时自动归一为首项；`m_Packages` 为空时退化为提示 Label。
9. **场景卸载时自动清理**（平铺）— 对应 `AssetComponent.m_AutoCleanupOnSceneUnload`。
10. **分隔线**
11. **热更配置 Foldout**（默认展开，key `"AssetHotfixConfigGroup"`）：
    - **EnableHotfix Toggle**（首位）— 常规资源热更总开关；关闭后跳过资源补丁检查和 Downloader 下载，不影响 App 大版本检查。WebGL 选用 `TagsOnLaunch` / `AllOnLaunch` 时，启动 Warmup 仍会执行；含 EnableHotfix→RuntimePlayMode 联动逻辑（详见联动规则表）。
    - 以下字段通过 `EditorGUI.DisabledScope(!m_EnableHotfix.boolValue)` 联动灰度（EnableHotfix=false 时不可编辑），按显示顺序：
      - HostServerUrlDebug — Debug 主机服务器地址 URL
      - HostServerUrlFallbackDebug — Debug 备用主机服务器地址 URL
      - HostServerUrlRelease — Release 主机服务器地址 URL
      - HostServerUrlFallbackRelease — Release 备用主机服务器地址 URL
      - 启动白名单 — 默认收起的二级 Foldout；标题内开关控制功能启停，展开后缩进显示全部子配置
        - 配置文件 URL Debug/Release 主备 — `VersionsCheckWhiteList.json` 文件地址
        - 版本元数据根 URL Debug/Release 主备 — 命中后仅用于 YooAsset 版本元数据；Bundle 仍走常规主机地址
        - 白名单文件独立使用主备完整轮数、请求重试次数、最近成功域名、UWR 埋点和请求超时配置；默认分别为 `1`、`1`、`true`、`true`、`5`
      - LaunchHotfixTags — 非 WebGL 是启动期补丁检查与下载范围（空列表=整包）；WebGL 下显示为“启动预热 Tag 列表”，只供 `TagsOnLaunch` 选择启动必须资源。HelpBox 仅提示填写要求和未填写时的用户可见行为。
      - 清空本地热更资源缓存 — 清理范围由 `EditorUtil.Asset.Cache` 统一实现；HelpBox 只提示会清理资源缓存且保留其他本地数据
      - QuitOnFailedOrCancel — 下载失败或取消时是否强制退出；WebGL 固定关闭且不覆盖 Android/iOS 已保存值
      - MaxDownloadConcurrency — 最大并发数（推荐 3-8）
      - FallbackRoundCount — CDN 请求每个逻辑周期的主备完整轮数；OfflinePlayMode 下禁用
      - RetryDownloadCount — CDN 资源请求重试次数；OfflinePlayMode 下禁用
      - PreferLastSuccessfulHost — CDN 新请求优先最近成功域名；OfflinePlayMode 下禁用
      - EnableUWRTracks — CDN 资源请求埋点开关；OfflinePlayMode 下禁用
      - AutoClearUnusedCacheOnHotfix — 非 WebGL 热更完成后是否自动清理旧缓存；WebGL 下禁用
      - CheckTimeout — 版本检查超时（秒）
      - ManifestRequestTimeout — Manifest 请求超时（秒），位于版本检查请求超时下方；`.hash` 和 `.bytes` 分别使用它
      - WebGLBundleRequestTimeout — WebGL Bundle 请求超时（秒）；仅 WebGL 可编辑，同时覆盖 StreamingAssets 和 CDN Bundle 请求
      - IdleTimeout — 文件下载空闲超时（秒）；WebGL 下禁用，其他平台可编辑
11. **分隔线**

热更配置的实际生效范围如下；表中的 Android/iOS 指 `HostPlayMode`，其 `OfflinePlayMode` 不执行 CDN 补丁下载：

| 配置 | Android/iOS | WebGL StreamingAssets | WebGL CDN |
|---|---|---|---|
| 失败/取消时强制退出 | 补丁下载失败或取消时生效 | 不生效，固定关闭 | 不生效，固定关闭 |
| 资源请求最大并发数 | 控制 Downloader 并发 | 控制 Bundle 异步加载与 Warmup 并发 | 控制 Bundle 异步加载与 Warmup 并发 |
| 主备完整轮数 | CDN 请求生效 | 不生效 | CDN 请求生效 |
| 资源请求重试次数 | CDN 请求生效 | 不生效 | CDN 请求生效 |
| 最近成功域名优先 | CDN 请求生效 | 不生效 | CDN 请求生效 |
| 启用 UWR 埋点 | CDN 请求生效 | 不生效 | CDN 请求生效 |
| 热更完成后自动清理缓存 | Downloader 成功后生效 | 不生效 | 不生效 |
| 版本检查请求超时 | Host `.version` 请求生效 | StreamingAssets `.version` 请求生效 | CDN `.version` 请求生效 |
| Manifest 请求超时 | Host `.hash/.bytes` 请求生效 | StreamingAssets Manifest 请求生效 | CDN Manifest 请求生效 |
| WebGL Bundle 请求超时 | 不生效 | Bundle 请求生效 | Bundle 请求生效 |
| 单文件字节流入超时 | Host CDN 下载生效 | 不生效 | 不生效 |

其中“主备完整轮数、资源请求重试次数、最近成功域名优先、启用 UWR 埋点”是 CDN 专属配置，仅 `HostPlayMode` 可编辑。StreamingAssets 请求仍使用对应的版本、Manifest 或 WebGL Bundle 超时，但不参与 CDN 主备候选轮换。

所有 Foldout 内条目通过 `EditorUtil.Draw.Space(16f)` 缩进，形成父子层级视觉。
四个 HostServerUrl 字段之间不再插入额外横线，保持同组平铺；实际生效组由节点上的 `DevelopMode` 决定。
所有 HelpBox 只说明配置用途、用户可见影响和必要操作；内部类型、方法、文件名、Handle 生命周期与调用链留在 Docs，不在 Inspector 中展示。配置项因平台、总开关、子选项或 Play Mode 禁用时，其对应 HelpBox 同步灰显。主备完整轮数、资源请求重试次数、最近成功域名优先和 UWR 埋点均在各自配置项下方显示简短说明。
启动白名单及其 URL 位于常规热更选项上方；全部新增 URL 支持既有占位符，白名单检查与 YooAsset 热更新均走各自的 UnityWebRequest 主备路径。关闭白名单、未配置当前 DevelopMode 地址或首次没有 DeviceID 缓存时自动跳过。
缓存清理按钮位于 LaunchHotfixTags 的 HelpBox 下方，复用 `EditorUtil.Asset.Cache`，不直接在 Inspector 中拼接或删除路径。

---

## §9 联动规则表

Inspector 编辑期双向联动，运行时不再二次覆盖：

| 触发字段 | 触发后值 | 连锁动作 |
|---|---|---|
| `EnableHotfix` | `false` | `RuntimePlayMode` → `OfflinePlayMode` |
| `EnableHotfix` | `true` | 若 `RuntimePlayMode == OfflinePlayMode` → `RuntimePlayMode = HostPlayMode`；否则保持原值 |
| `RuntimePlayMode` | `OfflinePlayMode` | `EnableHotfix` → `false` |
| `RuntimePlayMode` | `HostPlayMode` | `EnableHotfix` → `true` |

联动后均调用 `serializedObject.ApplyModifiedProperties()` + `serializedObject.Update()` 刷新 Inspector。

---

## §10 DrawRuntimePlayModePopup 说明

`DrawRuntimePlayModePopup()` 是 `AssetComponentInspector.Methods.cs` 中的私有方法，实现自定义 IntPopup，满足「终端模式禁 EditorSimulateMode」的约束：

- 合法选项固定为 `{ OfflinePlayMode, HostPlayMode }`；
- 入参校正：若当前值为 `EditorSimulateMode`（异常值），回落到 `OfflinePlayMode`；
- 用 `EditorUtil.Draw.Layout.Horizontal` 包裹 `Label + EditorGUILayout.IntPopup`，与同页其他 Property 视觉对齐；
- 值变更时触发 RuntimePlayMode→EnableHotfix 联动（见联动规则表）。

---

## 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [AssetComponent.md](../../../Runtime/Modules/Asset/AssetComponent.md)
- [AssetManagerConfig.md](../../../Runtime/Modules/Asset/AssetManager/Definitions/AssetManagerConfig.md)
- [AssetPlayMode.md](../../../Runtime/Modules/Asset/Definitions/AssetPlayMode.md)
- [WebGLAssetStrategies.md](../../../Runtime/Modules/Asset/WebGLAssetStrategies.md)
