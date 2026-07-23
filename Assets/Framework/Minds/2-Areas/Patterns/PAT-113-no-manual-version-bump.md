---
id: PAT-113
title: 禁手动抬 UPM package.json version
summary: UPM版本由统一入口写入且以registry为准
category: workflow
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-113-no-manual-version-bump
keywords:
  - PAT-113
  - 禁手动抬 UPM package.json version
  - PAT-113-no-manual-version-bump
tags: [pattern, methodology, upm, publish, versioning]
related:
  - PAT-147
---

# PAT-113：禁手动抬 UPM package.json version

## 适用场景（When）

- 修改 `Assets/Framework/` 或 `UPMPackages/<pkg>/` 下任何源码 / 文档 / 配置时
- 提交 refactor / feat / fix / chore / docs 任意类型 commit
- "顺手把版本号也抬了，下次发版省事"的诱惑出现时

**触发信号：** 任何 commit 的 `git diff` 输出中出现 `package.json` 文件，且 diff 包含 `"version": "..."` 这一行。

## 核心做法（What & How）

| 场景 | 允许动作 | 禁止动作 |
|---|---|---|
| 日常开发提交 | 改源码、改文档、改配置 | 触碰 `package.json::version` |
| 正式发布入口 | 统一写入版本号，并同步需要镜像版本的代码常量 | — |
| 用户明确要求人工指定 `X.Y.Z` | 一次性放行 | — |

**操作约束：**

1. 写代码前确认本次任务**不**修改任何 `package.json` 的 `version` 字段
2. commit 前检查 diff 是否带进 `package.json::version`；带进就回退该字段
3. 发版时只通过统一发布入口写入版本号，并在发布前完成撞号检查
4. 版本占用以目标 registry 的 `npm view <pkg> versions --json` 为最终事实源；Git release tag 只用于计算变更基准，不能证明某个版本尚未发布
5. registry 已占用目标版本时继续递增 patch，禁止因为 Git 中缺少对应 tag 就复用远端版本号

**版本号唯一写入入口：** 统一发布入口。

### 版本事实源优先级

```text
目标 registry 已发布版本
  > 本地 package.json 候选版本
  > Git release tag / 历史提交推断
```

发版前至少同时确认：

- `npm view <pkg> versions --json --registry <registry>`：目标版本是否已占用
- `npm view <pkg> dist-tags.latest --json --registry <registry>`：当前 latest 指向
- 本地 `package.json`：只作为待发布候选，不反向覆盖 registry 事实

Git tag 可能因历史上单独发布、漏打 tag 或尚未完成 Git 收尾而缺失。此时仍必须避开 registry 已存在的版本。

## 为什么这么做（Why）

- **版本号是发版动作的产物，不是源码状态**：源码改动节奏 ≠ 发版节奏。把版本号顺手抬高，等于把"我打算发版"伪装成"我改了代码"
- **不发版的 version bump 一定造成脱节**：本地 `package.json` 写 0.0.10，但 npm registry 还是 0.0.8，下次真发版要么跳号（0.0.7 → 0.0.9 缺 0.0.8）要么强制覆盖（本地 0.0.10 → 实际只能发 0.0.9）
- **版本基准与撞号兜底依赖唯一写入入口**：如果本地手改 version 但没发版，后续发布流程就无法判断“这是已发版版本”还是“未发版的脏版本”
- **Git 历史不一定覆盖所有真实发布**：包可能已经进入 registry，但没有对应 release tag；只看 tag 会把已发布版本误判为可复用版本

**不这么做会发生什么**：2026-05-26 发版时发现，三次提交（`51bec487` refactor kit.network / `974fbfbc` feat sdk.tga / `a7c0c0b5` doc 同步）都顺手改了 version，但都没走 publish。结果 Verdaccio 上 `kit.network` 最高 0.0.8，本地 0.0.10；`kit.network.login` 最高 0.0.7，本地 0.0.9。要么跳号要么回退，发版决策被迫由用户当场拍板（最终选择全部回退到与 Verdaccio 连续的下一个版本：0.0.10→0.0.9 / 0.0.9→0.0.8）。

## 反模式（Anti-patterns）

- **"refactor + bump version" 一锅端**：commit 既改代码又把 `"version": "0.0.8"` 抬到 `"0.0.9"`，理由是"反正下次发版要升"。结果该 commit 没发版，版本号在 git 里跳了，但 Verdaccio 没动。下次发版要么强制覆盖本地，要么真发出去成了"幽灵版本"——版本号写 0.0.9 但内容是 0.0.10 的代码
- **"feat 加新接口顺手 minor+1"**：功能提交没有同步发版，几天后另一人要修 bug，就无法判断本地版本号到底代表“已发版”还是“未发版代码”
- **"doc 同步顺手把所有包都 patch+1"**：commit `a7c0c0b5` 改了一堆文档，同时把 `kit.network` / `kit.network.login` / `sdk.tga` 三个包的 version 全 +1，理由是"反正文档也算改动"。但文档改动**不需要**发版（用户拉的是 npm tarball，不带文档），三个包就这样空跳一格
- **"Git 没 tag，所以版本没发过"**：把 release tag 当作 registry 镜像，直接复用 tag 中未出现的版本；如果该版本曾被单独发布，`npm publish` 会撞号并中断整批发布

## 补充验证依据（2026-07-21）

- `com.solotopia.nova.framework.sdk.iap@0.0.17` 已存在于目标 registry，但历史上单独发布且没有对应 Git release tag；本轮必须使用 `0.0.18`。
- `com.solotopia.nova.framework.sdk.iap.mobile@0.0.11` 已存在于目标 registry；本轮使用 `0.0.12`。
- 发布后通过逐包 `npm view <pkg>@<version>` 与 `dist-tags.latest` 校验，确认 registry 中的新版本可见且 latest 正确。

## 跨项目复用提示

- **跨技术栈完全适用**：npm / Cargo / pip / Maven / nuget 任何包管理器，version 都该由发版工具唯一管理
- **常见适配点**：
  - npm 项目：可在提交前校验脚本中检测 `package.json` 的 `version` 是否被改动
  - Cargo 项目：同理检 `Cargo.toml` 的 `version =`
  - 单包项目（不像 Nova 这种 mono-repo）也应遵守，否则 `cargo publish` / `npm publish` 会报错或覆盖
- **配套工具**：`semantic-release` / `changesets` / `release-please` 等都基于“version 由发布工具写入”的假设

## 关联

- 主要落点：Nova 发布流程、版本号唯一写入入口、发版前版本一致性检查
- 相关 Pattern：[[PAT-147-upm-publish-no-bulk-test-runner|PAT-147]]
