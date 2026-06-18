# EditorUtil.Config.RuntimeProvider

**类签名**：`public static class RuntimeProvider`（嵌套于 `EditorUtil.Config` 的 partial）
**命名空间**：`NovaFramework.Editor`

按需从激活 ConfigMasterSO 配对关系中读取 ConfigRuntimeSO 的 Editor 端访问器。通过 `WorkspaceActive` 锚点定位激活 master，再按 ADR-033 布局约定（`DemoRoot/Configs/ConfigRuntime.asset`）加载配对 SO，根除多 sample 共存时 `FindAssets` 玄学命中问题（见 ADR-047）。

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
// 经 WorkspaceActive 锚定激活 master，按 ADR-033 布局约定（DemoRoot/Configs/ConfigRuntime.asset）定位配对 SO；
// 无激活 master 或 ConfigRuntime 未导出时返回 null。
public static ConfigRuntimeSO GetCurrent();

// 获取当前运行时 Namespace。
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
      ③ AssetDatabase.GetAssetPath(master) → masterPath
      ④ 上溯两级：GetDirectoryName(GetDirectoryName(masterPath)) → demoRoot
      ⑤ demoRoot 为空 → Warning（master 路径层级不足，无法上溯至 DemoRoot）+ return null
      ⑥ runtimePath = "{demoRoot}/Configs/ConfigRuntime.asset"
      ⑦ LoadAssetAtPath<ConfigRuntimeSO>(runtimePath) → runtime
      ⑧ runtime == null → Warning（ConfigRuntime 未导出）+ return null
      ⑨ return runtime
```

---

## §10 常见误区

| 误区 | 正确理解 |
|------|---------|
| 认为 GetCurrent 仍走 FindAssets 三维匹配 | 已替换为 WorkspaceActive 锚点转发；FindAssets 逻辑已完全删除（ADR-047 红线） |
| 替代 ConfigLookup 读取 Namespace | 本类是 ConfigLookup 的继任者，优先通过 `GetNamespace()` 读取；旧的 `ConfigLookup` 已删除 |
| 找不到资产时返回 null 未处理 | `GetNamespace()` 找不到时返回 `string.Empty`；`GetCurrent()` 找不到时返回 null，调用方须判空 |
| 多 sample 共存时会选错 ConfigRuntimeSO | WorkspaceActive 锚点保证始终定位到激活 master 配对的 ConfigRuntime，不再有字典序歧义 |

---

## §11 使用示例

```csharp
// Editor 端读取当前 Namespace（供 Luban 导出流水线使用）
string ns = EditorUtil.Config.RuntimeProvider.GetNamespace();
if (string.IsNullOrEmpty(ns))
{
    Debug.LogWarning("ConfigRuntimeSO 未找到，Namespace 为空。");
    return;
}

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
- `GetCurrent()` 依赖 WorkspaceActive 锚点；若 Globals.json 未写入（首次安装或 Globals.json 被删），且当前无 Sample Scene 打开，则返回 null

---

## §13 关联文档

- [EditorUtil.Config.WorkspaceActive.md](EditorUtil.Config.WorkspaceActive.md)（`GetActiveRuntime()` 的实现所在）
- [ConfigRuntimeSO.md](../../../Runtime/Modules/Config/ConfigRuntimeSO.md)（被读取的 SO 类型）
- [EditorUtil.Luban.Pipeline.md](../EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)（主要消费方）
