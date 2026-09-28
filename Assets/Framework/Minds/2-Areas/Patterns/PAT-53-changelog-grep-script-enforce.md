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

CHANGELOG 约束必须由结构化写入工具与共享检查器共同执行，不能依赖文本补丁或发版前人工复核：

1. 日常条目只能由工具写入目标文件的 `[Unreleased]`。
2. 发版收口后必须重建含标准分类标题的空 `[Unreleased]` 骨架。
3. 相对最新 `upm-release-*` tag，根 CHANGELOG、Framework 与全部直接 UPM 包的已发布版本节必须保持字节级不变。
4. 同一检查器必须接入日常 health、提交索引、CI 和正式发布，显式发布子集不能缩小历史边界检查范围。
5. 历史勘误写入当前 `[Unreleased]`，不得用一条“更正”说明放行旧版本正文改写。

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

def changed_released_sections(before, after):
    before_sections = parse_sections(before)
    after_sections = parse_sections(after)
    return [
        version for version, body in before_sections.items()
        if version != "Unreleased" and after_sections.get(version) != body
    ]
```

## 为什么这样定

- “必须写 CHANGELOG”如果只停留在文档层，最终仍会漏
- 空 `[Unreleased]` 后的第一个 `### Fixed` 属于上一已发布版本，按通用标题锚点插入会静默改写历史
- 只在发版阶段检查虽然能阻止上传，但不能阻止错误进入普通提交并长期污染主分支
- 本地钩子可被绕过，真正的仓库级保证必须由服务端 CI 的 required check 提供

## 反模式

- 只在流程文档里写规则，不做机器校验
- 发布后才检查 CHANGELOG
- 使用全局 `### Fixed` 等重复标题作为文本插入锚点
- 只检查本轮候选包，让显式发布子集掩盖其他包的历史改写
- 看到 `[Unreleased]` 中有“更正”字样就放行任意旧版本修改
- 错误信息不带路径与补救动作
- 用模糊占位符替代真实版本节

## 跨项目复用提示

这条规则与 Nova 业务无关，适用于任何包管理体系。关键不在于一定用 `grep` 还是正则，而在于“当前版本节存在”必须被机器阻断校验。

## 关联

- [[ADR-031-upm-three-piece-mandatory|ADR-031]]
- [[PAT-13-publish-no-cascade|PAT-13]]
- [[PAT-46-iteration-grep-self-check|PAT-46]]
