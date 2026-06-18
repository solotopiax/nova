---
id: PAT-117
title: ScrollRect 不滑动先核对 Viewport vs Content 尺寸
summary: 滑不动先量 Viewport vs Content 尺寸
category: runtime
type: pattern
status: active
date: 2026-05-27
aliases:
  - PAT-117
keywords:
  - PAT-117
  - ScrollRect 不滑动先核对 Viewport vs Content 尺寸
tags: [pattern, ui, scrollrect, layout, debug]
related:
  - "[[PAT-103-vlg-child-control-height-mandatory|PAT-103]]"
  - "[[PAT-66-no-handcraft-prefab|PAT-66]]"
---

# PAT-117：ScrollRect 不滑动先核对 Viewport vs Content 尺寸

## 适用场景（When）

UI 上 ScrollRect 配置看起来正确（Viewport 有 Mask、Content 挂了 VLG + ContentSizeFitter、ScrollRect 引用都对），但运行/编辑器里就是滑不动；尤其是"内容明明很多条，应该能滑"这种直觉与现实不符的情况。

## 核心做法（What & How）

排查第一步：**量 Viewport 和 Content 的实际渲染尺寸**，而不是先去查 ScrollRect / Mask / drag 配置。

判定：

| Viewport vs Content | 含义 | 处理 |
|---|---|---|
| Viewport.height ≥ Content.height | 物理无溢出，ScrollRect 永远不会滑 | 缩 Viewport（改父容器 anchor / sizeDelta），或扩 Content（多塞行/调 padding） |
| Viewport.height < Content.height | 应该能滑，再去查 ScrollRect 配置 | 检查 movementType / horizontal/vertical 开关 / Mask Image / RaycastTarget |

测量办法：

- UnityMCP / IMGUI Inspector 上读 RectTransform 的 `rect.size`（不是 `sizeDelta`，stretch anchor 下两者不同）
- 或运行期 `Debug.Log($"V={viewport.rect.height} C={content.rect.height}")`

## 为什么这么做（Why）

这类问题最常见的误判是：

- 表象上像是 ScrollRect / Mask / Drag 配置错误；
- 但真正原因往往是 Viewport 比 Content 更大，物理上没有溢出；
- 这时继续改 ScrollRect 配置只会绕圈，必须先回到尺寸事实。

只看 ScrollRect 配置永远查不出来，因为配置确实没错。

## 反模式（Anti-patterns）

```
❌ 滑不动 → 直接改 ScrollRect.movementType（治不了根因）
❌ 滑不动 → 怀疑 Mask 没生效（Mask 与"能否滑"无关，只与"能否裁切"有关）
❌ 滑不动 → 加 EventSystem / GraphicRaycaster 排查（点击是另一个维度）
❌ 凭"Inspector 里 sizeDelta=0"就以为 Viewport 是默认大小（stretch anchor 下 sizeDelta 是 delta 不是绝对值）
✅ 先量 Viewport.rect.height vs Content.rect.height，不到 1:1 再去查 ScrollRect 配置
```

## 跨项目复用提示

通用 Unity UI 问题，与 Nova 无关。任何用 ScrollRect 的项目都适用。
推广到一般定理：**布局类 bug 排查先量尺寸，再查配置**——尺寸是事实，配置是意图，事实跟意图不一致时，意图永远输。
