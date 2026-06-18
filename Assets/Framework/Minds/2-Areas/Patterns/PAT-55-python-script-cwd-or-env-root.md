---
id: PAT-55
title: Python 工具脚本优先用 cwd 或环境变量解析项目根
summary: 工具脚本不要把项目根硬绑定到 `__file__` 的目录深度
category: workflow
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-55-python-script-cwd-or-env-root
keywords:
  - PAT-55
  - Python 工具脚本优先用 cwd 或环境变量解析项目根
  - PAT-55-python-script-cwd-or-env-root
tags:
  - pattern
  - python
  - tooling
  - workflow
related:
  - "[[ADR-031-upm-three-piece-mandatory|ADR-031]]"
---

# PAT-55：Python 工具脚本优先用 cwd 或环境变量解析项目根

## 适用场景

- 发布、构建、导出、样例生成等 Python 工具脚本
- 脚本可能被挪目录、被不同入口调用、被不同机器复用

## 核心规则

- 项目根优先从环境变量或当前工作目录解析
- 不要默认靠 `__file__` 多级回溯推断项目根

## 推荐写法

```python
PROJECT_ROOT = os.path.abspath(
    os.environ.get("NOVA_PROJECT_ROOT", os.getcwd())
)
```

## 为什么这样定

- 脚本的物理位置会变，但项目根语义不应跟着变
- 一旦把项目根绑死在“脚本目录往上几层”，目录调整后就会静默失效
- cwd / env 的契约更适合长期复用

## 反模式

- `os.path.dirname(__file__)` 连续多级回溯
- 脚本搬家后仍沿用旧层级推断
- 不给调用方约定 cwd 或环境变量

## 当前项目中的落地

- Nova 当前的样例与发布脚本已经采用 `NOVA_PROJECT_ROOT` / `cwd` 方案

## 关联

- 这条规则适用于 Nova 的长期工具脚本，不绑定某一代协作工具目录结构
