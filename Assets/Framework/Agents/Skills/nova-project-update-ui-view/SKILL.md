---
name: nova-project-update-ui-view
description: Use when 项目组要更新既有 Nova 业务 UIView、Prefab、布局或事件，批量绑定控件/按需生成字段，或在明确页面范围补本地化绑定，并完成相应验证时使用。
---

# Nova 定向更新业务界面

触发后先读取当前 Framework 的 [Docs/START_HERE.md](../../../Docs/START_HERE.md)，作为所有 `nova-project-*` Skill 的共同底线。

再读取 [references/contract.json](references/contract.json)，先定位既有 View、Prefab、UIGroup、注册源与最近同类实现。缺少已存在的目标时返回 `not_applicable`，转交 `nova-project-ui-create-view`；不要把“更新”悄悄扩大为创建页面。

## 渐进式披露

仅在命中下列条件时读取对应一页；不要递归展开 Docs 或其他 Skill。

| 条件 | 读取 |
|---|---|
| 要改 `UIView` 生命周期、可见性或事件解绑 | [Docs/Runtime/Modules/UI/Definitions/UIView.md](../../../Docs/Runtime/Modules/UI/Definitions/UIView.md) |
| 本次明确选择本次打开统一清理，或等待页面真正打开后继续 | [UIViewScope](../../../Docs/Runtime/Modules/UI/Definitions/UIViewScope.md) 或 [IUIOpenAwaiter](../../../Docs/Runtime/Modules/UI/UIManager/Interfaces/IUIOpenAwaiter.md)，只读所选分支；旧用法不迁移 |
| 要改打开、关闭、UIGroup、遮挡或对象池语义 | [Docs/Runtime/Modules/UI/UIComponent.md](../../../Docs/Runtime/Modules/UI/UIComponent.md) 与 [Docs/Runtime/Modules/UI/UIManager/UIManager.md](../../../Docs/Runtime/Modules/UI/UIManager/UIManager.md) |
| 要改注册源或导出物 | 当前项目已确认的 UI 注册/导出入口；只读其直接说明和本次目标 |
| 要改 Prefab、序列化引用或布局 | Unity Editor / MCP 中的目标 Prefab；不手写 YAML |
| 要改布局、根画布、方向或安全区 | [Docs/Runtime/Modules/UI/UIComponent.md](../../../Docs/Runtime/Modules/UI/UIComponent.md) 的“屏幕适配与安全区”，实施前冻结适配方案；UIComponent 配置复用 `nova-project-edit-settings`，Player Settings 按文档核对真实宿主通道 |
| 页面批量本地化Key/FontMark绑定、补缺失组件 | [批量本地化绑定](references/localization-binding.md)，条件复用 `nova-project-update-localization` 与 `nova-project-edit-settings` |
| 既有普通 Prefab 批量补控件引用，或明确选择生成机械字段 | [控件绑定与字段生成](references/control-bindings.md)，复用实际 `inspect-bindings` / `configure-bindings`；PSD输入先匹配原能力 |

打开入口先读取当前 [Docs/Runtime/Modules/UI/UIComponent.md](../../../Docs/Runtime/Modules/UI/UIComponent.md)：普通 `OpenUIViewSync/Async` 的字符串首参只接收 UI 表 `Name`；`OpenUIViewByAssetSync/Async` 的首参只接收唯一 `AssetLocation`，泛型入口仍按类型名查表，分组、暂停与池化都来自表格；缺配置或匹配不唯一时停止，不使用旧 bool 重载覆盖配置，也不绕过注册表加载。`Name` 指配置行名称，不是 Excel 文件名；第二参数 `userData` 即使是字符串也只作业务数据，不能继续传旧分组参数。

## 冻结输入

可选生命周期分支须明确哪些本次新增工作交给 `OpenScope`、哪些原有工作仍由原拥有者清理；只登记前者，禁止扫描接管手工 CTS/订阅或重复释放。等待打开分支冻结原注册身份、业务调用点与本请求取消责任，选择 `OpenUIViewAwaitAsync` / `OpenUIViewByAssetAwaitAsync` 并分别处理结果状态；不能把旧序号调用自动替换成新等待时序。旧自定义 Manager 无新可选能力时只阻断所选增强，不要求旧项目改接口。

选择作用域且页面可能从未 Awake 时，按 UIViewScope 文档区分原生令牌与本作用域短期 Update 兜底；关闭仍同步结束，销毁兜底需后续 PlayerLoop。验证非激活对象销毁、觉醒交接和重开身份，不向旧页添加监控或把修复代码存在当作 Play 已通过。

确认现有 View 与 Prefab 的精确路径、目标 UIGroup、预期行为、允许改动的节点/绑定、注册源、是否需要导出，以及最低验证目标。UIGroup、注册源、布局范围或导出入口存在多个候选时先请求选择。不得覆盖未知布局、重排无关节点、改 Nova Framework、PackageCache 或生成物以外的项目资产。

完整产品适配先列实际页面清单，覆盖独立启动画布的闪屏、Loading/启动弹窗及玩法、暂停/结算；按产品声明的横屏或竖屏执行，不默认补另一方向。按内容区域避让刘海/挖孔与手势区，不能把整页统一缩进或单张玩法截图当作完整适配证据，具体规则见同一 UIComponent 文档。

布局/画布/方向/安全区分支先按 UIComponent 文档冻结 `screenAdaptationPlan`，读取当前方案、有效值与唯一拥有者，明确原值/目标值及参考/边界比例和安全区验收。先复用现有根缩放与安全区接线，缺少内容容器时仅补业务范围的最小适配；不要重复缩放、重复扣边距或照抄物理像素 offset。UIComponent 配置通过 edit-settings 的精确范围执行；Player Settings 按文档核对真实宿主通道，缺少能力时保留缺口。纯事件、文案或数据绑定且布局不变时只核对本次影响，不强制全分辨率验收。

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
3. 仅当注册内容确有变化时调用 `nova.project.ui.export`。全量范围为 `all/code/data`；传精确 `sourcePath` 时只允许 `code/data`。先核 Plan 展示的目标 Unit、全部来源 Hash、共享 [UITables.cs（定位）](../../../Docs/Editor/EditorUtil/EditorUtil.UI/EditorUtil.UI.Exporter.md)、`_configs` 和准确删除范围，再确认一次 Execute；单文件 `all` 没有原子领域入口，不拼接两个调用。`code-only` 要求已有正式 JSON。Execute 后等待本次编译/代码重载，再通过 Receipt 或 `recovery_token` 只读 Verify；来源漂移、生成代码冲突或导出失败立即停止。缺少受控 Action 时保持 `partial`，不绕过边界。
4. 先取得 Unity 编译证据。用户要求实际交互时，再在获授权的 Unity 环境中验证打开、刷新、关闭和返回行为；布局分支还须按 UIComponent 文档验证适配计划与实际生效配置，必要证据缺失时保持 `partial`，不能只以编译通过证明适配完成。

普通源码更新达到 `compile` 且所需绑定持久化才能报告 `success`；用户要求实际交互或批量本地化时，还须相应 Play 与全部请求语言的实际显示证据，要求切换但未给集合时冻结最少两种。缺少 Unity 或必要 Play 证据时如实返回 `partial`。已有页面、Prefab 或注册事实冲突时保持 `blocked`，不猜测合并。删除、替换非生成资产、外部写入、Git 操作不属于本 Skill 的默认授权范围。
