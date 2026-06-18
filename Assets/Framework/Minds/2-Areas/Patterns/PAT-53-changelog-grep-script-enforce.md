---
id: PAT-53
title: 发版前校验 CHANGELOG 当前版本节存在，不靠人工自觉
summary: 发版前校验 CHANGELOG 当前版本节
category: workflow
type: pattern
status: active
date: 2026-06-05
aliases:
  - PAT-53-changelog-grep-script-enforce
keywords:
  - PAT-53
  - PAT-53-changelog-grep-script-enforce
  - 发版前校验 CHANGELOG 当前版本节存在，不靠人工自觉
tags: [pattern, methodology, publish, changelog, automation, quality]
related:
  - "[[ADR-031-upm-three-piece-mandatory|ADR-031]]"
  - "[[PAT-13-publish-no-cascade|PAT-13]]"
  - "[[PAT-46-iteration-grep-self-check|PAT-46]]"
---

# PAT-53：发版前校验 CHANGELOG 当前版本节存在，不靠人工自觉

## 适用场景

- 任何遵循 Keep a Changelog 的发版流程
- monorepo 多包同时发版
- 流程文档已写“必须维护 CHANGELOG”，但执行仍靠人工记忆

## 核心规则

发布脚本或 CI 必须在任何副作用动作之前，校验目标包的 `CHANGELOG.md` 中是否存在当前版本节。

可接受格式：

```markdown
## [0.5.0] - 2026-05-21
```

不接受：

- `## v0.5.0`
- `## 0.5.0`
- `## [unreleased]`

## 最小实现

```python
import re

def ensure_changelog_section(path, version):
    body = open(path, encoding="utf-8").read()
    pattern = re.compile(r'^##\s*\[' + re.escape(version) + r'\]', re.MULTILINE)
    if not pattern.search(body):
        raise SystemExit(f"[ABORT] {path} 未写 [{version}] 节")
```

## 为什么这样定

- “必须写 CHANGELOG”如果只停留在文档层，最终仍会漏
- 版本号和 CHANGELOG 节一旦脱节，发布产物会立刻失去可追溯性
- 这类约束必须变成脚本或 CI 的阻断条件，不能靠自觉

## 反模式

- 只在流程文档里写规则，不做机器校验
- 发布后才检查 CHANGELOG
- 错误信息不带路径与补救动作
- 用模糊占位符替代真实版本节

## 跨项目复用提示

这条规则与 Nova 业务无关，适用于任何包管理体系。关键不在于一定用 `grep` 还是正则，而在于“当前版本节存在”必须被机器阻断校验。

## 关联

- [[ADR-031-upm-three-piece-mandatory|ADR-031]]
- [[PAT-13-publish-no-cascade|PAT-13]]
- [[PAT-46-iteration-grep-self-check|PAT-46]]
