# EditorUtil.Config.WorkspaceActive

**类签名**：`public static class WorkspaceActive`（`EditorUtil.Config` 的嵌套 partial）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Config.WorkspaceActive` / `EditorUtil.Config.Events`

工程级活动工作区锚点；通过 `ProjectSettings/Nova/Globals.json` 同时持久化当前 `ConfigMasterSO`、`PipifySettingsSO`，以及进入 Sample 前的业务绑定。

进入 Sample 时会把 Sample 的配置对写成当前值，保证编辑器构建、Build Action 和 CI 看到同一套配置；返回非 Sample Scene 时，再从 `project*` 备份恢复业务配置。切换不是“取工程第一份资产”，无法唯一判断时保持空值并让后续入口明确失败。

> **Sample 路径推断使用"逐级向上递归"**：从活跃 Scene 所在目录起每层尝试 `Editor/ConfigMaster.asset`，第一个命中即返回，到 `Assets/Samples/` 边界停。一套逻辑同时覆盖：
> - **开发态扁平结构** `Assets/Samples/{Demo}/{Scene}.unity`
> - **UPM 导入态嵌套结构** `Assets/Samples/{PackageDisplayName}/{Version}/{SampleDisplayName}/{Scene}.unity`

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.cs` | `EditorUtil.Config.WorkspaceActive` / `EditorUtil.Config.Events` | active ConfigMaster 锚点逻辑；同文件还承载 Config 编辑侧保存事件 |

---

## §5 完整公开 API

```csharp
// 只读取 Globals 当前 ConfigMaster；失败返回 null，不根据 Scene 临时猜测
public static ConfigMasterSO Get();

// Single Scene 打开后协调完整工作区：
// 业务 → Sample：备份业务 pair，写入 Sample pair 与 activeSampleRoot
// Sample A → B：只替换当前 pair，不覆盖业务备份
// Sample → 业务：恢复业务 pair 并清空 activeSampleRoot
internal static bool ReconcileScene(string scenePath);

// 读取/显式设置当前 PipifySettings；手动绑定会同步写入 Globals
internal static PipifySettingsSO GetPipifySettings();
internal static bool SetPipifySettings(PipifySettingsSO settings);

// CLI/CI 成对设置 ConfigMaster + PipifySettings，一次原子写入
internal static bool TrySetExplicitWorkspace(
    ConfigMasterSO master,
    PipifySettingsSO settings,
    out string error);

// 获取当前激活 ConfigMaster 所配对的 ConfigRuntimeSO。
// 通过 Get() 锚定激活 master，首选 master.ExportTarget 序列化引用（导出时记录，GUID 追踪，资产可置于任意位置，不强制布局）；
// ExportTarget 为 null 时回退 ADR-033 布局约定（DemoRoot/Configs/ConfigRuntime.asset）兜底加载。
// 四种 null 成因（Warning 文案可区分）：
//   ① 无激活 master（Get() 返回 null）
//   ② ExportTarget 为 null 且 masterPath 为空（AssetDatabase.GetAssetPath 返回空字符串）
//   ③ ExportTarget 为 null 且路径上溯层级不足（上溯两级后为空）
//   ④ ExportTarget 为 null 且布局约定下 ConfigRuntime.asset 不存在（未导出）
public static ConfigRuntimeSO GetActiveRuntime();

// 显式设置激活 ConfigMasterSO；非 Sample 资产会同时更新业务备份
// master 为 null 时静默返回
public static void Set(ConfigMasterSO master);
```

`Globals.json` v2 保留旧字段作为当前值，并新增：

- `pipifySettingsGuid` / `pipifySettingsPathHint`
- `projectConfigMasterGuid` / `projectConfigMasterPathHint`
- `projectPipifySettingsGuid` / `projectPipifySettingsPathHint`
- `activeSampleRoot`

旧版只有 ConfigMaster 两字段时仍可读取。若旧值指向 Sample，只在工程内恰有一个非 Sample 候选时迁移；多个候选不会按 GUID 顺序猜测。
高于当前支持版本的 `schemaVersion` 会拒绝读取和改写，避免旧框架覆盖未来字段。

### Events

```csharp
// 当前激活 ConfigMaster 保存成功后触发；供 Inspector 等编辑器界面刷新派生视图
public static event Action<ConfigMasterSO> ActiveConfigMasterSaved;

// ConfigWindow 保存真实 ConfigMasterSO 后调用，内部广播 ActiveConfigMasterSaved
internal static void NotifyActiveConfigMasterSaved(ConfigMasterSO master);
```

该事件只表达“真实 ConfigMasterSO 已保存成功”，不承载 WorkingCopy 的未保存状态。SDKComponent Inspector 使用它在 ConfigWindow 保存后刷新 active `EnabledSDKs` 映射出的可见 Plugin 列表。
---

## §11 使用示例

```csharp
// ConfigWindow OnEnable 时加载激活的 ConfigMaster
ConfigMasterSO master = EditorUtil.Config.WorkspaceActive.Get();
if (master == null)
{
    Log.Warning(LogTag.Editor, "[ConfigWindow] 未找到激活的 ConfigMaster，请创建或选择一个。");
    return;
}

// 用户在 ConfigWindow TopBar 切换 ConfigMaster 后显式写入
EditorUtil.Config.WorkspaceActive.Set(selectedMaster);
```

---

## §13 关联文档

- [EditorUtil.Config.RuntimeProvider.md](EditorUtil.Config.RuntimeProvider.md)（GetCurrent() 的唯一调用方，委托至 GetActiveRuntime()）
- [ConfigMasterSO.md](../../../Editor/Config/ConfigMasterSO.md)
- [EditorUtil.Config.YooAssetInjector.md](EditorUtil.Config.YooAssetInjector.md)（在 WorkspaceActive.Get 成功后由 ConfigWindow 调用）
- [ConfigWindow.md](../../Windows/ConfigWindow.md)（主要消费方：启动期 Get / 切换 Namespace 时 Set）
