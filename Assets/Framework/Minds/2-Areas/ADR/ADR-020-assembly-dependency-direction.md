---
id: ADR-020
title: 程序集依赖方向单向（Editor → Runtime）
status: accepted
date: 2026-05-18
summary: 程序集单向依赖Runtime禁依赖Editor
category: arch
aliases:
  - assembly-dependency-direction
keywords: [ADR-020, assembly-dependency-direction, 程序集依赖方向单向（Editor → Runtime）]
tags:
  - adr
  - nova
  - framework
  - assembly
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer]]"
  - "[[ADR-016-framework-vs-business-access]]"
---

# ADR-020：程序集依赖方向单向（Editor → Runtime）

## 背景（Context）

Nova 框架代码切分为两份程序集：

| 程序集 | 命名空间 |
|---|---|
| `NovaFramework.Runtime.asmdef` | `NovaFramework.Runtime` |
| `NovaFramework.Editor.asmdef` | `NovaFramework.Editor` |

如果允许 Runtime 引用 Editor，会导致 Player 构建失败、包体膨胀、热更元数据污染和频繁重编。

## 决策（Decision）

**依赖方向单向：`NovaFramework.Editor` 引用 `NovaFramework.Runtime`，反向不成立。**

具体细则：

1. `.asmdef` 中：`NovaFramework.Editor.asmdef` 的 `references` 列表包含 `NovaFramework.Runtime`；`NovaFramework.Runtime.asmdef` 的 `references` **不得**包含 `NovaFramework.Editor`
2. Runtime 代码（`Assets/Framework/Scripts/Runtime/**`）禁止：
   - 引 `using UnityEditor;`
   - 引 `using NovaFramework.Editor.*;`
   - 调任何 `EditorUtil.*` 工具
3. 例外：`#if UNITY_EDITOR` 内可以访问 `UnityEditor.*`，但不得访问 `NovaFramework.Editor.*` 业务工具。
4. Editor 层只能访问 Runtime 的 public API。若需要编辑期诊断内部状态，优先通过 Runtime 显式提供的 public 只读诊断接口 / SPI；少量 Inspector 诊断可用反射读取，但不得用 `InternalsVisibleTo` 扩大程序集可见性。

## 后果（Consequences）

### 正面
- 玩家构建零报错。
- IL2CPP / HybridCLR 范围只落在 Runtime。
- Runtime 重编不触发 Editor 工具重编。
- 静态审查只看 Runtime 目录是否出现 Editor 引用。

### 负面
- Runtime 的编辑期诊断要挪到 Editor 层处理。
- 需要序列化绘制信息时只能走 public 契约、attribute + Editor 端读取，或显式反射读取；不能依赖 friend assembly 访问 `internal`。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 双向引用 + 用 `#if UNITY_EDITOR` 隔离 | 编译期看似可行，但 IL2CPP/HybridCLR 阶段裁剪 / AOT 元数据范围会被污染；且静态审查规则爆炸（每条 Editor 调用都要核对宏） |
| 把 Editor 与 Runtime 合并为一个 asmdef | 失去 Player Build 时自动剔除 Editor 类型的能力；Domain Reload 区间扩大 |
| 引入第三方"接口程序集"作为中介 | 增加一层 asmdef 与维护负担，没解决根本的"Runtime 不应感知 Editor"问题 |

## 验证依据（Verification）

- 静态审查：
  ```bash
  grep -rn "using UnityEditor\b\|using NovaFramework\.Editor" \
    Assets/Framework/Scripts/Runtime/ \
    | grep -v "#if UNITY_EDITOR"
  ```
  应输出空。
- `NovaFramework.Runtime.asmdef.references` 字段不得出现 Editor 程序集名
- `Assets/Framework/Scripts/Runtime/**` 不得出现 `InternalsVisibleTo(...)`
- 玩家构建（任意目标平台）通过编译 = 该决策仍生效
