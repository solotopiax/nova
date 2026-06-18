---
id: ADR-042
title: AssetManager 移除裸 Load API，所有 LoadXxx 返回 Handle 接口，调用方持有并负责释放
status: accepted
date: 2026-05-26
summary: LoadXxx 全返 Handle，调用方持有并 Release
category: asset
aliases:
  - ADR-042-assetmanager-load-api-all-return-handle
keywords:
  - ADR-042
  - ADR-042-assetmanager-load-api-all-return-handle
  - AssetManager 移除裸 Load API
  - Handle 接口
  - IAssetHandle
  - ISubAssetsHandle
  - IAllAssetsHandle
  - IRawFileHandle
  - ISceneHandle
tags:
  - adr
  - nova
  - asset
  - resource-management
  - handle
supersedes:
  - "[[ADR-019-yooasset-release-mandatory|ADR-019]]"
superseded-by: []
related:
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
  - "[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]"
---

# ADR-042：AssetManager 移除裸 Load API，所有 LoadXxx 返回 Handle 接口

## 背景（Context）

ADR-019 记录了 Nova 最初的资源加载模式：`IAssetManager.LoadAsync<T>(location)` 返回裸 T，YooAsset handle 由 `AssetManager` 内部以 `Dictionary<string, IAssetHandle>` 记账；调用方拿到的是裸资产对象，通过 `Release(location)` 字符串反查 handle 并释放引用计数。

这套设计存在两个根本性缺陷：

1. **引用计数易泄漏**：`Release(location)` 要求调用方记住 location 字符串，同一 location 被多处 Load 时需要多次 Release，漏掉任何一次则引用计数永不归零，资产永不卸载。ADR-019 专门为此立规，但无法从语言层面强制执行——`releaseFunc = _ => {}` 的空实现散布在多个模块。
2. **Handle 所有权不清晰**：`Dictionary<string, IAssetHandle>` 只按 location 记账，多个调用方 Load 同一 location 时共用一个 handle，Release 语义模糊（最后一个 Release 才真正卸载，但各调用方无从感知）。

重构目标：**让每次 LoadXxx 返回一个 Handle 接口，调用方显式持有所有权，在自己的生命周期末主动 Release / UnloadAsync。** 编译器层面杜绝"忽略返回值"的误用（返回值被忽略时 IDE 可警告），彻底取代 `Release(location)` 字符串反查模式。

## 决策（Decision）

### 删除的 API（已从代码库移除）

| 删除的方法 | 删除理由 |
|---|---|
| `IAssetManager.LoadSync<T>(location) → T` | 返回裸 T，无 Handle，引用计数泄漏 |
| `IAssetManager.LoadAsync<T>(location) → UniTask<T>` | 同上，异步版 |
| `IAssetManager.LoadSubAssetsSync<T>(location) → T[]` | 返回裸数组，无 Handle |
| `IAssetManager.LoadSubAssetsAsync<T>(location) → UniTask<T[]>` | 同上，异步版 |
| `IAssetManager.LoadAllAssetsSync<T>(location) → T[]` | 返回裸数组，无 Handle |
| `IAssetManager.LoadAllAssetsAsync<T>(location) → UniTask<T[]>` | 同上，异步版 |
| `IAssetManager.Release(string location)` | 字符串反查模式废弃，Handle 直接 Release |
| `ISoundManager.ReleaseSoundAsset(string location)` | 同上，Sound 模块旧入口 |

### 新增 Handle 接口（5 个）

| 接口 | 文件 | 关键成员 |
|---|---|---|
| `IAssetHandle` | `Interfaces/IAssetHandle.cs` | `IsValid`, `IsDone`, `AssetObject`, `Release()` |
| `IAssetHandle<T>` | `Interfaces/IAssetHandle.cs` | `: IAssetHandle` + `T Asset` |
| `ISubAssetsHandle` | `Interfaces/ISubAssetsHandle.cs` | `IsValid`, `IsDone`, `Release()` |
| `ISubAssetsHandle<T>` | `Interfaces/ISubAssetsHandle.cs` | `: ISubAssetsHandle` + `T[] Assets`（整批同生共死） |
| `IAllAssetsHandle` | `Interfaces/IAllAssetsHandle.cs` | `IsValid`, `IsDone`, `Release()` |
| `IAllAssetsHandle<T>` | `Interfaces/IAllAssetsHandle.cs` | `: IAllAssetsHandle` + `T[] Assets`（整批同生共死） |
| `IRawFileHandle` | `Interfaces/IRawFileHandle.cs` | `IsValid`, `IsDone`, `string FilePath`, `byte[] GetBytes()`, `Release()` |
| `ISceneHandle` | `Interfaces/ISceneHandle.cs` | `IsValid`, `IsDone`, `UniTask UnloadAsync()`（无 Release 方法） |

### 新增内部适配器（4 个）

将 YooAsset 原生 handle 包装为 Handle 接口，通过 `ReferencePool` 池化，零 GC 分配。

| 适配器 | 实现接口 |
|---|---|
| `YooAssetSubAssetsHandleAdapter<T>` | `ISubAssetsHandle<T>, IReference` |
| `YooAssetAllAssetsHandleAdapter<T>` | `IAllAssetsHandle<T>, IReference`（Assets 属性两遍循环避免 LINQ 分配） |
| `YooAssetRawFileHandleAdapter` | `IRawFileHandle, IReference`（`GetBytes()` 每次调用 `File.ReadAllBytes`，调用方应缓存） |
| `YooAssetSceneHandleAdapter` | `ISceneHandle, IReference`（`UnloadAsync()` 调用 `m_Inner.UnloadSceneAsync()` + `WaitUntil(IsDone)` + `ReferencePool.Put(this)`） |

### 重塑后的 IAssetManager 公开 API（10 个 LoadXxx）

```csharp
// 单资产
IAssetHandle<T>     LoadSync<T>(string location) where T : Object;
UniTask<IAssetHandle<T>> LoadAsync<T>(string location) where T : Object;

// 子资产集（整批同生共死）
ISubAssetsHandle<T>     LoadSubsSync<T>(string location) where T : Object;
UniTask<ISubAssetsHandle<T>> LoadSubsAsync<T>(string location) where T : Object;

// 目录下全部资产（整批同生共死）
IAllAssetsHandle<T>     LoadAllSync<T>(string location) where T : Object;
UniTask<IAllAssetsHandle<T>> LoadAllAsync<T>(string location) where T : Object;

// 原始文件（FilePath/GetBytes，每次 GetBytes 做磁盘 IO）
IRawFileHandle     LoadRawSync(string location);
UniTask<IRawFileHandle> LoadRawAsync(string location);

// 场景（卸载用 UnloadAsync，无 Release）
ISceneHandle     LoadSceneSync(string location);
UniTask<ISceneHandle> LoadSceneAsync(string location);
```

### 迁移受影响的模块

| 模块 | 改动要点 |
|---|---|
| `ConfigManager` | 新增 `m_ConfigHandle: IAssetHandle<ConfigRuntimeSO>` 字段；`LoadAsync` 存 handle；`Shutdown` 先 `m_ConfigHandle?.Release()` |
| `SoundManager.SoundAgent` | 新增 `m_SoundAssetHandle: IAssetHandle` 字段；`Stop`/`Reset` 时 Release |
| `TextLocalizing` | 新增 `m_LoadedFontHandle: IAssetHandle<Object>` + `m_LoadedMaterialHandle: IAssetHandle<Material>` 字段；切换语言前先 Release 旧 handle |
| `LocalizationManager` | 内部已通过 `IAssetManager.LoadAsync<T>` 加载，依赖注入字段类型更新为 `IAssetManager` |
| `UIManager` | 走 Luban 数据加载链路，已通过委托方式持有 handle 引用（ADR-019 时已修） |
| `NetworkManager` | 同上，ADR-019 遗留的空 `releaseFunc` 在本次重构中随 API 删除而一并清理 |
| `AssetComponent` | 门面 API 全部同步更新，签名与 IAssetManager 一一对应 |
| `MainDemo/DemoUIView` | Sample Demo 更新为 Handle 持有模式（try/finally 短期 + 字段长期） |

## 长期 Handle 持有模式

部分模块需要在整个生命周期持有 Handle（ConfigManager 持有 ConfigRuntimeSO、SoundAgent 持有音频 clip、TextLocalizing 持有字体/材质），正确姿势：

```csharp
// 字段持有（长期）
private IAssetHandle<ConfigRuntimeSO> m_ConfigHandle;

// 加载
var handle = await m_AssetManager.LoadAsync<ConfigRuntimeSO>(location);
m_ConfigHandle = handle;
m_Runtime = handle.Asset;

// Shutdown 时释放
m_ConfigHandle?.Release();
m_ConfigHandle = null;
m_Runtime = null;
```

短期使用建议 `try/finally`：

```csharp
var handle = await Nova.Asset.LoadAsync<TextAsset>(location);
try
{
    ProcessData(handle.Asset);
}
finally
{
    handle.Release();
}
```

## 特殊语义说明

### ISubAssetsHandle / IAllAssetsHandle：整批同生共死

调用 `Release()` 后，`Assets` 数组内**所有元素的引用计数同时归零**。不允许将单个元素从数组中取出后 Release 整包，再继续使用该元素——这与 YooAsset 底层引用计数语义一致。

### ISceneHandle：UnloadAsync 是唯一卸载入口

`ISceneHandle` 没有 `Release()` 方法，卸载场景必须调用 `await handle.UnloadAsync()`。在 `UnloadAsync` 内部，适配器完成 `UnloadSceneAsync` + `WaitUntil(IsDone)` + `ReferencePool.Put(this)` 全流程。

### IRawFileHandle.GetBytes()：每次调用触发磁盘 IO

`GetBytes()` 内部是 `File.ReadAllBytes(FilePath)`，没有缓存。调用方如需多次访问，应自行缓存 byte[]，用完后 Release。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 维持裸 T 返回 + `Release(location)` | ADR-019 已证明无法防止空 releaseFunc + 字符串反查漏配对，根本上依赖调用方纪律而非类型系统 |
| 使用 IDisposable 替代 Handle | 跨帧异步场景多；IDisposable using 作用域无法覆盖异步等待期间的持有；对象池语义与 Dispose-即-销毁 冲突 |
| 每个 Load 返回单独 struct（值类型 Handle）| struct 无法走 ReferencePool 池化，高频 Load 场景堆内存分配压力反增 |

## 后果（Consequences）

### 正面

- **引用计数泄漏从语言层面被封堵**：忽略 Handle 返回值时 IDE 可警告；Handle 持有在字段上，生命周期可观测
- **所有权语义清晰**：每个 Handle 由持有它的调用方负责 Release，不再依赖 `Dictionary<string, IAssetHandle>` 按 location 记账
- **ADR-019 废弃**：空 `releaseFunc` 模式随裸 T 返回 API 的删除而消亡，无需再依赖 code-reviewer 人工拦截

### 负面

- **调用方需要管理 Handle 生命周期**：短期使用必须 try/finally；长期持有必须在 Shutdown/Stop/OnDestroy 时 Release，认知成本上升
- **场景加载 API 仅支持异步卸载**：`ISceneHandle.UnloadAsync()` 无同步等价；若业务需要同步卸载场景，需要等待帧末完成

## 关联

- 上层原则：[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]（Load/Unload 配对收口，本 ADR 是其在 Handle 接口层面的系统性落地）
- 封装铁律：[[PAT-37-no-yooasset-outside-asset-module|PAT-37]]（Handle 接口是模块外与资源系统交互的唯一通道）
- 取代：[[ADR-019-yooasset-release-mandatory|ADR-019]]（ADR-019 记录的旧裸 T 返回 + Release(location) 模式已彻底删除）
- 规范落点：统一工程约束中的资源加载反模式零容忍条目
