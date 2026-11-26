---
id: PAT-28
title: Luban 表数据加载与资源句柄释放
type: pattern
status: active
date: 2026-05-17
summary: 表资源的加载和释放沿同一资源句柄
category: asset
aliases:
  - PAT-28-luban-load-release-symmetric
keywords:
  - PAT-28
  - LubanDataReceiver
  - IAssetHandle
  - 表资源释放
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

# PAT-28：Luban 表数据加载与资源句柄释放

## 适用场景

框架模块通过 `DataReceiver` 读取、解析并缓存表文件。按格式选择 JSON 接收器 `LubanDataReceiver` 或二进制接收器 `LubanBinaryDataReceiver`。Network 的 HostKey 和 NetCmd 已通过 `LubanRuntimeData.CreateReceiver` 按所配置格式选择接收器。

## 核心做法

- `IAssetManager.LoadAsync<T>()` 或 `LoadSync<T>()` 返回本次请求的 `IAssetHandle<T>`。持有者从 `handle.Asset` 取资源，通过同一 `handle.Release()` 释放。
- `DataReceiver` 接收加载委托返回的资源对象。对有效资源会在解析结束后调用所传的 `ReleaseAssetAction`，包括解析抛出异常的路径。
- 当前 Network 和 Sound 的表加载委托取得 `TextAsset` 后自行释放句柄，再将资源对象交给接收器。在这条链路中，接收器的释放回调为空。复核是否遗漏释放时，应顺着本次实际句柄查释放位置。
- 接入其他资源读取流程时，按实际使用期限明确谁持有和释放句柄。同一地址的多次加载分别核对各次载入的句柄。

## 为什么这么做

接收器负责消费表内容，资源句柄的持有者掌握释放时机。明确这两份职责，可以同时检查漏释放、重复释放和读取过程中的资源寿命。旧地址反查方式的迁移背景见 [[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]。

## 反模式

- 只返回裸资源对象，丢失本次载入的句柄。
- 按地址猜测应释放的请求。同一个地址可被多个调用方载入。
- 看见空的释放回调就判断发生泄漏。应先核对加载委托中的实际句柄所有权。

## 来源和示例

- [DataReceiver](../../../Docs/Runtime/Core/Table/DataReceiver.md) 和 [LubanDataReceiver](../../../Docs/Runtime/Core/Table/LubanDataReceiver.md)
- [LubanRuntimeData.cs](../../../Scripts/Runtime/Core/Table/LubanRuntimeData.cs)
- [NetworkManager.cs](../../../Scripts/Runtime/Modules/Network/Managers/NetworkManager/Implements/NetworkManager.cs)
- [SoundManager.Methods.cs](../../../Scripts/Runtime/Modules/Sound/Managers/Implements/SoundManager.Methods.cs)
