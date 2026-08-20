---
name: nova-project-onboard-sdk-kit
description: Use when 项目组要为已发布且包内文档可发现的 Nova SDK 或 Kit 安装或升级、完成单一三维配置与最小本地运行验证时使用。
---

# Nova 接入已发布 SDK / Kit

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 `references/contract.json`，并把 `nova-project-manage-upm-package` 和 `nova-project-configure-runtime` 作为本 Workflow 的两个可独立验收节点。仅在当前分支需要时，使用已解析 Framework 包内的 `Agents/Tools/nova_skills.py resolve --project-root <projectRoot>` 确认消费项目实际 Framework；再从消费项目已解析目标包的 `package.json`、`Packages/<package>/Nova/{Doc,Docs,DOCS}/INDEX.md`，或 `Library/PackageCache/<package>@<version>/Nova/{Doc,Docs,DOCS}/INDEX.md` 中找到首个存在的包内 INDEX。先读该 INDEX，再只读它链接的配置、平台前置、初始化或排障页面。不要以主框架公共文档替代目标包文档，不读取主仓 `.nova/`、`Minds/`、绝对路径或未命中的厂商源码。

## 范围、文档门与冻结输入

本 Workflow 只处理已发布、已配置 registry 且包名为 `com.solotopia.nova.framework.sdk.*` 或 `com.solotopia.nova.framework.kit.*` 的单个 SDK / Kit。安装或升级必须先进入 `nova-project-manage-upm-package` 的 `install-latest` 或 `upgrade-latest` 只读 Plan；指定历史版本、降级、来源切换、未发布包、批量升级、Framework 自身、任意第三方包或厂商后台操作一律 `not_applicable`。

冻结唯一项目根、精确包名、包管理意图、目标 Platform × Channel × DevelopMode、ConfigMaster、目标 ConfigRuntimeSO、目标 SDK/Kit Config 类型、明确字段变更集、目标平台、包内文档入口、平台前置清单，以及由包内文档定义的最小本地运行探针。目标包、来源、文档入口、三维坐标、Config 类型、平台前置或探针不唯一时返回 `blocked`，不得从包名、相邻坐标、Sample 或其他 SDK 猜测。

已安装包可先复核其精确 package id、版本、来源与包内 INDEX；只有它已解析且满足本次冻结意图时，包管理节点才可返回 `not_applicable` 后继续。首次安装或升级 Resolve 后如果找不到包内 INDEX，不进入配置或运行探针：已完成的 UPM 变更保留现场，Workflow 至少返回 `partial`，不自动卸载或回滚。

## 依赖图与受控执行

```text
冻结包、坐标与平台目标
          │
          ├─ UPM Operation：Plan → 用户确认 → Resolve/Verify
          │                         │
          │                         ▼
          └──── 解析目标包 → 包内 INDEX → 平台前置核验
                                             │
                                             ▼
                    Config Operation：单一三维坐标 → Runtime 快照 → 编译
                                             │
                                             ▼
                           包文档定义的最小本地运行探针
```

UPM Plan 与执行确认、Config 写入确认、平台前置写入确认分别生效；Workflow 不因编排合并或扩大任一确认。包更新、编译和 domain reload 稳定前不得读取新包内容或执行下一节点。仅可用目标包 INDEX 明确列出的既有 Unity Editor、MCP、Importer 或项目配置入口满足平台前置；若没有可验证的已声明入口，返回 `blocked`，不得手写 Gradle、Info.plist、Manifest、Prefab、Scene、ScriptableObject 或任意 JSON 字段补丁。

Config 变更只能通过 `nova-project-configure-runtime` 的受控 Unity 编辑或已开放的类型化 Action 完成。不得调用通用反射、`execute_code` 或“按字段名写 JSON”来修改 SDK/Kit 配置。包文档没有说明目标 Config 类型、字段语义、平台条件或安全输入方式时，停止在文档门，不把 Scanner 发现、插件实例存在或无报错编译当成已接入。

## 密钥、平台与最小运行证据

- 密钥、Token、密码、AES Key/IV、Cookie、私钥、设备标识和厂商后台凭据不进入 Prompt、Plan、日志、截图、Diff 或最终报告。只报告已脱敏的字段标签、是否已由用户按包文档的安全入口配置，以及下一步需要谁确认；不得回显任何值。
- 平台前置只按目标包文档逐项核验。涉及厂商后台建档、凭据发放、商店配置、外部上传、真机安装或用户授权的条件不由本 Workflow 执行；缺少它们时如实报告 `partial` 或 `blocked`。
- 最小运行探针必须是包文档明示的本地初始化、注册、可用性或错误信号，并在用户允许的现有项目入口中收集。Unity 编译、ConfigRuntimeSO 导出、插件 Config 已启用或单条“无异常”日志都不能单独证明厂商服务可用。
- 若包文档把真实设备、厂商回调或后台结果列为必要条件，本 Workflow 最高到本地配置与编译证据；可把只读日志问题交给 `nova-project-diagnose-device-runtime`，但不声称真机、登录、支付、广告、推送、归因或厂商后台已成功。

## 结果边界

达到精确 UPM 解析、包内文档、已确认单一三维配置、编译和包文档定义的最小本地探针时，才可报告 `success`；该结果仅表示项目内接入闭环，不表示厂商后台或真实设备成功。缺少运行探针、外部条件或无法完成文档化平台核验时为 `partial`；缺少唯一输入、确认、文档、平台前置或安全入口时为 `blocked`。本 Workflow 不默认导入 Sample、改业务流程、构建 Bundle / Player、安装应用、发布 CDN、使用凭据、提交或推送 Git。
