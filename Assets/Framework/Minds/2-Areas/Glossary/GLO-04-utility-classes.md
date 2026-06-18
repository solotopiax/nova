---
id: GLO-04
title: EditorUtil.Draw / Util.TypeCreator / Util.Json
type: glossary
status: active
date: 2026-05-14
summary: Nova 中优先使用的几个核心工具入口
category: core
keywords: [EditorUtil.Draw, GLO-04, Util.Json, Util.SysIO, Util.TypeCreator, 工具类优先]
tags: [glossary, nova, utility, editor, json]
aliases:
  - EditorUtil.Draw
  - Util.TypeCreator
  - Util.Json
  - Util.SysIO
  - 工具类优先
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[ADR-018-json-via-util-json]]"
---

# EditorUtil.Draw / Util.TypeCreator / Util.Json

## 一句话定义

Nova 有几类高优先级的统一工具入口，用来收口 Editor 绘制、对象创建、JSON 读写与文件系统访问，避免底层 API 直接散落。

## 代表性入口

| 工具 | 作用 |
|---|---|
| `EditorUtil.Draw` | Editor/Inspector/UI 绘制统一入口 |
| `Util.TypeCreator` | 反射创建与类型收口入口 |
| `Util.Json` | JSON 读写统一入口 |
| `Util.SysIO` | 文件系统访问入口 |

## 为什么重要

- 统一入口可以降低实现风格分裂。
- 底层策略发生变化时，只需集中调整。
- Review 时更容易识别是否绕开框架约定。

## 这个词条不负责什么

- 不提供完整工具类目录。
- 不替代具体 API 文档。
- 不意味着所有工具都必须挂在 `Util.*` 下。

## 使用判断

- 需要 Editor 绘制时，优先想 `EditorUtil.Draw`
- 需要按类型名创建对象时，优先想 `Util.TypeCreator`
- 需要 JSON 读写时，优先想 `Util.Json`
- 需要文件系统操作时，优先检查 `Util.SysIO`

如果某处直接绕过这些入口，应先确认是否有明确例外，而不是默认自行扩散底层调用。

## 关联

- [[ADR-001-component-manager-three-layer]]
- [[ADR-018-json-via-util-json]]
- [[GLO-05-three-tier-docs]]
