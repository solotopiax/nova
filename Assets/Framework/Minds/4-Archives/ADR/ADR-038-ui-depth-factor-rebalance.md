---
id: ADR-038
title: UI 深度系数容量再平衡（GroupFactor 1000 / ViewFactor 10）
summary: Factor 1000/10 平衡 short 容量
category: runtime
status: superseded
date: 2026-05-24
aliases:
  - ADR-038-ui-depth-factor-rebalance
keywords:
  - ADR-038
  - UI 深度系数容量再平衡（GroupFactor 1000 / ViewFactor 10）
  - ADR-038-ui-depth-factor-rebalance
tags: [adr, runtime, ui, canvas, sortingorder]
supersedes: []
superseded-by:
  - "[[ADR-041-ui-depth-factor-to-inspector|ADR-041]]"
related:
  - "[[PAT-83-canvas-sortingorder-overflow-clamp|PAT-83]]"
---

# ADR-038：UI 深度系数容量再平衡

## 背景（Context）

`UIDepthConfig` 原配置：

```csharp
public const int c_GroupDepthFactor = 10000;
public const int c_ViewDepthFactor  = 100;
```

注释明文：「分组深度上限约为 3（10000 × 3 = 30000）」。

`Canvas.sortingOrder` 字段类型是 `short`（-32768 ~ 32767），任何超出 short 范围的 int 赋值都会被 Unity 静默截断。`UIGroupHelperBase.SetDepth` 只 `Log.Warning` 不修正，配置错了直接表现为"层级失效"。

实地验证：MainDemo 的 `Nova.prefab` 配 `Demo=50, DemoSub=60`，sortingOrder = 500000 / 600000 远超 short.MaxValue → 层级混乱。

**容量分配失衡：** 把绝大部分容量塞进了"组间隔"，结果反而限死了"组数量"——3 组上限对中型项目（Background / Normal / Modal / Tip / System / Loading / HUD…）远不够。

## 决策（Decision）

调整为 **GroupFactor=1000 / ViewFactor=10**：

| 参数 | 原值 | 新值 |
|---|---|---|
| `c_GroupDepthFactor` | 10000 | **1000** |
| `c_ViewDepthFactor` | 100 | **10** |

**容量校验：**

| 维度 | 上限 | 推算 |
|---|---|---|
| 分组数量 | 32 | 32 × 1000 = 32000 < 32767 |
| 单组内 view 数 | 100 | 100 × 10 = 1000 = 一组间隔 |
| 单 view 内子 Canvas 偏移 | 10 | base+9 < base+10（下一 view 基线） |

**附带改动：** Nova.prefab UIGroup 配置同步降为 `Menu=0, Demo=1, DemoSub=2`。

## 后果（Consequences）

### 正面
- 中型项目 ≤ 32 组都能正确分层，不撞 short 上限
- 单组容纳 100 view 仍宽松（实际并存 < 20）
- view 内子 Canvas 仍可叠 10 层（足够 Modal/ScrollView/Tooltip 嵌套）

### 负面
- 旧 prefab 中曾配 depth=5/10/50 等数值的 UIGroup 必须同步降到新上限以下
- 业务侧若有引用 `c_GroupDepthFactor` 数值的代码会受影响（grep 已确认仅 UIGroupHelperBase / UIView 两处使用）

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 100 / 1 | 子 Canvas 没有偏移空间（差 1 就撞下一 view），完全压死 view 内层级灵活度 |
| 1000 / 100 | 每组只容 10 view（10×100=1000），太紧；Modal 队列叠 5~8 个就到顶 |
| 保留 10000 / 100 | 组上限只有 3，中型项目根本不够用 |
| 改用 int 自定义比较器代替 sortingOrder | 改造面巨大，背离 Unity 原生 Canvas 排序机制 |

## 验证依据（Verification）

- 文件：`Assets/Framework/Scripts/Runtime/Modules/UI/Managers/UIManager/Definitions/UIDepthConfig.cs`
- 调用点：`UIGroupHelperBase.SetDepth`（line 71-89）、`UIView.OnDepthChanged`（line 175-192）
- 配置点：`Assets/Framework/Prefabs/Nova.prefab` UIGroups
- grep：`grep -rn "c_GroupDepthFactor\|c_ViewDepthFactor" Assets/Framework/Scripts/`（仅 2 处使用）
- 编译验证：UnityMCP `read_console` 0 error / 0 warning

## 来源（Origin）

- 会话日期：2026-05-24
- 关键对话节选：
  > 用户："你要评估这个设定是否合理，是否可以适当降低值的定义"
  > AI 推导：在 short 容量内做"组×视图×子层"三段平衡，定 1000/10 方案
  > 用户："可以，开始吧"

## 关联

- 配套 Pattern：[[PAT-83-canvas-sortingorder-overflow-clamp|PAT-83]]（越界 Clamp + Error 兜底）
- 规则文件：影响 `framework-component-manager-architecture.md`（数值外部化原则在 UI 模块的具体落地）
