---
id: PAT-50
title: Unity 原生 ProgressBar 必须 delayCall 推到下一帧
summary: 同步栈内 Clear 不销毁，弹 Toast 会卡死
category: editor
type: pattern
status: active
date: 2026-05-20
aliases:
  - PAT-50-unity-progressbar-must-delaycall
keywords:
  - PAT-50
  - PAT-50-unity-progressbar-must-delaycall
  - Unity 原生 ProgressBar 必须 delayCall 推到下一帧
tags:
  - pattern
  - editor
  - ui
  - pipify
related:
  - "[[ADR-026-pipify-runner-no-batch-locking|ADR-026]]"
---

# PAT-50：Unity 原生 ProgressBar 必须 delayCall 推到下一帧

## 适用场景（When）

任何在 Editor 工具流水线 / 长任务结束阶段调 `EditorUtility.ClearProgressBar()` 并紧接着弹自定义结果窗口（Toast / Dialog）的代码。

## 核心做法（What & How）

```csharp
public void EndBatch(bool success, TimeSpan totalElapsed)
{
    EditorUtility.ClearProgressBar();   // 同步调用，但 Unity 真正销毁窗口要等下一个 editor tick
    string toast = BuildToastText(success, totalElapsed);
    EditorApplication.delayCall += () =>
    {
        EditorUtility.ClearProgressBar();   // 兜底，已无窗口时 no-op
        ToastWindow.Show(toast, success);
    };
}
```

要点：
- `EditorUtility.ClearProgressBar()` 同步返回不代表窗口已经从 EditorWindow 列表里移除
- 紧接着 `Show` 自定义窗口会抢焦点，而 ProgressBar 还在 EditorWindow 列表里没被清掉 → 窗口卡死状态（标题"Progress"、文本"Copy patch file : 1/15"）
- 用 `EditorApplication.delayCall` 推到下一个 editor tick，Unity 已完成窗口生命周期清理后再弹 Toast，干净

## 为什么这么做（Why）

Unity 6000 的 EditorWindow 销毁走异步 message pump。同步 API 只是"标记销毁"，真正从 IMGUI / window list 移除发生在下一帧的 OnGUI 之前。同步调用栈里立刻弹新窗口会让 ProgressBar 永远滞留——这是 IMGUI 框架的固有约束，不是 bug。

## 反模式（Anti-patterns）

- ❌ `ClearProgressBar()` 后同步 `Show` Toast → 进度条卡死
- ❌ 改用 `Repaint()` 或 `Application.MarkDirty()` 试图强刷 → 无效，根因是销毁排队不是绘制问题
- ❌ 改大 ProgressBar 自身的销毁延时 / 异步关闭逻辑 → 它是 Unity 内部窗口，没有公开 API 可调

## 跨项目复用提示

通用于所有 Unity Editor 工具——批处理 / 资源导入 / 构建产物收尾时，凡有自定义结果窗口都要走 delayCall。

