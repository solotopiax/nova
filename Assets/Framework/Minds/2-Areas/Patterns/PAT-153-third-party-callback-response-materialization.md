---
id: PAT-153
title: 第三方回调响应必须在回调返回前物化
type: pattern
status: active
date: 2026-08-06
summary: 回调拥有响应生命周期时须在返回前复制正文与字节
category: runtime
aliases:
  - PAT-153-third-party-callback-response-materialization
  - 第三方响应回调生命周期
keywords:
  - PAT-153
  - BestHTTP
  - HTTPResponse
  - Empty response
  - 回调响应释放
  - 响应体物化
  - DownloadTextAsync
tags:
  - pattern
  - runtime
  - network
  - async
  - lifecycle
  - besthttp
related:
  - "[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]"
  - "[[PAT-03-runtime-verify-three-step|PAT-03]]"
  - "[[PAT-146-unitask-cached-completion-for-retry|PAT-146]]"
---

# PAT-153：第三方回调响应必须在回调返回前物化

## 适用场景（When）

- 第三方网络库通过完成回调交付响应，并在回调返回后释放请求或响应对象。
- 适配层把回调桥接为 `Task`、`UniTask` 或其他异步结果，调用方稍后才读取正文、原始字节或响应头。
- 请求状态和 HTTP 状态均成功，但框架响应的 `Body` / `RawData` 为空；浏览器或第三方库原始回调却能读到完整内容。
- 下载逻辑同时存在外部取消、空闲超时和池化响应对象，需要明确唯一完成者与对象所有权。

## 核心做法（What & How）

### 1. 先确认第三方对象的真实所有权窗口

不要根据异步 API 的返回类型推断响应可长期持有。应直接检查第三方源码或文档，确认：

- 完成回调在何处执行；
- 响应在回调返回前还是返回后释放；
- `Data`、`DataAsText`、Headers 是独立副本还是对内部缓冲区的视图。

如果第三方在回调返回后立即 `Dispose` 请求或响应，适配层必须把该回调视为唯一安全读取窗口。

### 2. 在回调内部完成框架响应物化

在第三方回调返回前读取并复制：

- HTTP 状态码与成功标记；
- 文本正文；
- 原始字节；
- 响应头；
- 错误与下载进度。

回调对外完成的是已经独立持有数据的框架 `HttpResponse`，不能先把第三方 `HTTPResponse` 放进异步任务，再由 continuation 或下一帧读取。

### 3. 用原子完成权处理回调、取消和超时竞态

完成回调、外部取消和 idle timeout 可能同时到达。适配层应使用原子状态确保只有一个路径取得完成权：

- 回调获胜：物化结果并完成异步源；
- 取消或超时获胜：中止请求并返回对应失败响应；
- 迟到回调：不再次完成任务，也不泄漏已创建的池化响应。

如果回调已构建框架响应但失去完成权，必须立即将该对象归还 `ReferencePool`。调用方只负责归还最终成功交付给自己的响应。

## 为什么这么做（Why）

BestHTTP 3.0.18 的请求事件实现会先调用 `source.Callback(source, source.Response)`，随后在 callback 非空时执行 `source.Dispose()`。Nova 旧实现把 `Task<HTTPResponse>` 转为 `UniTask`，在异步恢复后才调用 `BuildHttpResponse`；此时第三方响应已被释放，因此出现 HTTP 200、`IsSuccess=true`，但 `Body` 与 `RawData` 为空。

把复制动作放回 BestHTTP callback 内后，框架响应不再依赖第三方对象的后续生命周期。原子完成权同时避免取消或超时先返回后，迟到回调再次完成任务或遗留池化对象。

## 反模式（Anti-patterns）

- 看到 `Task<HTTPResponse>` 就默认响应在 await 后仍然有效。
- 使用 `ContinueWith(... ExecuteSynchronously)` 作为生命周期保证；调度器内联是执行策略，不是第三方响应所有权契约。
- 只检查 HTTP 状态码，不断言正文和原始字节，导致“200 空响应”漏过测试。
- 修正文成功路径，却不处理取消、idle timeout 与迟到回调之间的完成竞态。
- 回调构建池化对象后完成失败，却没有归还该对象。

## 验证依据（Verification）

- 第三方源码：BestHTTP 3.0.18 `RequestEvents.cs` 在 callback 返回后调用 `source.Dispose()`。
- Nova 实现：`UPMPackages/com.solotopia.nova.framework.besthttp/Nova/Runtime/BestHttpTransport.Methods.cs` 在 `HTTPRequest.Callback` 内构建框架响应。
- 红灯复现：本地 HTTP 200 返回 `["device-id"]`，旧实现得到空 `Body`。
- 绿灯回归：`BestHttpTransportDownloadTests.DownloadTextAsync_PreservesSuccessfulResponseBody` 断言 `Body` 与 `RawData`，EditMode 1/1 通过。
- 真实流程：MainDemo HostPlayMode 成功打印“启动白名单文件拉取成功”与“启动白名单命中”，不再出现 `Empty response`。

## 关联

- 白名单业务契约：[[ADR-076-startup-whitelist-metadata-routing|ADR-076]]
- 运行时验证方法：[[PAT-03-runtime-verify-three-step|PAT-03]]
- UniTask 生命周期边界：[[PAT-146-unitask-cached-completion-for-retry|PAT-146]]
