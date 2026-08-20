# Util.HybridCLR

**类签名**：`public static class Util.HybridCLR`（嵌套于 `public static partial class Util`）
**命名空间**：`NovaFramework.Runtime`

HybridCLR 生态唯一 Facade。封装 AOT 元数据补充加载（`LoadAotMetadataAsync`）与业务 DLL 注入（`LoadGameAssemblyAsync`），底层字节通过 `AssetComponent.LoadAssetAsync<TextAsset>` 加载，不走 File IO。Editor 下所有方法均为 no-op。

---

## §2 文件表

| 文件 | 说明 |
|---|---|
| `Runtime/Core/Util/Util.HybridCLR/Util.HybridCLR.cs` | 全部实现：AOT / 业务 DLL 加载、完成态缓存与同程序集并发共享 |

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|---|---|---|---|
| `s_LoadedAOTMetadata` | `HashSet<string>` | 空集合 | 已完成加载的 AOT 元数据程序集身份 |
| `s_LoadingAOTMetadata` | `Dictionary<string, UniTaskCompletionSource>` | 空字典 | 同程序集并发加载共享完成结果 |
| `s_LoadedGameAssemblies` | `Dictionary<string, Assembly>` | 空字典 | 已完成加载的业务程序集缓存 |
| `s_LoadingGameAssemblies` | `Dictionary<string, UniTaskCompletionSource<Assembly>>` | 空字典 | 同程序集并发加载共享 Assembly 或异常 |

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
  ├─ 已完成 → 直接 return
  ├─ 正在加载 → await 同一 UniTaskCompletionSource
  ├─ bytes = await LoadDllBytesAsync(location)
  │    ← FrameworkManagersGroup.GetManager<IAssetManager>()
  │    ← assetManager.LoadAsync<TextAsset>(location)
  ├─ result = HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(bytes, mode)
  ├─ result != OK → 抛 InvalidOperationException（含 location 和错误码）
  ├─ 成功 → 写完成态并唤醒全部等待者
  └─ 失败 → 向等待者传播异常并移除进行中状态，允许重试
```

### LoadGameAssemblyAsync 流程

```
#if UNITY_EDITOR
  → Log.Debug no-op，返回 Util.Assembly.GetAssembly(location)

#else（IL2CPP 运行时）
  ├─ 已完成 → return 缓存 Assembly
  ├─ 正在加载 → await 同一 UniTaskCompletionSource<Assembly>
  ├─ bytes = await LoadDllBytesAsync(location)
  ├─ asm = System.Reflection.Assembly.Load(bytes)
  ├─ Util.Assembly.RefreshAssemblies()
  ├─ 成功 → 缓存并向全部等待者返回同一 Assembly
  └─ 失败 → 传播异常并移除进行中状态，允许重试
```

**HomologousImageMode.SuperSet 选择理由**：SuperSet 模式允许 AOT 程序集中的泛型实例化为热更代码中实际使用的超集，兼容性最佳，是 HybridCLR 官方推荐的默认值。

---

## §10 常见误区

| 误区 | 说明 |
|---|---|
| 在 Editor 中调用有实际效果 | `#if UNITY_EDITOR` 分支 no-op；AOT metadata 补充只在 IL2CPP 构建（Android/Standalone）下生效 |
| 重复或并发调用 | `.dll` 后缀会被归一化；同程序集并发调用等待同一加载结果，不会提前返回 null |
| 底层直接走 File IO | 已切换为 `IAssetManager.LoadAsync<TextAsset>(location)`，DLL 必须作为 TextAsset 打入普通 AssetBundle 并以 `.bytes` 扩展名存入 |
| 错误码非 OK 时不处理 | `LoadAotMetadataAsync` 已在非 OK 时抛 `InvalidOperationException`，调用方无需额外检查返回值 |
| `AssetComponent` 为 null | `FrameworkComponentsGroup.GetComponent<AssetComponent>()` 返回 null 时方法记录 Error 并返回 null 字节，调用方会收到 `InvalidOperationException` |

---

## §11 使用示例

```csharp
// 启动列表由 ProcedureLoadDll 调用；运行时按需 DLL 也必须走同一 Facade

// AOT metadata 加载（顺序：全部 AOT metadata 完成后再加载业务 DLL）
foreach (DllAssetEntry entry in settings.AotMetadataDlls)
{
    await Util.HybridCLR.LoadAotMetadataAsync(entry.AssetLocation);
}

// 业务 DLL 加载
foreach (DllAssetEntry entry in settings.StartupGameDlls)
{
    await Util.HybridCLR.LoadGameAssemblyAsync(entry.AssetLocation);
}
```

---

## §13 关联文档

- [Util.Assembly.md](Util.Assembly.md)（RefreshAssemblies / GetAssembly）
- [ProcedureLoadDll.md](../Modules/Procedure/Procedures/ProcedureLoadDll.md)（主要调用方）
- [AssetComponent.md](../Modules/Asset/AssetComponent.md)（DLL 字节加载底层）
