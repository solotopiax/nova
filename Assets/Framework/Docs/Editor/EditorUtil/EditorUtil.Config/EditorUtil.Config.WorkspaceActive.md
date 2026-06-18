# EditorUtil.Config.WorkspaceActive

**类签名**：`public static class WorkspaceActive`（`EditorUtil.Config` 的嵌套 partial）
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Config.WorkspaceActive`

工程级激活 ConfigMaster 锚点；通过 `ProjectSettings/Nova/Globals.json` 持久化当前激活 ConfigMasterSO 的 GUID，根除多 Sample 共存时 `FindAssets` 玄学命中问题。

> **第③段路径推断已升级为"逐级向上递归"**：从活跃 scene 所在目录起每层尝试 `Editor/ConfigMaster.asset`，第一个命中即返回，到 `Assets/Samples/` 边界停。一套逻辑同时覆盖：
> - **开发态扁平结构** `Assets/Samples/{Demo}/{Scene}.unity`
> - **UPM 导入态嵌套结构** `Assets/Samples/{PackageDisplayName}/{Version}/{SampleDisplayName}/{Scene}.unity`

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.WorkspaceActive.cs` | `EditorUtil.Config.WorkspaceActive` | 全部逻辑：Get / Set / TryInferFromOpenedSampleScene / WriteGlobals / GetProjectRoot + 内嵌 `GlobalsJson` 序列化模型 |

---

## §5 完整公开 API

```csharp
// 获取当前激活的 ConfigMasterSO；按五段回退策略返回，失败返回 null
// ① 当前活跃 Scene 在 Assets/Samples/X/ 下，且与 Globals.json 缓存的 ConfigMaster 不属同一 sample 根
//    → 强制按 scene 重新推断并覆盖 Globals.json（避免多 sample 切换串味）
// ② Globals.json 存在 + GUID 加载成功 → 返回（pathHint 变化时自动回写）
// ③ Globals.json 存在 + GUID 加载失败（资产被删）→ 返回 null
// ④ Globals.json 不存在 + 当前活跃 Scene 在 Assets/Samples/ 下 → 从 scene 所在目录起向上递归找 Editor/ConfigMaster.asset 并写入返回
// ⑤ Globals.json 不存在 + 不在 Sample Scene → 返回 null
public static ConfigMasterSO Get();

// 获取当前激活 ConfigMaster 所配对的 ConfigRuntimeSO。
// 通过 Get() 锚定激活 master，再按 ADR-033 布局约定（DemoRoot/Configs/ConfigRuntime.asset）加载配对 SO。
// 三种 null 成因（Warning 文案可区分）：
//   ① 无激活 master（Get() 返回 null）
//   ② masterPath 为空（AssetDatabase.GetAssetPath 返回空字符串）或 demoRoot 层级不足（路径上溯两级后为空）
//   ③ ConfigRuntime 未导出（文件不存在于 DemoRoot/Configs/ConfigRuntime.asset）
public static ConfigRuntimeSO GetActiveRuntime();

// 显式设置激活 ConfigMasterSO；以 GUID + pathHint 原子写入 Globals.json
// master 为 null 时静默返回
public static void Set(ConfigMasterSO master);
```

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
- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md)
- [EditorUtil.Config.YooAssetInjector.md](EditorUtil.Config.YooAssetInjector.md)（在 WorkspaceActive.Get 成功后由 ConfigWindow 调用）
- [ConfigWindow.md](../../Windows/ConfigWindow.md)（主要消费方：启动期 Get / 切换 Namespace 时 Set）
