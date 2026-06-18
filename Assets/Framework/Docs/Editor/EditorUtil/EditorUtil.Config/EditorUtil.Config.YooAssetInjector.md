# EditorUtil.Config.YooAssetInjector

**类签名**：`public static class YooAssetInjector`（`EditorUtil.Config` 的嵌套 partial）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Config.YooAssetInjector`

Asset 模块编辑期注入层；按 ConfigMasterSO 中显式声明的路径字段注入 `YooAssetSettings` 与加载 `BundleCollectorSetting`，替代 `Resources.Load` / `AssetDatabase.FindAssets` 全工程扫描，根除多 Sample 共存时命中错副本问题。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.YooAssetInjector.cs` | `EditorUtil.Config.YooAssetInjector` | 全部逻辑：`Inject` / `LoadBundleCollector` |

---

## §5 完整公开 API

```csharp
// 按 ConfigMasterSO.YooAssetSettingsPath 注入 YooAssetSettings 到 YooAssetConfiguration 静态全局
// 进入即调用 BundleCollectorSettingData.ResetCache() 作废 YooAsset 内部静态缓存
// master 为 null 或 YooAssetSettingsPath 为空时静默返回
// 路径对应资产不存在时记 Log.Warning 并静默返回
public static void Inject(ConfigMasterSO master);

// 按 ConfigMasterSO.BundleCollectorSettingPath 加载 BundleCollectorSetting
// master 为 null 或 BundleCollectorSettingPath 为空时返回 null
// 路径对应资产不存在时返回 null
// <returns>BundleCollectorSetting 实例；未配置或不存在时返回 null</returns>
public static BundleCollectorSetting LoadBundleCollector(ConfigMasterSO master);
```

### 自动钩子（[InitializeOnLoadMethod]，无需业务调用）

| 钩子 | 时机 | 行为 |
|------|------|------|
| `RegisterYooAssetExplicitPathProvider` | Editor 启动 / 域重载 | 向 `SettingLoader.RegisterExplicitPathProvider` 注册回调，按当前激活 master 的 `BundleCollectorSettingPath` 解析 `BundleCollectorSetting`，替代 YooAsset 内置 `AssetDatabase.FindAssets` 全工程扫描；激活 master 缺失或路径字段为空时回调返回 null，YooAsset 自动回退兜底 |
| `HookSceneOpenedAutoInject` | Editor 启动 / 域重载 | 域级常驻订阅 `EditorSceneManager.sceneOpened`（仅响应 `OpenSceneMode.Single`）；同时 `EditorApplication.delayCall` 启动期立即按当前激活 master `Inject` 一次，不依赖 ConfigWindow 是否打开。解决 Editor 启动后从未打开 ConfigWindow 即触发构建/查询 YooAssetSettings 的流程，避免 `s_settings` 仍为 null → 走 `Resources.Load` 全工程兜底命中错副本 |

---

## §11 使用示例

```csharp
// 业务侧通常无需手动调用 Inject——HookSceneOpenedAutoInject 已在域重载/场景切换时自动触发
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

- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)（字段来源：`YooAssetSettingsPath` / `BundleCollectorSettingPath`）
- [EditorUtil.Config.WorkspaceActive.md](EditorUtil.Config.WorkspaceActive.md)（调用前获取 master 的入口）
- [ConfigWindow.md](../../Windows/ConfigWindow.md)（主要调用方：OnSceneOpenedRefresh 阶段调用 `Inject`）
