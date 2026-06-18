---
id: PAT-87
title: 按钮文字可见性硬校验（空/同色/透明/位置 四检）
summary: 按钮 TMP 四检：空+同色+透明+位置外溢 0 容忍
category: review
type: pattern
status: active
date: 2026-05-24
aliases:
  - PAT-87-button-text-visibility-check
keywords:
  - PAT-87
  - 按钮文字可见性硬校验（空/同色/透明/位置 四检）
  - PAT-87-button-text-visibility-check
tags:
  - pattern
  - review
  - ui
  - qa
  - color
  - visibility
related:
  - "[[PAT-80-demo-view-pure-color-style|PAT-80]]"
  - "[[PAT-85-vault-search-synonym-expansion|PAT-85]]"
---

# PAT-87：按钮文字可见性硬校验

## 适用场景（When）

- 任何批量修改 UI 颜色或新建 BaseDemoView 派生 Variant 后。
- code-reviewer / qa-reviewer 审查 UI 改动时。

## 核心做法（What & How）

### 校验脚本（必跑）

UnityMCP `execute_code` 跑下面的"按钮四检"——**Empty / SameColor / LowAlpha / OutOfBounds 任一非零立即标 P0**：

```csharp
var prefabGuids = UnityEditor.AssetDatabase.FindAssets("t:Prefab", new[] { "<UI Prefab 根目录>" });
int totalBtn = 0, emptyBtn = 0, sameColor = 0, lowAlpha = 0, outOfBounds = 0;
var issues = new System.Collections.Generic.List<string>();
foreach (var guid in prefabGuids)
{
    var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
    var contents = UnityEditor.PrefabUtility.LoadPrefabContents(path);
    try
    {
        foreach (var btn in contents.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            totalBtn++;
            var tmp = btn.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            var bg = btn.GetComponent<UnityEngine.UI.Image>();
            if (tmp == null) { issues.Add("NO_TMP " + path + "::" + btn.name); continue; }
            // 检 1：空文字（IsNullOrWhiteSpace 同时盖 null/""/全空格）
            if (string.IsNullOrWhiteSpace(tmp.text))
            { emptyBtn++; issues.Add("EMPTY " + path + "::" + btn.name); }
            // 检 2：低透明度
            if (tmp.color.a < 0.5f)
            { lowAlpha++; issues.Add("LOWALPHA " + path + "::" + btn.name); }
            // 检 3：背景色与文字色同色（RGB 三通道差值 < 0.1 视为肉眼不可分）
            if (bg != null)
            {
                var fg = tmp.color; var b = bg.color;
                if (System.Math.Abs(b.r - fg.r) < 0.1f && System.Math.Abs(b.g - fg.g) < 0.1f && System.Math.Abs(b.b - fg.b) < 0.1f)
                { sameColor++; issues.Add("SAMECOLOR " + path + "::" + btn.name + " bg=" + b + " fg=" + fg); }
            }
            // 检 4：位置外溢——TMP 是 button 直接子节点时，必须 stretch (0,0)-(1,1) + pos=(0,0)
            if (tmp.transform.parent == btn.transform)
            {
                var rt = tmp.GetComponent<UnityEngine.RectTransform>();
                bool isStretch = rt.anchorMin == new UnityEngine.Vector2(0, 0) && rt.anchorMax == new UnityEngine.Vector2(1, 1);
                bool zeroPos = rt.anchoredPosition == UnityEngine.Vector2.zero;
                if (!isStretch || !zeroPos)
                { outOfBounds++; issues.Add("OOB " + path + "::" + btn.name + " anchor=" + rt.anchorMin + "-" + rt.anchorMax + " pos=" + rt.anchoredPosition); }
            }
        }
    }
    finally { UnityEditor.PrefabUtility.UnloadPrefabContents(contents); }
}
return $"Total={totalBtn}, Empty={emptyBtn}, SameColor={sameColor}, LowAlpha={lowAlpha}, OutOfBounds={outOfBounds}\n" + string.Join("\n", issues);
```

### 通过线

`Empty == 0 && SameColor == 0 && LowAlpha == 0 && OutOfBounds == 0` 才算通过。任何非零条目立即修，不许"少量遗漏可接受"。

### 四检语义

| 检测项 | 意义 | 兜底原则 |
|---|---|---|
| **Empty** | TMP.text 为空 / 全空格 / null | 按钮**必有显式文字**；状态展示文本（FpsText/StatsText 等运行时填充）给"--"占位避免编辑期被误判 |
| **SameColor** | 背景与文字 RGB 三通道差值 < 0.1 | PAT-80 颜色规范 = 应该这样；本扫描 = 实际这样 |
| **LowAlpha** | TMP.color.a < 0.5 | 低 alpha 文字看似有内容实际看不见，等于无字 |
| **OutOfBounds** | 直接子节点 TMP 的 RectTransform 不是 stretch 或 pos ≠ (0,0) | 按钮内 TMP 必须 anchorMin=(0,0)/anchorMax=(1,1)/pivot=(0.5,0.5)/pos=(0,0)/sizeDelta=(0,0) 满父级；任何"中心锚定 + 绝对坐标"会导致文字漂出按钮（典型表现：anchor=(0.5,0.5) + pos=(-384, -1368)） |

### Variant 继承坑（重点）

Unity Prefab Variant 默认继承基 Prefab 的颜色 override。改 BaseDemoView 时若**只修了"按钮背景"没修"按钮文字"**，文字字段会被 Variant 视作"未 override"继承父级——结果父级白色，新背景白色 → 35 个 Variant 同时白底白字。

**反推链**：发现单个 Variant 白底白字 → 90% 概率是基 Prefab（BaseDemoView / DemoNodeItemView）的问题，先去基 Prefab 修，比一个个 Variant 改高效。

### 联动 PAT-80

PAT-80 规定"按钮 = 白底黑字"。本 PAT 是**校验闸门**——颜色规范是"应该这样"，可见性扫描是"实际这样"。两者必须配套：
- 改了 PAT-80 颜色 → 必跑本 PAT 扫描
- 本 PAT 扫描发现违规 → 按 PAT-80 规则修

## 为什么这么做（Why）

颜色规范、文本内容、透明度和位置外溢都要纳入扫描。单靠肉眼或单项检查，都会留下用户可见的漏项。

## 反模式（Anti-patterns）

```text
❌ 反模式 1：改完颜色不跑扫描，凭"我都按 PAT-80 改的"信心交付
   后果：Variant 继承坑 / 漏改的死角 / 跨基类 Prefab 的隐式继承

❌ 反模式 2：扫描发现违规，但因"只是少量按钮没字"放行
   后果：用户启动看到的就是这"少量"，零容忍

❌ 反模式 3：只查按钮背景或只查按钮文字，不交叉判定
   表现：白色背景查 N 个、黑色文字查 M 个，但没算"背景==文字"
   后果：白底白字漏掉

❌ 反模式 4：发现一个 Variant 违规直接改 Variant，不去基 Prefab 找根因
   后果：35 个 Variant 改 35 次，工作量爆炸 + 漏一个继续白底白字

❌ 反模式 5：扫描清单只查颜色不查"文字内容"
   表现：白底白字能查出，但 text="" 完全漏掉
   后果：第二轮翻车——143 按钮中 61 个空文字从未被检测，用户开 Inspector 才发现
   修正：检测项必须覆盖"看不见字"的所有路径——空文字 / 同色 / 低 alpha 三检全跑

❌ 反模式 6：状态展示文本（FpsText/StatsText）默认空白
   后果：编辑期被三检误标 P0；无法区分"待运行时填充"和"漏填 bug"
   修正：编辑期给 "--" 占位，运行时代码 SetText 覆盖

❌ 反模式 7：扫描清单不查"位置 / 锚点 / 父级关系"
   表现：内容/颜色/透明度都对，但 anchor=(0.5,0.5) + pos=(-384,-1368) 漂出按钮
   后果：第三轮翻车——143 按钮全部 RT 错位、视觉看不见，但前 3 检全过
   修正：补 OutOfBounds 检测——按钮直接子节点 TMP 必 stretch + pos=(0,0)
```

## 跨项目复用提示

- 任何 Unity UI 项目的 code-review / QA 都该把这条扫描挂上去
- 非 Unity 项目（Web/Flutter）同理：CSS 颜色规则定了之后跑 contrast 扫描（WCAG AA ≥ 4.5:1）
- 颜色对比度可升级为 WCAG 标准：纯 RGB 差值 < 0.1 是粗筛，正式可计算亮度对比比
