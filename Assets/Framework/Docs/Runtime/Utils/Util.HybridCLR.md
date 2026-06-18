# Util.HybridCLR

**类签名**：`public static class Util.HybridCLR`（嵌套于 `public static partial class Util`）
**命名空间**：`NovaFramework.Runtime`

HybridCLR 生态唯一 Facade。封装 AOT 元数据补充加载（`LoadAotMetadataAsync`）与业务 DLL 注入（`LoadGameAssemblyAsync`），底层字节通过 `AssetComponent.LoadAssetAsync<TextAsset>` 加载，不走 File IO。Editor 下所有方法均为 no-op。

---

## §2 文件表

| 文件 | 说明 |
|---|---|
| `Runtime/Core/Util/Util.HybridCLR/Util.HybridCLR.cs` | 全部实现：`LoadAotMetadataAsync` / `LoadGameAssemblyAsync` / `LoadDllBytesAsync`（private） + 两个幂等守卫 `HashSet` |

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `s_LoadedAOTMetadata` | `HashSet<string>` | 空集合（Ordinal 比较） | 已加载 AOT 元数据程序集名称守卫，防止重复调用 `RuntimeApi.LoadMetadataForAOTAssembly` |
| `s_LoadedGameAssemblies` | `HashSet<string>` | 空集合（Ordinal 比较） | 已加载业务程序集名称守卫，防止重复 `Assembly.Load` |

---

## §5 完整公开 API

```csharp
/// 从 Asset 异步加载 AOT 元数据 DLL 字节，
/// 并调用 HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly 补充 AOT 泛型元数据。
/// Editor 下为 no-op（输出 Debug 日志直接返回）。
/// 幂等：同一 location 第二次调用直接返回。
/// location:   Asset 地址，对应 AssetComponent.LoadAssetAsync 的 location 参数（与 HybridCLR assemblyName 等价）
/// mode:       同源镜像模式，默认 SuperSet（推荐值）
public static async UniTask LoadAotMetadataAsync(
    string location,
    global::HybridCLR.HomologousImageMode mode = global::HybridCLR.HomologousImageMode.SuperSet);

/// 从 Asset 异步加载业务 DLL 字节，并通过 System.Reflection.Assembly.Load 注入 AppDomain。
/// Editor 下为 no-op，直接返回 AppDomain 中已有的源码编译产物。
/// 幂等：同一 location 第二次调用直接返回已加载的 Assembly，不重复 Load。
/// 加载成功后自动调用 Util.Assembly.RefreshAssemblies 刷新反射视图。
/// location:   Asset 地址，对应 AssetComponent.LoadAssetAsync 的 location 参数（与程序集名等价）
/// 返回：加载成功的 System.Reflection.Assembly；Editor 下返回已存在的编译产物
public static async UniTask<System.Reflection.Assembly> LoadGameAssemblyAsync(
    string location);
```

---

## §9 关键算法

### LoadAotMetadataAsync 流程

```
#if UNITY_EDITOR
  → Log.Debug no-op，直接 return

#else（IL2CPP 运行时）
  ├─ s_LoadedAOTMetadata.Add(location) 失败（已存在）→ 直接 return（幂等）
  ├─ bytes = await LoadDllBytesAsync(location)
  │    ← FrameworkManagersGroup.GetManager<IAssetManager>()
  │    ← assetManager.LoadRawAsync(location)
  ├─ result = HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(bytes, mode)
  ├─ result != OK → 抛 InvalidOperationException（含 location 和错误码）
  └─ Log.Debug 记录成功
```

### LoadGameAssemblyAsync 流程

```
#if UNITY_EDITOR
  → Log.Debug no-op，返回 Util.Assembly.GetAssembly(location)

#else（IL2CPP 运行时）
  ├─ s_LoadedGameAssemblies.Add(location) 失败（已存在）→ return 已有 Assembly（幂等）
  ├─ bytes = await LoadDllBytesAsync(location)
  ├─ asm = System.Reflection.Assembly.Load(bytes)
  ├─ Util.Assembly.RefreshAssemblies()
  └─ return asm
```

**HomologousImageMode.SuperSet 选择理由**：SuperSet 模式允许 AOT 程序集中的泛型实例化为热更代码中实际使用的超集，兼容性最佳，是 HybridCLR 官方推荐的默认值。

---

## §10 常见误区

| 误区 | 说明 |
|---|---|
| 在 Editor 中调用有实际效果 | `#if UNITY_EDITOR` 分支 no-op；AOT metadata 补充只在 IL2CPP 构建（Android/Standalone）下生效 |
| 重复调用 | 两个守卫 HashSet 保证幂等；同名程序集多次调用安全 |
| 底层直接走 File IO | 已切换为 `IAssetManager.LoadRawAsync(location)`，DLL 必须打入 AB 包并以 `.bytes` 扩展名存入 |
| 错误码非 OK 时不处理 | `LoadAotMetadataAsync` 已在非 OK 时抛 `InvalidOperationException`，调用方无需额外检查返回值 |
| `AssetComponent` 为 null | `FrameworkComponentsGroup.GetComponent<AssetComponent>()` 返回 null 时方法记录 Error 并返回 null 字节，调用方会收到 `InvalidOperationException` |

---

## §11 使用示例

```csharp
// 由 ProcedureLoadDll 内部循环调用，业务层不直接使用

// AOT metadata 加载（顺序：全部 AOT metadata 完成后再加载业务 DLL）
foreach (DllAssetEntry entry in settings.AotMetadataDlls)
{
    await Util.HybridCLR.LoadAotMetadataAsync(entry.AssetLocation);
}

// 业务 DLL 加载
foreach (DllAssetEntry entry in settings.GameDlls)
{
    await Util.HybridCLR.LoadGameAssemblyAsync(entry.AssetLocation);
}
```

---

## §13 关联文档

- [Util.Assembly.md](Util.Assembly.md)（RefreshAssemblies / GetAssembly）
- [ProcedureLoadDll.md](../Modules/Procedure/Procedures/ProcedureLoadDll.md)（主要调用方）
- [AssetComponent.md](../Modules/Asset/AssetComponent.md)（DLL 字节加载底层）
