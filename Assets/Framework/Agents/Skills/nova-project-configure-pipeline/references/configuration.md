# Batch 配置参考

完整代码事实见当前 Framework 的 `Docs/Editor/EditorUtil/EditorUtil.Pipify/EditorUtil.Pipify.ConfigurationEditing.md`。本参考只保留构造请求时容易误用的边界。

| 意图 | 请求 |
|---|---|
| 创建日常批次 | `mode=create`，`batchName` 为新名，`steps` 使用已注册 ID |
| 从已有批次复制 | `mode=clone`，`sourceBatchName` 为来源，`batchName` 为不同目标 |
| 改已有批次 | `mode=edit`，来源缺省为目标 |
| 重命名批次 | `mode=edit`，显式 `sourceBatchName` 与新 `batchName` |
| 改描述 | `description={value:"..."}`；空值明确清空；省略保留 |
| 保留全部步骤 | 省略 `steps` |
| 删除、排序或复制步骤 | `steps` 是完整目标列表；`sourceIndex` 始终指向编辑前来源索引；重复来源索引形成独立条目 |
| 删除全部步骤 | `steps=[]`，需在 Plan 明示 |
| 新增步骤 | 省略该项 `sourceIndex`，`stepId` 必须已注册；用真实默认工厂创建 |
| 调整字段 | `edits=[{fieldName,valueJson}]`；省略保留，重复字段拒绝 |

`valueJson` 是 JSON token 的文本，不是任意参数对象。例如：

```json
{
  "stepId": "bundlebuilder.build",
  "edits": [
    { "fieldName": "ClearBuildCache", "valueJson": "false" },
    { "fieldName": "BuildVersion", "valueJson": "\"\"" },
    { "fieldName": "Compression", "valueJson": "\"LZ4\"" }
  ]
}
```

Step ID、字段与候选必须经 live inspect 确认。整数不接受浮点或字符串，布尔不接受 0/1，枚举使用精确名称。空字符串版本仍在实际执行时生成动态版本。

先检查现值，按最终状态验证显隐及只读：一次编辑可同时打开开关与设置子字段。CDN 自动关联开启时派生路径只读；Google Play 的禁用审核选项不能写；平台字段只能由 Unity Active BuildTarget 提供。WebGL 首包候选以当前环境为准。

当前新默认工厂仅自动创建已核实框架 DTO。未核实 Package DTO 可以先在窗口创建并保存，再复用已有条目；未知 Package 动态提供者拒绝自动字段编辑。旧空参数或缺少非字符串字段不隐式迁移。数组/列表/复杂对象字段保留原 token，但当前不编辑。

`create/clone` 同名目标已存在且完整配置一致为 no-op；存在不同配置必须显式 `edit`。配置失败写前为 blocked，写开始后为 partial，Receipt 只用于只读 Verify；不自动回滚、不运行 Step、不重放。
