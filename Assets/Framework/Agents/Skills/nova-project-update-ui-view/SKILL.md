---
name: nova-project-update-ui-view
description: Use when 项目组要在既有 Nova 消费项目中定向更新已注册的业务 UIView、Prefab、序列化绑定或 UI 注册内容，且页面身份、预期改动与允许写入范围已经明确时使用。
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

## 冻结输入

确认现有 View 与 Prefab 的精确路径、目标 UIGroup、预期行为、允许改动的节点/绑定、注册源、是否需要导出，以及最低验证目标。UIGroup、注册源、布局范围或导出入口存在多个候选时先请求选择。不得覆盖未知布局、重排无关节点、改 Nova Framework、PackageCache 或生成物以外的项目资产。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已确认的业务 View、目标行为与源码范围 | 当前项目的业务源码与 `UIView` 生命周期模式 | 定向更新的业务 View 源码 | 局部 diff 与 Unity 编译 |
| 已确认的 Prefab、节点和绑定范围 | Unity Editor / Unity MCP | Unity 保存的 Prefab 与序列化绑定 | 目标 Prefab 检查结果 |
| 已确认的 UI 注册源与导出入口（仅变更时） | 当前项目已有的 UI 注册/导出 Action | 本次注册变更及必要生成物 | 注册 diff 与选定导出结果 |
| 需要验证的打开/关闭行为 | 已存在的 `Nova.UI` 打开、关闭与查询入口 | 可复现的目标页面行为 | 明确授权后的 Play smoke |

## 实施与验证

1. 先只读比对当前 View 生命周期、Prefab 绑定和注册事实，再锁定最小写入集。
2. 更新业务源码；涉及 Prefab、Scene 或序列化引用时，只通过 Unity Editor / MCP 写入。
3. 仅当注册内容确有变化时调用已确认的局部导出 Action；导出失败立即停止，不为保险执行全量导出、Config 导出或 Build。
4. 先取得 Unity 编译证据。用户要求实际交互时，再在获授权的 Unity 环境中验证打开、刷新、关闭和返回行为。

达到 `compile` 才能报告 `success`；缺少 Unity 或 Play 证据时如实返回 `partial`。已有页面、Prefab 或注册事实冲突时保持 `blocked`，不猜测合并。删除、替换非生成资产、外部写入、Git 操作不属于本 Skill 的默认授权范围。
