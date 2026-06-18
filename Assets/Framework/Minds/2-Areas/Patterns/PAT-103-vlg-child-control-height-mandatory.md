---
id: PAT-103
title: VerticalLayoutGroup 动态行容器必开 ChildControlHeight
summary: 动态行 VLG 必开 ChildControlHeight
category: runtime
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-103-vlg-child-control-height-mandatory
keywords:
  - PAT-103
  - VerticalLayoutGroup 动态行容器必开 ChildControlHeight
  - PAT-103-vlg-child-control-height-mandatory
tags: [pattern, ui, layout-group, content-size-fitter, anti-pattern]
related: []
---

# PAT-103：VerticalLayoutGroup 动态行容器必开 ChildControlHeight

## 适用场景（When）

容器满足以下三个条件**任一**时必须强制 `ChildControlHeight = true`：

1. 容器挂 `VerticalLayoutGroup` + `ContentSizeFitter (VerticalFit=PreferredSize)`；
2. 子元素在运行时通过 `Instantiate` 动态追加（行数不固定）；
3. 子元素是 `TMP_Text` 等会自适应高度的组件（preferredHeight 取决于内容）。

典型场景：日志面板、聊天列表、动态展示行的反馈区。

## 核心做法（What & How）

### 反模式配置（重叠 bug）

```yaml
VerticalLayoutGroup:
  childControlWidth:  true
  childControlHeight: false   # ❌ 错点
  childForceExpandHeight: false
ContentSizeFitter:
  verticalFit: PreferredSize
```

### 正确配置

```yaml
VerticalLayoutGroup:
  childControlWidth:  true
  childControlHeight: true    # ✅ 必须开
  childForceExpandHeight: false  # 子行只用 preferredHeight，不强拉伸
ContentSizeFitter:
  verticalFit: PreferredSize
```

要点：

1. `ChildControlHeight=1` 让 LayoutGroup 接管子行高度计算（按子行 preferredHeight 分配）；
2. `ChildForceExpandHeight=0` 阻止 LayoutGroup 把剩余空间均分给子行（否则单行会被拉到容器满高）；
3. ContentSizeFitter 的 verticalFit 才能拿到正确的 preferredSize 累加值。

## 为什么这么做（Why）

`ChildControlHeight=0` 时：

- LayoutGroup 不计算子行高度，**直接读子行 RectTransform 的 sizeDelta.y**；
- 动态 Instantiate 出来的子行 sizeDelta.y 默认是 0；
- 于是每行 height=0、anchoredPosition.y 全是 0；
- ContentSizeFitter 累加得到 content 总高也是 0；
- **视觉表现：所有日志行叠在容器顶部同一像素**。

`ChildControlHeight=1` 时 LayoutGroup 会主动 `LayoutRebuilder.ForceRebuildLayoutImmediate`，按 TMP preferredHeight 分配每行高度并依次堆叠。

## 反模式（Anti-patterns）

1. **静态固定高度的 LayoutGroup 容器开 ChildControlHeight=1**：会覆盖已设置好的 sizeDelta，按钮排布会被改写。动态行容器与固定高度容器必须分开判断。
2. **以为 ContentSizeFitter 自己会算 child 高度**：CSF 只读 LayoutGroup 输出，LayoutGroup 不算就是 0。
3. **改完不验证 Play Mode**：静态 prefab 看不出问题，必须真追加 ≥3 行。

## 判定速查

| 场景 | ChildControlHeight |
|---|---|
| 子元素动态实例化 + TMP 自适应高度 | **1** |
| 子元素是固定 sizeDelta 的按钮 / 静态卡片 | **0** |
| 子元素混合（部分动态部分固定） | 拆成两个容器，分别配置 |

## 跨项目复用提示

通用 Unity UGUI 反模式。任何动态行列表（聊天、日志、邮件、商品列表）都适用。
