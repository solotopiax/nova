---
id: ADR-091
title: 重要升级声明以包内日志为源并按工程用户确认
summary: 包内Important声明按工程用户和包版本确认
category: editor
status: accepted
date: 2026-10-21
aliases:
  - ADR-091-important-updates-changelog-and-local-acknowledgement
keywords:
  - ImportantUpdates
  - CHANGELOG
  - Important
  - 重要升级声明
  - 本地阅读确认
  - UPM升级批次
tags: [adr, nova, editor, upm, changelog, persistence]
related:
  - "[[ADR-023-no-editor-prefs-in-framework|ADR-023]]"
  - "[[ADR-031-upm-three-piece-mandatory|ADR-031]]"
  - "[[PAT-18-editor-window-vs-util-split|PAT-18]]"
  - "[[PAT-53-changelog-grep-script-enforce|PAT-53]]"
---

# ADR-091：重要升级声明以包内日志为源并按工程用户确认

## 背景

包升级成功只能证明代码已安装，不能证明使用者知道需要执行的迁移。跨版本升级、批量升级和脚本重载还会使“本次操作范围”“已生效版本”“已经阅读”相互混淆。另建声明清单会让包内 CHANGELOG 与弹窗内容产生两个来源；机器全局的确认记录也会使不同工程或用户互相跳过提醒。

## 决策

1. **内容只来自各包自己的 CHANGELOG。** 正式声明位于 `## [SemVer] - YYYY-MM-DD` 下唯一的 `### Important` 分类，整个分类对应一个版本的完整声明，可包含多项操作。开发期放在 `[Unreleased]`，正式发布时归档；空分类从正式版本剔除，无正文则不提示。消费端忽略开发草稿和普通分类，不从 `Breaking` 自动推断声明，也不建立 `ImportantUpdates.json` 等平行来源。已发布版本不可改写，勘误或补充进入后续版本。
2. **发布者明确选择本轮文案。** 每次预检逐包展示已登记全文或“无”，询问保留、修改、补充或本次无，与最终发布确认合并。未回答不等于本次无。发布工具复用同一日志维护入口，并在上传前核对 signed tarball 中的完整 CHANGELOG 与预检源文件一致，避免声明被排除或丢失。
3. **以实际安装状态决定适用内容。** Editor 稳定后等待离线 `Client.List(true, true)` 终态，再核对 Unity 包注册信息、实际目录中的 `package.json` 和 registry 包的 lock 版本。提交请求、修改 manifest、出现 lock 或等待固定时长都不代表安装成功。读取实际安装包内的完整日志，按 SemVer 升序展示不高于实际版本且尚未确认的全部 Important；首次使用也不自动把历史声明标为已读，不联网下载旧包。
4. **显式单包与批次有固定范围。** `PlugPals.InstallPackage` 在写 manifest 前保存包名、原版本、目标版本和批次 ID；批量提交复用工具层 `BeginBatch`。单包只处理该包，批量只合并显式目标中实际到达目标版本的包，失败目标不展示、不确认。自动带动的依赖和范围外旧声明留到下一次真正启动补查。外部 Package Manager、Git 同步等没有显式批次的变更按已安装包补查，不猜测用户点击次数。
5. **确认按工程、用户和包版本持久化。** 存档位于工程内 `Library/Nova/ImportantUpdates/<本机用户哈希>/State.json`，不进入 Git，不使用 `EditorPrefs`。`Acknowledged` 的键为 `包名@正式版本`，`Observed` 仅用于解释升级跨度，`Pending` 仅保存待核验批次。读取存档后在内存筛选并生成固定展示快照；用户确认时重新读取存档、核对安装版本与适用声明集合和正文，再原子保存本窗口实际展示的确认键。记录损坏或保存失败明确报错，保留原文件和未确认状态。
6. **阅读确认独立于安装和迁移。** 只有用户勾选阅读并点击窗口确认按钮才能保存；关闭、失焦、Editor 退出或 domain reload 都不记录已读，未确认内容继续在安全时机提醒。批次跨重载仅恢复核验与展示，绝不重放安装。确认仅表示阅读知晓，不证明迁移已执行。
7. **展示入口保持自动且不能代签。** 工具层承担扫描、安装核验、筛选和持久化，`ImportantUpdatesWindow` 只展示固定快照并转发确认。模态窗口等待 UPM、编译、资源更新及 Play Mode 切换结束，batchmode 留待交互式 Editor。无手动菜单或永久跳过开关，历史回查复用 PlugPals 日志入口。Agent 不得用脚本、反射、状态文件修改或自动勾选代替人的阅读确认。

## 后果

- 包内日志同时承担发布说明与升级提示的权威来源；声明的唯一性、版本和日期错误明确阻断，代码围栏及 HTML 注释里的示例标题不参与声明解析。
- 安装成功、观察到版本、展示过内容和阅读确认各自有独立含义；部分失败和重载不会把其他包或未来版本顺带标为已读。
- 个人阅读记录属于工程本地派生状态，区别于需要团队共享的工具配置；[[ADR-023-no-editor-prefs-in-framework|ADR-023]] 的禁用 EditorPrefs 边界继续适用，配置不因此迁入 Library。清理 Library 后允许重新提示。
- 机制需要具备该能力的 Framework；编译错误导致工具无法加载时，恢复后再补查。提示和阅读记录不替代项目组迁移验收。

## 被排除方案

| 方案 | 排除原因 |
|---|---|
| 新增 JSON 清单或把所有 Breaking 自动弹窗 | 产生第二来源或把未确认的推断当成正式声明 |
| 看到 manifest/lock 变化就判定升级成功 | 无法区分已提交、解析失败和实际生效版本 |
| 按包只记一次或自动跳过首次历史声明 | 跨版本重要说明会被遗漏，观察版本不能代表阅读 |
| 全局 EditorPrefs 或随 Git 共享确认资产 | 不同工程、用户会互相影响阅读状态 |
| 关闭即已读、Agent 自动确认 | 用界面事件或自动化冒充人的知晓行为 |
| 重载后继续执行保存的安装目标 | 恢复记录不具有再次安装的授权，失败不能自动重放 |

## 来源与验证依据

- 内容契约与使用边界：`Assets/Framework/Docs/Editor/EditorUtil/EditorUtil.ImportantUpdates/EditorUtil.ImportantUpdates.md`、`Assets/Framework/Docs/Editor/Windows/ImportantUpdatesWindow.md`。
- 提取、状态、调度与确认：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.ImportantUpdates/` 下的 `EditorUtil.ImportantUpdates.Changelog.cs`、`EditorUtil.ImportantUpdates.cs`、`EditorUtil.ImportantUpdates.Initializer.cs`；呈现层为 `Assets/Framework/Scripts/Editor/Windows/ImportantUpdatesWindow/ImportantUpdatesWindow.cs`。
- 显式操作范围：`Assets/Framework/Scripts/Editor/EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.cs` 的 `InstallPackage` 与 `Assets/Framework/Scripts/Editor/Windows/PlugPalsWindow/PlugPalsWindow.Methods.cs` 的 `UpgradeAll`。
- 发布维护复用 `.agents/skills/nova-publish/` 下的 `SKILL.md`、`scripts/changelog_tool.py`、`scripts/publish_packages.py`；本地回归验证多行读写、归档、历史保护和真实 tgz 中的完整日志一致性。
- `Assets/Tests/Editor/ImportantUpdatesChangelogTests.cs`、`ImportantUpdatesStateTests.cs` 覆盖解析、跨版本与批次范围、重载恢复、按用户及展示版本确认、正文漂移和损坏存档。已执行定向 Editor 回归及真实模态窗口原生关闭检查；关闭未改变确认记录。上述依据不代表已验证真实 registry 升级或项目迁移完成。

## 关联

- [[ADR-031-upm-three-piece-mandatory|ADR-031]]、[[PAT-53-changelog-grep-script-enforce|PAT-53]]：包级日志与已发布历史保护。
- [[ADR-023-no-editor-prefs-in-framework|ADR-023]]：工程状态和机器全局偏好边界。
- [[PAT-18-editor-window-vs-util-split|PAT-18]]：工具层与窗口层职责分离。
