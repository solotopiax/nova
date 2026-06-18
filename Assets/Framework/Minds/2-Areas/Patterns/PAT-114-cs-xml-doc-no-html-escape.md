---
id: PAT-114
title: C# XML 注释禁止 HTML 转义
summary: XML 注释直接写尖括号
category: quality
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-114
  - xml-doc-no-html-escape
keywords:
  - PAT-114
  - C# XML 注释禁止 HTML 转义
  - xml-doc-no-html-escape
tags:
  - pattern
  - csharp
  - docs
  - quality
---

# PAT-114：C# XML 注释禁止 HTML 转义

## 适用场景

- 写 `.cs` 文件的 XML 注释
- 注释里出现泛型、比较符或类型表达式
- 从旧文件、旧包、旧骨架复制注释后做清理

## 核心规则

- 在 C# XML 注释正文里，`List<T>`、`Dictionary<TKey, TValue>`、`a < b` 这类文本直接写字面量。
- 不要把它们写成 `List&lt;T&gt;`、`a &lt; b`。

## 为什么这样定

- Nova 代码规范希望文档输出、IDE 悬停和源码阅读保持同一套可读形式。
- HTML 转义通常来自旧骨架复制，会把源码注释可读性拉低，并且容易持续扩散。

## 自检建议

- 新写完的 `.cs` 文件，检查是否出现 `&lt;`、`&gt;`、`&amp;`
- 批量清理旧代码时，也优先用这组关键词筛查

## 反模式

- 复制旧注释后直接提交
- 以为“编译不报错就没问题”
- 把 Markdown/HTML 里的转义写法原样搬进 `.cs` 注释

## 关联

- 这是 Nova 的 C# 文档红线之一，应与日常静态检查一起执行
