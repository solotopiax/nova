---
name: nova-project-router
description: Use when 项目组以自然语言提出 Nova 项目接入、业务开发、构建或排障需求，且任务范围、风险或应触发的 Nova 消费端 Skill 尚不明确时使用。
---

# Nova 项目任务路由

先读取 `references/contract.json`，再只读当前消费项目和 Framework 包内 `Docs/INDEX.md`。目标是产出可执行的边界与下一步，而不是直接修改项目。

## 路由步骤

1. 区分项目组任务与框架组任务。新增 Runtime 模块、Inspector、SDK Plugin、Framework API 或修改 `Packages/com.solotopia.nova.framework` 都标为 `not_applicable`，说明应转交框架组。
2. 冻结目标、消费项目路径、目标平台/渠道/模式、目标场景或产物，以及允许的副作用。缺失且会改变方案时，提出一个最小澄清问题。
3. 消除常见歧义：

| 用户词 | 必须确认 |
|---|---|
| 页面 / 界面 | 只建 View，还是还需 Prefab、UI 注册、入口与运行验证 |
| 表 / 配置 | Luban、UI、Localization、Network、Sound、Vibrate 或 ConfigMaster |
| 打包 / 发布 | Bundle、Player、CDN、商店、UPM；外部写入或 Git 不因名称自动获授权 |
| 资源更新 / 热更 | 构建 Bundle、部署 CDN、运行时下载或缓存清理 |
| 接 SDK | 启用已有消费包，还是开发新的 SDK Plugin |
| 启动不了 | 编译、Play 门禁、黑屏、加载链、网络或服务端结果 |

4. 选择最窄的路径：
   - 范围或项目事实不清：`nova-project-check-readiness`。
   - 新建或注册业务 `UIView`：`nova-project-ui-create-view`。
   - 已有数据表驱动业务页面并从现有入口打开：`nova-project-data-driven-ui`。
   - 单一确定的 Operation 不强制经过 Workflow；多个写入操作仅在读写集无冲突时并行。
5. 明确不可做的事：不扫描主仓 `Minds/` 或 `.nova/`，不套用 Sample，不猜 UIGroup / 表字段 / 资源地址，不手写 Prefab 或 Scene YAML，不把 Pipify 顺序 Batch 当作可并行 DAG。

## 输出

输出 `success`（仅指路由完成）、`blocked` 或 `not_applicable`，并包含：冻结的输入、推荐 Skill、原因、计划写入集、确认门、最低验证证据和未验证项。路由 Skill 本身不应声称业务功能已完成。
