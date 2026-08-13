---
id: PAT-162
title: 公开快照按包边界分类，并按暂存态验收 Git LFS
summary: 公开包边界与暂存态 LFS 验收
category: workflow
type: pattern
status: active
date: 2026-08-13
aliases:
  - PAT-162-public-package-boundary-and-staged-lfs-validation
  - 公开同步商业包边界与 LFS 指针验收
keywords:
  - PAT-162
  - PUBLIC_EXCLUDE
  - 公开同步
  - Git LFS
  - LFS pointer
  - 暂存态
  - com.starlus.sdk.datamaster
  - com.solotopia.nova.framework.sdk.datamaster.abtest
tags:
  - pattern
  - workflow
  - upm
  - public-sync
  - lfs
related:
  - "[[PAT-119-upm-private-fork-local-diff-marking|PAT-119]]"
  - "[[PAT-141-vendor-source-readonly|PAT-141]]"
  - "[[ADR-031-upm-three-piece-mandatory|ADR-031]]"
---

# PAT-162：公开快照按包边界分类，并按暂存态验收 Git LFS

## 适用场景

- Nova 正式 UPM 发布后的 GitHub 公开快照同步。
- 公开快照包含“Nova 适配层 + 商业原厂包依赖”的 SDK 包。
- 新快照首次引入或更新超过 GitHub 100MB 限制的 Git LFS 文件。

## 核心做法

### 1. 按包职责，而不是依赖名称，确定公开边界

- Nova 适配层与原厂 SDK 是两类不同物料。适配层即使声明商业原厂包为 dependency，也不能仅因名称或依赖关系被自动判为不可公开。
- 当前已明确的 DataMaster 边界是：`com.solotopia.nova.framework.sdk.datamaster.abtest` 为可公开的 Nova 适配层；`com.starlus.sdk.datamaster` 是不可公开的原厂包。
- 不可公开包必须在共享 `PUBLIC_EXCLUDE` 中使用目录前缀显式排除，不能以“当前工作树不存在、未跟踪或刚好被忽略”替代规则；否则它以后出现为候选文件时会被公开同步脚本复制出去。
- 包级 `AGENTS.md`、共享排除清单与当前获准的公开范围必须一致。若三者冲突，当前用户对本轮发布范围的明确指令优先；但在下一次公开同步前必须把持久化规则和测试对齐，不能长期依赖临时覆盖。

### 2. 先暂存快照，再检查 Git LFS 指针

公开同步完成复制和脱敏后，按以下顺序验收大文件：

1. 在公开副仓暂存将要提交的快照。
2. 枚举工作树中所有大于 100MB 的文件。
3. 对每个大文件同时验证：`git check-attr filter` 为 `lfs`，且暂存区 blob 是 Git LFS pointer，而不是实际二进制内容。
4. 非 LFS 大文件、未被 LFS 属性覆盖的文件，或暂存区不是 pointer 的文件一律中止 push。

只在 `git add` 前调用 `git lfs ls-files --name-only` 不足以验证新复制的 LFS 文件：该命令默认观察当前已检出提交，尚未进入当前提交的路径可能不在结果中。不能因为首次输出停顿就跳过 hook、使用 `--no-verify` 或 force push。

### 3. 将 LFS 传输与 Git ref 闭环分开验收

- `git push` 的 `pre-push` 阶段会先上传 LFS 对象；这段等待本身不是失败证据。
- 只有普通 push 完成后，远端目标分支和 release tag 都指向预期 commit，且 `git lfs status` 对目标远端无待推对象，才算公开同步完成。
- LFS pointer 仅表示 Git history 中不含大二进制；它不替代远端 LFS 对象上传和可下载性的验证。

## 原因

- 将适配层与原厂包一并排除会破坏公开安装入口；反过来，把原厂包当作“当前未出现所以安全”会在未来引入时造成泄漏。
- Git LFS 在工作树可呈现实际大文件、在 Git blob 中只保存约百字节 pointer。只检查文件大小会误杀合法 LFS；只检查属性或当前 HEAD 的 LFS 列表又会漏掉尚未暂存的新文件。
- 公开发布需要同时证明源代码边界、Git blob 边界和远端 LFS 对象边界，三者缺一不可。

## 当前待对齐项

- `UPMPackages/com.solotopia.nova.framework.sdk.datamaster.abtest/AGENTS.md` 仍保留“本包含商业 SDK 依赖，不进入公开仓同步”的旧表述，与当前获准的适配层公开边界不一致。
- `.agents/skills/_shared/public_exclude.py` 尚未显式排除 `UPMPackages/com.starlus.sdk.datamaster/`；该目录当前不存在并不构成未来公开同步的安全保证。
- `sync_public.py` 与 `sync_public_repo.py` 都在 `git add -A` 前以 `git lfs ls-files --name-only` 豁免大文件，尚未落实本条的暂存态 pointer 验收。

这些项是后续规则/脚本修复范围；本条记录不替代对相关文件的单独修改授权。

## 反模式

- 因适配层依赖商业 SDK，就把适配层与原厂包一并排除或一并公开。
- 把原厂包“当前不存在”误认为已经被公开同步机制排除。
- 在未暂存新快照时，以当前 HEAD 的 `git lfs ls-files` 结果判断新大文件是否为 LFS。
- 为绕过大文件门禁或等待时间使用 `--no-verify`、force push，或在远端 ref 未核验时宣称公开同步成功。
- 只检查 Git pointer，却不检查 LFS 对象是否已在远端完成上传。

## 验证依据

- 公开边界：`com.solotopia.nova.framework.sdk.datamaster.abtest` 的 `package.json` 明确其为 Nova DataMaster 对接层，并把 `com.starlus.sdk.datamaster` 声明为商业依赖；`upm-release-2026.08.13-01` 公开 tag 包含该适配层，不含原厂包。
- 排除机制：`.agents/skills/_shared/public_exclude.py` 目前不包含原厂 DataMaster 包前缀；`sync_public.py` 使用候选清单与该共享排除器重建公开快照。
- LFS 行为：公开 tag 中 Firebase `FirebaseCppApp-13_14_0.bundle` 的工作树文件为 111,527,216 字节，而 Git blob 仅 134 字节，并受 `.gitattributes` 的 `filter=lfs` 约束。
- 脚本顺序：两个公开同步入口都在大文件检查之后才执行 `git add -A`，因此不能用当前 HEAD 的 LFS 列表替代暂存态检查。
- 发布闭环：本轮公开 `main` 与 `upm-release-2026.08.13-01` 均指向 `d7065fcf`，`git lfs status` 无待推对象。

## 关联

- 私有 fork 差异标注：[[PAT-119-upm-private-fork-local-diff-marking|PAT-119]]
- 原厂源码只读与授权边界：[[PAT-141-vendor-source-readonly|PAT-141]]
- UPM 三件套：[[ADR-031-upm-three-piece-mandatory|ADR-031]]
