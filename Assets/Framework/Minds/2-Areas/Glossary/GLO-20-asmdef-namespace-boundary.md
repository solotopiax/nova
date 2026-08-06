---
id: GLO-20
title: asmdef 与 namespace 的边界
summary: asmdef 决定编译依赖，namespace 只组织代码名称
category: arch
status: active
date: 2026-08-06
aliases:
  - GLO-20-asmdef-namespace-boundary
  - asmdef
  - namespace
keywords:
  - GLO-20
  - asmdef
  - namespace
  - Assembly Definition
  - 程序集边界
tags: [glossary, nova, unity, architecture, asmdef]
related:
  - "[[PAT-140-upm-package-vs-sample-dependency|PAT-140]]"
---

# GLO-20：asmdef 与 namespace 的边界

## 定义

- **asmdef**：Unity Assembly Definition 资产，决定哪些源码编译进同一程序集、程序集之间如何引用、适用平台以及版本宏条件。
- **namespace**：C# 名称组织机制，用于避免类型重名和表达代码语义，不创建程序集，也不自动建立编译依赖。

## Nova 边界

- Runtime 与 Editor 的隔离由目录和 asmdef 引用共同保证；相同 namespace 不能让 Runtime 合法引用 Editor 程序集。
- UPM `package.json dependencies` 管安装期包解析，asmdef `references` 管编译期程序集依赖，namespace 不承担其中任何职责。
- 新增跨模块调用应先判断接口与程序集边界，不能仅通过补 `using` 或改 namespace 绕过依赖规则。
- `versionDefines` 属于 asmdef 的条件编译能力，可按已安装包版本生成宏；它不是运行时功能开关。

## 易混淆项

- `using Xxx` 只缩短类型名，不等于项目已经引用包含该类型的程序集。
- 两个文件处于相同 namespace，仍可能因 asmdef 无引用而无法编译。
- asmdef 引用成功也不代表包会被自动安装；安装来源由 UPM manifest 与 package dependencies 决定。

## 示例

Sample 引用了另一个 Kit 的类型时，导入后要能编译，既需要依赖包被安装，也需要对应 asmdef reference；把 Sample namespace 改成与目标 Kit 相同不能解决依赖缺失。

## 来源

- [[PAT-140-upm-package-vs-sample-dependency|PAT-140]]：UPM 包依赖、asmdef 编译引用与 Sample 导入边界。
- `.nova/RULES.md`：禁止 Runtime 反向依赖 Editor。

---
