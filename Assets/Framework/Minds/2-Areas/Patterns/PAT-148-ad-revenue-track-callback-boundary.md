---
id: PAT-148
title: 广告收益打点与展示回调边界
summary: 收益打点与协议共用批次，曝光通知按次
category: module
type: pattern
status: active
date: 2026-07-20
source: code-and-docs
aliases:
  - PAT-148-ad-revenue-track-callback-boundary
keywords:
  - PAT-148
  - 广告收益打点
  - Banner ILRD
  - ad_impression
  - RaiseShowCompleted
tags: [pattern, nova, framework, sdk, ad, track]
related:
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[PAT-33-sdk-plugin-sop|PAT-33]]"
  - "[[PAT-114-cs-xml-doc-no-html-escape|PAT-114]]"
  - "[[PAT-116-cs-doc-mirror-sync|PAT-116]]"
---

# PAT-148：广告收益打点与展示回调边界

## 适用场景（When）

- 维护 `com.solotopia.nova.framework.sdk.ad` 的广告渠道基类。
- 接入或调整 MAX、AdMob、其他广告渠道的收益回调、展示回调和埋点桥接。
- 处理 `ad_ilrd`、`ad_impression`、`nova_ad_show`、`nova_ad_hidden`、`OnShowCompleted` 等广告行为事件。
- 修改广告模块代码注释、日志或文件头模板。

## 核心做法（What & How）

### 1. 收益事件按语义拆分

对已经接入分析收益事件的渠道，`ad_impression` 表示单次广告曝光收益归因，应在每次收益回调中即时派发，不因为 Banner 刷新频繁而节流。这里约束接入后的语义，不表示每个渠道都已实现相同分析事件；具体支持范围以渠道源码和 Docs 为准。

`ad_ilrd` 表示广告收益事件。非 Banner 广告按单次收益回调尝试派发；Banner 广告因自动刷新频繁，复用共享累计算法，默认累计 5 次后派发。阈值可配置，同一渠道内全部 Banner 共享次数和金额，不同渠道相互隔离，不能把“算法共享”理解为跨渠道合并收入。

Banner ILRD 累计必须同时保存：

- 已累计次数
- 已累计金额

这两个值必须持久化到本地存档，防止游戏重启后丢失未满阈值的 Banner 收益批次。

### 2. 聚合规则放在广告基类，不放单一渠道

Banner 收益的累计算法不是 MAX 独有能力，而是广告渠道可以复用的共享规则。  
因此累计、持久化、清档和阈值判断应沉在 `AdChannelPluginBase` 层；具体渠道只负责构造本渠道的 `ad_ilrd` 载荷并在达到阈值时派发。

渠道实现不得各自复制一套 Banner 累计逻辑，否则会导致：

- 各渠道节流规则不一致。
- 本地存档 key 难以统一隔离。
- 后续调整阈值或上传语义时出现遗漏。

### 2.1 需要同节奏的上报共用一个批次和派发入口

分析收益打点与后台收益协议需要保持一致节奏时，应在渠道同一个收益派发入口使用同一批金额，不能各建一个“阈值相同”的计数器或存档。独立状态会在目标不可用、异常、重启或清理时产生偏移；即使配置相同，也不能保证金额和派发次数一致。

累计回调返回未派发时保留原批次，下游协议也必须同步等待。否则协议先发送、分析批次仍保留，下一笔收入加入后再次发出包含旧金额的新请求，会形成重复计入风险。原始收益业务通知仍按笔触发，不作为第二个自动上传入口。

批次消费与网络成功是两种确认：已有链路以“已尝试派发”消费批次时，不得把它描述为服务端已入账或保证最终送达。网络失败不自动恢复已消费批次；若需可靠补发，应明确设计幂等和待确认状态，不能临时叠加第二份累计。同一请求重试复用业务 ID，新的一笔收益使用新 ID。

向分析平台与业务服务器各发送一次是不同目的地；是否重复应按目的地、业务 ID 和批次金额判断。还应核对同一分析插件是否实现了多个接收能力，不能用业务协议只发一次证明所有分析目标都只收到一次。

### 3. 不改变既有埋点字段类型

`ad_ilrd` 的属性类型必须保持兼容既有平台：

- `publisher_revenue` 保持数值类型。
- `value` 保持数值类型。
- `af_revenue` 保持文本类型。

不同平台对同一收益字段可能有“必须文本”或“必须数字”的要求，不能为了统一格式而改字段类型。

金额累计内部可用 `decimal` 避免小额收入累计损失精度；落盘和文本字段应使用稳定格式，避免小额金额被写成科学计数法。

### 4. 展示成功回调避开 Banner

`RaiseShowCompleted` 用于触发 `OnShowCompleted`，保留给展示成功回调链路使用。  
非 Banner 广告的 `On*Displayed` 回调中可调用 `RaiseShowCompleted`。

Banner 展示回调不能接入 `RaiseShowCompleted`。Banner 会在展示/刷新期间反复触发展示回调，如果接入展示成功事件，会让业务侧收到持续重复的 `OnShowCompleted`。

### 5. 注释与日志规则

广告模块新增或修改正文注释、XML 注释、日志文案时必须使用中文，尤其是：

- 持久化 key 的用途。
- 埋点 schema 的字段语义。
- 跨渠道共享规则。
- 线程、回调和生命周期边界。
- 兼容旧平台字段类型的原因。

C# 文件头模板不是正文注释，必须保留仓库既有英文标签：

```text
copyright
All Rights Reserved
filename
author
created
descrip
```

其中 `descrip` 后的说明内容可以使用中文。

## 反模式（Anti-patterns）

- 只在 MAX 内实现 Banner ILRD 聚合，导致其他广告渠道继续高频上传。
- 因 Banner 刷新频繁而把 `ad_impression` 一起节流。
- 把 `af_revenue` 改成数值，或把 `publisher_revenue` / `value` 改成文本。
- 只保存 Banner 累计次数，不保存累计金额。
- 只在内存累计 Banner 收益，游戏重启后丢失未满批次。
- 分析打点和后台协议分别累计、分别存档，仅靠相同阈值维持一致性。
- 分析批次未消费时先发送后台协议，恢复派发后重复发送包含旧金额的新批次。
- 把原始收益通知再接到自动协议上传，和渠道派发入口重复上报。
- 把已尝试派发、网络成功和服务端入账视作同一确认。
- 在 Banner displayed 回调里触发 `RaiseShowCompleted`。
- 为了“中文化”把文件头模板标签改成中文。
- 新增复杂打点逻辑但不写中文注释，或日志仍保留英文说明。

## 验收口径

- Banner 每次收益回调都上传 `ad_impression`。
- Banner 的 `ad_ilrd` 按配置阈值派发，且次数和金额跨启动保存；不同渠道隔离。
- 后台协议与分析打点要求同节奏时，共用同一累计批次及消费入口；无目标、重启恢复和派发异常不会让两套状态分别前进。
- 原始收益通知保持逐次；协议与分析目的地的重复问题分别核对，不以派发结果代替网络或入账证据。
- 非 Banner 的 `ad_ilrd` / `ad_impression` 仍按每次收益回调上传。
- `ad_ilrd` 字段类型与既有平台要求一致。
- 非 Banner displayed 回调触发 `RaiseShowCompleted`，Banner displayed 回调不触发。
- 相关代码日志和正文注释为中文；文件头模板标签为英文。
- 代码事实变化时同步 `Nova/Doc` 文档，遵守 [[PAT-116-cs-doc-mirror-sync|PAT-116]]。

## 来源（Origin）

- 会话日期：2026-07-20
- 触发背景：广告模块收益打点审计与 MAX 展示成功回调补齐。
- 关键结论：
  - `ad_impression` 每次都传。
  - `ad_ilrd` 的 Banner 5 次聚合规则适用于所有广告渠道。
  - Banner 聚合状态必须存档。
  - `OnShowCompleted` 保留，`RaiseShowCompleted` 只接非 Banner 展示成功回调。
  - 正文注释和日志必须中文，C# 文件头模板标签保持英文。

## 关联

当前实现与具体参数以 [广告模块索引](../../../../../UPMPackages/com.solotopia.nova.framework.sdk.ad/Nova/Doc/INDEX.md) 和 [MAX 收益派发说明](../../../../../UPMPackages/com.solotopia.nova.framework.sdk.max/Nova/Doc/MaxAdPlugin.md#banner-收益打点) 为准；源码依据为渠道基类的 `AdChannelPluginBase.Track.cs`、`AdChannelPluginBase.RevenueReport.cs` 及 MAX 的 `MaxAdPlugin.Track.cs`。本条记录共用批次的原因与确认边界，不保存当次测试结果或本地测试身份。

- SDK 插件架构：[[ADR-022-sdk-plugin-architecture|ADR-022]]
- SDK Plugin SOP：[[PAT-33-sdk-plugin-sop|PAT-33]]
- C# XML 注释规范：[[PAT-114-cs-xml-doc-no-html-escape|PAT-114]]
- 代码与 Docs 同步：[[PAT-116-cs-doc-mirror-sync|PAT-116]]
