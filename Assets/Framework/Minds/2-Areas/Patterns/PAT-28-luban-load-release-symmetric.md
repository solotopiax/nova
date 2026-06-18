---
id: PAT-28
title: Luban DataReceiver 资源对称释放：Build*Delegates + assetLocationMap 反查
type: pattern
status: active
date: 2026-05-17
summary: Luban Load与Release必须对称释放
category: asset
aliases:
  - PAT-28-luban-load-release-symmetric
keywords:
  - PAT-28
  - PAT-28-luban-load-release-symmetric
tags:
  - pattern
  - nova
  - asset
  - luban
  - resource-management
related:
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
---

# PAT-28：Luban DataReceiver 资源对称释放（Build*Delegates + assetLocationMap）

## 适用场景（When）

任何 Manager 走 `LubanDataReceiver(dataCache, unit, loadFunc, releaseFunc)` 加载 JSON 数据表的场景。已知命中模块：Sound / Vibrate / UI / Localization / Table（Network 待迁移）。

## 核心做法（What & How）

**结构铁律：Load 委托和 Release 委托必须由同一个 Build 方法成对产出，绝不允许 load 走顶层闭包、release 走 `_ => { }` 或现场内嵌 lambda。**

模板（异步路径，同步路径对称）：

```csharp
private void Build{Module}DataAsyncDelegates(
    out DataReceiver.LoadAssetAsyncFunc loadFunc,
    out DataReceiver.ReleaseAssetAction releaseFunc)
{
    IAssetManager assetManager = m_AssetManager;
    Dictionary<UnityEngine.Object, string> assetLocationMap = new Dictionary<UnityEngine.Object, string>();

    loadFunc = async (assetLocation) =>
    {
        UnityEngine.Object asset = await assetManager.LoadAsync<TextAsset>(assetLocation);
        if (asset != null)
        {
            assetLocationMap[asset] = assetLocation;
        }
        return asset;
    };

    releaseFunc = rawAsset =>
    {
        if (rawAsset is UnityEngine.Object unityAsset
            && assetLocationMap.TryGetValue(unityAsset, out string location))
        {
            assetLocationMap.Remove(unityAsset);
            assetManager.Release(location);
        }
    };
}
```

调用侧（在 `LoadAsync` / `LoadSync` 中）一行替换原本的 `_ => { }`：

```csharp
Build{Module}DataAsyncDelegates(out DataReceiver.LoadAssetAsyncFunc loadFunc, out DataReceiver.ReleaseAssetAction releaseFunc);
```

## 为什么这么做（Why）

1. **同一条调用链里保存反查关系**：业务只拿到裸 `UnityEngine.Object`，释放时必须能把它对应回 `assetLocation`。
2. **load 与 release 必须对称**：`assetLocationMap` 在 load 时写入、release 时清理，避免把状态散到外部成员字段。
3. **闭包 + 局部 Dictionary 优于成员字段**：单次 `LoadAsync` 的生命周期结束后，反查表也应结束生命周期。

## 反模式（Anti-patterns）

| 反模式 | 后果 |
|---|---|
| `releaseFunc = _ => { }` + 注释「YooAsset 引用计数自管」 | 真机内存累积泄漏；ADR 已一票否决 |
| `loadFunc` 在顶层定义、`releaseFunc` 在 `AddTask` 方法内现场内嵌 lambda（捕获循环局部变量 location） | 「load 和 release 姿势不一致，不太好」（用户原话）；不易跨模块复用，闭包变量极易出 bug |
| 把 `assetLocationMap` 提到成员字段 | 多次 `LoadAsync` 调用之间脏数据累积；清空时机难定 |
| 用 `WeakReference` / `ConditionalWeakTable` 等"高级"反查 | YooAsset 资产是普通 UnityEngine.Object，不需要弱引用语义；徒增复杂度 |

## 跨项目复用提示

可直接复用到任何「YooAsset + 资产委托加载」场景，不限于 Luban——本质是「load/release 委托必须在同一函数闭包内成对产出 + 用反查表桥接裸资产到 location」的通用资源管理姿势。
