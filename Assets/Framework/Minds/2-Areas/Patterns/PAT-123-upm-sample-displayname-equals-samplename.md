---
id: PAT-123-upm-sample-displayname-equals-samplename
title: UPM Sample displayName 必须等于 sampleName
summary: displayName 须等于 sourceDir 末段
category: workflow
type: pattern
status: active
date: 2026-05-29
aliases:
  - PAT-123-upm-sample-displayname-equals-samplename
  - PAT-123
keywords:
  - PAT-123-upm-sample-displayname-equals-samplename
  - UPM Sample displayName 必须等于 sampleName
  - PAT-123
tags: [pattern, upm, sample, publish, workspace-active]
related:
  - "[[ADR-047-editor-active-master-anchor|ADR Editor Active Master Anchor]]"
  - "[[PAT-121-publish-sample-rewrite-symmetric|PAT Publish Sample Rewrite Symmetric]]"
  - "[[PAT-120-create-sample-user-decides-dirname|PAT Create Sample User Decides Dirname]]"
---

# PAT-123：UPM Sample displayName 必须等于 sampleName

## 适用场景（When）

- 任何子框架 / 主框架包通过 UPM `Samples~/` 机制对外分发演示工程
- 维护 `nova-samples.json` 描述 sample 元数据时
- 编辑 `package.json` 的 `samples[]` 字段（人工或脚本写入）时
- 触发信号：外部工程 `Package Manager → Import` sample 后，依赖"按相对路径推断 sample 根目录"的工具（如 `EditorUtil.Config.WorkspaceActive` 的 sample scene 推断分支）失效，例如 ConfigWindow 提示"未检测到激活的 ConfigMaster"

## 核心做法（What & How）

| 字段 | 取值规则 | 示例 |
|------|----------|------|
| `nova-samples.json` 的 `sampleName` | 开发态 `sourceDir` 末段（即 `Assets/Samples/<末段>/`） | `LoginDemo` |
| `nova-samples.json` 的 `displayName` | **直接抄 `sampleName`，禁带空格 / 复数 / 友好措辞** | `LoginDemo`（不是 `Login Demos` / `Login Demo` / `登录示例`） |
| `nova-samples.json` 的 `description` | 描述类文案放这里 | `登录业务演示工程` |
| `package.json` 的 `samples[].displayName` | 由发版脚本 `_inject_samples_into_pkg_json` 从 `nova-samples.json` 注入，**强制覆盖 = sampleName**，不消费用户输入的 `displayName` 字段 | 同 sampleName |

**UPM 落盘路径公式**：

```
Assets/Samples/<package.json::displayName>/<package.json::version>/<samples[].displayName>/
                  ↑ 包级目录                       ↑ 版本号目录              ↑ 末段（必须 == 开发态 sampleName）
```

**只有最末段 == 开发态 `sourceDir` 末段**，外部工程 import 后场景所在目录的相对路径才与开发态共用同一推断逻辑（如 `WorkspaceActive` 的"向上递归找 `Editor/ConfigMaster.asset`"）。

**新建 sample 检查清单**（顺手完成，避免事后补丁）：

1. `nova-samples.json` 写 `sampleName` 时即决定文件夹真名
2. `displayName` 字段直接复制 `sampleName`，**禁手敲第二遍**
3. `description` 单独写文案，禁混入 `displayName`
4. publish 脚本会强制覆盖 `package.json::samples[].displayName`，无需手动同步 `package.json`
5. 对存量包：先 `npm view <pkg> samples --json` 自查远端版本是否已带正确 displayName，未对齐则发 patch 补丁版本

## 为什么这么做（Why）

- **UPM 用 `samples[].displayName` 作为 import 后落盘的最末层文件夹名**——这是 Unity Package Manager 内部硬编码行为，无法配置
- 末段是工具链（`WorkspaceActive` / 路径 rewriter / Inspector 字段路径推断）跨"开发态扁平结构"与"UPM 导入态嵌套结构"共用同一逻辑的**唯一锚点**
- 末段一旦带空格 / 复数 / 改写措辞，开发态 `sourceDir` 末段与导入态末段就分叉，所有依赖"末段一致"的推断分支全部失效
- 0.0.10 / 0.0.11 期间因把 `displayName` 取作 "Login Demos"（带空格 + 复数），外部工程 import 后落到 `Assets/Samples/Nova Framework - Kit - Network - Login/0.0.11/Login Demos/`，但开发态是 `Assets/Samples/LoginDemo/`，导致 ConfigWindow 在该 sample scene 上提示"未检测到激活的 ConfigMaster"——用户体验直接报废

## 反模式（Anti-patterns）

- **把 `displayName` 当 UI 友好文案**：在 `nova-samples.json` 写 `"displayName": "登录示例"` / `"Login Demo"`（带空格）/ `"Login Demos"`（复数）。这些都会让 UPM 落盘文件夹变成中文 / 带空格 / 复数，与开发态目录分叉
- **publish 脚本消费用户输入的 `displayName`**：脚本若直接把 `nova-samples.json::displayName` 写进 `package.json::samples[].displayName`，等于把约束推给数据维护者，每个新包都要重审一遍——必须在脚本侧用 `samples[].displayName = sampleName` 强制覆盖兜底
- **把"友好文案"塞 `description`**：而是塞回 `displayName`——属于反向操作，`description` 字段就是为人类可读文案设计的，没人会因为它影响推断
- **改 `sampleName` 不改开发态目录**：开发态目录是 `Assets/Samples/LoginDemo/`，`sampleName` 写成 `Login`——等于又破坏一致性。`sampleName` 必须就是开发态目录末段字面值

## 跨项目复用提示

- **本条 Pattern 直接绑定 Unity Package Manager 的 import 行为**，跨 Unity 项目可直接搬
- 非 Unity 项目（如 npm 包带 examples / Python 包带 examples）一般无类似 import 重落盘约束，不适用
- 适配点：若团队的 UPM samples 不依赖路径推断（如不用 `WorkspaceActive` / 不做 Inspector 路径 rewrite），约束可放宽——但仍建议保持 `displayName == sampleName`，避免未来引入路径推断时被旧数据反咬
- 类似约束在其他"用户可命名 + 系统按命名落盘 + 工具按落盘路径推断"的链路里都成立（典型如 git submodule path 必须与 worktree 目录一致）

## 关联

- 规范落点：发布脚本中的 `_inject_samples_into_pkg_json` 与 `_restore_pkg_json` 强制覆盖逻辑；样例创建流程中"`displayName` 必须等于 `sampleName`"红线警示
- 相关 ADR：[[ADR-047-editor-active-master-anchor|ADR Editor Active Master Anchor]]（WorkspaceActive 推断逻辑的上层决策）
- 相关 Pattern：[[PAT-121-publish-sample-rewrite-symmetric|PAT Publish Sample Rewrite Symmetric]]（publish 脚本对称重写约定）、[[PAT-120-create-sample-user-decides-dirname|PAT Create Sample User Decides Dirname]]（sample 目录名由用户决定的上位约束）
- 相关原则：`displayName` 必须等于 `sampleName`
