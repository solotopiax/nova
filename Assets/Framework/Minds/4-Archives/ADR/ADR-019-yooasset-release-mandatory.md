---
id: ADR-019
title: YooAsset Load 后必须显式 Release(location)，releaseFunc 不得空实现
status: superseded
date: 2026-05-17
archived-date: 2026-06-08
summary: 旧版 YooAsset 显式 Release 约束
category: asset
aliases:
  - ADR-019-yooasset-release-mandatory
keywords: [ADR-019, ADR-019-yooasset-release-mandatory]
tags: [adr, nova, asset, yooasset, resource-management]
supersedes: []
superseded-by:
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
related:
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
---

# ADR-019：YooAsset Load 后必须显式 Release(location)，releaseFunc 不得空实现

## 背景（Context）

Nova 的 `AssetManager.LoadAsync<T>(location)` / `LoadSync<T>(location)` 内部走 YooAsset，拿到 handle 后调用 `handle.GetAssetObject<T>()` 把强类型资产返回给业务层；handle 本身**只在 AssetManager 内部以 `Dictionary<string, IAssetHandle>` 形式被记账**，调用方拿到的是**裸资产**（`UnityEngine.Object` 或子类）。

现状有一条普遍存在的错误认知：很多模块的 Luban 数据加载链路（`LubanDataReceiver` 需要传入 `loadFunc` + `releaseFunc`）把 `releaseFunc` 写成 `_ => { }`，并配以注释「YooAsset 引用计数自管，无需手动 Release」。这条认知**是错的**：

- YooAsset 的引用计数是相对 handle 的，不是相对裸资产的；handle 不被显式 Release，引用计数永不归零，资产不会被卸载
- AssetManager 的设计是「谁调用 Load 谁负责 Release(location)」，`Release(location)` 内部走 `pkg.TryUnloadUnusedAsset(location)` 才真正使引用计数归零
- Editor 下 YooAsset 的开发模式可能掩盖泄漏（编辑器直接吃 AssetDatabase），真机出包才暴露内存暴涨

现网模块 Vibrate/UI/Network 都中招过空 `releaseFunc`；Sound 是已经修对的范本。本会话从 Vibrate 起步顺手把 UI 也改了，剩 Network 待清理。

## 决策（Decision）

**所有走 `IAssetManager.Load*` 的路径，必须有对应的 `IAssetManager.Release(location)` 调用与之配对。`releaseFunc = _ => { }` 在 Nova 框架内一票否决，code-reviewer 必须红线拦截。**

具体到 Luban 数据加载链路：

- `LubanDataReceiver` 构造参数中的 `releaseFunc` 必须能根据传入的 `UnityEngine.Object` 反查出 `assetLocation`，并调用 `m_AssetManager.Release(location)`
- 标准实现走 `Build*Delegates` + `Dictionary<UnityEngine.Object, string>` 反查表（详见 [[PAT-28-luban-load-release-symmetric|PAT-28]]）
- 禁止注释「YooAsset 引用计数自管」，发现一律删

## 后果（Consequences）

### 正面

- 资源泄漏闭环：YooAsset 引用计数在 Load/Release 配对下能正常归零，长时间运行不堆积
- 跨模块统一姿势：Sound/Vibrate/UI/Network/Localization/Table 等所有 Luban 数据加载走同一模板
- code-reviewer 有明确红线：`releaseFunc = _ => { }` 即缺陷，无需再讨论"是不是真泄漏"

### 负面

- 旧模块（已知 Network 仍有空实现）需要逐一改造，不是无成本切换
- `Build*Delegates` 模板带来一份 `Dictionary` 反查表的内存开销（量级很小，可接受）

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 维持 `_ => { }` + 注释「YooAsset 引用计数自管」 | 错误认知，handle 不显式 Release 就泄漏 |
| 让 `LubanDataReceiver` 内部自己持有 location 串 | 跨层耦合，DataReceiver 不应感知资产管理；委托模式更解耦 |
| 在 `loadFunc` 内部 `using` 句柄 | YooAsset handle 不是 IDisposable；且 Load 完成时机和 Release 时机经常分离（例如 UI 注册表加载完即 Release，但实际 Prefab/Sprite 资源要后续才释放） |

## 验证依据（Verification）

- 源码：
  - `Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/Implements/AssetManager.Load.cs`：`GetAssetObject<T>()` 后 handle 不再返回给调用方
  - `Assets/Framework/Scripts/Runtime/Modules/Asset/Managers/Implements/AssetManager.Cleanup.cs`：`Release(location)` → `pkg.TryUnloadUnusedAsset(location)`
- 已修复参考：`SoundManager.Methods.cs::BuildSoundData{Async,Sync}Delegates` / `VibrateManager.Methods.cs::BuildVibrateData{Async,Sync}Delegates` / `UIManager.Methods.cs::BuildUIRegistry{Async,Sync}Delegates`
- 待修复：`NetworkManager`（`releaseFunc = _ => { }` 仍在）

## 来源（Origin）

- 会话日期：2026-05-17
- 关键对话节选：

  > 用户：`Assets/Framework/Scripts/Runtime/Modules/Vibrate/Managers/Implements/VibrateManager.cs` 中 LoadVibrateDataAsync 的 releaseFunc 怎么是空实现呢？
  > AI：核查 AssetManager.Load.cs / Cleanup.cs，确认 handle 在 GetAssetObject<T>() 后被框架内部丢弃只留 location 索引；空 releaseFunc 等于不 Release，引用计数永不归零 → 泄漏。
  > 用户：改 → "Vibrate的load和release姿势不一致，不太好" → "按sound重构吧"
  > （后续）AI：UIManager 也是同模式空实现 + 错误注释，本会话顺手改正。

## 关联

- 上层原则：[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]（Load/Unload 配对收口，本 ADR 是其在 YooAsset 路径上的具体落地）
- 实施模板：[[PAT-28-luban-load-release-symmetric|PAT-28]]
- 规范落点：待评审是否纳入统一工程约束，或单独沉淀资源释放规范
