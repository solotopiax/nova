---
id: PAT-109
title: UPM Kit/SDK 包必备 Nova/Docs 文档目录
summary: Kit/SDK 包必带 Nova/Docs 与 INDEX.md
category: docs
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-109
  - PAT-109-upm-package-docs-mandatory
keywords:
  - PAT-109
  - UPM Kit/SDK 包必备 Nova/Docs 文档目录
  - PAT-109-upm-package-docs-mandatory
tags: [pattern, docs, upm, kit, sdk, workflow]
related:
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
  - "[[PAT-05-l0-l1-l2-docs|PAT-05]]"
  - "[[ADR-031-upm-three-piece-mandatory|ADR-031]]"
---

# PAT-109：UPM Kit/SDK 包必备 Nova/Docs 文档目录

## 适用场景（When）

- 新建或发版 Nova 自家 Kit/SDK 包时。
- 审查 UPM 包结构与文档完整性时。

> **不约束的范围**：纯三方库包（如 `com.solotopia.hybridclr` / `com.solotopia.unitask` / `com.solotopia.luban` / `com.solotopia.yooasset`）—— 这类包对外文档由 `Core/` 里三方原 README 承载，Solotopia 侧不强制重写，仅要求 `Nova/readme.txt` 占位记录魔改清单（继续按 [[PAT-41-upm-package-layout-and-manifest|PAT-41]] 执行）。

## 核心做法（What & How）

### 1. 目录结构（强约束）

```
UPMPackages/com.solotopia.nova.framework.{kit|sdk}.<name>/
├── package.json
├── README.md                          # 包级简介（PAT-31 三件套已要求）
├── CHANGELOG.md                       # PAT-31 三件套已要求
├── LICENSE.md                         # PAT-31 三件套已要求
└── Nova/
    ├── Scripts/                       # 已有
    ├── Protos/ (可选)                 # 已有
    └── Docs/                          # 新增强制
        ├── INDEX.md                   # 本包文档总索引（强制）
        └── <ClassName>.md             # 每个公开类一份（按需扩展子目录）
```

`Nova/Docs/` 与 `Nova/Scripts/` 同级，**严禁**放包根（避免与 Core/ 三方原装文档逻辑混淆，且 Solotopia 定制层应统一收口在 `Nova/` 内）。

### 2. INDEX.md 强制内容

每个 Kit/SDK 包的 `Nova/Docs/INDEX.md` 至少包含：

```markdown
# <包 displayName> 文档索引

## 公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `NetService` | 网络请求静态编排器 | [NetService.md](./NetService.md) |
| `NetBuilder` | 请求构建工具（内部 API） | [NetBuilder.md](./NetBuilder.md) |
| `Login` | 登录业务 Service | [Login.md](./Login.md) |
| ... | ... | ... |

## 错误码

- [NetErrorCode.md](./NetErrorCode.md) / [LoginErrorCode.md](./LoginErrorCode.md)

## 相关
- 主框架文档：`Assets/Framework/Docs/Runtime/Modules/Network/NetworkComponent.md`
- 主框架索引：`Assets/Framework/Docs/INDEX.md`
```

### 3. 每个公开类一份 .md 文档（参照 Framework Docs 模板）

最少必备小节：
- `## 1. 简介`（一句话职责）
- `## 2. 公开 API`（签名 + 用途）
- `## 3. 使用示例`（≥1 段最小可工作代码块）
- `## 4. 内部约束`（如 `[EditorBrowsable(Never)]` / internal 等访问性说明）
- `## 5. 关联`（链相关类、ADR、PAT、主框架 Component 文档）

不强求每个 internal 类都写，但**所有 public + sibling Kit 包能调到的 API**（包括 `[EditorBrowsable(Never)]` 标记的）都要有文档。

### 4. 与主框架 Docs 的关系

| 层级 | 写在哪 | 写什么 |
|---|---|---|
| 主框架 Component（`NetworkComponent`） | `Assets/Framework/Docs/Runtime/Modules/Network/NetworkComponent.md` | Component 自身 + `Kit<T>()` 入口 + 主框架视角的扩展点 |
| Kit 包内类（`NetService` / `Login` / `NetBuilder`） | `UPMPackages/<pkg>/Nova/Docs/<Class>.md` | 类自身 API 全文档；不重复 Component 层 |
| 主框架 INDEX.md | `Assets/Framework/Docs/INDEX.md` | 列主框架文档 + 给每个 Kit 包加一行**软链**到 `UPMPackages/<pkg>/Nova/Docs/INDEX.md` |

**严禁把 Kit 包内类文档写到 `Assets/Framework/Docs/`**——主框架 Docs 不应感知 Kit 实现细节，否则 Kit 包搬走时主框架文档残留断链。

### 5. 发版校验

统一发布流水线追加一道结构校验：
- 包名匹配 `com.solotopia.nova.framework.{kit,sdk}.*` 时
- 检查 `Nova/Docs/INDEX.md` 存在
- 检查 `Nova/Docs/` 下 `*.md` 计数 ≥ `Nova/Scripts/Runtime/` 下 public class 计数的某个比例（粗校验，避免空架子）
- 缺失则 ABORT 发版

## 为什么这么做（Why）

- 框架对外延伸的包，文档级别不应低于主框架。
- 业务接入依赖文档而不是源码。
- 文档与类共生，跨包搬迁时不留断链。
- README / CHANGELOG 不能替代 API 专文。

## 反模式（Anti-patterns）

- ❌ Kit/SDK 包只有 README.md，没有 `Nova/Docs/`
- ❌ Kit 包内类文档写到 `Assets/Framework/Docs/Runtime/Modules/<Module>/<Class>.md`（主框架 Docs 不应感知 Kit 实现细节）
- ❌ `Nova/Docs/` 放包根（应在 `Nova/` 下，与 Scripts 同级）
- ❌ 仅有一份 `Nova/Docs/README.md` 没有 INDEX.md（业务侧无法快速定位类）
- ❌ 文档只列签名不带使用示例（业务侧仍需读源码）
- ❌ 类文档落后于代码（改了 API 不改文档）

## 跨项目复用提示

任何 UPM 重度的项目都适用：把组织名替换成自己的即可。模板的内核是「框架对外延伸的每个包，文档级别与框架本体对齐」。

## 落地参考点

- 主框架 Docs 范本：`Assets/Framework/Docs/`（含 ARCHITECTURE.md / INDEX.md / Runtime / Editor 分层）
- 现有 Kit 包结构：`UPMPackages/com.solotopia.nova.framework.kit.network/Nova/{Scripts,Protos}/`（**当前缺 Docs/**，应按本规范补齐）
- 现有 SDK 包结构：`UPMPackages/com.solotopia.nova.framework.sdk.tga/Nova/Scripts/`（**当前缺 Docs/**，应按本规范补齐）

## 关联

- 相关 Pattern：[[PAT-41-upm-package-layout-and-manifest|PAT-41]]、[[PAT-05-l0-l1-l2-docs|PAT-05]]、[[ADR-031-upm-three-piece-mandatory|ADR-031]]
- 落地行动项：对存量 Kit/SDK 包补齐 `Nova/Docs/`。
