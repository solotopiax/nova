---
name: nova-project-ui-create-view
description: Use when 项目组要在现有 Nova 消费项目中创建并注册业务 UIView、Prefab 与 UI 导出项，且已明确目标页面、命名空间、UIGroup 和允许的本地写入范围时使用。
---

# Nova 创建业务界面

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

再读取 `references/contract.json`、随包 `Docs/Runtime/Modules/UI/UIComponent.md`、`Docs/Runtime/Modules/UI/Definitions/UIView.md` 和 `Docs/Runtime/Modules/UI/UIManager/UIManager.md`，然后检查当前项目最近的同类业务页面。只修改业务项目内容，不修改 Nova Framework、PackageCache 或 UIManager；不手工编辑生成的 C# / JSON，通过项目已确认的 UI 导出入口更新本次需要的生成物。

## 渐进式披露

- L0：先用 frontmatter、Catalog 与共同底线判断页面创建是否属于本 Skill。
- L1：只读取本 Skill、`references/contract.json` 和已列出的 UI Docs，冻结 View、UIGroup、注册源与写入集。
- L2：仅在这些输入已确认后，读取项目中最近的同类 View、Prefab 和实际导出入口；不要预先展开全部 UI、Table 或 Config 文档。
- L3：确认后才调用源码编辑、Unity Editor/MCP 和`nova.project.ui.export`；Unity 资产写入保持单写者。
- L4：只验证本次 View 的导出、编译和按需 Play 证据；未执行的层级保持 `partial`。

打开入口先读取当前 `Docs/Runtime/Modules/UI/UIComponent.md`：普通 `OpenUIViewSync/Async` 的字符串首参只接收 UI 表 `Name`；`OpenUIViewByAssetSync/Async` 的首参只接收唯一 `AssetLocation`，泛型入口仍按类型名查表，分组、暂停与池化都来自表格；缺配置或匹配不唯一时停止，不使用旧 bool 重载覆盖配置，也不绕过注册表加载。`Name` 指配置行名称，不是 Excel 文件名；第二参数 `userData` 即使是字符串也只作业务数据，不能继续传旧分组参数。

## 冻结输入与预检

确认 `viewName`、命名空间/asmdef、目标 UIGroup、Prefab 路径和资源地址、UI 注册源、布局/交互范围、池化与遮挡策略。若 UIGroup、注册源或资产目录有多个合理候选，先请求用户选择；不要猜测或顺手新增 Framework 结构。

确认现有场景和导出链能够承载 UI：活动场景中的 Nova/UIComponent、UIGroup 的存在性、项目使用的 UI Excel/Collector/导出入口。只有用户明确要求时才启动 Unity；缺少 Unity 时只交付计划，不宣称页面已创建成功。

## 实施顺序

1. 在项目业务目录创建继承 `UIView` 的代码，沿用最近的命名、生命周期、事件解绑和数据刷新模式；不使用 `GameObject.Find`，不手工编辑生成的 C# / JSON。
2. 通过 Unity Editor 或 Unity Editor 自动化通道 创建 Prefab、挂载脚本并绑定序列化引用；绝不手写 Prefab 或 Scene YAML。
3. 仅在用户确认的 UI 注册源中登记 `Name`、`AssetLocation`、`UIGroupName`、`PauseCoveredUIView` 和 `InObjectPools` 等实际需要的字段。
4. 通过 `nova.project.ui.export` 执行本次必要导出。全量传 `scope=all/code/data`；定向导出传精确 `sourcePath`，只允许 `code` 或 `data`。先 Plan 核对全部 Excel、已保存的场景/`UISettings`、命名空间及写入集，再用当前 `PlanId` 确认一次 Execute。单文件代码仍会更新共享 `UITables.cs`，所有模式会更新 `_configs`；计划同时展示反格式或同源完整过期代码的删除范围。单文件 `all` 不支持，不拼接两次调用伪装事务。`code-only` 的 Map 属性要求已有正式 JSON；新注册先完成 `data`，再 Plan `code`。
   Execute 返回 `partial + Receipt`，等待本次 Unity 编译、代码重载后只读 Verify；跨重载用 `recovery_token`，不重放 Execute。缺少有效所有权、来源漂移、脏场景或定制配置无法冻结时停止；没有受控 Action 时保持 `partial`。导出失败立即停止，不顺带执行全量 Excel 导出、Config 导出或 Build。
5. 先收集编译证据，再在明确授权且可用的 Unity 环境中验证目标 UIGroup 的打开/关闭。若只完成代码与静态检查，返回 `partial`。

## 完成边界

达到 `unity-compile` 才能报告 `success`；若用户要求实际打开，则还必须返回 Play 证据或明确标为 `partial`。已有同名 View、Prefab 或注册行内容冲突时保持 `blocked`，不覆盖猜测。删除、替换非生成资产、外部发布、Git 操作均不属于本 Skill 的授权范围。
