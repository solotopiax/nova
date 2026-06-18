---
id: PAT-DRAFT
title: 生成器模板须与复制源同一真相源，缺失声明应硬失败
summary: 生成器模板与复制源必须共享真相源，缺失声明应直接失败
status: archived
date: 2026-06-05
archived-date: 2026-06-08
type: pattern
category: workflow
aliases:
  - PAT-DRAFT-2026-06-05-generator-template-single-source-fail-fast
tags:
  - pattern
  - archive
  - skill
  - publish
---

# PAT-DRAFT：生成器模板须与复制源同一真相源，缺失声明应硬失败

## 归档说明

- 保留为历史草稿，供后续若需转正式 PAT 时复用结论。
- 已移除会话与 hook 残留，仅保留生成器治理规则。

## 适用场景

- 脚手架/生成器用「全量复制某模板目录」作为真相源（如 `create_sample.py` copytree MainDemo），同时又在别处用独立模板「再声明一遍」同样的路径/事实时。
- 配置/描述符（如 `nova-samples.json` 的 `sampleManifestRelative`）声明了对某个外部资源的引用，发布/构建脚本据此去定位资源时。
- 同一个 bug 在多个生成产物里批量复现（本例 6 个 demo 全错），需要决定「逐个修实例」还是「修源头」时。

## 核心做法

1. **单一真相源**：copytree 的模板既然是真相源，任何再次硬编码同一路径的地方（`write_nova_samples_json` 里写死 `Configs/`）必须删除，或对真相源做一致性校验。否则模板与复制源会从源头分叉——本例 manifest 实际在 `Editor/`，硬编码却写 `Configs/`。
2. **修源头不修实例**：跨多产物复现的缺陷，改生成器/模板，而非逐个手修。gamelogin 早期撞坑只手修了自己的 descriptor，没回修 `create_sample.py`，导致后续 6 个 demo 继续中招（典型「治标未治本」）。
3. **声明即契约、缺失即硬失败**：配置一旦声明引用某资源，脚本定位不到应 `ABORT` 而非 `WARN skip`；只有「字段为空/未声明」才允许合法跳过。本例 `manifest not found, skip` 把错误淹在日志里，带病发布出空清单的 tarball。

## 反模式

- 复制模板 + 另写硬编码常量，两套路径不强制对齐 → 必然分叉。
- 缺失资源打 WARN 后静默 skip → 错误被淹没，发布出残缺产物，import 端才暴雷。
- 撞坑只修当前实例、不回修生成器，把同一个坑留给所有未来产物。
