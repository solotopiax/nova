---
name: nova-project-configure-pipeline
description: 在 Nova 项目组要用自然语言创建、克隆、调整已保存的 Pipify Batch，增删排序步骤或设置真实参数，并希望复用日常流水线时使用。
---

# Nova 配置日常流水线

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

读取 `references/contract.json`。仅在配置参数或完整目标列表时读 [配置参考](references/configuration.md)；具体字段含义不清时才读 `Docs/Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.ConfigurationEditing.md` 与目标 Step 的 Docs。不要展开全部 Step 或其它 SDK 文档。平台构建、CDN 部署或缓存清理组合才读取 [平台交付](references/platform-deployment.md)。

本 Skill 维护具名 Batch，默认只配置和保存。用户要求实际运行时复用已有 `nova.project.pipify.run-batch`；构建、通知、外部部署等副作用按实际 Batch 逐项判断，配置授权不能代替运行授权。

## 从需求得到可审阅配置

从当前项目资产和持久化工作区定位真实 `PipifySettingsSO`、已有 Batch 与注册 Step，读取资产 GUID、当前平台和已保存参数，不要求新手抄写技术字段。多个合理工作区或业务结果需要选择时，只收集最少决策。

先用 `nova_project_action(operation="describe", action_id="nova.project.pipify.inspect-settings")` 读取 live Schema，再检查目标配置。可选 `stepId` 查询真实字段声明；凭据只看 `hasValue`。未知默认值、Missing Step 或未支持字段应明确说明，不从相邻配置猜值。

把用户结果转换为 `create / clone / edit` 及目标 Batch。复用已有 Step 和参数；调整顺序或删除时生成完整目标 `steps` 列表，使用编辑前 `sourceIndex` 精确保留/复制条目。同一 StepId 可重复，不能仅按 ID 合并。保留未请求字段，空字符串需明确清空。

## 固定执行入口

| 操作 | Action | 请求身份 |
|---|---|---|
| 只读检查与参数 Schema | `nova.project.pipify.inspect-settings` | `settingsGuid`，可选 `batchName`、`stepId` |
| 配置并只保存目标 Batch | `nova.project.pipify.configure-batch` | `settingsGuid`、`mode`、`batchName`，以及实际变更 |
| 用户明确要求运行已保存 Batch | `nova.project.pipify.run-batch` | **`pipifySettingsGuid`**、`batchName` |

三个入口各自先 `describe`，按当前 Schema `plan`。配置 Plan 展示目标资产、完整步骤顺序、字段地址、删除条目与风险，密码不回显。已有授权持续有效；仍按协议用当前 `plan_id` 作为确认 token，`execute` 同时绑定 `action_id`。输入或资产漂移后重新 Plan，断线/重载只用 `recovery_token` Verify，不能重放 Execute。

参数值采用 `edits[{fieldName,valueJson}]`，只接受与当前窗口一致的字符串、布尔、整数、浮点、枚举。步骤列表支持结构编辑；Array/List/复杂参数和属性没有受控编辑入口，报告具体字段边界，不写任意 ParamsJson、不传 Type.Method 或 C# 字符串。动态版本空值保留为运行时占位；只读平台字段不写。

窗口草稿或脏资产阻塞时，说明需要先保存或放弃草稿，不自动覆盖。保存仅目标 Settings；相同配置无需写入。配置后的 Verify 成功只证明 Batch 已保存；可再 inspect 复核目标步骤与脱敏字段。

## 结果和证据

只配置任务的 `success` 要求目标文件和完整状态通过 configure-batch Verify，并对应已审阅的 Batch/步骤/字段；未保存、漂移、字段未支持或状态未知返回 `partial` / `blocked`。无适用 Pipify 目标返回 `not_applicable`，不创建全局工作区旁路。

要求运行时，额外记录 Job 的终态与真实产物/日志，按各 Step 所需证据验收。Job 成功不自动证明 Player、真机或外部部署成功。固定 Batch 保存后后续任务直接复用，不每轮重写代码或新建 Skill。

最后报告已保存内容、实际运行边界与尚未支持字段。临时验证配置、对象、脚本和产物按本轮归属清理；用户要求交付的 Batch 保留，并明确清场结果。
