---
id: PAT-104
title: 接口废弃直接删 shim 禁留 [Obsolete] 过渡
summary: 废弃接口同批迁移所有调用点，禁留 [Obsolete] 兼容方法
category: workflow
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-104-no-obsolete-shim-rule
keywords:
  - PAT-104
  - 接口废弃直接删 shim 禁留 [Obsolete] 过渡
  - PAT-104-no-obsolete-shim-rule
tags: [pattern, workflow, refactor, deprecation]
related: []
---

# PAT-104：接口废弃直接删 shim，禁留 [Obsolete] 过渡

## 适用场景（When）

任何 Nova 框架内部 / Demo / 业务侧的**接口废弃**操作，包括但不限于：

- 方法签名重命名 / 拆分 / 合并；
- 字段删除或类型变更；
- 框架基类公开 API 移除。

## 核心做法（What & How）

接口废弃时**一次性完成全量迁移**，不留任何形式的过渡兼容层：

1. **不允许**留 `[Obsolete] public void OldApi(...) { /* 空实现 */ }`；
2. **不允许**留 `[Obsolete("用 NewApi 代替")] public void OldApi(...) => NewApi(...)`；
3. **不允许**留 `#pragma warning disable CS0618` 局部抑制；
4. 一次 PR / 一次提交内：删旧接口 + 改完所有调用点 + 编译通过。

如果迁移工作量过大无法一次完成 → 拆分为**多个完整的小步**（每步内部仍是"删 + 改完"原子化），不是"先留 shim 再慢慢替换"。

## 为什么这么做（Why）

- **shim 一旦留下就不会被删**——团队会忘记、CI 不报错、下游会写新的 SetApiHint 调用，技术债累积；
- **[Obsolete] 警告会被淹没**——Unity 编译器警告海洋里再多一条没人看；
- **codebase 一致性是默认状态而非例外**——禁止"半新半旧"的中间态；
- **重构的勇气来自一次性彻底**——留 shim 等于承认重构没做完。

## 反模式（Anti-patterns）

1. **在基类留 [Obsolete] 空方法兜底子类**：本会话第一波样板时 editor-coder 为遵守"严禁触碰其他 view"红线留了 [Obsolete] shim，被用户当场否决："不要 [Obsolete] 比较，直接删干净"。
2. **"以后再删"的渐进迁移**：永远不会"以后"。
3. **Editor 工具的迁移开关 / 双轨开关**：除非有可量化的灰度需求，否则禁止。
4. **以"兼容业务侧"为由保留 shim**：业务侧也是项目内代码（除非真有外部 SDK 用户），同步迁移。

## 判定速查

| 情况 | 是否允许留 shim |
|---|---|
| 框架内部 + Demo 内部接口废弃 | ❌ 禁 |
| Editor 工具内部接口废弃 | ❌ 禁 |
| 跨业务 DLL 边界（HybridCLR 业务程序集对外的稳定 API） | ⚠️ 单独决策，需明确 deprecation window |
| 已发布到外部用户的 UPM 包公开 API | ⚠️ 走 SemVer，留 deprecation window |

> Nova 当前所有模块都满足"框架 + Demo 内部"性质，**默认禁 shim**。

## 跨项目复用提示

软件工程通用原则——"不留尾巴的重构"。任何项目内部代码均适用，对外 SDK 边界另议。

