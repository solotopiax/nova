---
id: PAT-148
title: 广告收益打点与展示回调边界
summary: Banner ILRD聚合，impression即时
category: module
type: pattern
status: active
date: 2026-07-20
source: cur-session
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

`ad_impression` 表示单次广告曝光收益归因，应在每次收益回调中即时上传，不因为 Banner 刷新频繁而节流。

`ad_ilrd` 表示广告收益事件。非 Banner 广告按单次收益回调即时上传；Banner 广告因自动刷新频繁，使用全渠道统一的累计规则：满 5 次收入回调后上传一次。

Banner ILRD 累计必须同时保存：

- 已累计次数
- 已累计金额

这两个值必须持久化到本地存档，防止游戏重启后丢失未满 5 次的 Banner 收益批次。

### 2. 聚合规则放在广告基类，不放单一渠道

Banner ILRD 的“5 次上传一次”不是 MAX 独有规则，而是所有广告渠道共享规则。  
因此累计、持久化、清档和阈值判断应沉在 `AdChannelPluginBase` 层；具体渠道只负责构造本渠道的 `ad_ilrd` 载荷并在达到阈值时派发。

渠道实现不得各自复制一套 Banner 累计逻辑，否则会导致：

- 各渠道节流规则不一致。
- 本地存档 key 难以统一隔离。
- 后续调整阈值或上传语义时出现遗漏。

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
- 在 Banner displayed 回调里触发 `RaiseShowCompleted`。
- 为了“中文化”把文件头模板标签改成中文。
- 新增复杂打点逻辑但不写中文注释，或日志仍保留英文说明。

## 验收口径

- Banner 每次收益回调都上传 `ad_impression`。
- Banner 的 `ad_ilrd` 满 5 次收入回调后上传一次，且次数和金额跨启动保存。
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

- SDK 插件架构：[[ADR-022-sdk-plugin-architecture|ADR-022]]
- SDK Plugin SOP：[[PAT-33-sdk-plugin-sop|PAT-33]]
- C# XML 注释规范：[[PAT-114-cs-xml-doc-no-html-escape|PAT-114]]
- 代码与 Docs 同步：[[PAT-116-cs-doc-mirror-sync|PAT-116]]
