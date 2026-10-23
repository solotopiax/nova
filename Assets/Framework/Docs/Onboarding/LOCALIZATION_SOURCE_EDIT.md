# 本地化文本新增 Key 与语言

当前 Framework 包内 Agents/Tools/nova_table_source.py 复用 inspect → plan → execute → verify。本文只描述 layout=localization-texts 的专用源结构编辑；已有标量值仍走[按行键编辑表源](../../Agents/Skills/nova-project-integrate-table/references/source-edit.md)，正式产物仍由 nova.project.localization.export 生成。

## 发现与输入

从有效 LocalizationSettings 读取源文件和全部代码/数据生成根。用当前 Python 执行：

    python3 -B "<frameworkRoot>/Agents/Tools/nova_table_source.py" --project-root "<projectRoot>" inspect --source "Assets/Game/Excels/Localizations/Texts.xlsx" --layout localization-texts

Windows 使用 py -3 -B。xlsx 读取依赖当前 Python 的 openpyxl；缺失返回 blocked，不自动安装。输出实际 Sheet、有效 Key、语言、Desc 存在性、追加行、待用列、整文件 sourceHash 和 languageSourceHash。可选 --sheet 只筛选检查输出，不缩小 Plan 对整个工作簿的语言集合检查。

语言名从当前随包 Scripts/Runtime/Core/Definitions/Language.cs 的真实 NovaFramework.Runtime.Language 枚举解析，不维护第二份清单；拒绝数字、大小写近似、别名和 Unspecified。Plan 绑定该声明摘要，包更新或源码漂移后重新规划。

只接受已有 Nova 四行标记 Sheet：##comment / ##var / ##type / ##comment，已有唯一 Name、可选 Desc/#Desc 和至少一种真实 Language，类型均为 string。兼容单个 #English；多重 #、未知字段、重复语言、重复/敏感 Key、公式和异常结构拒绝。所有有效 Sheet 的原/最终语言集合一致；# 开头的非导出页不改。

严格请求如下。addLanguages/addKeys 可二选一或同 Plan，合计 1–256 项，不接受 cells、插列位置或其他额外字段。路径、语言和 Key 必须替换为当前事实：

    {
      "source": "Assets/Game/Excels/Localizations/Texts.xlsx",
      "sourceHash": "<inspect 的整文件 SHA256>",
      "sourceRoots": ["Assets/Game/Excels/Localizations"],
      "generatedRoots": ["Assets/Game/Jsons/Localizations", "Assets/Game/Scripts/Runtime/DataTypes/Localizations"],
      "layout": "localization-texts",
      "addLanguages": [{
        "sheet": "Texts", "language": "Japanese",
        "values": [{"key": "Shop_Title", "value": "ショップ"}, {"key": "Shop_Cancel", "value": ""}]
      }],
      "addKeys": [{
        "sheet": "Texts", "key": "Shop_Confirm", "description": "商城确认",
        "translations": {"ChineseSimplified": "确认", "English": "Confirm", "Japanese": "確認"}
      }]
    }

- addLanguages 的 values 必须为该 Sheet 每个现有有效 Key 显式给出一次译文，不多不少不重复，可明确空字符串。新增语言必须覆盖同工作簿全部有效 Sheet；不同源文件由原 Operation 核对有效 Unit 集合并分别处理，单文件成功不代表全项目一致。
- addKeys 只追加唯一新 Key，translations 必须包含全部最终语言，包括同 Plan 新语言；description 必须显式给出，没有 Desc 列时只接受空描述，不隐式建列。
- 填充值须为字符串，拒绝以 = 开头的公式表达式和凭据材料。结构写集最多 8192 个单元格，超限按明确源文件/业务任务拆分，不静默截断。
- sourceRoots/generatedRoots 继承原 CLI 的项目、链接、包、厂商和生成物拒写边界，必须来自有效项目配置。

## 写入与验证

    python3 -B "<frameworkRoot>/Agents/Tools/nova_table_source.py" --project-root "<projectRoot>" plan --request - --plan Library/Nova/AgentActions/TableSource/localization-plan.json
    python3 -B "<frameworkRoot>/Agents/Tools/nova_table_source.py" --project-root "<projectRoot>" execute --plan Library/Nova/AgentActions/TableSource/localization-plan.json --confirmation-token "<本次 planId>" --receipt Library/Nova/AgentActions/TableSource/localization-receipt.json
    python3 -B "<frameworkRoot>/Agents/Tools/nova_table_source.py" --project-root "<projectRoot>" verify --receipt Library/Nova/AgentActions/TableSource/localization-receipt.json

Plan 从 stdin 或项目内明确 JSON 读取，冻结整文件、原表头、Key/语言/合并、物理写集和样式来源。请求、Plan、Receipt 和写锁使用上述本地缓存根，不入 Git；路径和事务边界见[共享表源事务](../../Agents/Skills/nova-project-integrate-table/references/source-edit.md)。输出只给地址、类型和摘要，不回显译文；核对写集与已有授权后使用同一 planId，token 不代替用户授权。

新增语言只填最右已声明字段后相邻的空白物理列，不移动旧列、不扩展合并、不跳过有内容的备注列。新增 Key 追加到现有使用区之后，不覆盖尾部格式或重排旧行。目标有内容、位于合并区或超出 Excel 行列界限时拒绝。已有空白格保留样式；新格/新行复用已验证语言格和最后有效数据行的样式/高度。只改对应 Sheet 目标 XML 和必要 dimension，原非目标 cell、合并及其他 ZIP 成员保持，不整书 save 重建。

执行复用项目单写者锁、写前整文件检查、同目录临时文件、权限保留、一次性 Plan 和 Receipt。失败 partial 后不重放；Verify 只读整文件 hash、全部新增值、旧 Key/字段位置和合并关系，不修复、回滚或导出。退出码 success=0、blocked=2、partial=3，必须检查 status。

## 后续消费

CLI success 仅证明源结构修改完成。原本地化 Operation 后续核对所有有效 Unit 语言集合，调用正式 localization.export 完整文本/代码与支持语言输出，核对 Config/资源加载和 TextLocalizing 绑定，再验证用户请求的全部语言。字体资源、FontMark、回退、启动语言和 UI 绑定仍走原领域入口，本工具不自动修改。

缺字体、正式导出、运行入口或 Play 证据时保留 partial/blocked，不把源写入称为游戏同步显示。正式用户译文保留；隔离测试清理本轮样表、请求、Plan/Receipt、锁和临时文件，不清理其他 Agent 或玩家数据。
