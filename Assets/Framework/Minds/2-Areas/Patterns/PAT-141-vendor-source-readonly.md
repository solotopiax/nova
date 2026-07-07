---
id: PAT-141
title: 封装第三方 SDK 时源目录只读，文件完整搬入不增删改
summary: 第三方源目录只读，封装是复制入包，不写不删不改不省略
category: module
type: pattern
status: active
date: 2026-07-05
source: cur-session
aliases:
  - PAT-141-vendor-source-readonly
  - vendor-source-readonly
tags: [pattern, nova, sdk, external, discipline]
related:
  - "[[PAT-33-sdk-plugin-sop|PAT-33]]"
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
  - "[[ADR-070-sdk-enable-via-configmaster-enabledsdks|ADR-070]]"
---

# PAT-141：封装第三方 SDK 时源目录只读

## 适用场景（When）

- 把第三方 SDK / 库（原厂交付目录，如 `~/Downloads/<vendor>`）封装成 Nova UPM 包时。
- 任何"以第三方原始交付物为输入"的加工任务。

## 核心做法（What & How）

- **对第三方源目录只读**：只 `cp` / 读取，**绝不**在源目录 `Write` / 新建 / 删除 / 改写任何文件（包括临时笔记、分析文档）。加工产物一律落到 Nova 仓库内。
- **第三方文件完整搬入包**：源目录里的全部文件（含 `README.md` / 说明文档 / LICENSE / `.meta`）原样复制进包的 `Core/`（或对应 vendor 目录），**不省略、不替换、不改写**。包级 README 另写新文件，但不得以此为由丢弃厂商原 README。
- **不改第三方代码**：厂商代码原样保留；确需适配（如补依赖引用）只做最小必要且可追溯的改动，并在包文档记录。
- **对照厂商文档落地**：严格对照厂商 README / 接入文档实现封装，接入顺序与 API 语义逐条对齐，不臆测。

## 为什么这么做（Why）

- 第三方源是外部资产，写入 / 删改破坏其完整性与可追溯性，也可能违反授权。
- 丢弃厂商 README / 说明文档 = 封装包丢失第一手接入依据，后续维护者无源可查。
- 用户在本次会话中明确划为红线。

## 反模式（Anti-patterns）

- ❌ 往第三方源目录 `Write` 分析 / 扫盲 / 笔记文档（污染源目录）。
- ❌ 封装时"我们写新 README 就不搬厂商 README"——丢弃第三方说明文档。
- ❌ 改动第三方源目录内任何文件（含删除、重命名、内容修改）。
- ❌ 不读厂商 README 就凭记忆实现封装，导致接入顺序 / 语义偏差。

## 跨项目复用提示

通用工程纪律，可直接搬到任何"封装/集成第三方交付物"的项目。

## 来源（Origin）

- 会话日期：2026-07-05
- 关键对话节选：
  > 用户：第三方只读，不许往内部添加和修改内容。
  > 用户：你是不是私自删除该 sdk 的说明文档了？？尤其是那个 README.md，你触碰红线了，我之前严格要求过，第三方代码和文件不要做任何修改。
  > AI（教训）：封装 DataMaster 时漏搬厂商 README.md、并曾把扫盲文档写入第三方源目录，均为错误，已补回 README 并恢复源目录原状。

## 关联

- 相关 Pattern：[[PAT-33-sdk-plugin-sop|PAT-33]]（SDK 插件接入 SOP）
- 相关 Pattern：[[PAT-41-upm-package-layout-and-manifest|PAT-41]]（UPM 包布局）
- 相关 ADR：[[ADR-070-sdk-enable-via-configmaster-enabledsdks|ADR-070]]（DataMaster 封装同期决策）
