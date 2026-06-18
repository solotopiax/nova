---
title: Nova 术语速查
date: 2026-06-05
---

# Nova 术语速查

本页只做高频命名对齐。

## 核心术语

| 术语 | 含义 | 参考 |
|---|---|---|
| Manager 三层继承链 | `I{Xxx}Manager -> {Xxx}ManagerBase -> {Xxx}Manager` | [GLO-02](../2-Areas/Glossary/GLO-02-framework-manager-tiers.md) |
| Component / Procedure / Manager | `Component` 承接 Unity 入口，`Procedure` 承接流程编排，`Manager` 承接纯 C# 逻辑 | [GLO-03](../2-Areas/Glossary/GLO-03-component-procedure-manager.md) |
| 工具门面 | `EditorUtil.Draw`、`Util.TypeCreator`、`Util.Json` 等统一工具入口 | [GLO-04](../2-Areas/Glossary/GLO-04-utility-classes.md) |
| 三级文档体系 | `INDEX / 模块层 / 类规格` 三层组织 | [GLO-05](../2-Areas/Glossary/GLO-05-three-tier-docs.md) |
| Asset 地址 | 资源定位字符串的统一中文称呼 | [GLO-07](../2-Areas/Glossary/GLO-07-asset-location.md) |

## 容易混淆的词

| 推荐用语 | 避免 |
|---|---|
| Asset 地址 | 资产地址、资源地址 |
| Runtime 模块 | 系统、逻辑块等模糊叫法 |
| View | 在业务 UI 类名中混用 `Panel`、`Window`、`Dialog` |

## 使用建议

- 只要是术语问题，先查 `Glossary`
- 只要是当前代码命名，回到 `Docs + 源码`
