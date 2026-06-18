---
id: PAT-133
title: Editor 外部 CLI 解析：PATH+候选兜底，版本卡实测闭区间
summary: Editor CLI 解析 PATH 兜底，版本卡闭区间
category: editor
type: pattern
status: active
date: 2026-06-03
aliases:
  - PAT-133-editor-cli-path-resolve-version-range
keywords:
  - PAT-133
  - Editor 外部 CLI 解析：PATH+候选兜底，版本卡实测闭区间
  - PAT-133-editor-cli-path-resolve-version-range
tags: [pattern, methodology, editor, tooling]
related: []
---

# PAT-133：Editor 外部 CLI 解析——PATH+候选兜底，版本卡实测闭区间

## 适用场景（When）

- Editor 工具需调用机器上的外部 CLI 可执行文件（dotnet / python3 / node 等）做代码生成、数据导出、构建。
- 触发信号：写"检测 XX 是否安装""拼 XX 路径跑进程""校验 XX 版本"的 Editor 代码时。

## 核心做法（What & How）

### 路径解析（双管齐下）

1. 先扫 `PATH` 环境变量（按 `Path.PathSeparator` 拆分，逐目录拼可执行名探测）。
2. PATH 未命中 → 遍历**平台候选安装路径**兜底，任一命中即用。

框架两处实证（优先级顺序可因工具而异）：

| 工具 | 入口 | 策略顺序 |
|---|---|---|
| dotnet | `EditorUtil.Luban.CliRunner.ResolveDotnetPath()` | PATH 优先 → 候选兜底 |
| python3 | `EditorUtil.Environment.Python3Checker` | 显式候选(A) → PATH(B) → py launcher → where → python 降级 |

dotnet 候选路径（macOS/Linux）：`/usr/local/share/dotnet/dotnet`、`/usr/local/bin/dotnet`、`/opt/homebrew/bin/dotnet`、`~/.dotnet/dotnet`；Windows：`ProgramFiles/dotnet/dotnet.exe`、`~/.dotnet/dotnet.exe`。

### 版本校验（闭区间，非只卡下限）

- 解析**完整版本号**（`System.Version`，先 strip `-preview`/`-rc` 后缀），按**实测兼容闭区间**判定。
- 例：Luban 要求 .NET SDK `[8.0.127, 10.0.203]`，过低过高均硬阻断，并在状态下方给**锁版本**安装命令（`dotnet-install.sh --channel 8.0` / `winget ... SDK.8`）。

## 为什么这么做（Why）

- **Unity Editor 经 GUI（Finder/Dock/桌面快捷方式）启动时不继承 shell 登录态 PATH**（macOS launchd 尤甚）——只读 PATH 会把"明明装了"误判为"未安装"。
- 外部工具链对宿主 runtime 版本敏感，**只卡下限会放过"过高不兼容"**，跑时才炸，错误现场远离根因。

## 反模式（Anti-patterns）

- **只 `which dotnet` / 只读 PATH 就判"未安装"**：GUI 启动的 Unity 必漏，用户装了也报找不到。
- **版本只卡主版本号下限（`major >= 8`）**：放过过高版本与"补丁过低"，工具在区间外报错却显示"环境就绪"。
- **安装命令给"装最新"（`brew install --cask dotnet-sdk`）**：装出区间外版本，与校验区间自相矛盾；须锁 channel（`--channel 8.0`）。

## 跨项目复用提示

- 路径双管齐下策略与技术栈无关，任何"GUI 进程调外部 CLI"场景可直接搬（Electron / JetBrains 插件同理）。
- 候选路径列表、版本区间值是**项目/工具实测产物**，搬运时须按目标工具重新标定，不可照抄数值。

## 关联

- 规范落点：建议沉淀到统一工程约束或 Editor 工具规范。
- 相关代码：`EditorUtil.Luban.CliRunner` / `EditorUtil.Environment.LubanChecker` / `EditorUtil.Environment.Python3Checker`。
- 本文已吸收相关历史排查结论，不再保留外部协作指针。
