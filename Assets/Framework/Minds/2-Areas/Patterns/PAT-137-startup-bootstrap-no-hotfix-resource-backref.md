---
id: PAT-137
title: 启动前必需依赖不得反向依赖热更后资源
summary: bootstrap 依赖不可反向绑定热更后资源 否则形成启动闭环
category: hotfix
type: pattern
status: active
date: 2026-06-08
aliases:
  - PAT-137-startup-bootstrap-no-hotfix-resource-backref
keywords:
  - PAT-137
  - 启动前必需依赖不得反向依赖热更后资源
  - PAT-137-startup-bootstrap-no-hotfix-resource-backref
tags: [pattern, startup, hotfix, asset, architecture]
related:
  - "[[ADR-025-yooasset-url-template-placeholders|ADR-025]]"
  - "[[ADR-051-launch-asset-slice-strategy|ADR-051]]"
  - "[[PAT-43-optional-remote-check-tolerance|PAT-43]]"
---

# PAT-137：启动前必需依赖不得反向依赖热更后资源

## 适用场景（When）

- 某配置、地址、开关、白名单或路由信息必须在 `BootstrapAsync()`、版本检查、热更下载之前就可用。
- 方案评审中打算把这类启动前依赖改为“从表里查”“从热更配置里解”“从运行时资源里解析”。
- 发现某项设计最终不得不**回退**到更直接的形态（如 key 回退为 URL），需要判断这是不是机制红线而不只是临时返工。

## 核心做法（What & How）

1. **先判定该依赖是不是 bootstrap 依赖**  
   只要它决定“如何开始热更 / 去哪里取资源 / 是否能继续启动”，就按 bootstrap 依赖处理。
2. **bootstrap 依赖必须来自启动前即可读取的真相源**  
   允许：
   - Inspector / Prefab 直填
   - 包内静态配置（如 StreamingAssets / Resources / 原生注入）
   - 编译宏 / `Application.version` 这类启动前可得值  
   不允许：
   - 热更后资源表
   - 需要 `Asset` 加载后才能拿到的配置资产
   - 依赖 `LoadManifestAsync` / 资源更新完成后才就绪的数据
3. **一旦发现闭环，优先回到更早层的直接配置形态**  
   不要继续在当前层补桥接、补兜底、补“先假设表已就绪”的特殊逻辑。

## 为什么这么做（Why）

### Asset 热更地址回退事件

本次 `Asset` 面板一度尝试把热更主/备地址从直接 URL 改成 `cmdName`。表面目标是统一“业务定义 cmd、框架只桥接”的理念，但启动链路一展开就发现：

```text
要热更
-> 先要热更地址
-> 先要 cmdName 对应 URL
-> 先要业务 cmd 表
-> 先要资源加载 / 热更完成
```

这会形成**bootstrap 依赖反向绑定热更后资源**的闭环。结果不是“实现麻烦一点”，而是设计本身走不通。

因此本次不是普通回退，而是在守住一条更高优先级机制：

> 启动前必须可得的依赖，不能再反向依赖热更后资源。

`Asset` 热更地址最终只能回退为直接 URL；`cmdName` 方案被否，不是因为命名不好，也不是因为桥接代码不优雅，而是因为它违反了启动拓扑。

## 反模式（Anti-patterns）

| 反模式 | 表面收益 | 真正后果 |
|---|---|---|
| 把热更地址改成运行时表 key（如 `cmdName`） | 看起来统一业务配置入口 | 启动形成闭环，设计走不通 |
| 发现闭环后继续补“启动前临时解析表”的兜底 | 看起来能兼容新方案 | 把架构红线伪装成实现复杂度，后续继续误用 |
| 把“回退到 URL”理解成普通返工 | 看起来只是撤回改动 | 丢失“为什么只能这样做”的长期知识，后续重复踩坑 |

## 跨项目复用提示

- 这条规则不只适用于 `Asset` 热更地址。凡是启动前的版本检查 URL、首包路由、灰度入口、必需鉴权地址、预下载种子配置，都应先问一句：  
  **它是不是在热更前就必须可得？**
- 如果答案是“是”，那它就不该再依赖热更后资源。
- 发生回退行为时，要额外检查：本次回退守住的是不是长期机制；若是，应优先沉淀进 `Minds`，而不是只改代码和 `Docs`。

## 关联

- 基础占位符约束：[[ADR-025-yooasset-url-template-placeholders|ADR-025]]
- 启动期热更策略边界：[[ADR-051-launch-asset-slice-strategy|ADR-051]]
- 启动期可选远端检查容错：[[PAT-43-optional-remote-check-tolerance|PAT-43]]
