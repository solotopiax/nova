---
name: nova-project-manage-upm-package
description: Use when Nova 项目组要安装某个已配置 registry 包的最新版本、把已直接安装的包升级到最新版本，或安全卸载一个 direct UPM 依赖，并需经过计划、确认、Resolve 与后验验证时使用。
---

# Nova 管理消费项目 UPM 包

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

仅在当前 UPM 操作需要时，才按下述路由读取对应文档。

先读取 `references/contract.json` 冻结操作与副作用。安装/升级调用 MCP Tool `nova_project_action` 驱动 `nova.project.upm.manage-latest`；卸载使用独立 Action `nova.project.upm.uninstall-direct`。只有需要理解 Action 协议或 MCP Adapter 边界时才读取 `Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.md`，需要理解 registry、manifest、依赖预检或卸载清理语义时才读取 `Docs/Editor/EditorUtil/EditorUtil.PlugPals/EditorUtil.PlugPals.md`。不要预加载发布、SDK/Kit 配置或全部 UPM 文档。

## 支持范围

- `install-latest`：安装已配置 registry 中的远程最新版。
- `upgrade-latest`：把当前 direct registry 依赖升级到同一来源的最新版。
- `uninstall`：卸载当前 direct dependency，底层固定路由 `nova.project.upm.uninstall-direct`。

不接受历史版本、降级、任意 registry URL、来源切换、批量升级、Framework 发布或 `com.solotopia.nova.framework` 自身操作。

## Plan → Confirm → Execute → Verify

1. 确认 Unity 不在编译、更新包或 Play Mode。安装/升级向 `nova.project.upm.manage-latest` 传入 action 和 packageName；卸载向 `nova.project.upm.uninstall-direct` 仅传 packageName。Plan 必须只读。
2. 展示精确动作、包名、当前/目标版本、registry、缺失依赖、卸载消费者和清理边界。`blocked` / `not_applicable` 不得绕过。
3. 安装、升级与卸载 Action 均可通过 MCP 执行；卸载仍含 `Destructive`，必须绑定当前一次性 PlanId 确认。Tool 缺失或 Action 未注册时返回 `blocked`，不得改用任意 C#、反射、手写 manifest 或旧 Action 绕过。
4. 只有用户确认精确计划后，才使用同一 action_id 调用 execute，并把当前 planId 作为 confirmation_token。任一状态漂移后必须重新计划，不得自动重放。
5. Execute 提交 Resolve 后只能报告 `partial`。Unity 稳定后用同一 action_id 验证；安装/升级必须精确命中目标 registry 版本，卸载必须同时从 direct manifest 和解析图消失。

## 安全语义

安装/升级复用 PlugPals 的 dependencies 预检与 scoped registry 维护，不降级。卸载只处理 direct dependency；仍有其他节点消费时必须 `blocked`。不手写 packages-lock，不删 PackageCache、清缓存、修改 registry 配置或重放失败操作。

## 结果与边界

Plan 未执行不算成功；Execute 已提交但未完成后验时为 `partial`；达到精确后验才为 `success`。不默认启用 SDK/Kit、修改 ConfigMaster、导入 Sample、改业务代码、运行 Play、构建 Bundle/Player、发布包或执行 Git 操作。
