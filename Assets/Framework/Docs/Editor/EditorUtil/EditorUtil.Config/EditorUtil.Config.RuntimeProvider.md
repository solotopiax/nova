# EditorUtil.Config.RuntimeProvider

**类签名**：`public static class RuntimeProvider`（嵌套于 `EditorUtil.Config` 的 partial）
**命名空间**：`NovaFramework.Editor`

按需从激活 ConfigMasterSO 配对关系中读取 ConfigRuntimeSO 的 Editor 端访问器。通过 `WorkspaceActive` 锚点定位激活 master，首选激活 master 的 ExportTarget 序列化引用（资产可置于任意位置），ExportTarget 为 null 时回退 ADR-033 布局约定兜底，根除多 sample 共存时 `FindAssets` 玄学命中问题（见 ADR-047）。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Config/EditorUtil.Config.RuntimeProvider.cs` | `EditorUtil.Config.RuntimeProvider` | 访问器实现 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Config (public static partial class)
        └── RuntimeProvider (public static class)
```

---

## §4 关键字段表

无字段（静态工具类，无状态）。

---

## §5 完整公开 API

```csharp
// 获取当前激活 ConfigMaster 配对的 ConfigRuntimeSO。
// 经 WorkspaceActive 锚定激活 master，首选 master.ExportTarget 序列化引用（资产可置于任意位置）；
// ExportTarget 为 null 时回退 ADR-033 布局约定（DemoRoot/Configs/ConfigRuntime.asset）兜底加载。
// 无激活 master 或定位失败时返回 null。
public static ConfigRuntimeSO GetCurrent();

// 获取激活 ConfigMasterSO 的顶层 Namespace（供当前 Luban 生成使用）。
// 直接读取激活 ConfigMasterSO 的 Namespace 源头真相（见 ADR-005 单一写入路径），
// 经 WorkspaceActive.Get() 锚点定位激活 master（见 ADR-047）；找不到激活 master 时返回 string.Empty。
public static string GetNamespace();
```

---

## §9 关键算法

### GetCurrent — WorkspaceActive 锚点转发

```
GetCurrent():
  → WorkspaceActive.GetActiveRuntime()
      ① Get() 获取激活 ConfigMasterSO
      ② 无激活 master → Warning + return null
      ③ master.ExportTarget 非空 → 直接返回（GUID 追踪，资产可置于任意位置）
      ④ ExportTarget 为 null → 回退 ADR-033 布局约定兜底：
         AssetDatabase.GetAssetPath(master) → masterPath
         上溯两级拼 {demoRoot}/Configs/ConfigRuntime.asset 加载
         masterPath 为空 → Warning + return null
         上溯层级不足 → Warning + return null
         ConfigRuntime.asset 不存在（未导出）→ Warning + return null
      ⑤ return runtime
```

### GetNamespace — 激活 Master 的顶层源值

`GetNamespace()` 调用 `WorkspaceActive.Get()`，按 `ProjectSettings/Nova/Globals.json` 的 `configMasterGuid` 定位激活 `ConfigMasterSO`；无可加载 Master 时返回 `string.Empty`，有 Master 时返回 `master.Namespace ?? string.Empty`。它不读取 `GetCurrent()` 返回的 `ConfigRuntimeSO.Namespace`，不调用 `DimensionalResolver.ResolveNamespace`，因此不应用 Namespace 的维度覆盖。后者属于 Config 导出到 Runtime 的另一条解析路径。

空返回值可能是未绑定 Master，也可能是已绑定 Master 明确配置的空 Namespace，两者不能混同。需要冻结代码生成来源时，复用内部只读 `WorkspaceActive.TryGetPersistedConfigMaster(...)` 核对 Globals GUID / pathHint / 实际资产，再读取顶层 Namespace；此入口不修复或写入，`WorkspaceActive.Get()` 则可能修复 pathHint。读取本身不按场景选择 Master，场景工作区协调属于独立的 `WorkspaceActive.ReconcileScene(...)` 路径。

---

## §10 常见误区

| 误区 | 正确理解 |
|------|---------|
| 认为 GetCurrent 仍走 FindAssets 三维匹配 | 已替换为 WorkspaceActive 锚点转发；FindAssets 逻辑已完全删除（ADR-047 红线） |
| 替代 ConfigLookup 读取 Namespace | 本类是 ConfigLookup 的继任者，优先通过 `GetNamespace()` 读取；旧的 `ConfigLookup` 已删除 |
| 找不到资产时返回 null 未处理 | `GetNamespace()` 找不到时返回 `string.Empty`；`GetCurrent()` 找不到时返回 null，调用方须判空 |
| 多 sample 共存时会选错 ConfigRuntimeSO | WorkspaceActive 锚点保证始终定位到激活 master 配对的 ConfigRuntime，不再有字典序歧义 |
| 认为必须把 ConfigRuntime.asset 放在 DemoRoot/Configs/ 下 | 已不再强制，ExportTarget 记录的位置即权威，布局约定仅作未配置时的兜底 |

---

## §11 使用示例

```csharp
// Editor 端读取当前 Namespace（供 Luban 导出流水线使用）
string ns = EditorUtil.Config.RuntimeProvider.GetNamespace();
// 空字符串不能证明 ConfigRuntimeSO 或 Master 缺失：
// 受控代码导出需另外核对已持久化 Master 身份；已绑定且明确空值可使用 global namespace。

// 构建期获取激活 master 配对的 ConfigRuntimeSO（如 NovaSDKBuildProcessor）
ConfigRuntimeSO runtimeSO = EditorUtil.Config.RuntimeProvider.GetCurrent();
if (runtimeSO == null)
{
    // 无激活 master 或 ConfigRuntime 未导出时返回 null，调用方须判空
    return;
}
```

---

## §12 注意事项

- 仅在 Editor 运行时可用（依赖 `UnityEditor.AssetDatabase`），不可在 Runtime 代码中引用
- `public` 可见性：框架 Editor 层内外均可调用，但依赖 `UnityEditor` 故只能在 Editor 上下文使用
- `GetCurrent()` 依赖 Globals 中可加载的激活 Master 及其配对 Runtime；Globals 未绑定或配对 Runtime 不存在时返回 null。`Get()` / `GetCurrent()` 本次读取不会依据打开的 Sample Scene 自动补绑定；场景路由协调是另一条流程。

---

## §13 关联文档

- [EditorUtil.Config.WorkspaceActive.md](EditorUtil.Config.WorkspaceActive.md)（`GetActiveRuntime()` 的实现所在）
- [ConfigRuntimeSO.md](../../../Runtime/Modules/Config/ConfigRuntimeSO.md)（被读取的 SO 类型）
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)（主要消费方）
