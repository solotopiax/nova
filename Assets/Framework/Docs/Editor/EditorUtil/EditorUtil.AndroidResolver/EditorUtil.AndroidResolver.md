# EditorUtil.AndroidResolver

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public static partial class EditorUtil.AndroidResolver` |
| 命名空间 | `NovaFramework.Editor` |
| 全局访问 | `EditorUtil.AndroidResolver` |
| 功能描述 | 通过反射调用 EDM4U PlayServicesResolver.ResolveSync，强制重建 Android 依赖目录 |

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.AndroidResolver.cs` | `partial EditorUtil.AndroidResolver` | 公有接口：`Resolve()` |
| `EditorUtil.AndroidResolver.Methods.cs` | `partial EditorUtil.AndroidResolver` | 私有方法：`ExecuteResolveSync`、`FindResolverType` |

---

## §5 完整公开 API

```csharp
/// 触发一次同步 Android 依赖解析（等价于 EDM4U 菜单 Assets → External Dependency Manager → Android → Force Resolve）。
/// 内部通过反射调用 GooglePlayServices.PlayServicesResolver.ResolveSync(true)，强制重建 Assets/GeneratedLocalRepo/**。
/// 找不到 EDM4U 类型/方法或反射调用抛异常时直接抛 InvalidOperationException，由调用方（Pipify Runner）
/// 捕获并标记 Step 失败；禁止静默跳过，避免下游 BuildPlayer 在 GeneratedLocalRepo 缺失时才崩。
public static void Resolve()
```

---

## §10 常见误区

| 误区 | 真相 |
|---|---|
| EDM4U `ResolveSync` 是双参数 `(bool, bool)` | 实际签名是 `ResolveSync(bool forceResolution)` 单参数；反射拼错签名会让 `GetMethod` 返回 null |
| 找不到 EDM4U 类型/方法时 Warning 跳过即可 | 跳过后 `Assets/GeneratedLocalRepo/**` 不会被重建，Step 3 BuildPlayer 阶段才被动触发 EDM4U，但目标目录缺失会报 `DirectoryNotFoundException` 并把 HybridCLR `GenerateStripedAOTDlls` 拖崩；当前实现一律抛异常，让 Pipify Runner 在 Step 1 就显式失败 |

---

## §11 使用示例

```csharp
// 在 HybridCLR 编译之前执行，确保 GeneratedLocalRepo 目录存在
EditorUtil.AndroidResolver.Resolve();
EditorUtil.HybridCLR.CompileDllActiveBuildTarget();

// 或通过 Pipify Batch（推荐）：
// PipifyWindow 中将 Step "edm4u.android_resolve" 拖到 "hybridclr.generate_all" 之前
```

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md) — EditorUtil 工具集概览
- [EditorUtil.HybridCLR.md](../EditorUtil.HybridCLR/EditorUtil.HybridCLR.md) — HybridCLR 原子操作合集
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md) — 全 Step 清单
