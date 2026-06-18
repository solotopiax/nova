---
id: ADR-017
title: Component 必须对外完全隔绝持有的 Manager
status: accepted
date: 2026-05-16
summary: Component对Manager完全隔绝只通过接口
category: arch
aliases:
  - ADR-017-component-manager-isolation
keywords: [ADR-017, ADR-017-component-manager-isolation, Component 必须对外完全隔绝持有的 Manager]
tags: [adr, nova, architecture, encapsulation]
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-016-framework-vs-business-access|ADR-016]]"
---

# ADR-017：Component 必须对外完全隔绝持有的 Manager

## 背景（Context）

Nova 三层架构中，`XXXComponent` 是 Unity 入口，`XXXManager` 是纯 C# 容器。如果 Component 把它持有的 Manager 字段通过 `public IXxxManager Manager => m_Manager` 暴露给外界，会出现：

- 调用方写 `XXXComponent.Manager.Xxx()`，绕过 Component 这一层封装
- Component 失去"边界守门员"的意义，变成"裸壳容器"
- Manager 内部接口签名改动，外部代码全炸（按层独立演化的目标失败）

## 决策（Decision）

**XXXComponent 中的 XXXManager 字段永远对外完全隔绝。** 所有外部调用必须经 Component 同名薄代理转发。

| 项 | 规则 |
|---|---|
| **修饰符** | Component 内 Manager 字段一律 `private` |
| **不允许** | `public Manager` 属性、`internal` 暴露、`public IXxxManager` 返回值 |
| **代理方式** | Component 公开同名方法薄代理转发：`public UniTask BootstrapAsync(LaunchConfig launch, CancellationToken ct = default) => m_AssetManager.BootstrapAsync(launch, ct);` |
| **方法命名** | Component 名已隐含模块名，禁止重复前缀。`AssetComponent.BootstrapAsync()` 而非 `BootstrapAssetsAsync()`，`LaunchComponent.LoadConfigAsync()` 而非 `LoadLaunchConfigAsync()` |

## 后果（Consequences）

### 正面

- Manager 接口改动只影响 Component 内部转发层，外部零波及
- Component 真正成为"模块对外窗口"，其余分层封装规则（如「框架层 vs 业务层访问分层」）才有意义
- API 签名一致性：模块对外能力 = Component 公开方法集合
- Inspector 观察 Component 时，Manager 不会作为公开属性出现

### 负面

- 每个 Manager 接口方法都需要 Component 写一份薄代理，代码量翻倍
- Manager 接口扩展时必须同步 Component 转发层，否则外部拿不到新能力
- 调试时无法在外部代码里直接探测 Manager 内部状态

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| `public IXxxManager Manager` 属性 | 调用方穿透 Component 拿接口再点字段，分层失败 |
| `internal IXxxManager Manager`（asmdef 内放行） | 同 asmdef 业务代码仍能拿到 Manager 直访，破坏 Component 唯一窗口的语义 |
| 不做封装，直接全 public | Manager 内部实现成为公开契约，重构成本爆炸 |

## 验证依据（Verification）

- grep 关键词（应零命中）：`public IXxxManager` 在 Component 类内
- grep 关键词（应零命中）：外部代码出现 `XXXComponent.Manager.Xxx`
- 当前现状（已核查）：`AssetComponent.Visitors.cs:43`、`ConfigComponent.Visitors.cs:48` Manager 字段均 `private`，对外靠薄代理属性暴露
- 历史用户原话："XXXComponent 中的 XXXManager 永远是对外隔绝的！"

## 关联

- 相关 ADR：[[ADR-001-component-manager-three-layer|ADR-001]]
- 姊妹 ADR：框架层 vs 业务层访问分层铁律（同期下沉）
