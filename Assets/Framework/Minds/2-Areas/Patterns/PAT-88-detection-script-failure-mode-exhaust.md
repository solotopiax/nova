---
id: PAT-88
title: 检测脚本必须穷举"失败模式"（v1 漏 → 用户翻车 → v2 补的反模式）
summary: 检测必穷举失败模式 漏一种=用户翻车
category: review
type: pattern
status: active
date: 2026-05-24
aliases:
  - PAT-88-detection-script-failure-mode-exhaust
keywords:
  - PAT-88
  - 检测脚本必须穷举"失败模式"（v1 漏 → 用户翻车 → v2 补的反模式）
  - PAT-88-detection-script-failure-mode-exhaust
tags:
  - pattern
  - review
  - qa
  - methodology
  - detection
related:
  - "[[PAT-87-button-text-visibility-check|PAT-87]]"
  - "[[PAT-85-vault-search-synonym-expansion|PAT-85]]"
---

# PAT-88：检测脚本必须穷举"失败模式"

## 适用场景（When）

- 写任何"扫描脚本"、"校验工具"、"健康检查"、"lint 规则"
- 用 grep / `execute_code` / 正则做批量缺陷检测
- 给 PAT-87 这类"硬校验"扫描清单加新检测项

## 核心做法（What & How）

### 写检测脚本前先列"失败模式清单"

不要先写脚本，先列：**用户视角"看不见 / 用不了 / 出错"的所有具体路径**。

例：「按钮没字」这一用户体验失败，对应的失败模式有：
1. TMP.text == ""（空文字）
2. TMP.color.a < 0.5（透明）
3. TMP.color ≈ 背景色（同色）
4. TMP.fontSize == 0（字号 0）
5. TMP.font == null（字体丢失）
6. TMP.gameObject.activeSelf == false（被禁用）
7. TMP 在屏幕外（RectTransform anchor 跑飞）
8. RectTransform.size = (0, 0)（被 LayoutGroup 压扁）

**铁律**：检测脚本必须**至少覆盖前 3-5 种最常见失败模式**；漏掉的失败模式 = 100% 概率会被用户撞上。

### 命名约定

检测函数 / 报告字段名直接用"失败模式"命名（不是"通过条件"）：

```csharp
// ✅ 好：报告里直接告诉你"哪种失败模式触发了"
return $"Total={total}, Empty={empty}, SameColor={same}, LowAlpha={low}";

// ❌ 坏：只有一个汇总数，要回去看脚本才知道含义
return $"Total={total}, Issues={total - passed}";
```

### 通过线 = 全部失败模式都为零

不允许"少量遗漏可接受"——**任一失败模式 ≥ 1 即 P0**。这是"检测脚本"和"统计脚本"的本质区别：检测脚本服务于"零容忍"，不是"看趋势"。

### 失败模式清单的演进策略

每次用户撞上一个**未覆盖的失败模式** → 立即补进检测脚本，并沉淀到对应的 Pattern 文档。

## 为什么这么做（Why）

- 用户看到的是结果，检测脚本要覆盖的是导致结果的所有失败模式。
- 漏掉一种模式，“通过”就没有意义。
- 失败模式越明确，扫描脚本越不容易漏项。

## 反模式（Anti-patterns）

```text
❌ 反模式 1：写检测脚本前不列失败模式清单，凭"我能想到的"加检测
   后果：覆盖最显眼的 1-2 种，漏掉常见的 3-5 种

❌ 反模式 2：检测漏掉一种失败模式 → 用户翻车 → 加上就完了，不沉淀
   后果：下次写新检测脚本同样的坑还会踩

❌ 反模式 3：报告只给"通过/不通过"汇总，不分失败模式列计数
   后果：维护人不知道"漏的是哪种"，加新检测时重叠或漏盖

❌ 反模式 4："这种 bug 太罕见可以不查"
   后果：所谓"罕见"通常是"我没遇到过"——用户的真实场景比 AI 的想象广得多
```

## 跨项目复用提示

- 静态扫描工具（lint / formatter）的扩展也适用
- Web 项目的 a11y 扫描：「文字看不见」对应的失败模式 = 同色 + 透明 + display:none + visibility:hidden + 父级 overflow:hidden + 字号 0
- 任何"分类型缺陷扫描"都应先做"失败模式枚举"再写代码
