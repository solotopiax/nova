---
id: PAT-115
title: CHANGELOG 与项目组文案写人话
summary: 先讲功能作用，重要更新按实质筛选，变更分类有正文才出现
category: docs
type: pattern
status: active
date: 2026-06-05
updated: 2026-10-23
aliases:
  - PAT-115
  - CHANGELOG 写人话
keywords:
  - PAT-115
  - CHANGELOG 写人话
  - 项目组文案
  - 重要更新
  - Important
  - 安装入口
  - 空分类
tags: [pattern, changelog, docs, publish, copywriting]
related:
  - "[[PAT-62-readme-changelog-dual-sync]]"
  - "[[PAT-53-changelog-grep-script-enforce]]"
---

# PAT-115：CHANGELOG 与项目组文案写人话

## 适用场景

- 每次发版时编写或追加 CHANGELOG
- 包内 CHANGELOG、项目根 CHANGELOG、子包 CHANGELOG
- `Added / Changed / Removed / Fixed / Breaking Changes` 等所有版本节
- 项目组会读到的 README、接入说明、配置说明、界面提示和重要更新

## 核心规则

先说明项目组能做什么、会得到什么作用，再给确有必要的使用条件与操作。普通 CHANGELOG 每个条目用一句话概括同一功能点，主语采用使用者能看见的能力或行为变化；重要更新可以按阅读需要分段说明功能、影响和必要操作。

| 维度 | 要求 |
|---|---|
| 顺序 | 功能与作用在前；使用条件和操作随后 |
| 长度 | 普通变更一句话概括同一能力点；重要更新只展开项目组需要关注的内容 |
| 主语 | 用户可见功能 / 模块 / 行为 |
| 技术名称 | 不用字段名、类型名、签名、内部链路或文件清单代替功能说明；必要操作涉及的包名与入口必须准确 |
| Breaking | 描述“使用方要做什么”，不是“内部改了哪几个签名” |
| 详细细节 | 下沉到 `Docs`、ADR、PAT 或提交历史 |

### 分类有实际正文才出现

- `Added`、`Changed`、`Fixed`、`Breaking`、`Important` 仅在有实际正文时出现；不为模板或骨架生成、保留空分类标题。
- 未发布区没有任何条目时，只保留 `## [Unreleased]`；新增真实条目时再创建对应分类。
- 正式版本只保留非空分类，收口或清除未发布正文后不能留下空分类。
- 此规则用于当前未发布内容、新生成的版本及模板；已发布历史正文仍保持不可变，不借清理空分类批量改写历史。

### 重要更新的选择与唯一来源

- 只提炼值得项目组关注的实质功能和升级影响，不要求每个包都有重要提示。
- 常规依赖调整、示例修复保留在普通变更记录；确实要求项目组执行迁移或改变使用方式时，说明受影响者及必要动作。
- 发布名单、包的选取规则、内部流程等维护策略放在维护规范中，不写入消费者 CHANGELOG。
- 由 Agent 根据实际变更提炼草稿，经用户确认后写入各包自己的 `CHANGELOG.md` 的 `Important`；具体格式、版本归档和发布授权复用 `.nova/rules/RELEASE.md` 与 `nova-publish`，不增加第二套确认流程。
- 实际声明只维护在各包 CHANGELOG；本条目保存写作规则，不复制各版本声明，也不建立平行的重要说明清单。

### 安装条件与操作必须可执行

- 写清功能是否需要额外安装，以及准确包名；不凭显示名称或习惯拼写猜包名。
- 写清当前真实可用的入口，包括 PlugPals 的外网或内部云仓库位置；必要时给出界面中的完整操作路径。
- 商业依赖或内部包确有授权前提时一并说明，不能把“找到包”写成“所有项目都可直接使用”。
- 安装条件紧接功能和作用，避免项目组读完说明后仍需猜依赖、位置或下一步。

## 为什么这样定

- CHANGELOG 面向升版者和使用者，不是面向作者复盘 diff
- 代码细节一多，真正的行为变化反而被淹没
- “写人话”能让没读过本次代码的人在很短时间内判断：这版我能看到什么变化、我需不需要跟进
- 同一写法用于界面和接入说明，才能让项目组从功能一路找到使用条件与可执行操作。
- 重要提示按实际价值筛选，能保留关注度；按包凑提示会淹没真正需要跟进的升级影响。

## 反模式

- 把 CHANGELOG 写成压缩 diff
- 逐条罗列字段、签名和路径
- 同一能力点拆成很多技术实现小条目
- 直接搬 commit message
- 把内部发布策略写成项目组需要关注的功能更新
- 为每个候选包补一条内容相似的重要提示
- 只写“安装防护插件”，没有准确包名、仓库位置和必要授权条件
- 在另一份说明清单复制 Important 正文，造成两处维护和内容漂移
- 预先生成空的五类标题作为模板，或清除正文后保留分类标题
- 用新规则批量删除已发布历史节中的空标题

## 跨项目复用提示

所有遵循 Keep a Changelog 的项目都适用。Nova 只是在此基础上再强调：详细实现真相回到 `Docs + 源码`，CHANGELOG 只保留用户视角的变化概括。

## 来源、影响与验证依据

- 当前仓库规则：根 `AGENTS.md` 的“面向项目组的文案”；发布声明的唯一来源和原有授权边界见 `.nova/rules/RELEASE.md` 的“CHANGELOG 收口”及 `.agents/skills/nova-publish/SKILL.md`。
- 当前格式与消费方式：`Assets/Framework/Docs/Editor/EditorUtil/EditorUtil.ImportantUpdates/EditorUtil.ImportantUpdates.md`。正文由各包 CHANGELOG 读取，未发布草稿不作为已发布声明展示。
- 实际功能先行示例：`Assets/Framework/CHANGELOG.md` 的 `Important` 说明自然语言协助查阅、配置、导出、构建和排障的作用；`UPMPackages/com.solotopia.nova.framework.sdk.iap/CHANGELOG.md` 的 `Important` 说明商品配置上传带来的操作收益。
- 实际安装条件示例：`UPMPackages/com.solotopia.nova.framework.memory.protection/CHANGELOG.md` 的 `Important` 同时说明防护作用、准确依赖包名、PlugPals 内部云仓库入口及授权前提。
- 实际升级动作示例：`UPMPackages/com.solotopia.nova.framework.sdk.ad/CHANGELOG.md` 与 `UPMPackages/com.solotopia.nova.framework.sdk.iap.thirdpay/CHANGELOG.md` 的 `Important` 分别说明重试配置、自定义渠道适配，以及服务端和支付网页配对要求。
- 影响所有面向项目组的文案；以上来源用于核对写法和实际内容，不代替功能运行、安装授权或服务端兼容性验收。具体版本声明留在原包中，随原有发布流程归档，不在本条目记录发布过程。

## 关联

- [[PAT-62-readme-changelog-dual-sync|PAT-62]]
- [[PAT-53-changelog-grep-script-enforce|PAT-53]]
