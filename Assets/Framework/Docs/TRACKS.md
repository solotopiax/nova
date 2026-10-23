# Tracks 埋点表维护规范

所有 Framework、SDK、Kit 的 Tracks 源表和汇总表采用同一套登记与格式规则。表中事件、属性和上报时机必须对照实际调用核实；Excel 是登记源，代码是客户端实际发送行为的依据，服务端行为还需服务端证据。

## 归属与文件

| 内容 | 维护位置 |
|---|---|
| Framework 埋点及既有用户、公共属性说明 | `Assets/Framework/Tracks/Tracks.xlsx`（发布包内为 `Tracks/Tracks.xlsx`） |
| 广告聚合及 MAX 等广告渠道的事件、追加字段 | `com.solotopia.nova.framework.sdk.ad/Nova/Tracks/Tracks.xlsx` |
| IAP 核心及 Mobile、ThirdPay、Voucher 的事件 | `com.solotopia.nova.framework.sdk.iap/Nova/Tracks/Tracks.xlsx` |
| TGA 的既有固定用户、公共属性 | Framework 既有说明页，不另建 TGA 表 |
| 独立业务模块（Login、Bind、Firebase、WeChatMiniGame 等） | 所属包的 `Nova/Tracks/Tracks.xlsx` |
| MemoryProtection 独立核心扩展 | `com.solotopia.nova.framework.memory.protection/Nova/Tracks/Tracks.xlsx`，仅 `打点表-MemoryProtection`；从 Framework 原表迁出，事件、属性和目标不变 |

新增、修改或删除实际埋点调用时，同步维护既有归属的表。源码位于独立子包不表示必须独立建表；禁止为了补漏把广告渠道、支付子包或 TGA 的既有定义拆出。只有接口转发、没有自有固定事件或属性的适配包不需要空表。

保留已有事件名、属性名、事件顺序、工作簿归属与数据语义。格式统一可以调整合并和样式，不能借此重构埋点定义。是否检查某个模块由用户任务范围决定，未检查不表示该模块符合或不符合登记规范。

## 页名与内容边界

- 独立模块事件页命名为 `打点表-<模块名>`，例如 `打点表-WeChatMiniGame`；已有符合规范的名称保留。
- 所有标签页名称不使用 `$`，不同源表的页名必须唯一；生成器保留源名，不负责重命名或拆分。
- 单元格中的事件名、属性名按实际发送名称原样登记。引力的 `$MPRegister`、`$PayEvent`、`$pay_amount` 等含 `$` 的原厂标识必须保留，不能因页名规范删除。
- 保留真正列出固定属性名、类型、设置方式、来源和时机的既有说明页。动态命名占位符、接口能力、自定义事件入口、腾讯回传接口参数或“原厂自动采集”范围说明放入模块文档，不混入属性表，不单独建立泛化说明页。
- 中文事件说明、属性说明与枚举释义使用中文；标识符按代码原样保留，不把源码 XML 注释标签写入表格。

## 事件页九列

列顺序固定为：事件名、事件中文名、事件说明、属性名、属性类型、属性说明、上报时机、目标、备注。

一个事件的属性连续登记；A～C 只在该事件或说明分段的首行填写。空白表示延续当前事件，不能跨事件合并。没有固定自定义属性的真实事件可以登记一行并说明无固定属性，不为其虚构字段。已有 G～I 的值保留，不能为格式统一改写或删除。

“目标”列沿用原有值（例如 `tga`、`gravity`、`ITrackPlugin/TGA`），不得批量换成接口名 `ITrackPlugin`。代码可选接口与人类登记的统计目标是不同概念；只有真实上报目的地变化且本任务授权时才修改目标值。

## Network 视觉模板

唯一模板是 `Assets/Framework/Tracks/Tracks.xlsx` 中的 `打点表-Network`。源表与 `EditorUtil.TrackRegistry.XlsxWriter` 生成表必须同步遵守，不能只修改当前 Excel。

| 项目 | 标准 |
|---|---|
| 表头 | 深蓝 `FF1F4E78`、白字、Calibri 11pt、加粗、水平/垂直居中、自动换行 |
| 数据底色 | 全部浅绿 `FFEAF4EC`，禁止按行或事件交替颜色 |
| A/B 数据 | Calibri 11pt、不加粗、左对齐、顶部对齐 |
| C 数据 | 宋体-简 11pt、不加粗、左对齐、顶部对齐、自动换行 |
| D～I 数据 | Calibri 11pt，沿用模板默认对齐与换行设置 |
| 数据边框 | 细线 `FFB7C7D6` |
| A～I 列宽 | 28、18、36、40、14、58、32、16、48 |
| 行高 | 首行 17，默认数据行 16.8 |
| 合并 | 只合并 A～C，同一事件内按后续非空说明分段；D～I 不合并 |
| 查看 | 冻结首行并筛选；有独立标题行的既有说明页冻结前两行 |

属性说明页保留原来的标题和字段结构，使用同一表头和单色数据样式；普通列宽 32、末列 58。不得把它们强制转换成事件九列。

## 查缺补漏的证据

从调用点、事件封装、实际属性构造和分发路径核对事件集合、字段集合、类型、单位、空值条件、枚举域和上报时机。共享属性与渠道追加属性都需核对，不能只搜事件字符串。

- ID 类型按实际载荷确认，不能把所有 ID 机械改成同一类型。
- 客户端配置价格、本地化价格、报价和可信实付金额分别登记，不能互换。
- 客户端 Debug、测试订单、沙箱和模拟支付开关分别核对，不能把客户端结论套到服务端事件。
- 同名失败字段按具体 Store 区分枚举域；保留事件专属原因与补充诊断的准确含义。
- 未找到调用的预留事件明确标注当前未触发；服务端事件保留既有约定，不据客户端源码推断已部署服务端类型或语义。
- 原厂 SDK 名称以当前收录的代码或原厂文档确认；调用已提交不等于后台已接收。

## 汇总与交付检查

SDK Inspector 的“打开打点表”按钮和受控 `nova.project.tracks.generate-registry` Action 共用 `EditorUtil.TrackRegistry.Generate`，生成 `Library/Nova/Tracks/Tracks.generated.xlsx`；Action 冻结全登记源并核验实际输出。生成表是本地派生产物，不手工维护、不入 Git，不在 Editor 启动或重载时自动生成。来源顺序和有效包解析见 [TrackRegistry 文档](Editor/EditorUtil/EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.md)。

每次修改完成后检查：

1. 页名无 `$` 且唯一；单元格内原厂 `$` 名称保持原样。
2. 原有事件、属性、顺序、上报目标和工作簿归属未被格式处理改变。
3. 事件数据统一浅绿，字体与列宽匹配模板；合并仅限 A～C 且没有丢失非空文字。
4. Excel ZIP/XML 可读取，未改动的部件保留；删页时清理该页关系、内容类型和筛选定义名，避免无效引用。
5. 没有源码注释标签、已撤回子表引用、伪造字段或未经证据支持的服务端说明。
6. 修改生成器时编译并验证实际输出，不把源表检查当作生成器已验证。
7. 清理本轮临时脚本、日志和验证产物，保留用户原有改动及正式产物；说明未覆盖的 Unity 界面和后台接收验证。

## 自然语言能力与结构事务

日常埋点维护复用 `nova-project-onboard-sdk-kit/references/tracks-maintenance.md`；同领域 Operation 覆盖调用接入、已有文字、新增事件/属性/事件页、定向目标与事件页样式，不新增逐事件、字段或渠道 Skill。已有文字复用 `Agents/Tools/nova_table_source.py`；事件页结构复用 `Agents/Tools/nova_tracks_source.py` 的固定 `inspect/plan/execute/verify/recover`，权威源限当前真实登记的自有工作区，已安装缓存或厂商包只读。

结构 Plan 冻结全登记源/meta、manifest/lock、package.json、Unity 已解析 package 元数据、代码证据与完整草稿，不写源或汇总。草稿仅新增事件、追加属性、新事件页、定向文字/目标和单色样式；原事件/属性/顺序/归属保留，目标修改需精确原值与目的地证据。只改被授权页与必须的 OOXML 部件，不保存重建整本工作簿；说明页、删除、重命名、移表和不支持扩展关系明确阻断。全书公式/图表/计算链、数据验证、条件样式和打印/自定义定义名等坐标绑定保持只读；唯一支持的内置 `_xlnm._FilterDatabase` 按精确页序同步到本次 `A1:I<lastRow>` 筛选范围。

`nova.project.tracks.generate-registry` 提供全源只读快照与受控 `Plan → Execute → Verify` 汇总；Plan 的 `DataJson.registry` 可直接供结构 CLI 使用。源编辑后需重新 Plan 汇总，校验全页内容、顺序、Network 单色样式及实际生成文件 SHA-256。此入口扩展显式 Agent 调用，不改变 Editor 启动/重载不自动汇总的约定。

源 Execute 先消费一次性计划、持久备份及 Receipt，再原子替换；提交开始后取消/失败返回 `partial`，只读 Verify，不重放。源编辑的快照、请求、计划、原字节备份、Receipt 和共享写锁统一放本地 `Library/Nova/AgentActions/TableSource/`，不入 Git；路径和事务边界见上述 Skill reference。恢复先从真实备份和严格原草稿重算 after SHA/部件写集/动作摘要，仅允许该次 before/after 整源摘要，第三种摘要视为后续用户编辑并拒绝覆盖；不盲删任何页、源或备份。汇总替换已有输出时同样在 Plan 列出备份路径并保留 Receipt。源码、登记源、本地汇总和厂商接收分别验收，备份/Receipt 在完成验收前保留。

## 关联

- [SDK 文档索引](Runtime/Modules/SDK/INDEX.md)
- [TrackRegistry](Editor/EditorUtil/EditorUtil.TrackRegistry/EditorUtil.TrackRegistry.md)
