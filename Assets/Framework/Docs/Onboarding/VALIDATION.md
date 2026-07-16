# 验证与构建

验证的目标是防止确定的 Nova 结构损坏，同时保留 Unity 项目对场景、资源和构建方式的选择空间。

## 三种严重性

| 层级 | 用途 | 是否阻止日常 Play / Build |
|---|---|---|
| Hard Error | 确定会破坏当前 Nova 托管运行拓扑的结构错误 | Play 可阻止；Build 仅由项目显式接入门禁 |
| Warning | 推荐做法、可疑偏离或所有权不明确 | 否 |
| Release Strict | 项目显式启用的发布验收条件 | 只影响显式严格发布流程 |

### Hard Error

Hard Error 应保持很小，只覆盖有直接证据的错误，例如：

- 承载 Nova 的托管入口不是完整 canonical `Nova.prefab`，而是残缺手工节点。
- 当前会共同运行的拓扑出现多个 Nova。
- 项目已明确声明的 Content 场景包含 Nova。
- 当前托管入口的必需序列化引用损坏。

检查只针对当前操作与项目明确声明的托管闭包。仓库里未参与本次运行的替代入口、插件 Demo、测试 Scene 或 Sample 不应导致当前 Build 失败。

### Warning

以下通常是 Warning，而不是非法：

- 业务代码直接使用 Resources、Addressables 或 Unity `SceneManager`。
- 项目没有 Contract、Pipify、representative asset 或分目录 `AGENTS.md`。
- 场景未分类，或采用与 Nova Sample 不同的目录和命名。
- 未声明范围内的资源或 Scene 无法确定所有者。

Warning 应给出证据和建议，不自动修改场景、Build Settings、Collector 或资源目录。

### Release Strict

Release Strict 由项目或发布 CI 显式启用，可检查该项目声明的 Bundle、fingerprint、代表资源、真实 Player Smoke 和发布证据。不同项目可以定义不同发布闭包；这些条件不反向成为所有 Nova 项目的通用要求。

## Unity Build 保持可用

Unity Editor 的 Build 菜单、`BuildPipeline.BuildPlayer` 与 `EditorUtil.Build` 都可以直接构建 Player。ProjectGuard 不注册全局 Build preprocessor；菜单中的 Build Validate 以当前启用的 Build Settings Scene 生成报告，但不会替用户启动或阻断构建。自定义 BuildPipeline 若要把报告作为门禁，应由项目显式传递并校验自己的 Scene 闭包；Warning 永不阻断。

Pipify 的 `build.package` 是可选自动化入口，适合批量串联导出、Bundle 与 Player 构建，但它不是唯一入口。普通 Unity Build 不要求先运行 Pipify，也不要求存在 Release Smoke 证据。

`EditorUtil.Build` 的具体参数见 [EditorUtil.Build](../Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md)，Pipify 见 [EditorUtil.Pipify](../Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.md)。

## Agent 验证顺序

1. 确认本次操作涉及的入口、Scene、BuildTarget 和 Nova 托管范围。
2. 运行日常/Build Profile，先修复 Hard Error。
3. 逐条解释 Warning；结合项目事实决定采纳、记录或忽略。
4. 使用项目原有入口执行 Play 或 Unity Build，并验证目标流程。
5. 只有明确的发布任务才启用 Release Strict，并按项目自己的发布清单补足证据。

验证器只报告和门禁，不应自动补载 Content，也不应因扫描到插件 Resources 就改写或删除插件内容。
