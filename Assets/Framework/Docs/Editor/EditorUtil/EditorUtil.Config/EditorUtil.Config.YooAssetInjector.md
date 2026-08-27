# EditorUtil.Config.YooAssetInjector

**类签名**：`public static class YooAssetInjector`（`EditorUtil.Config` 的嵌套 partial）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Config.YooAssetInjector`

Asset 模块编辑期注入层；按 ConfigMasterSO 中显式声明的路径字段注入 `YooAssetSettings` 与加载 `BundleCollectorSetting`，替代 `Resources.Load` / `AssetDatabase.FindAssets` 全工程扫描，根除多 Sample 共存时命中错副本问题。Sample 不再常驻 `Resources/YooAssetSettings.asset`；Editor Play Mode 会在 `BeforeSceneLoad` 按当前三维坐标重新注入，Player 构建则由构建期 staging 临时提供运行时副本。

UPM Sample 导入到 `Assets/Samples/<Package>/<Version>/<Sample>/` 或业务方整体移动 Demo 后，ConfigMaster 内原有项目相对路径可能失效。维度解析器会在原路径不可加载时，仅回退到 ConfigMaster 同目录下同名且类型匹配的配置资产；该重定位只影响解析结果，不回写序列化资产。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.cs` | `EditorUtil.Config.YooAssetInjector` | 注入与收集器加载：`Inject` / `LoadBundleCollector` |
| `Editor/EditorUtil/EditorUtil.SceneRoute.cs` | `EditorUtil.SceneRoute` | Single 场景打开时的 ConfigMaster / YooAsset / Pipify 路由编排与四阶段日志摘要 |

---

## §5 完整公开 API

```csharp
// 按 ConfigMasterSO 当前三维坐标解析并注入 YooAssetSettings 到 YooAssetConfiguration 静态全局
// 原路径失效但 ConfigMaster 同目录存在同名、同类型资产时，使用迁移后的实际路径
// 进入即作废 BundleCollectorSetting 缓存并清空上一工作区注入的 YooAssetSettings
// master 为 null 或 YooAssetSettingsPath 为空时保持未注入状态
// 路径对应资产不存在时记 Log.Warning 并静默返回
public static void Inject(ConfigMasterSO master);

// 按已经完成三维解析的项目相对路径直接注入 YooAssetSettings。
// path 为空时静默返回；资产不存在时记录 Warning 并返回。
public static void InjectByPath(string path);

// 按 ConfigMasterSO 当前三维坐标解析并加载 BundleCollectorSetting
// master 为 null 或 BundleCollectorSettingPath 为空时返回 null
// 路径对应资产不存在时返回 null
// <returns>BundleCollectorSetting 实例；未配置或不存在时返回 null</returns>
public static BundleCollectorSetting LoadBundleCollector(ConfigMasterSO master);
```

### 自动钩子（[InitializeOnLoadMethod]，无需业务调用）

| 钩子 | 时机 | 行为 |
|------|------|------|
| `RegisterYooAssetExplicitPathProvider` | Editor 启动 / 域重载 | 向 `SettingLoader.RegisterExplicitPathProvider` 注册回调，按当前激活 master 的 `BundleCollectorSettingPath` 解析 `BundleCollectorSetting`，替代 YooAsset 内置 `AssetDatabase.FindAssets` 全工程扫描；激活 master 缺失或路径字段为空时回调返回 null，YooAsset 自动回退兜底 |
| `InjectActiveSettingsOnEditorLoad` | Editor 启动 / 域重载 | 通过 `EditorApplication.delayCall` 启动期按当前激活 master `Inject` 一次，不依赖 ConfigWindow 是否打开。解决 Editor 启动后从未打开 ConfigWindow 即触发构建/查询 YooAssetSettings 的流程，避免 `s_settings` 仍为 null → 走 `Resources.Load` 全工程兜底命中错副本 |
| `EditorUtil.SceneRoute` | `EditorSceneManager.sceneOpened` 的 `OpenSceneMode.Single` | 解析 active ConfigMaster 一次，依次输出 Scene / ConfigMaster / YooAssetSettings / PipifySettings 四条摘要，并调用 `YooAssetInjector.Inject`。Additive 打开不切换路由；相同场景重开仅重新确认和注入，不会为了日志额外写入 `Globals.json` |
| `YooAssetRuntimeSettingsStaging.InjectForEditorPlayMode` | `BeforeSceneLoad` | YooAsset 在 `SubsystemRegistration` 清空静态 Settings 后，重新按当前 ConfigMaster 的 Platform / Channel / DevelopMode 解析路径并调用 `InjectByPath`；不创建 Resources 副本 |

---

## §11 使用示例

```csharp
// 业务侧通常无需手动调用 Inject——SceneRoute 已在 Single 场景切换时自动触发，启动期也会延迟注入一次
// 仅在「字段编辑后立即生效」等显式刷新场景下需要手动调用
ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
EditorUtil.Config.YooAssetInjector.Inject(master);

// Pipify 流水线 export.config Step 结束后，确保 YooAsset 使用正确的收集器配置
BundleCollectorSetting collector = EditorUtil.Config.YooAssetInjector.LoadBundleCollector(master);
if (collector == null)
{
    Debug.LogWarning("[Pipify] BundleCollectorSetting 未配置，跳过 YooAsset 相关 Step。");
    return;
}
```

---

## §13 关联文档

- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)（字段来源：`YooAssetSettingsPath` / `BundleCollectorSettingPath`）
- [EditorUtil.Config.WorkspaceActive.md](EditorUtil.Config.WorkspaceActive.md)（调用前获取 master 的入口）
- [ConfigWindow.md](../../Windows/ConfigWindow.md)（窗口打开或显式绑定时调用 `Inject`；场景切换注入由 `SceneRoute` 统一完成）
