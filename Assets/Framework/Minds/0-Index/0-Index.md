---
title: Nova Minds 索引
date: 2026-06-05
---

# Nova Minds 索引

`Assets/Framework/Minds/` 是 **Nova 的长期知识层**。  
它回答的是：

- 为什么这样设计
- 哪些术语必须统一
- 哪些模式长期成立
- 历史上有哪些关键决策与踩坑

它**不**承担：

- 当前 API 手册
- 字段级实现说明
- 当次会话过程记录
- 本地协作工具与环境细节

## 与 Docs 的分工

| 位置 | 定位 |
|---|---|
| `Assets/Framework/Docs/` | 当前代码事实层 |
| `Assets/Framework/Minds/` | 长期知识层 |

如果问题是“现在代码怎么实现”，先查 `Docs`。  
如果问题是“为什么这样定 / 这个词该怎么用 / 改动会不会撞历史边界”，再查 `Minds`。

## 默认查询顺序

1. 本页
2. 对应类型索引
3. 命中的正文条目

## 类型入口

- [ADR 索引](0-Index-ADR.md)：架构决策
- [PAT 索引](0-Index-PAT.md)：模式与反模式
- [GLO 索引](0-Index-GLO.md)：术语与概念
- [MOC 索引](0-Index-MOC.md)：模块图谱
- [RES 索引](0-Index-RES.md)：外部资料
- [变更记录](0-Log.md)：知识层治理与结构调整日志
- [术语速查](0-Index-Terms.md)：高频命名对齐
- [文件夹指南](0-Index-FolderGuide.md)：目录边界

## 默认只看这些区域

- `0-Index/`
- `2-Areas/ADR/`
- `2-Areas/Patterns/`
- `2-Areas/Glossary/`
- `2-Areas/MOC/`

只有在追溯历史或找外部背景时，才进入：

- `3-Resources/`
- `4-Archives/`
- [审计归档索引](../4-Archives/Audits/INDEX.md)：历史审计、计划与风险台账
- [旧协作归档索引](../4-Archives/Collaboration/INDEX.md)：已退役协作栈的历史材料

## 使用边界

- 普通局部改动默认不展开整个 `Minds`
- `Docs` 与 `Minds` 冲突时，以 `Docs + 源码` 为当前事实基线
- 只有能长期复用的结论才进入 `Minds`
