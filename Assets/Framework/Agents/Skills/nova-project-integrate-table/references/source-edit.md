# 按行键编辑 Excel / CSV 表源

适用于“把物品 1001 的价格改为 120”等已存在行的标量编辑。先从有效 TableSettings 的 ConfigPath 找 `luban.conf`，结合 `dataDir/schemaFiles` 和实际 `tables.xml` 的 `input` 确认源文件与 Sheet；从代码/数据输出描述取得生成目录。不能把生成 JSON/访问代码、包快照或其他 Demo 的同名表当源。

工具位于当前 Framework 解析根的 `Agents/Tools/nova_table_source.py`。用消费项目现有 Python 执行，不依赖开发仓绝对路径；xlsx 要可导入 `openpyxl`，缺依赖准确返回 blocked，未经授权不安装全局环境。CSV 使用 UTF-8/BOM；`.xls/.xlsm`、含宏/二进制扩展、行插入/删除、外部 Schema/行键/公式、数组/ref/enum等复杂类型不在本 CLI 范围；已有自描述 xlsx 的普通字段追加使用下文专用分支，保留对应模块的既有专门入口。

## 固定执行

先运行 `python3 -B <frameworkRoot>/Agents/Tools/nova_table_source.py --project-root <projectRoot> inspect --source <relativeSource>`，取得实际 Sheet、唯一表头、字段/`##type`、行数与 `sourceHash`。随后指定 `--sheet/--row-key/--key-json/--column` 定向读取安全字段原值；普通 Excel / CSV 没有 `##type` 时还要明确 `--row-key-type/--type`，不推断数字文本应是 int 还是 string。类型仅支持 `int/long/float/double/bool/string`，请求原值和新值须为相应 JSON 类型。

从项目实际声明确认 rowKey，不能把 `ID/Id` 大小写、第一列或行号当约定。多字段编辑仍按每项唯一 key 定位。`inspect` 只读，目录锁和 Plan 文件属于后续执行状态。

下面是已核实的 MainDemo 示例，其他消费项目不能照抄路径或摘要；先以当前 inspect 结果替换：

```json
{
  "source": "Assets/Samples/MainDemo/Excels/Tables/TableList.xlsx",
  "sourceHash": "<inspect returned full file SHA256>",
  "sourceRoots": ["Assets/Samples/MainDemo/Excels/Tables"],
  "generatedRoots": [
    "Assets/Samples/MainDemo/Jsons/Tables",
    "Assets/Samples/MainDemo/Scripts/Runtime/DataTypes/Tables"
  ],
  "cells": [
    {
      "sheet": "Demo_Item", "rowKey": "Id", "key": 1001,
      "column": "Price", "type": "int", "beforeValue": 100, "value": 120
    }
  ]
}
```

`sourceRoots` 只能来自已冻结的真实源根；`generatedRoots` 必须列出本次有效导出描述的所有代码/数据输出根，即使为空也显式写 `[]`。源在生成目录、vendor、Packages/PackageCache、链接目录、项目外或路径跳转时拒绝。重复键、重复目标单元格、错类型、原值不匹配、文件摘要漂移、公式目标或凭据/身份字段拒绝，不自动转换/重排/跳过失败行。

1. `plan --request - --plan Library/Nova/AgentActions/TableSource/<task>-plan.json` 从 stdin 读取上述严格 JSON，1–256 个已有单元格；也可读取项目内明示请求 JSON。Plan 只读表源，返回唯一 `planId`、真实字段地址与写集；普通输出不回显 before/value。
2. 核对精确计划与已有授权后，`execute --plan <planRelative> --confirmation-token <planId> --receipt Library/Nova/AgentActions/TableSource/<task>-receipt.json`。token 不代替用户授权。执行按项目单写者锁，再查整文件和 before；同目录 tmp 完成并核对摘要后 `os.replace`，保留文件权限。
3. `verify --receipt <receiptRelative>` 只读检查整文件 hash、唯一行键和目标类型/值，不再次写入。计划一次消费，断线、源漂移或失败后不重放。此 CLI 没有 recover 子命令或整表备份恢复；有 Receipt 时先只读 verify，不能把 Tracks 的 recover 协议套到普通表源。写入开始后不完整为 partial，保留现场并报告缺失项。

共享写锁以独占锁文件实现，普通退出会清除自己的锁；进程崩溃可能留锁，必须确认原执行已结束后处理，不能按时间自动抢锁。单文件提交会在替换前重验源摘要并使用 `os.replace`，但不是多文件事务，也无法对共享锁之外的外部写者提供原子比较交换。

Execute 先保存 `completed=false` Receipt，再持久消费 Plan，然后替换源，最后写完成 Receipt。前两次状态写入的异常可能已经留下持久状态；源提交或最终 Receipt 写入失败可能返回 partial。不能因 CLI 返回 blocked/非零就自动重放、删除现场或认定源完全没变。字段名的敏感检测是有限规则，匹配通过不构成任意数据都安全的保证。

xlsx 写入仅替换目标 Sheet 的 cell XML，保留其样式属性和所有其他 ZIP 成员，不通过整书 save 重建；其他单元格的公式、样式与资源不变。xlsx 字符串按文本保存，64 位整数超过精确浮点表示范围时按数值文本保存以免失真。CSV 仅替换目标字段文本，保留 BOM、其他字段、引号、空行与换行；新增 string 若去除前导空白后以 `= / + / @ / -` 开头，因可能被表格软件解释为公式而拒绝，数值型负数按其明确类型写入。含宏工作簿明确拒绝，不能称为支持宏编辑。

请求、计划、Receipt 与写锁仅放项目本地 `Library/Nova/AgentActions/TableSource/`，属于不入 Git 的操作缓存；`.agents/` 保留可版本管理的 Agent 配置。固定状态根以外的 `Library/` JSON 不作为状态输入，`Library/` 内任何文件仍不能作为普通表源。计划/Receipt 包含已冻结的非敏感数据；凭据/身份字段不能用本工具读写，秘密不进入请求、计划或报告。执行摘要只报告地址、类型与 hash。CLI 退出码 success=0、blocked=2、partial=3；必须读 `status`。

## 追加已有自描述表的普通字段

自然语言如“怪物表新增血量 int，现有怪物先填100”继续使用同一 CLI，不按字段建立 Skill。必须先从有效 `luban.conf/schemaFiles/dataDir` 确认实际 Schema、表名、Sheet 和行键，不能把同名 Sample 当业务表。

只读接口：

```text
python3 -B <frameworkRoot>/Agents/Tools/nova_table_source.py --project-root <projectRoot> inspect --source <relative.xlsx> --layout table-fields --schema <relative.xml> --table <exact table name> --sheet <exact Sheet> --row-key <existing field> --row-key-type int
```

`row-key-type` 可为 `int/long/string`，须与实际 `##type` 一致。Schema 必须唯一声明 `readSchemaFromFile="true"` 和精确 `Sheet@xlsx`，文件相对于 Schema 所在目录解析；其他 `inputFiles`、外部字段 Schema 或复杂映射明确 blocked。inspect 返回 `sourceHash/schemaHash`、原字段及其类型/列/注释、元数据行和全部有效行键，不读取其他数据值。

追加请求复用原 `plan/execute/verify`：

```json
{
  "source": "Assets/Game/Tables/Monsters.xlsx",
  "sourceHash": "<inspect returned SHA256>",
  "sourceRoots": ["Assets/Game/Tables"],
  "generatedRoots": ["Assets/Game/Data/Tables", "Assets/Game/Scripts/GeneratedTables"],
  "layout": "table-fields",
  "schema": "Assets/Game/Tables/tables.xml",
  "schemaHash": "<inspect returned Schema SHA256>",
  "table": "TbMonster", "sheet": "Monster",
  "rowKey": "Id", "rowKeyType": "int",
  "addFields": [{"name": "Health", "type": "int", "comment": "怪物初始血量", "default": 100}]
}
```

以上路径、表名和行键仅说明请求格式，必须用实际项目事实替换。每个字段必须提供 `name/type/comment`，`default` 与 `values` 二选一。逐行值形态为 `"values":[{"key":1001,"value":100},{"key":1002,"value":150}]`，必须恰好覆盖全部原行键；不依赖列表顺序，不漏行、猜值或跳过坏行。default 是用户明确选择且被 Plan 冻结的同类型值。支持 `int/long/float/double/bool/string`；JSON 数值、布尔和文本不能混用，整数字段按32/64位范围、浮点按有限范围检查。空值、空文本、重名字段、重复/未知键、复杂类型、凭据身份字段与公式样式文本拒绝。

只有唯一 `##var`、`##type` 与后置字段 `##comment`、普通连续原字段以及有效数据行的 xlsx 可追加。目标 Sheet 的公式、合并、额外结构指令、原字段内部空洞或相邻非空内容明确 blocked；其他 Sheet 的原公式保留。最多256字段、合计8192个新元数据/值单元格。字段仅填入最右原字段之后相邻空列，旧列、旧行和旧内容不移动，不修改 Schema 文件。新增缺失物理格只继承同行最后原字段的样式，已有空物理格保留原样式；新字段 name/type/comment 分别写入已冻结元数据行。

Plan 同时包含 Schema 映射及摘要、旧列/原键/地址/样式来源、新值和完整保留证明。Execute 前用原源与 Schema 重建全部写集，按原单写者锁和一次消费提交；相同 `VERSION=1` 下原标量、本地化、Tracks 收据继续有效。非目标 ZIP 成员的原局部头、压缩字节和描述符原样保留，中央目录只改必需偏移；原目标 Sheet 除新格与 dimension 外的 XML 原字节保留。标准单盘 ZIP 支持，ZIP64/分盘或未知尾部拒绝。Verify 同时核整文件、全新增列/行值、原键/字段/注释、Schema 摘要与旧 Sheet/其他ZIP的内容和压缩记录摘要；只读，不重放。

字段追加完成后，必须对已确认 Project/Description 正式导出代码与数据（`scope=all`），等待 Unity 编译；用户要求游戏中可用时再按主 Skill 完成实际 Play 读键/字段。测试验收需恢复权威 xlsx 后重新正式导出，不能手工删生成属性。

## 正式消费闭环

CLI success 只证明源编辑完成。然后复用 `nova.project.table.export`：数据字段修改用已确认描述 `scope=data`；普通字段追加用同 CLI 的 table-fields 并导出代码和数据；复杂结构/代码目标变化用相应既有入口。保持 Source → 正式导出数据/代码 → Config 加载描述/Asset 地址 → Play `LoadAsync/LoadSync` → `HasTable/GetTable` 读取该 key/字段，不能只看数据文件存在或 Count 非零。

导出失败保留源修改与最早直接错误，不手改生成 JSON、不自动回滚或改其他描述。完整接入仅在真实 Play 读取新值后 success；只有源、导出或编译证据为 partial。日常持久数据任务保留目标值；测试任务按原基线恢复源并重新正式导出，清除本轮请求/Plan/Receipt/锁/tmp，且不清理其他 Agent 或用户状态。
