---
id: PAT-83
title: Canvas sortingOrder 越界 Clamp + Error 兜底
summary: int写short字段必Error+Clamp 拒静默截断
category: runtime
type: pattern
status: active
date: 2026-05-24
aliases:
  - PAT-83-canvas-sortingorder-overflow-clamp
keywords:
  - PAT-83
  - Canvas sortingOrder 越界 Clamp + Error 兜底
  - PAT-83-canvas-sortingorder-overflow-clamp
tags:
  - pattern
  - runtime
  - ui
  - canvas
  - defensive-programming
related:
  - "[[ADR-038-ui-depth-factor-rebalance|ADR-038]]"
---

# PAT-83：Canvas sortingOrder 越界 Clamp + Error 兜底

## 适用场景（When）

- 把 `int` 计算结果赋给 `short` / `byte` / `Color32` 等窄类型字段
- 框架层用 `short` 字段（`Canvas.sortingOrder` / `Renderer.sortingOrder` / `SpriteRenderer.sortingOrder` 等）做层级排序
- 任何"配置驱动 + 数值计算 + 写入受限字段"的链路

## 核心做法（What & How）

### 三件套铁律

1. **越界用 `Log.Error` 而非 `Log.Warning`**——配置错误属于不可恢复（用户看不到层级表现错误的根因）
2. **必须 `Mathf.Clamp` 兜底**——不能让 Unity 静默截断（`(short)500000 = -24288` 这种回绕）
3. **错误日志必须给排查指引**——指出"检查 XX 配置或调整 YY 系数"

### 标准模板

```csharp
public virtual void SetDepth(int depth)
{
    int sortingOrder = c_DepthFactor * depth;
    if (sortingOrder < short.MinValue || sortingOrder > short.MaxValue)
    {
        Log.Error(LogTag.UI, Txt.Format(
            "视图分组深度 {0} 计算的 sortingOrder {1} 超出有效范围（{2} ~ {3}），已 Clamp 到边界。" +
            "请检查 UIGroup 配置或调整 UIDepthConfig.c_GroupDepthFactor。",
            depth, sortingOrder, short.MinValue, short.MaxValue));
        sortingOrder = Mathf.Clamp(sortingOrder, short.MinValue, short.MaxValue);
    }
    m_CachedCanvas.sortingOrder = sortingOrder;
}
```

### 关键决策点

| 选择 | 原因 |
|---|---|
| Error 而非 throw | UI 层抛异常会让整帧渲染中断，比层级错乱更严重；记 Error + Clamp 让人能看见错误又能继续运行 |
| Clamp 而非取模 / 截断 | Clamp 后行为可预期（粘在边界）；取模会让深度极大的视图反而显示在最底（语义反转） |
| 日志带"如何修复"的指引 | 错误日志的目标是引导排查；只说"溢出了"等于没说 |

## 为什么这么做（Why）

会话中的真实失败场景：

- 旧实现 `Log.Warning + 直接写 sortingOrder`
- 用户配 `Demo=50, DemoSub=60` → sortingOrder = 500000/600000
- Unity 静默截断到 short 范围 → 层级紊乱
- 用户视角："我配的层级数值完全没生效"——根本看不到 Warning 提示，更找不到根因

**根本逻辑：** 静默失败 (silent failure) 是工程上最难调试的故障。"Warning + 错误数据写入"等于让坑发生在生产环境；"Error + Clamp" 至少把坑暴露在控制台。

## 反模式（Anti-patterns）

```csharp
// ❌ 反模式 1：Warning + 直接写
if (sortingOrder > short.MaxValue) Log.Warning(...);
m_CachedCanvas.sortingOrder = sortingOrder;   // Unity 静默截断

// ❌ 反模式 2：完全无校验
m_CachedCanvas.sortingOrder = c_DepthFactor * depth;

// ❌ 反模式 3：用 throw（UI 层抛异常打断渲染）
if (sortingOrder > short.MaxValue) throw new ArgumentOutOfRangeException();

// ❌ 反模式 4：取模回绕
m_CachedCanvas.sortingOrder = sortingOrder % short.MaxValue;   // 语义反转
```

## 跨项目复用提示

适用于任何 Unity 项目：

- `Canvas.sortingOrder` / `Renderer.sortingOrder` / `SpriteRenderer.sortingOrder`
- `Canvas.sortingLayerID`（int 但有 SortingLayer 全局上限）
- `MeshRenderer.sortingOrder`

非 Unity 场景同样适用："任何 int 计算赋给窄类型字段的边界处置"原则可直接搬到游戏开发外的窄类型 API 边界。

