---
id: PAT-45
title: 大规模重命名落地后必须 grep 局部变量名
type: pattern
status: active
date: 2026-05-19
summary: 大规模重命名后必须grep局部变量旧名
category: naming
aliases:
  - PAT-45-rename-grep-local-variables
keywords:
  - PAT-45
  - PAT-45-rename-grep-local-variables
  - 大规模重命名落地后必须 grep 局部变量名
tags:
  - pattern
  - refactor
  - naming
  - code-review
related:
  - "[[PAT-04-read-what-you-change|PAT-04]]"
---

# PAT-45：大规模重命名落地后必须 grep 局部变量名

## 适用场景（When）

执行模块级 / 类级重命名（如 Launch → App、UserService → AccountService）后，做最终验证。

特别是 IDE 的"重命名符号"或 grep replace 完成、编译通过、静态审查通过、运行时验证通过——**仍可能漏掉局部变量名**。

## 核心做法（What & How）

### 第 1 步：识别"未对齐"局部变量名

重命名 X→Y 后，全工程 grep 旧名 X 与旧名 X 的小驼峰形式 `xCamel`：

```bash
grep -rn "Launch\|launch" Assets/ --include="*.cs"
```

特别关注**局部变量声明**模式：

```csharp
// 编译能过，但变量名是旧语义残留——必须改
AppComponent launchComponent = FrameworkComponentsGroup.GetComponent<AppComponent>();
//           ^^^^^^^^^^^^^^^ 旧名残留
```

### 第 2 步：分类残留位置

| 位置 | 是否必改 | 工具 |
|---|---|---|
| 类型声明 / 字段 / 方法签名 | ✅ 必改 | IDE rename / grep |
| 调用点 (`x.OldMethod()`) | ✅ 必改 | 编译器报错 |
| **局部变量名** | ✅ 必改但**编译不报错** | **手动 grep**！ |
| 注释中的旧名 | ✅ 必改 | grep 注释行 |
| 字符串字面量（`"NovaFramework.Runtime.LaunchManager"`） | ✅ 必改 | grep 字符串 |
| 文档 .md | ✅ 必改 | grep .md |
| 测试 fixture 名 | ✅ 必改 | grep 测试目录 |

### 第 3 步：拆子任务时显式声明

把大规模重命名拆给子任务时，任务描述里必须含：

> 重命名落地后请 grep 旧名残留：
> 1. 类型 / 字段 / 方法签名（编译器会兜住，但顺手验证）
> 2. **局部变量名**（编译不报错，必须手动检查）
> 3. 字符串字面量
> 4. 注释

### 第 4 步：静态审查默认补一刀

静态审查阶段执行：

```bash
# 重命名 X → Y
grep -rn "\bx[A-Z]\|\bX[a-z]" Assets/ --include="*.cs" | grep -v "// "
```

任何匹配项 → 检查是否是漏掉的旧名。

## 为什么这么做（Why）

### 本会话踩坑

Launch → App 重命名后，runtime-coder 完成代码改动，编译 0 error 0 warning，code-reviewer 静态审查 PASS，qa-reviewer 运行时 PASS——**但 ProcedureAppDownload.cs 中残留 4 处局部变量名 `launchComponent`**：

```csharp
// 残留：变量类型已是 AppComponent 但名字还叫 launchComponent
AppComponent launchComponent = FrameworkComponentsGroup.GetComponent<AppComponent>();
AppDownloadRoute route = launchComponent.DownloadRoute;
```

用户截图后才发现。修复后编译仍然 0 error 0 warning——**编译器和测试都不会兜住这类问题**。

### 局部变量名特殊性

- 编译器只校验类型，不校验语义。`SomeType oldNameVar` 编译永远过。
- IDE rename 工具针对类型/方法/字段，**对局部变量声明的拼写不敏感**。
- 测试只验证行为，不验证可读性。
- 静态审查时容易因为类型已对而漏看变量名。

### 长期成本

局部变量名不一致会让阅读者疑惑："这函数里又有 launchComponent 又有 AppComponent，到底是什么关系？" 引发后续维护者错误推断或误改。

## 反模式（Anti-patterns）

```csharp
// ❌ 类型已改，变量名是旧名
AppComponent launchComponent = ...;
NewService oldServiceRef = ...;

// ❌ 拆子任务重命名时只说"重命名 X 到 Y"，不显式列残留检查清单

// ❌ 静态审查只看类型签名变化，不 grep 旧名小驼峰
```

```csharp
// ✅ 完整对齐
AppComponent appComponent = FrameworkComponentsGroup.GetComponent<AppComponent>();

// ✅ 子任务说明
// "重命名后 grep 旧名 + 旧名小驼峰，特别检查局部变量"
```

## 跨项目复用提示

完全可复用。所有静态语言（Java/C#/Go/TypeScript）的大规模重命名都有同样问题。建议:
- 把 `grep -rn "\bOldName\|\boldName" src/` 加进 PR 模板的 checklist
- 在 code-review 工具中加个 lint 规则：变量名与类型名"语义距离"过远时告警
