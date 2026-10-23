# 埋点与 Tracks 日常维护

用户说明想统计什么、何时发生及目的地后，先核对现有业务调用、公共接口与目标包 Docs。复用 `ITrackPlugin.TrackEvent/SetUserProperty`、`IAttributionPlugin.TrackEvent` 和现有事件/账号链；GameLogin 已通知的注册/登录不再加一次。业务 source 修改走本 Workflow 已授权工作区编辑，事件订阅和 UI 触发复用相应 Operation；逐事件、字段、渠道不新增顶层 Skill。

## 源表归属

只读已安装 Framework `Docs/TRACKS.md` 及目标包的 `Nova/Tracks/Tracks.xlsx`。广告渠道归广告聚合，IAP 子包归 IAP，TGA 固定属性归 Framework 已有说明页；保留原事件、属性、目标和单元格内原厂 `$` 标识。事件页九列、中文说明、Network 单色样式及 A～C 合并按当前规范执行。

消费项目不手改已安装包快照。项目自有事件维护自有权威工作簿并与 source 对照；当前汇总器没有自动扫描业务任意目录的承诺，不把新工作簿放到任意目录就称已纳入汇总。需纳入正式模块时按模块现有发布/源目录责任维护，消费端工具缺口明确保留。

## 现有生成接口与边界

`NovaFramework.Editor.EditorUtil.TrackRegistry.Generate(projectRoot)` 是现有 Editor API。SDK Inspector 的“打开打点表”已调用它，生成 `Library/Nova/Tracks/Tracks.generated.xlsx`；汇总读取已有登记，不解析源码、不证明后台接收，不在启动或重载时自动生成。稳定汇总通过 `nova.project.tracks.generate-registry` 的 `describe → plan → execute → verify` 调度；底层仍只调用该 C# API，不每轮临时拼执行代码。

已安装包中的维护文档、业务源码和 Excel 可只读审查。自有 Workbook 的已有九列事件页文字补漏，复用同一个 `Agents/Tools/nova_table_source.py` 和现有 inspect/plan/execute/verify 事务，不新增保存接口。该叶子入口不改结构；新增事件/属性/事件页、定向目标与事件页样式使用下文同领域结构 CLI，删除/重命名/说明页结构治理仍保持明确边界。

## 确定性编辑已有文字

用户目标只涉及项目自有既有事件页文字时，复用父契约的 `tracks-source-text` 画像和 `tracks.source.text` 任务；不新增 Skill、Action 或 CLI。画像显式选择空子 DAG，最少输入为 `projectRoot` 与 `tracksOwnedWorkbookEventPropertyAndTextWriteSet`，仅执行下面的既有表源事务。原父及完整原依赖的 `SKILL.md/contract.json`、真实任务函数源和本引用仍全部冻结为 `sourceRevision`；原确认、锁及 `writeScope` 保持，其他接入任务未选画像时继续原验收门。

本目标需要当前静态审查与实际 `artifact` 证据：定向 inspect、原整源与文字写集、一次 Plan/Execute/Verify 的真实收据、整源与所有文字/身份/位置/合并/样式/非目标 ZIP 部件核验，以及本轮清场。证据绑定准确 `taskId=tracks.source.text`、`profileId=tracks-source-text` 和当前全部源码指纹；旧结构或其他分支记录不作为本画像验收。完成文字维护后不要求无关 SDK Play 或厂商上报；若用户同时要求运行业务、结构变更或正式汇总，分别进入其真实分支并验收。

先核对项目自有权威 Workbook 与代码来源，再读取唯一事件页，不对已安装包写入：

```text
python3 -B <resolvedFramework>/Agents/Tools/nova_table_source.py --project-root <projectRoot> inspect --source <relativeTracks.xlsx> --layout tracks-events --sheet <exactSheet> --event-name <actualEvent> --property-name <actualProperty> --column 属性说明
```

`layout=tracks-events` 只接受首行精确九列、页名无 `$` 的 `.xlsx` 事件页。`eventName/propertyName` 为实际复合身份；相同属性可以存在于不同事件，同事件内重复则阻断。空属性不猜为无固定属性；原表确实写出 `无固定属性` 才可用该精确字符串定位。敏感身份/凭据属性与明显凭据材料不进入查询或计划。

可修改列仅为 `事件中文名/事件说明/属性类型/属性说明/上报时机/备注`，值类型为字符串；类型说明仍须有真实载荷证据。`事件名/属性名/目标` 保持只读。A～C 合并文字只定位当前说明段首格，Plan 会列出实际 `address/selectedPropertyRow/eventStartRow/mergedRange/mergeFingerprint`；从多个属性指定同一合并首格会拒绝重复写入。G～I 不延续、合并或自动补值。非规范合并、说明页、未知布局、公式和缺失已有单元格明确阻断，不自动整理格式。

请求示例（beforeValue 使用定向 inspect 返回的真实原值）：

```json
{
  "source": "Assets/Game/Tracks/Tracks.xlsx",
  "sourceHash": "<wholeFileSha256>",
  "sourceRoots": ["Assets/Game/Tracks"],
  "generatedRoots": [],
  "layout": "tracks-events",
  "cells": [{
    "sheet": "打点表-Business",
    "eventName": "business_enter",
    "propertyName": "level_id",
    "column": "属性说明",
    "type": "string",
    "beforeValue": "关卡",
    "value": "当前关卡编号"
  }]
}
```

复用[表源事务参考](../../nova-project-integrate-table/references/source-edit.md)的固定 `plan --request ... --plan ...`、`execute --plan ... --confirmation-token <planId> --receipt ...`、`verify --receipt ...`；仅借其事务与边界，不执行 Luban 导出。计划与 Receipt 使用不同的项目 `Library/Nova/AgentActions/TableSource/*.json`，完整 sourceHash、beforeValue 与合并布局均冻结；1 至 256 个现有单元格、同项目单写者、一次执行，失败后只 Verify 不重放。已有授权覆盖具体文字写集时直接执行，合并首格影响完整事件说明段须在计划中明确。

保存仅替换目标 sheet 的已存在 cell XML，保留其它 ZIP 部件、原合并、样式、事件顺序和目标。Verify 核对整源、复合身份、位置、合并摘要及值；运行期事件实现、正式模块发布和 TrackRegistry 汇总仍分别按真实消费者验证。真实 Framework Network 工作簿的隔离副本已覆盖叶子、合并首格、身份/目标/样式保留与漂移，但这不是当前消费项目或厂商后台的验收。

新增/改动代码、权威登记、正式产物及实际业务触发须同轮一致。只读审查、源表登记、生成汇总、厂商接收分别提供证据；临时日志和汇总测试产物清场。生产事件或厂商上报运行需已有精确授权和对应环境，不用本地源码成功代替。

## 事件页结构与正式汇总

用户目标仅覆盖本节表结构与本地汇总时，选择父契约的 `tracks-source-structure` 画像及 `tracks.source.structure` 任务：必需输入为 `projectRoot` 与 `tracksStructureRegistryCodeEvidenceAndCompleteDraft`，使用原结构 CLI 和汇总 Action，子 DAG 为 `requires=[]`。仍冻结完整原父与原子 Skill 的 `SKILL.md/contract.json`、当前实际函数源码和本引用；画像只跳过本目标不需要的 UPM、Config 与 SDK 初始化，不削弱原写入、确认、锁和幂等规则。

验收需要准确绑定 `taskId/profileId` 的当前 `static` 与真实 `artifact`：原源及完整草稿/全登记冻结、一次源事务和 Verify、写后重新 Plan 的全源汇总及 Verify、实际源/产物 SHA、页序/行/样式/非目标部件与本轮清场。普通表结构编辑不产生 C#，不要求无关 SDK Play；若同时修改生成器或业务代码，则另补实际编译及该运行链证据，不能由本画像验收代替。发送、服务端约定、厂商接收与设备业务继续各自验收。

需要新增事件、追加真实属性、新增事件页、定向调整统计目标或统一事件页样式时，复用 `Agents/Tools/nova_tracks_source.py`。固定操作为 `inspect → plan → execute → verify`，异常恢复为 `recover → verify`。文字叶仍优先沿用上面的 `nova_table_source.py`；不为列、事件、SDK 或渠道新增 Skill。

先通过 `nova.project.tracks.generate-registry` 的 `describe` 与只读 `plan {"scope":"all"}` 取得 `DataJson.registry`。把该对象原样存为项目 `Library/Nova/AgentActions/TableSource/<name>-registry.json`；快照包含真实全部来源、各整源与 `.meta` 摘要、manifest/lock、包清单和当前 Unity 已解析 package 元数据，不能自己删来源、改归属或用 `Tracks.generated.xlsx` 冒充源。Plan 不写源或汇总，不需执行 Generate 就能取得快照。Gateway 的 `<project-root>/` 路径占位符由 CLI 按已验证项目根严格还原，再重验后缀、链接和全源身份；不存在文件的序列化快照可能省略 `sha256:null`，仅 `exists=false` 时规范补空摘要。项目根外的其他脱敏路径无法核验时明确阻断，不猜用户目录。跨源重名或页名 `$` 先报告具体阻塞。

快照、请求、Plan、原字节备份、Receipt 与共享写锁统一保存到 `Library/Nova/AgentActions/TableSource/` 本地缓存，不纳入 Git。路径与事务边界复用[表源事务参考](../../nova-project-integrate-table/references/source-edit.md)，不清理尚需验收或恢复的记录。

结构入口只编辑真实汇总器登记的既有权威源：开发仓 `Assets/Framework/Tracks/Tracks.xlsx`、自有 Nova SDK/Kit `UPMPackages/<package>/Nova/Tracks/Tracks.xlsx`，或当前真实 `Embedded` 的自有 Nova 包。消费态 PackageCache、registry 快照、vendor、符号链接和任意业务目录不能写；独立项目业务表仍须已有正式模块归属，不能仅创建到任意 Assets 路径便宣称被汇总。归属锁定为 `ownerRoot`，禁止借结构编辑拆广告渠道、IAP 子包或 TGA 既有登记。

```text
python3 -B <resolvedFramework>/Agents/Tools/nova_tracks_source.py --project-root <projectRoot> inspect --source <exactRelativeTracks.xlsx> --owner-root <actualOwnerRoot> --registry Library/Nova/AgentActions/TableSource/<name>-registry.json
```

强类型草稿仅支持：

| kind | 参数与影响 |
|---|---|
| `appendEvent` | `sheet/eventName/chineseName/description/properties`；事件追加到页尾，保留现有事件与顺序 |
| `appendProperties` | `sheet/eventName/properties`；插入该事件末尾，保留原有属性及其目标 |
| `newSheet` | `sheet`；新增全登记源唯一、无 `$` 的 `打点表-*` 事件页；同一草稿必须补真实事件 |
| `setCell` | `sheet/eventName/propertyName/column/beforeValue/value`；现有文字列或单个 `目标`，改目标额外须 `targetEvidence`，不自动改整事件目标 |
| `normalizeStyle` | `sheet`；统一 Network 单色、字体、边框、列宽、行高、冻结和筛选；只在 A～C 按事件/非空说明分段合并 |

每个 `properties` 行明确提供 `name/type/description/timing/target/remarks` 字符串；名称按真实载荷保留 `$`，不补动态占位或凭据字段。无固定属性事件可保持空 `name/type` 并在 description 明确 `无固定属性` 的真实单行，或沿用已有同名说明行；不造预留字段。A～C 文字变更定位说明段首格，其影响覆盖该段全部属性；同草稿重复首格阻断。G～I 按实际行保存，不继承、合并或批量替换目标。已有说明页保持其结构；全书公式、图表、计算链、图形、表对象、扩展关系、数据验证/条件样式/打印区域等物理坐标绑定（仅内置 `_xlnm._FilterDatabase` 精确随 autoFilter 更新）、非规范合并、删除、重命名及移表明确阻断，不默默清除功能。

请求使用严格 JSON（省略号和示例摘要均需替换为实际内容）：

```json
{
  "source": "Assets/Framework/Tracks/Tracks.xlsx",
  "sourceHash": "<inspectWholeSourceSha256>",
  "ownerRoot": "Assets/Framework",
  "registry": "<replaceWithActualDataJsonRegistryObject>",
  "codeEvidence": [{"path": "Assets/Game/Tracking.cs", "sha256": "<actualWholeCodeSha256>"}],
  "operations": [{
    "kind": "appendEvent", "sheet": "打点表-Network",
    "eventName": "game_enter", "chineseName": "进入游戏", "description": "现有业务入口完成后登记",
    "properties": [{"name": "source", "type": "string", "description": "实际进入来源", "timing": "进入成功时", "target": "gravity", "remarks": ""}]
  }]
}
```

这里的 source、owner 和 sheet 只展示字段语法，正式执行必须按真实模块归属选择。`codeEvidence` 冻结实际 Assets 或本次自有 Nova 工作区/Embedded 模块调用/载荷 C# 文件的整份摘要，登记本身不证明事件实现或厂商接收；源码位于已安装缓存包时只读查验，本工具不能据此获取包写入权限。`registry` 实际必须是对象。Plan 输出完整源写集、被修改 ZIP 部件、动作身份及前后 SHA，不把草稿正文回显到 Console。

```text
python3 -B <resolvedFramework>/Agents/Tools/nova_tracks_source.py --project-root <projectRoot> plan --request Library/Nova/AgentActions/TableSource/<name>-request.json --plan Library/Nova/AgentActions/TableSource/<name>-plan.json
python3 -B <resolvedFramework>/Agents/Tools/nova_tracks_source.py --project-root <projectRoot> execute --plan Library/Nova/AgentActions/TableSource/<name>-plan.json --confirmation-token <planId> --receipt Library/Nova/AgentActions/TableSource/<name>-receipt.json
python3 -B <resolvedFramework>/Agents/Tools/nova_tracks_source.py --project-root <projectRoot> verify --receipt Library/Nova/AgentActions/TableSource/<name>-receipt.json
```

Plan 不写源，生成字节可重复。Execute 与现有表源 CLI 复用同项目单写锁；前置 hash/metadata/code/页身份不符即阻断。先消费一次性计划并保存原表备份与 Receipt，再同目录临时写入、fsync、整源 hash 重验及原子替换；只改精确目标页、追加 styles 或新页所需 workbook/rels/content-types 部件，其余 ZIP 部件保持 bytes。标准 `xl/printerSettings/*.bin` 打印设置只保留不改写；宏 `vba` 一律拒绝，目标未知二进制扩展保持只读，不把 `.bin` 文件名统称为宏。结构操作会将受影响事件页统一样式，Plan 必须展示该页影响范围。消费计划后的任意状态写入、取消或提交失败均为 `partial`，列出 Plan/备份/Receipt 的实际存在状态；Receipt 存在时只 Verify、不重放 Execute。

用户明确要求恢复该次精确源写入时，执行 `recover --receipt <receipt> --confirmation-token restore:<planId>`。恢复先从真实备份和原严格草稿独立重算 after SHA、部件写集及动作摘要，只接受当前整源等于本次 before/after SHA，并重验归属、其他登记源及元数据；若用户后续编辑已产生第三个 SHA 则拒绝覆盖。不盲删新页、源文件或备份。恢复仅撤销该次 Excel 写入，允许业务源码已另行改动，但不会同时回滚代码；Verify 仍核对原 codeEvidence，源码变化时报告 `partial`，由当前源码重新审查登记。恢复成功仍须 Verify；备份与 Receipt 是恢复证据，完成验收后仅清理本轮明确归属的状态。

源 Verify 后重新 Plan `nova.project.tracks.generate-registry {"scope":"all"}`，按已有授权走 `execute → verify`，核对全源/hash/meta、页序、全部行内容和实际 Network 单色样式。旧 Plan 绑定写前源，不能复用。该 Action 只替换 `Library/Nova/Tracks/Tracks.generated.xlsx`，Plan 列出既有输出备份的精确路径；Execute Receipt 支持断线后只读 Verify，Plan 载荷没有执行后摘要不能称成功。源表事务恢复和本地汇总不是生产数据回滚；业务实际发送、服务端约定及厂商接收分别验收。生成测试及本轮临时状态完成后清场，正式源及用户原有产物保留。

inspect 对单页不受支持的结构可返回 `unsupported-structure`，整体 success 只表示完成检查，不表示所有页可修改。Plan 的摘要和 canonical ID 不构成签名或授权，Execute 仍在共享锁内从真实源重建草稿；单文件原子替换不能保护未遵守共享锁的外部编辑。Verify 始终只读，恢复前后都不自动重建汇总或回滚业务代码。
