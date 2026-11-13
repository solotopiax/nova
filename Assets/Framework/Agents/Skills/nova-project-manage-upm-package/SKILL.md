---
name: nova-project-manage-upm-package
description: Use when Nova 项目组要安装某个已配置 registry 包的最新版本、把已直接安装的包升级到最新版本，或安全卸载一个 direct UPM 依赖，并需经过计划、确认、Resolve 与后验验证时使用。
---

# Nova 管理消费项目 UPM 包

触发后先读取当前 Framework 的 [Docs/START_HERE.md](../../../Docs/START_HERE.md)，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

仅在当前 UPM 操作需要时，才按下述路由读取对应文档。

先读取 [references/contract.json](references/contract.json) 冻结操作与副作用。安装/升级调用 MCP Tool `nova_project_action` 驱动 `nova.project.upm.manage-latest`；卸载使用独立 Action `nova.project.upm.uninstall-direct`。只有需要理解 Action 协议或 MCP Adapter 边界时才读取 [Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md](../../../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md)，需要理解 registry、manifest、依赖预检或卸载清理语义时才读取 [Docs/Editor/EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.md](../../../Docs/Editor/EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.md)。不要预加载发布、SDK/Kit 配置或全部 UPM 文档。

仅查看版本及发布时间时，打开 `Nova/Open PlugPals` 并展开对应包：`Release Time` 显示最新版本的发布时间，位于 `Core Version` 下方；没有 `Core Version` 时紧跟 `Latest Version`。`Local Version` 和 `Latest Version` 均只显示版本号。时间采用编辑器本地时区（`yyyy/MM/dd HH:mm:ss`，24 小时制），优先读取最新版本包内的 `nova.releaseTime`，旧包才回退所属仓库该版本的上传时间，缺失时显示 `未知`；公网和内网规则一致。详细来源见 [Docs/Editor/Windows/PlugPalsWindow.md](../../../Docs/Editor/Windows/PlugPalsWindow.md)。只读查看不执行安装、升级或卸载，也不修改包清单。

## 支持范围

- `install-latest`：安装已配置 registry 中的远程最新版。
- `upgrade-latest`：把当前 direct registry 依赖升级到同一来源的最新版。
- `uninstall`：卸载当前 direct dependency，底层固定路由 `nova.project.upm.uninstall-direct`。

不接受历史版本、降级、任意 registry URL、来源切换、批量升级、Framework 发布或 `com.solotopia.nova.framework` 自身操作。

## 任务所需包的自主安装

按 [NA-014：授权范围、实时合同和恢复回执约束实际执行](../../../Docs/Onboarding/DEVELOPMENT_CONSTRAINTS.md#na-014授权范围实时合同和恢复回执约束实际执行) 的长期授权，Agent 可以为任何 Nova 项目的当前任务自行选择 PlugPals 所需 package 与必要依赖，不要求用户先报包名或逐包再次批准。先查现有能力、来源/版本、平台与依赖，说明选型依据及精确安装计划；本 Skill 的 `install-latest` 分支符合该范围时直接按有效授权继续。升级、卸载和本入口不支持的操作仍分别判断原授权与专用入口。

多个必要包按依赖串行逐包处理：当前包 Resolve/Verify 完成并稳定后，再为下一包取得新 Plan；不并行写 manifest/lock，也不预先复用会失效的多包计划。由 UPM 正常解析的依赖保持传递关系，不无故提升为 direct dependency。安装授权不代替具体能力接入或实际运行验收。

## Plan → Confirm → Execute → Verify

1. 确认 Unity 不在编译、更新包或 Play Mode。安装/升级向 `nova.project.upm.manage-latest` 传入 action 和 packageName；卸载向 `nova.project.upm.uninstall-direct` 仅传 packageName。Plan 必须只读。
2. 展示精确动作、包名、当前/目标版本、registry、缺失依赖、卸载消费者和清理边界。`blocked` / `not_applicable` 不得绕过。
3. 安装、升级与卸载 Action 均可通过 MCP 执行；卸载仍含 `Destructive`，必须绑定当前一次性 PlanId 确认。Tool 缺失或 Action 未注册时返回 `blocked`，不得改用任意 C#、反射、手写 manifest 或旧 Action 绕过。
4. 核对用户当前授权或上述长期安装授权是否覆盖精确计划；已覆盖时直接继续，不重复询问。未覆盖的动作先取得所需授权，再使用同一 action_id 调用 execute，并把当前 planId 作为 confirmation_token。任一状态漂移后重新计划并核对授权覆盖范围，不得自动重放；计划 token 不能替代授权，已有授权也不能替代当前 token。
5. Execute 提交 Resolve 后只能报告 `partial`。Unity 稳定后用同一 action_id 验证；安装/升级必须精确命中目标 registry 版本，卸载必须同时从 direct manifest 和解析图消失。

后台长期停留在 `partial` 时，先只读核对原回执、目标 manifest 与当前解析图。若目标写入已完成且仅解析未跟进，可按当前 live Schema 使用包外受控宿主的 Resolve 能力继续解析，再 Verify 原回执；不重新执行安装/卸载，不修改目标、registry 或其它依赖。宿主无此能力则保留 `partial`，不改用任意代码。

## 安全语义

安装/升级复用 PlugPals 的 dependencies 预检与 scoped registry 维护，不降级。卸载只处理 direct dependency；仍有其他节点消费时必须 `blocked`。不手写 packages-lock，不删 PackageCache、清缓存、修改 registry 配置或重放失败操作。

## 结果与边界

安装完成后，如已安装包的 [CHANGELOG.md（当前包定位）](../../../Docs/Editor/EditorUtil/EditorUtil.ImportantUpdates/EditorUtil.ImportantUpdates.md) 正式版本含 `### Important`，Editor 会按当前单包范围自动弹出重要升级声明，跨版本内容按版本分组。提示只在用户亲自阅读并明确确认后记为已读；Agent 不得自动勾选、调用确认函数或修改 [Library/Nova/ImportantUpdates（定位）](../../../Docs/Editor/EditorUtil/EditorUtil.ImportantUpdates/EditorUtil.ImportantUpdates.md)。模态窗口等待用户确认时，应说明此等待项，不将其误判为 Resolve 卡死或重放升级。仅在需要解释提示/本地恢复时读取 [Docs/Editor/EditorUtil/EditorUtil.ImportantUpdates/EditorUtil.ImportantUpdates.md](../../../Docs/Editor/EditorUtil/EditorUtil.ImportantUpdates/EditorUtil.ImportantUpdates.md)。已安装包的技术核验与人工阅读确认分别报告。

Plan 未执行不算成功；Execute 已提交但未完成后验时为 `partial`；达到精确后验才为 `success`。不默认启用 SDK/Kit、修改 ConfigMaster、导入 Sample、改业务代码、运行 Play、构建 Bundle/Player、发布包或执行 Git 操作。
