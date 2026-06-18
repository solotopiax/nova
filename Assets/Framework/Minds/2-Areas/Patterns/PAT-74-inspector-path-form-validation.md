---
id: PAT-74
title: Inspector 路径输入校验形态先于存在
summary: Inspector 路径校形态不校存在性
category: inspector
type: pattern
status: active
date: 2026-05-23
aliases:
  - PAT-74
  - PAT-74-inspector-path-form-validation
keywords:
  - Inspector 路径输入校验形态先于存在
  - PAT-74
  - PAT-74-inspector-path-form-validation
tags:
  - pattern
  - editor
  - inspector
  - validation
related: []
---

# PAT-74：Inspector 路径输入校验形态先于存在

## 适用场景（When）

Inspector 面板上的「路径输入框」字段——如：

- 数据导出位置（要求是文件路径）
- 类型导出位置（要求是目录路径）
- Asset 地址（任意非空字符串）
- 模板文件路径

凡用户可手动输入或通过文件夹选择面板写入路径的场景，统一适用。

## 核心做法（What & How）

### 三类语义对应三种校验

| 字段语义 | 校验方法 | 合法判据 |
|---|---|---|
| 文件路径（文件可不存在） | `IsValidFilePath` | 非空 + 不以 `/` `\` 结尾 + 文件名形如 `xxx.xxx`（含非空主干 + 非空扩展名） |
| 目录路径（目录可不存在） | `IsValidDirectoryPath` | 非空 + 末段不形如 `xxx.xxx`（不像文件名）|
| 任意非空 | `!string.IsNullOrEmpty` | 仅非空检查 |

### 形态判据细化（文件路径）

- `xxxx`（无点号）→ 非法
- `xxxx.`（点号在末尾）→ 非法
- `xxxx.yyy` → 合法
- `.gitignore`（点号在首位）→ 非法（`dotIndex > 0` 排除）

### 形态判据细化（目录路径）

- `Docs/Tables` → 合法
- `Docs/Tables/` → 合法（自动 trim 末尾分隔符再判）
- `Docs/file.json` → 非法（末段像文件名）
- 目录是否真的存在不影响校验结果

### UI 反馈

校验失败时**仅画红色外框**（不填红背景），通过 `EditorUtil.Draw.SourceFileTree.DrawInvalidBorderForLastRect` 实现，仅在 `EventType.Repaint` 阶段绘制。

## 为什么这么做（Why）

- **形态先于存在**：用户可能先填路径再创建目录/文件，存在性校验会误报
- **职责分离**：路径合法性是 Inspector 的事，目录/文件是否真实存在是导出流水线的事，分层校验避免双重失败
- **红框而非背景**：纯红外框视觉警示足够，红背景影响可读性（用户曾明确反馈）
- **UI 与状态分离**：DrawInvalidBorderForLastRect 仅 Repaint 阶段绘制，避免重复 Layout

## 反模式（Anti-patterns）

- 用 `Util.SysIO.File.Exists` 校验数据导出位置 → 文件还没生成就一直报红
- 用 `Util.SysIO.Directory.Exists` 校验类型导出位置 → 目录尚未手动创建就一直报红
- 校验失败时填红背景 → 视觉过载，影响可读性
- 在 Layout/MouseDown 等非 Repaint 阶段画红框 → 重复 Layout 导致控件位移
- 同一字段语义用不同校验方法 → 用户在不同 Inspector 看到不一致的校验行为

## 跨项目复用提示

适用于任何 IMGUI / Inspector 编辑器扩展中"用户输入路径但路径未必已存在"的场景。Web 表单同理：表单提交前的路径合法性是字符串形态，文件实际可访问性是后端事。

