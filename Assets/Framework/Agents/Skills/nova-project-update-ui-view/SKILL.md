---
name: nova-project-update-ui-view
description: Use when 项目组要更新既有 Nova 业务 UIView、Prefab、布局或事件，或按明确页面范围批量补 TextLocalizing 的文本Key/字体绑定，并完成相应验证时使用。
---

# Nova 定向更新业务界面

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

再读取 `references/contract.json`，先定位既有 View、Prefab、UIGroup、注册源与最近同类实现。缺少已存在的目标时返回 `not_applicable`，转交 `nova-project-ui-create-view`；不要把“更新”悄悄扩大为创建页面。

## 渐进式披露

仅在命中下列条件时读取对应一页；不要递归展开 Docs 或其他 Skill。

| 条件 | 读取 |
|---|---|
| 要改 `UIView` 生命周期、可见性或事件解绑 | `Docs/Runtime/Modules/UI/Definitions/UIView.md` |
| 要改打开、关闭、UIGroup、遮挡或对象池语义 | `Docs/Runtime/Modules/UI/UIComponent.md` 与 `Docs/Runtime/Modules/UI/UIManager/UIManager.md` |
| 要改注册源或导出物 | 当前项目已确认的 UI 注册/导出入口；只读其直接说明和本次目标 |
| 要改 Prefab、序列化引用或布局 | Unity Editor / MCP 中的目标 Prefab；不手写 YAML |
| 页面批量本地化Key/FontMark绑定、补缺失组件 | [批量本地化绑定](references/localization-binding.md)，条件复用 `nova-project-update-localization` 与 `nova-project-edit-settings` |

打开入口先读取当前 `Docs/Runtime/Modules/UI/UIComponent.md`：普通 `OpenUIViewSync/Async` 的字符串首参只接收 UI 表 `Name`；`OpenUIViewByAssetSync/Async` 的首参只接收唯一 `AssetLocation`，泛型入口仍按类型名查表，分组、暂停与池化都来自表格；缺配置或匹配不唯一时停止，不使用旧 bool 重载覆盖配置，也不绕过注册表加载。`Name` 指配置行名称，不是 Excel 文件名；第二参数 `userData` 即使是字符串也只作业务数据，不能继续传旧分组参数。

## 冻结输入

确认现有 View 与 Prefab 的精确路径、目标 UIGroup、预期行为、允许改动的节点/绑定、注册源、是否需要导出，以及最低验证目标。UIGroup、注册源、布局范围或导出入口存在多个候选时先请求选择。不得覆盖未知布局、重排无关节点、改 Nova Framework、PackageCache 或生成物以外的项目资产。

批量目标先从用户指定页面/目录查出实际Prefab、TextMeshProUGUI节点、现有TextLocalizing及权威Key/FontMark映射，展示明确节点集合与差异；已有授权覆盖后串行执行共享Unity MCP，不让新手逐节点配置，不全工程扩大扫描。缺Key来源、字体资源或可靠持久保存入口时保留具体缺项，不能随便用当前显示文字猜唯一Key。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已确认的业务 View、目标行为与源码范围 | 当前项目的业务源码与 `UIView` 生命周期模式 | 定向更新的业务 View 源码 | 局部 diff 与 Unity 编译 |
| 已确认的 Prefab、节点和绑定范围 | Unity Editor 自动化通道 | Unity 保存的 Prefab 与序列化绑定 | 目标 Prefab 检查结果 |
| 已确认的 UI 注册源与导出入口（仅变更时） | `nova.project.ui.export` Plan/Execute/Verify | 本次注册变更及必要生成物 | 注册 diff 与选定导出结果 |
| 需要验证的打开/关闭行为 | 已存在的 `Nova.UI` 打开、关闭与查询入口 | 可复现的目标页面行为 | 明确授权后的 Play smoke |

## 实施与验证

1. 先只读比对当前 View 生命周期、Prefab 绑定和注册事实，再锁定最小写入集。
2. 更新业务源码；涉及 Prefab、Scene 或序列化引用时，只通过 Unity Editor / MCP 写入。
3. 仅当注册内容确有变化时调用 `nova.project.ui.export`。全量范围为 `all/code/data`；传精确 `sourcePath` 时只允许 `code/data`。先核 Plan 展示的目标 Unit、全部来源 Hash、共享 `UITables.cs`、`_configs` 和准确删除范围，再确认一次 Execute；单文件 `all` 没有原子领域入口，不拼接两个调用。`code-only` 要求已有正式 JSON。Execute 后等待本次编译/代码重载，再通过 Receipt 或 `recovery_token` 只读 Verify；来源漂移、生成代码冲突或导出失败立即停止。缺少受控 Action 时保持 `partial`，不绕过边界。
4. 先取得 Unity 编译证据。用户要求实际交互时，再在获授权的 Unity 环境中验证打开、刷新、关闭和返回行为。

普通源码更新达到 `compile` 且所需绑定持久化才能报告 `success`；用户要求实际交互或批量本地化时，还须相应 Play 与全部请求语言的实际显示证据，要求切换但未给集合时冻结最少两种。缺少 Unity 或必要 Play 证据时如实返回 `partial`。已有页面、Prefab 或注册事实冲突时保持 `blocked`，不猜测合并。删除、替换非生成资产、外部写入、Git 操作不属于本 Skill 的默认授权范围。
