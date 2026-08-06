---
id: PAT-140
title: UPM 包依赖与 sample 依赖的边界
summary: deps 管安装、asmdef 管编译，sample 依赖须声明
category: module
type: pattern
status: active
date: 2026-07-03
source: cur-session
aliases:
  - PAT-140-upm-package-vs-sample-dependency
keywords:
  - PAT-140
  - UPM依赖
  - Sample依赖
  - asmdef引用
tags: [pattern, module, upm, sample, dependency]
related: []
---

# PAT-140：UPM 包依赖与 sample 依赖的边界

## 适用场景（When）

- 某 Kit 包的运行时代码不引用另一个 Kit，但其 sample（示例工程）为演示完整流程引用了另一个 Kit。
- 纠结"要不要在 package.json dependencies 加这个依赖"时。
- 判断某处依赖该写进 package.json 的 `dependencies` 还是 asmdef 的 `references`。

## 核心做法（What & How）

三个机制层各管一件事，不可混用：

- **package.json `dependencies`**：管「UPM 包安装期解析」——安装本包时包管理器自动拉取哪些其他包。是**包级强制**（所有安装者都拉）。
- **asmdef `references`**：管「编译期程序集链接」——编译器实际链接哪些程序集。编译时对应程序集必须在工程里。
- **`Samples~`（带波浪号）**：Unity 约定，里面内容**默认不编译**，只有用户在 Package Manager 点 Import 到 `Assets/` 后才参与编译。

由此得出判定规则：

1. **包运行时代码是否真引用另一个包** → 是才在 package.json dependencies + asmdef references **双向声明**（ADR-020/ADR-031 铁律）；零引用则**不加**，否则是假依赖、且强迫所有安装者拉不需要的包。
2. **只有 sample 引用另一个包** → 该依赖属「示例层」，**不该**写进包的 dependencies（会强迫纯用运行时的用户也装）。sample 的 asmdef references 保留引用即可。
3. **sample 跨包依赖 UPM 零自动机制**：Unity 的 `samples` 字段 schema 只有 `displayName`/`description`/`path` 三个字段，**没有** dependencies、没有"Import 时检查/自动装某包"能力。sample 引用的外部程序集，UPM 不会在 Import 时校验或提示——缺了直接编译报错。只能靠 README / nova-samples.json description **文字提示**（纯人读，无执行力）。

## 为什么这么做（Why）

- 包 dependencies 一旦加，任何安装者都被强制拉——用"强制依赖"满足"可选按需"（Import sample 时才需要）是错配，且破坏包的职责独立性（如绑定包不该被登录包绑死，见 [[ADR-067-login-bind-save-separation|ADR-067]]）。
- Samples~ 默认不编译，所以平时缺依赖不报错、不占安装；只有 Import 后才暴露——这是 Unity Package Sample 的固有特性，绕不过。

## 反模式（Anti-patterns）

- 为了让 sample import 后能编译，把 sample 才需要的包塞进运行时包的 dependencies → 破坏包独立性。
- 以为"asmdef 关联了就能用，不用写 dependencies" → 对：Samples~ 默认不编译故平时不报错；错：UPM 不会替 sample 自动拉这个依赖，用户得自己装。
- 以为 nova-samples.json / README 的依赖提示是"机制" → 它只是文字，UPM/Unity 不解析，用户不读照样编译失败。

## 跨项目复用提示

Unity UPM 通用机制，可直接复用到任何 Unity 包工程。

## 来源（Origin）
- 会话日期：2026-07-03
- 关键对话节选：
  > 用户：sample 的 asmdef 依赖 login 程序集，但 sample 也属于 bind 的 package 吧？难道不写 dependencies，只 asmdef 关联就可以使用了？
  > 用户：有这个提示机制？这个是我们自己开发的，还是 unity 自有的机制？
  > AI：Unity 对 sample 跨包依赖零原生机制；README/description 只是文字提示，无执行力；包 dependencies 不该为 sample 便利而加，否则破坏职责分离。

## 关联
- 相关 ADR：[[ADR-020-assembly-dependency-direction|ADR-020]]、[[ADR-031-upm-three-piece-mandatory|ADR-031]]、[[ADR-067-login-bind-save-separation|ADR-067]]
- 相关 Pattern：[[PAT-41-upm-package-layout-and-manifest|PAT-41]]
