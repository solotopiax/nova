---
id: ADR-016
title: 框架层 vs 业务层访问分层铁律
status: accepted
date: 2026-05-16
summary: 框架与业务访问入口分层各自封装
category: arch
aliases:
  - ADR-016-framework-vs-business-access
keywords: [ADR-016, ADR-016-framework-vs-business-access, 框架层 vs 业务层访问分层铁律]
tags: [adr, nova, architecture, layering]
supersedes: []
superseded-by: []
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-008-managerbase-internal-abstract|ADR-008]]"
  - "[[ADR-017-component-manager-isolation|ADR-017]]"
---

# ADR-016：框架层 vs 业务层访问分层铁律

## 背景（Context）

Nova Framework 同时存在三套访问 Manager 的入口：

1. `Nova.XXX.YYY` 业务聚合器（如 `Nova.Config.DevelopMode`、`Nova.UI.OpenUIView`）
2. `FrameworkManagersGroup.GetManager<IXxxManager>()` 框架内 Manager 互访
3. `XxxComponent.属性` Component 薄封装

分层若不约束，容易出现：

- 框架层代码里写 `Nova.Config.xxx`，把业务层聚合器吸进框架内部，造成反向依赖
- Component 暴露 `public IXxxManager` 属性，业务层穿透 Component 拿到接口，再点字段，破坏分层
- 业务层直接 `FrameworkManagersGroup.GetManager<I...>()`，绕过 Component 设定的边界

## 决策（Decision）

**按调用方所在层判定，不是按被调对象判定：**

| 调用方所在层 | 合法访问路径 | 违规示例 |
|---|---|---|
| **框架层** `Assets/Framework/Scripts/Runtime/**` | Manager → Manager：`FrameworkManagersGroup.GetManager<IXxxManager>()`；Component → Component：不允许 | `Nova.Config.xxx` / `Nova.UI.xxx` 等任何 `Nova.XXX` |
| **业务层** `Assets/Game/**`（含 `Test/`、`Procedures/`、业务 MonoBehaviour） | `Nova.XXX.属性 / 方法`（Component 薄封装 API） | 直接 `FrameworkManagersGroup.GetManager<I...>()` 或引用 `IXxxManager` |

**框架层 Component 对外封装规则：**

- Component 禁止暴露 `IXxxManager` 公开属性（即使返回类型是接口也不行）
- 所有 Manager 字段由 Component 薄封装转发，外部只拿到数据值（如 `DevelopMode`、`AppID`），拿不到接口本身
- Manager 字段改名/新增时同步 Component 转发层

## 后果（Consequences）

### 正面

- 框架层零 `Nova.XXX` 依赖，可独立测试与剥离
- 业务层对接口零感知，Component 封装边界稳定
- Manager 接口签名改动只影响框架内部调用方，业务层零波及

### 负面

- Component 薄代理方法数量随 Manager 接口同步膨胀，代码量增加
- 框架内 Manager 互访必须经 `GetManager<I...>()`，相比直接字段访问多一层间接
- 业务层不能用框架内的 `FrameworkManagersGroup`，调试时难以快速试探内部 API

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 框架内允许使用 `Nova.XXX` 聚合器 | 反向依赖；聚合器初始化时序耦合；DoHManager 等模块踩过坑 |
| Component 暴露 `public IXxxManager` 公开属性 | 业务层穿透 Component 拿接口再点字段，分层封装形同虚设 |
| 业务层走 `FrameworkManagersGroup.GetManager<I...>()` | 业务层引用 `IXxxManager` 内部契约，框架内部重构会炸业务代码 |
| 不分层，统一 `Nova.XXX` 一套 | 无法做框架内 Manager 间高频互访的优化，且无法剥离业务聚合器 |

## 验证依据（Verification）

- grep 关键词（应零命中）：框架层路径下出现 `Nova\.(Config|UI|Event|Asset|...)`
- grep 关键词（应零命中）：业务层路径下出现 `IXxxManager` 或 `FrameworkManagersGroup`
- 历史踩坑：DoHManager 用 `Nova.Config?.ConfigManager?.DevelopMode` 把业务聚合器吸进框架层
- 待修缺口：`Assets/Framework/Scripts/Runtime/Modules/UI/Managers/UIViewManager/Definitions/UIView.Methods.cs:95` 仍存 `Nova.UI.CloseUIView(this)`，需改为 `FrameworkManagersGroup.GetManager<IUIManager>()?.CloseUIView(this)`

## 关联

- 相关 ADR：[[ADR-001-component-manager-three-layer|ADR-001]]、[[ADR-008-managerbase-internal-abstract|ADR-008]]
- 待起草：Component-Manager 隔绝铁律（同期下沉的姊妹 ADR）
