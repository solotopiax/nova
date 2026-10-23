# 验证与构建

验证只覆盖本次操作影响的真实链路：源码与配置、正式导出、编译、Play、Player、设备和外部服务分别说明。检查通过只证明其覆盖范围，不继承历史成功记录。

## 构建前只读检查

需要确认当前项目能否进入构建准备时，使用 `nova-project-preflight-build`。它调用 `nova.project.build.inspect-readiness`，底层探针归属 `EditorUtil.Build`，核对：

- 请求平台与活动平台、对应 Build Support。
- 启用的 Build Settings 场景存在且可解析。
- 持久化 ConfigMaster 绑定、Platform/Channel/DevelopMode 和导出的 Runtime 坐标。
- 场景 Asset/App 的渠道快照与 Runtime 一致。
- YooAsset Settings、Collector、Package，以及启用 HybridCLR 时的安装与 link.xml 配置。

Action 的执行成功表示检查完成；还需读取 `ready`、错误数和逐条规则。`ready=true` 只证明当前前置条件，没有执行 Bundle、Generate 或 Player，也不证明设备运行正常。检查不写配置、不导出、不切平台、不修复。

## 场景与资源检查

Agent 仍需按实际运行入口核对完整 canonical `Nova.prefab`、唯一 Nova 拓扑、Content 加载/卸载所有者及必需引用。仓库中未参与本次运行的替代入口、Sample 和插件 Demo 不属于当前运行闭包；插件 Resources、BuiltIn Resources 或不同于 Sample 的目录不等于错误。

Nova 已移除 Project Guard 的窗口、规则 Profiles 和自动 Play 检查。上述使用规范由任务中的检查与真实运行验证落实，不再依赖旧报告或自动拦截承诺。

## 合法构建入口

Unity Build Profiles、`BuildPipeline.BuildPlayer` 与 `EditorUtil.Build` 都可以构建 Player。Pipify 的 `build.package` 可批量串联导出、Bundle 和 Player；它不自动运行完整只读预检，组合任务应在构建前显式调用对应 Skill。

构建参数见 [EditorUtil.Build](../Editor/EditorUtil/EditorUtil.Build/EditorUtil.Build.md)，流水线见 [EditorUtil.Pipify](../Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.md)。

## Agent 验证顺序

1. 冻结本次目标、入口、场景闭包、平台与最小写入集。
2. 对照任务检查实际配置与结构；构建任务按需执行只读预检。
3. 只修已授权的问题，建议与未知项单独说明。
4. 按本次最低证据执行编译、Play 或构建；只有任务需要时才进入设备或外部服务验证。
5. 在对话报告本轮结果并清理临时对象、日志和测试态；不向包内提交历史验收记录。尚在使用的事务备份按工具恢复生命周期处理。
