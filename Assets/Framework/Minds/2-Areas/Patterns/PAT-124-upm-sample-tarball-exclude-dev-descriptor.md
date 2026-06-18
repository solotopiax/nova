---
id: PAT-124-upm-sample-tarball-exclude-dev-descriptor
title: UPM 发版描述符禁随 tarball 落到外部工程只读区
summary: 开发期描述符 .npmignore 排除出 tarball
category: workflow
type: pattern
status: active
date: 2026-05-29
aliases:
  - PAT-124-upm-sample-tarball-exclude-dev-descriptor
  - PAT-124
keywords:
  - PAT-124-upm-sample-tarball-exclude-dev-descriptor
  - UPM 发版描述符禁随 tarball 落到外部工程只读区
  - PAT-124
tags: [pattern, workflow, publish, upm, npmignore, sample]
related:
  - "[[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]"
  - "[[PAT-78-sample-demo-full-flow-sop|PAT-78]]"
  - "[[PAT-121-publish-sample-rewrite-symmetric|主/子包路径重写对称]]"
---

# PAT-124：UPM 发版描述符禁随 tarball 落到外部工程只读区

## 适用场景（When）

- Nova 任意 UPM 包（主框架 / kit / sdk）维护「仅开发期消费」的元数据文件（如 `nova-samples.json`、`package.json.publish.bak`、`Minds/`）
- 这类文件在源工程被 Unity 自动生成 `.meta`，并被 git 跟踪
- 发版（`npm publish` 或 `file:` 依赖）时若不显式排除，会随 tarball 进入外部工程的 `Packages/<pkg>/` 只读区

## 核心做法（What & How）

**铁律**：开发期描述符 + 备份文件**绝不入 tarball**。每个 UPM 包根目录必须自带 `.npmignore`，至少覆盖以下三类：

| 类别 | 典型文件 | 排除原因 |
|---|---|---|
| 发版流水描述符 | `nova-samples.json` + `.meta` | 仅 Python publish 脚本读，外部工程无消费方 |
| 临时备份 | `package.json.publish.bak` + `.meta` | publish 期间临时备份，发版完应清理；落到外部即长期残留 |
| 知识库 / 开发资料 | `Minds/`、`Samples~.dev/`、`Docs/` 中开发期目录 | 体积膨胀且外部工程不需要 |

**`.npmignore` 模板（每包必备）**：

```gitignore
# 开发期占位目录,publish 时 rename 为 Samples~ 才被收录
Samples~.dev/
Samples~.dev.meta

# Obs 知识库不入 UPM 包
Minds/
Minds.meta

# 发版描述符仅开发期使用，不入 UPM tarball：外部工程 Unity 会因其落到只读
# Packages/<pkg>/ 而对 .meta 不认（immutable folder 警告），并触发 SamplePathRewriter 重跑
nova-samples.json
nova-samples.json.meta
```

**自检命令（发版前）**：

```bash
cd <pkg-root> && npm pack --dry-run 2>&1 | grep -E "nova-samples|publish.bak|Minds/"
```

输出为空才放行。任一命中 → 补 `.npmignore` 后重试。

**新增 dev-only 文件时的 checklist**：

1. 在源工程引入新文件前先问"这文件外部工程要消费吗？"——否则进 `.npmignore`
2. 改 `.npmignore` 后跑 `npm pack --dry-run` 自检
3. PR 描述里显式列出新增的 `.npmignore` 条目，让 reviewer 复核

## 为什么这么做（Why）

外部工程的 `Packages/<pkg>/` 是 **immutable folder**（Unity Package Manager 注入，硬只读），后果链：

1. **Unity 拒绝为 .meta 落地**：Console 持续刷 `Asset Packages/com.solotopia.nova.framework/nova-samples.json has no meta file, but it's in an immutable folder. The asset will be ignored.`
2. **触发 AssetDatabase 反复 Refresh**：警告每次 import 域重载都打一遍
3. **`SamplePathRewriter:RunRewrite` 被反复触发**：rewriter 的入口扫到该文件存在 → 启动 → 命中 `.nova-path-rewritten` 标记 → return；但启动本身有开销，多 sample 共存时连锁
4. **0.5.13 同款问题**：`package.json.publish.bak` 因 publish 失败未清理留在源 → 入 tarball → 外部工程 Console 同款报错；当时通过把备份移到系统临时目录解决

把"发版描述符仅源工程使用"这个**开发期意图**通过 `.npmignore` 显式编码到打包链路，是最小代价的解。

## 反模式（Anti-patterns）

- **侥幸认为「文件小不影响」**：体积不是问题，外部 Console 警告 + rewriter 反复启动才是。一条警告每次 import 重复打十几遍，污染外部工程开发者注意力
- **把开发期清理责任推给运行时**：在 SamplePathRewriter 里加 `if path.endswith("nova-samples.json"): skip` 是治标——根因在于这文件**根本不应该出现在外部工程**
- **首次发版漏配 `.npmignore` 后只补单包**：本轮就踩了这坑——主包 0.5.15 补了，子包 login 一开始没补，直到验证 dry-run 才发现两包都得补
- **`.gitignore` 与 `.npmignore` 混淆**：`.gitignore` 不影响 `npm publish`；npm 默认用 `package.json#files` + `.npmignore` 决定 tarball 内容，必须显式写
- **复制粘贴 `.npmignore` 时漏 `.meta` 配套行**：Unity 给每个非排除文件生成 `.meta`，光排 `nova-samples.json` 不排 `nova-samples.json.meta` → tarball 仍含 .meta（Unity 看到 .meta 但找不到主文件，还是会报错）

## 跨项目复用提示

任何"npm 私域 + Unity Package Manager + `file:`/registry 双发布渠道"的工程都适用：

- 通用规则：**外部消费方读不到的文件，全部 `.npmignore` 排除**
- 通用自检：`npm pack --dry-run` 必跑
- 跨技术栈延伸：Maven / Cargo / Composer 等私域包同样有"开发期文件混入发布物"风险，原则一致

