---
id: ADR-030
title: UPM 发布工具迁移为 nova-publish skill，废弃 Tools/Publish 目录
summary: Tools/Publish 并入 nova-publish
category: workflow
status: accepted
date: 2026-05-21
aliases:
  - ADR-030-tools-publish-to-cc-skill
keywords: [ADR-030, ADR-030-tools-publish-to-cc-skill]
tags: [adr, nova, workflow, publish, upm, claude-code]
supersedes: []
superseded-by: []
related:
  - "[[ADR-022-sdk-plugin-architecture|ADR-022]]"
  - "[[PAT-13-publish-no-cascade|PAT-13]]"
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
  - "[[PAT-55-python-script-cwd-or-env-root|PAT-55]]"
---

# ADR-030：UPM 发布工具迁移为 nova-publish skill，废弃 Tools/Publish 目录

## 背景（Context）

Nova Framework UPM 发布历史上落在 `Tools/Publish/`：

- `publish_packages.py`：strip Nova.prefab、复制 `Assets/Game/` 为 Samples~/Demo、改名 Samples~ 后 `npm publish`、finally 兜底还原。
- `publish_packages.command`：macOS 双击启动壳。
- `templates/sample-scaffold/`：子框架 sample 脚手架模板。
- 开发者凭记忆 `python Tools/Publish/publish_packages.py` 调用，团队成员需自行翻 README。

问题：

1. 发布流程的「步骤约束」与「实现细节」分离——SKILL 化前流程文档落在 `.claude/skills/nova-publish/SKILL.md`（dry-run / 撞号兜底 / tag 验证 / 推送等），但脚本却独立放 `Tools/Publish/`，逻辑跨两处维护。
2. `Tools/` 目录无明确归属约束，长期会退化为杂物堆。
3. `Tools/Publish/__pycache__/` 等 Python 缓存反复污染未跟踪文件列表。
4. 用户明示："这个工具我希望沉淀为 claude code 的 skill，这个目录以后不要了。"

## 决策（Decision）

`Tools/Publish/` 整体并入 `.claude/skills/nova-publish/`，废弃原目录：

| 旧路径 | 新路径 |
|---|---|
| `Tools/Publish/publish_packages.py` | `.claude/skills/nova-publish/scripts/publish_packages.py` |
| `Tools/Publish/templates/sample-scaffold/` | `.claude/skills/nova-publish/templates/sample-scaffold/` |
| `Tools/Publish/publish_packages.command` | 删除（CC skill 调用直接走 `python` 命令） |
| `Tools/Publish/__pycache__/` | 删除 + `.gitignore` 加 `__pycache__/` / `*.pyc` |

**调用约定**：

- 团队入口命令：`python .claude/skills/nova-publish/scripts/publish_packages.py`
- 该命令在 `.claude/CLAUDE.md` 「UPM 发布」节固化（团队共享，入 git）
- 流程文档：`.claude/skills/nova-publish/SKILL.md`（步骤 1~11 含 dry-run / 撞号兜底 / tag 验证 / 推送）

**脚本路径解析**：从 `__file__` 三级回溯改为 `os.environ.get("NOVA_PROJECT_ROOT", os.getcwd())`，要求调用方在项目根 cwd 中调用。skill 嵌套层级与项目结构解耦（详见 [[PAT-55-python-script-cwd-or-env-root|PAT-55]]）。

**`Tools/` 目录残余**：仅保留 `Tools/SQLiteStudio/` 等一次性外置工具，不再放 Nova 自维护脚本。

## 后果（Consequences）

### 正面

- 发布工具与 CC 工作流一体化，单点维护：步骤、实现、模板都在 `.claude/skills/nova-publish/` 下。
- 团队成员通过 `.claude/CLAUDE.md` 统一入口看到命令，不依赖私货 `CLAUDE.local.md`。
- `Tools/` 目录恢复纯净，避免 Python 缓存等噪音。

### 负面

- 历史 commit / ADR-022 / 旧 plan 文档中残留 `Tools/Publish/` 字面引用，需逐处清理（ADR-022 在 `2-Areas/`，须走 `/nova-obs inbox-to-areas` 通道补改）。
- skill 目录嵌套深（`.claude/skills/nova-publish/scripts/publish_packages.py`），命令字符更长。
- 任何调用方必须**在项目根 cwd** 触发，否则需显式设置 `NOVA_PROJECT_ROOT` 环境变量。

## 被排除的方案（Alternatives）

| 方案 | 否决理由 |
|---|---|
| 保留 `Tools/Publish/` 独立目录 | 流程文档与脚本两处维护，命令凭记忆调用，团队对齐成本高 |
| 改为 npm scripts（`npm run publish:nova`） | 需在多个 package.json 各自挂脚本，与 Verdaccio 私域无关；无法承载 dry-run 等流程交互 |
| Editor 菜单按钮触发 | `npm publish` 子进程在 Unity 内部运行不便排查；CI / 远程会话场景无法用 |
| 仅迁移脚本不改路径解析 | `__file__` 回溯继续与目录结构耦合，下次搬家又坏 |

## 验证依据（Verification）

- `git status` 确认旧路径已删：`Tools/Publish/` 全部空目录已 `git rm` / 文件 `git mv`。
- `git grep "Tools/Publish"` 在入 git 文件中应只剩 `Assets/Framework/Minds/2-Areas/ADR/ADR-022-sdk-plugin-architecture.md:57`（待 obs 通道更新）+ 历史 commits。
- `python .claude/skills/nova-publish/scripts/publish_packages.py` 在项目根 cwd 下可正常运行（待 T21 端到端验证）。
- `.claude/CLAUDE.md` 「UPM 发布」节命令字符串与脚本实际位置一致。

## 来源（Origin）

- 会话日期：2026-05-21
- 关键对话节选：
  > 用户："/Users/<user>/Desktop/Nova/Tools/Publish  这个工具我希望沉淀为 claude code 的 skill，这个目录以后不要了"
  > 用户："为什么新 skill 路径（本地文件，不入 git）？我要走版本管理"
  > AI 落地动作：`git mv` 脚本与模板目录、改写 PROJECT_ROOT、清空 SKILL.md 内 bootstrap 残留、`.claude/CLAUDE.md` 新增 UPM 发布节、`.gitignore` 加 `__pycache__/` `*.pyc`。

## 关联

- 规则文件：`.claude/CLAUDE.md`「UPM 发布」节（已固化命令入口）；`.claude/skills/nova-publish/SKILL.md`（流程权威）。
- 相关 ADR：[[ADR-022-sdk-plugin-architecture|ADR-022]]（SDK/插件架构含发布流水线引用，待同步）。
- 相关 Pattern：[[PAT-13-publish-no-cascade|PAT-13]]（发布默认不级联）；[[PAT-41-upm-package-layout-and-manifest|PAT-41]]（UPM 包结构）；[[PAT-55-python-script-cwd-or-env-root|PAT-55]]（Python 脚本路径解析）；[[PAT-56-team-vs-personal-claude-md-split|PAT-56]]（团队 vs 个人 CLAUDE.md 分工）。
