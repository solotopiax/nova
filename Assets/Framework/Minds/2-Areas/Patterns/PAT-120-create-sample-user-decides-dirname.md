---
id: PAT-120-create-sample-user-decides-dirname
title: 脚手架与样例生成工具的人类可见文案字段必须由用户决定
summary: 视觉文案命名权归用户，禁机械派生，未给值必先询问
category: workflow
type: pattern
status: active
date: 2026-05-29
aliases:
  - PAT-120-create-sample-user-decides-dirname
  - PAT-120
keywords:
  - PAT-120-create-sample-user-decides-dirname
  - 脚手架与样例生成工具的人类可见文案字段必须由用户决定
  - PAT-120
tags: [pattern, workflow, naming, scaffolding, sample]
related:
  - "[[PAT-111-api-naming-avoid-host-module-literal|PAT-111]]"
  - "[[PAT-123-upm-sample-displayname-equals-samplename|PAT-123]]"
---

# PAT-120：脚手架与样例生成工具的人类可见文案字段必须由用户决定

## 适用场景（When）

- 写脚手架类工具 / CLI（生成项目骨架、Sample、模板工程）
- 工具产出物中存在**会被人类看到的命名**——目录名、Package Manager 显示名、菜单文案、入口标题
- 工具同时存在**仅供机器消费的派生命名**——asmdef、namespace、文件路径前缀、GUID 等

## 核心做法（What & How）

把工具的命名字段分两类，区别对待：

| 类型 | 例子 | 处理方式 |
|---|---|---|
| **机器派生字段** | asmdef name / namespace / 内部目录结构 / 类型 ID | 从输入参数确定性派生，规则写在文档里，禁人工干预 |
| **人类可见字段** | sample 目录名 / Package Manager `displayName` / 菜单标题 | **必须由用户决定**，未提供时工具层必须显式询问 |

实施 checklist：

1. 工具入口同时接收两类字段时，把人类可见字段放在 argv 末位，让用户清晰感知"这是我决定的"
2. 脚本层硬校验该字段非空 + 合法（regex `^[A-Za-z][A-Za-z0-9_]*$` 之类）
3. 调用方未明确该字段值时，工具层必须先给候选并显式询问，禁擅自落默认名
4. 工具文档在「命名约定」节明确区分“自动派生”与“用户决定”两栏
5. **项目级命名风格软规则由调用层在询问阶段把守**——例如 Nova 项目“sample 目录名一律单数 `Demo` 后缀，禁 `Demos` / `Samples` / `Examples` 复数后缀”，候选名一律遵守该风格；用户传入违规命名时工具应做软提示而不是硬拒。脚本层只管“合法字符 / 非空 / 唯一”，不管“风格倾向”

## 为什么这么做（Why）

- **业务命名权归用户**：像 sample 目录名这类字段会出现在 Package Manager 列表 / 菜单 / 文档截图里，业务方对"客户/外部开发者第一眼看到什么字"有强诉求。机械派生剥夺了这一话语权。
- **派生算法终会失灵**：从包名最后一段截 + 后缀的策略，遇到层级嵌套（`kit.network.login` vs 父级 `kit.network`）就会产生命名重复或层级不清。
- **机器侧字段反而要锁死**：asmdef / namespace 一旦让用户随手填，跨包语义就乱了；这类字段必须严格派生 + 文档化。

## 反模式（Anti-patterns）

- 工具文档写“自动派生 `<XXX>Demos`”，并把该派生作为唯一路径
- 调用时用户没说目录名 → 调用方自己拍一个“应该叫 LoginSamples 吧”直接传给脚本
- 把 `displayName` 字段也走机器派生，导致 Package Manager 中 16 个 sample 全是 `<Pkg>Demos` 格式无差异化
- 反向：让用户决定本该机器派生的字段（如让用户自己填 asmdef name），引入跨包碰撞风险
- **询问阶段推荐违反项目命名风格的候选名**——例如 Nova 项目要求单数 `Demo` 后缀，候选名却给出 `<XXX>Samples` / `<XXX>Demos` 等复数形式

## 跨项目复用提示

任何"模板生成器" / "脚手架工具" / "代码生成 CLI"都适用本铁律：**先把字段按"谁看 / 谁消费"分类，再决定让用户填还是机器派生**。Cookiecutter / Nx generators / `dotnet new` 等现有工具基本都遵循这个划分（`name` 由用户填，`namespace` 多数从 `name` 派生但允许用户覆盖）。

## 历史触发场景

早期样例创建流程曾把包名最后一段机械派生为 `<XXX>Demos`。当 `kit.network.login` 与父级 `kit.network` 同时存在时，这种派生会造成命名风格冲突，也无法表达项目要求的单数 `Demo` 后缀规则。本文因此固定为长期规则：用户决定人类可见名称，机器只负责确定性派生内部字段。

## 关联

- [[PAT-111-api-naming-avoid-host-module-literal|PAT-111]]：命名分层，避免把宿主语义硬写进公共 API。
- [[PAT-123-upm-sample-displayname-equals-samplename|PAT-123]]：sample 对外展示名与目录名保持一致的下游约束。
