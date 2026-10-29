# 日常存档维护

该参考复用 `nova-project-integrate-persistence` 的同一契约。固定存储调用写在项目自有的明确业务方法中；已有同版本方法先复用，不让 Agent 每次重新拼装序列、执行临时 C# 或反射私有 Inspector 方法。

普通存储接入、默认元数据检查、Editor 值 AES、SQLite Cipher 和 Privacy 默认 AES 轮换都复用此 Skill。三种存储与独立轮换分支复用同 `inspect-data/migrate-encryption/recover-maintenance`，分别冻结 scope 与备份/恢复契约；元数据成功不代替迁移前置，不按密码、存储或字段增加 Skill。

本机备份、操作日志与维护标记统一保存在项目 `Library/Nova/AgentActions/PersistenceMaintenance/`，不进入 `.agents/` 或 Git。未结束维护前不清理其缓存。

## 按用户目标选路线

| 用户目标 | 入口与交付 | 执行边界 |
|---|---|---|
| “退出后数据没了”“某个存档读不出来” | 只读核对现有业务调用、对应 Manager Docs、脱敏日志与现有 Inspector 数据视图 | 不调用 Save、Load、AES 切换或编辑按钮来探查；FileFragment Load 可恢复或隔离坏档，打开 Editor 数据视图也可能取得 SQLite 连接或做懒加载。需避免文件变化时先只读文件元数据与已有日志 |
| “新增一个存档字段”“旧版进度改为新版结构” | `workspace-edit` 修改冻结的项目业务模型与迁移方法，复用公开存储接口和现有启动入口 | 业务接入，不是受控 Project Action；不直接修改现有 `.dat`、DB、注册表、Config 或生成物；真实迁移运行单独冻结数据集 |
| “清掉这批测试存档” | 已确认测试分类/条目，复用业务已有明确清理方法及公开 Remove/Save 契约 | Play 观察与测试数据清理不能扩张到玩家数据、整个 persistentDataPath 或其它存储实现；没有对应现存入口时先实施项目业务入口再独立验收 |
| “检查这份 Editor 分片存档”“启用 FileFragment AES” | 受控 `inspect-data` → `migrate-encryption` → Verify，失败时独立 `recover-maintenance` | 只支持当前 Editor 共享 FileFragment 全集和唯一自有普通 Prefab AES 字段；显式 scope、源 SHA 和原 Operation 凭据，详见下节 |
| “检查 PlayerPrefs 索引有没有缺项”“看看 SQLite 文件是否齐全” | 同一 `nova.project.persistence.inspect-data`，`storage=PlayerPrefs` 或 `SQLite`，实际计划确认后执行并 Verify | 默认 Unity provider 托管索引或固定四文件元数据；不读取玩家值、保存偏好、开库或证明落盘/一致快照；写维护须另冻结下方值范围与实际 Plan |
| “启用 PlayerPrefs/SQLite 值 AES” | 同三项受控 Action，按下方独立 storage/scope 计划、执行、后验与恢复 | 已索引字符串或标准 SQLite 值；PP 需接受全局 Save，SQLite 同库事务与 Nova lease；不扩大为任意玩家数据编辑 |
| “换 SQLite 数据库密码” | 同 `migrate-encryption` 的 `rotate-sqlite-cipher` 分支 | macOS Editor 标准 SQLite 非空→不同非空密码，同库轮换，唯一组件字段与独立封存恢复；不处理明文转换、任意 schema/SQL/值编辑 |
| “轮换现有 Privacy AES 密钥并保留存档” | 同三 Action 的 `inspect-privacy-aes/rotate-privacy-aes` 分支 | macOS 当前 Editor 标准三存储、当前 Privacy 逻辑组与配对 Master/Runtime；全局 Save、默认消费者边界需明确；设备与外部消费者独立验收 |
| “任意批量编辑玩家存档” | 冻结业务模型、完整目标与恢复条件，先复用授权业务方法 | 当前固定维护不开放任意 schema/SQL/值编辑；缺业务规则或精确授权时保持对应 `blocked`，不使用字段 patch/旧 Inspector/反射绕过 |
| “清理热更缓存重新下载” | 路由 `nova-project-integrate-resource`，定向读 `Docs/Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Cache.md` | 与业务存档删除不同。已有 `Nova/Clean Hotfix Caches` 菜单只清 YooAsset Editor 沙盒及顶层 `*.version`，保留 DeviceID、其他文件与首包；调用前须由命中的资源契约声明当前宿主入口和精确删除范围 |

只读源码或文件快照不能证明当前缓存值、设备数据或下次启动耐久性。生产数据内容、AES Key/IV、数据库密码、账号和订单只按授权脱敏，报告使用分类、条目身份及计数，避免回显值。

## 受控元数据检查

先按 `Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md` 读取当前分支。复用同一 `inspect-data`，提供自有普通 Prefab 中唯一标准 PersistComponent 的 `targetGuid/globalObjectId`、精确 `storage=PlayerPrefs` 或 `SQLite`、`dataScope=current-editor-global-data`；不传 `enableAES/sourceSha256`、路径、密码或 SQL。保持 EditMode，无 Persist Inspector、运行实例、编译、目标草稿和目标 PrefabStage；不主动关闭或保存它们。

- PlayerPrefs 仅默认 Unity provider，小游戏 provider 返回 `not_applicable`。读取两类已知索引及 HasKey，不读取条目值或未知键；结果为计数、引用缺项、索引 UTF8 大小与整集 `metadataSha256`。缺项返回 `partial`，非法/重复/超限或双次观察变化返回 `blocked`；不存在分类索引不等于没有存档。完整键空间、全局 dirty 和落盘状态仍为 `unknown`。
- SQLite 不开任何连接，仅读当前 Editor `Persist/SQLite/game.db` 及 `-wal/-shm/-journal` 固定文件的存在性、大小、SHA256；未知目录项只计数。固定路径必须无链接/目录，单文件及合集最多 64 MiB，目录最多 32 项；双次有界读取变化或范围/占用不满足时 `blocked`。孤立伴随文件、零字节主库或未知项为 `partial`。不验证数据库格式、schema、玩家值或 Cipher；不生成 `-shm`。全部文件不存在只报告空观察。
- 执行与 Verify 都重新读取；不把旧收据当一致快照。成功仅限元数据检查完成，明确 `playerValues=not_read`、`durableState=unknown`、`writeMaintenanceSupported=false`；SQLite 逻辑一致性、活动连接为 `unknown`，数据库完整性为 `not_checked`。元数据摘要不是 FileFragment `beforeSha256`，不能用于迁移。Action 保留覆盖 FileFragment 的共同 Credential/确认声明，但两个元数据分支不读取 Master/Privacy、密码或玩家值，已明确同范围授权不重复询问。

不要为固定检查生成临时 C#、反射私有 Inspector、初始化 Manager、全局 Save、关闭连接或调用 ReadOnly SQLite 来探查；现强类型 Action 已可直接复用。

## 保存、删除与平台差异

先按 `Docs/Runtime/Modules/Persist/PersistComponent.md` 核对 Config 后 Persist 的实际加载顺序，再只读所选实现的 Docs。

- PlayerPrefs 分类保存底层仍是全局 `Save()`；`SetXxx` 后同进程 `GetXxx` 不能证明落盘。删除仅使用 Persist 管理的已确认分类/条目，不调用 `UnityEngine.PlayerPrefs.DeleteAll()`。
- FileFragment 删除最后一个条目或 `RemoveAll(classify)` 后，还需 `Save()` 处理正式文件、`.bak` 和残留 `.tmp`。恢复成功可能重建正式文件；无法恢复的坏档可能被隔离为 `.corrupt.<UTC 时间>`，不能把空返回值直接解释为玩家没有存档。
- WebGL `Save()` 发起 IndexedDB 同步，不等于回调已成功；浏览器重开验证与普通 Play 证据分开。微信小游戏等无 IndexedDB 宿主当前仅保证会话内 FileFragment 语义，不能承诺重启保存。SQLite 在 WebGL 不适用。
- SQLite 值级 `UseAESEncrypt` 与数据库级 `CipherPassword` 独立。SQLite Inspector 在 Play 前关闭 Editor 连接、退出后重建；不要同时让维护过程与 Runtime 写同一个 DB。

## 未开放范围与提交边界

PlayerPrefs 原生 Save 是全局提交，可能把其他工具未保存的值一起落盘。接口仍没有全量键、全局草稿或只提交 Nova 键的能力；值维护只有显式 `acknowledgeGlobalSave=true` 才可执行，恢复只能还原已索引键，不能撤销无关 dirty 的落盘。未接受该副作用时阻断，不能用先 Save 或关闭 Inspector 代替。

SQLite 当前 Nova factory、Runtime 与 Inspector 的登记连接由 `SQLiteMaintenanceLease` 共同约束；值维护与 Cipher 在同库真实写事务内完成。外部未登记 raw 连接不受进程内 lease 约束，事务成功也不证明没有空闲外部 handle。只判断 WAL 或普通文件锁不足以排除这些写者，自动关闭连接又可能提交草稿。`Util.SQLite.CreateConnection/CloseConnection` 分别可创建数据库、开始事务和提交/checkpoint，不能作为纯只读探查；原生 ReadOnly 查询 WAL 也可能生成 `-shm`。默认元数据分支仍不开库，值维护及轮换的 Plan/Verify 则可能改变伴随文件；复制活库不证明逻辑一致快照。

缺少可证明的提交/连接边界时返回 `blocked`，记录需要补齐的条件，不转成私有 Inspector 调用、通用字段 patch、手改真实 DB/平台偏好文件或任意 SQL。后续应复用现三 Action 和同一 Skill 增加经过验证的强类型分支，不为这些限制新增一套重复入口。

## 迁移实施与验收

冻结源/目标业务 schema 版本、完整 classify/item 集、值类型与默认值、目标账号/存档槽、冲突取舍、旧记录保留时限、失败恢复和重复执行策略。项目迁移方法先检查版本，再显式转换、保存并独立读取；只有全部目标通过后才按单独授权更新版本标记或处理旧数据。FileFragment 不提供跨分类事务，不能因单个分片安全替换就声明整个迁移原子完成。

优先使用本轮隔离测试分类验证源→目标、未知/坏数据、失败保留和再次运行不会重复写入。真实数据集需要冻结快照和恢复路径，并先证明恢复可读；缺少这些条件时只完成源码实现并报 `partial` 或 `blocked`，不执行真实迁移。已存在且行为、输入、验收都符合本次冻结契约的项目迁移入口可以直接复用，不创建第二份算法。

### 开发仓隔离 schema 示例

仅验证迁移策略时，可读取长期测试 [PersistenceSchemaMigrationExampleTests.cs](../../../../../Tests/Editor/PersistenceSchemaMigrationExampleTests.cs)，在 Unity Test Runner 的 EditMode 中筛选 `NovaFramework.Tests.Editor.PersistenceSchemaMigrationExampleTests`。示例只在每例唯一的系统临时目录操作固定 `save-v1.json/save-v2.json`：v1 的整数 `level/coins` 与可选 `name` 转为 v2 的 `progress.level/wallet.coins/displayName`，缺省名称为空；等级至少1、金币非负，均限Int32，未知字段、重复字段、坏类型与不支持版本拒绝。已有等价 v2 保留其原字节，冲突目标不合并或覆盖；原 v1 字节保留到用例结束，再由测试只清理自身目录。

该示例独立发布新目标文件并重新读盘，覆盖发布前失败保留/重试、发布后失败幂等、目标竞争与来源变化拒绝；不调用 `Nova.Persist`、PlayerPrefs、Config、AES、数据库或 Keychain，不声明多写者锁、跨文件事务或掉电耐久性。它是测试程序集内的 DTO/文件适配器，不是新生产入口或维护 Action。隔离示例的 EditMode 证据只证明本示例策略；`persistence.migration.source` 的真实业务模型、加载/保存调用与独立 Play 仍需另验，真实玩家数据运行仍需精确授权。

### 开发仓业务入口与真实 FileFragment

MainDemo 的 `DemoProgressMigration` 及 README“业务进度结构迁移”可用于查阅现有迁移策略。它接收已初始化的 `IFileFragmentManager`，属于当前普通业务消费边界下的存量 Manager 依赖示例，不得原样复制、注入或换一层 Service 推广为新业务入口；接入先通过本 Skill 的消费入口门。其 v1→v2 将版本、来源摘要与进度同条目保存，保留旧分类，重复迁移依据同一来源摘要，不把旧值覆盖回已更新的游戏进度。保存结果不确定时拒绝继续，不自动重放；这些行为仍需本次真实适配与独立读取证据。

`NovaFramework.Tests.Editor.DemoPersistMigrationPlayTests` 是隔离测试参考：父宿主保留场景并切换空场景，再进入 Play，构造自有临时根的真实 FileFragment，精确注销仅自有实例并初始化，关闭/重开后独立读盘；不访问默认存档、现有 Manager、AES 或数据库。隔离测试替身与正式业务分别判断，不因测试允许注入 Manager 就许可业务注入 `Nova.Persist.FileFragment`。该示例不授权真实数据执行，不证明 PlayerPrefs/SQLite、设备掉电或所有平台验收；测试存在也不表示本次已执行。

## 受控 FileFragment AES 分支

先按 `Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md` 核对真实请求与范围。`targetGuid/globalObjectId` 必须定位自有普通 Prefab 的唯一标准 FileFragment PersistComponent；`storage=FileFragment`、`dataScope=current-editor-global-data` 明确当前 Editor 的整个共享目录，不能解释为该 Prefab 专属、某个账号槽或设备数据。保持 EditMode，无 Persist Inspector、Runtime、编译和配置草稿。

本任务若已授权建立或整理维护目标，先通过 Unity Editor 正规保存普通 Prefab，核对完整序列化差异并形成干净磁盘基线，再开始检查与 Plan；下述其它维护分支也沿用此前置。首次保存可能补齐其它既有序列化内容，不能夹带进维护的唯一字段写集。未授权草稿或额外变化保持 `blocked`；写入后失败的旧 Operation 保留 `partial`，按原 token 独立恢复，不重放迁移。

1. `inspect-data` 严格只读现存全部 `.dat`/`.dat.bak`，不传 `enableAES/sourceSha256`；内部使用当前 AES 凭据，执行也须确认当前实际 Plan，已明确的同范围授权不重复问。保存返回的 `beforeSha256`。坏档、孤立备份、残留 `.tmp/.corrupt`、链接和容量超限会阻断，检查不暗中恢复。
2. `migrate-encryption` 传相同目标/scope、显式 `enableAES` 和 `sourceSha256=beforeSha256`。确认当前真实 Plan 后执行一次；固定调用直接复用 Action，不重复生成转换代码。Key/IV仅由服务从冻结的当前 Master 读取，不出现在请求或报告。
3. 用原迁移 `recoveryToken` Verify。成功需全集文件、全部主/备历史值、单字段保存及原字节备份均核验；保留 `Library/Nova/AgentActions/PersistenceMaintenance/<operationId>/backup/` 和 journal，不当临时测试文件删除。
4. `partial` 后禁止重放迁移；使用维护收据 `operationId` 与原迁移 `recoveryToken` 作为 `migrationRecoveryToken`，新建 `recover-maintenance` Plan、确认、执行、Verify。只恢复原/新已知字节与完整资产状态；第三方写入、原凭据丢失或恢复材料漂移会阻断。

该分支成功只表示 Editor 本地维护。旧 Inspector 文本解密失败可返回空字符串，SQLite `RefreshCipherEncrypt` 先删库再建库；这些旧方法不作为受控值维护实现，不能根据字段读回推断全量转换或恢复。

## PlayerPrefs 与 SQLite 值 AES 维护

先按 `Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md` 核当前全部字段。目标仍是唯一标准组件的自有普通 Prefab，稳定 EditMode，无运行实例、Persist Inspector、编译和配置草稿；隐私 Key/IV 只由服务内部取得。请求不包含玩家值、路径、密码、SQL 或额外字段。

1. PlayerPrefs 用 `inspect-data(storage=PlayerPrefs, dataScope=current-editor-managed-indexed-data)` 获取已索引字符串 `sourceSha256`，会内部读取值但不输出。迁移相同目标/scope，显式 `enableAES/sourceSha256/acknowledgeGlobalSave=true`；缺索引、缺项、非字符串或非法名称阻断。默认 metadata scope 保持无值检查。
2. SQLite 先默认四文件检查取得 `metadata.metadataSha256`；迁移使用 `storage=SQLite/dataScope=current-editor-sqlite-managed-data/sourceSha256=<metadata SHA>/enableAES`。该 SHA 仅是 Plan 前物理变化探测；Plan/Verify 经 Nova lease 与真实 `BEGIN IMMEDIATE` 冻结 schema/值并另记录逻辑 SHA，可能改变 WAL/SHM。Execute 原厂一致逻辑备份后，再在同库新写事务重核和逐行转换/commit，不删除或替换主库。
3. SQLite 只接受 Nova `Name TEXT PRIMARY KEY, Value TEXT` 两列标准表，无额外约束/触发器/自定义索引、值均 TEXT；schema/空表也进摘要，最多16384行/结构记录与64MiB内容，未知或坏数据阻断。Nova factory、Runtime 与 Inspector 的登记连接由 `SQLiteMaintenanceLease` 约束；已有连接拒维护，不主动提交/关闭。外部 raw 连接由实际写事务隔离，不声称操作系统绝对排他。
4. 严格 AES/UTF8 解码，坏值不变空串、不跳过；原/新值写受保护 `before-values.bin/after-values.bin`。SQLite 通过参数绑定的 `ATTACH DATABASE … KEY …`、`BEGIN IMMEDIATE` 与 `sqlcipher_export` 生成受保护 `before-sqlite.db`，提交并分离备份连接后独立核验；原厂 backup API 不支持此处明文/Cipher 跨格式保护，不能替代该流程。schema2 `values-journal.json` 绑定原字段收据、目标、逻辑/恢复摘要与阶段；Key/IV 和玩家值不进入 Request/Receipt。数据提交后字段保存失败为 `partial`，保留正式恢复材料。
5. 恢复使用原 `operationId/migrationRecoveryToken` 建独立 Plan。PP 只接受已知键的原/新值；SQLite 只接受完整原或新逻辑并用同库新事务恢复原值后保存原字段，拒第三方漂移。成功需当前限定数据、单字段与备份独立重读；不以测试或代码审查代替真实任务后验。

成功限定当前 Editor 本地值 AES 维护，真实设备重启耐久性另验。CipherPassword 与 Privacy Key/IV 使用下方独立分支；任意数据编辑和 schema 迁移由授权业务实现处理。正式恢复目录保留，不当测试垃圾删除。

schema1 FileFragment/schema2 值维护的中断恢复统一见 [存档维护](../../../../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md)：新版保护原 Prefab 全文，只允许对应单布尔标量原/新状态与精确 owned 脏态；半写日志仅原 token 绑定后隔离保留。旧日志缺保护文件时只有磁盘整文件仍等于原 planned SHA 可恢复，已保存新布尔值的历史日志会阻断。

FileFragment 内部恢复默认 `authorizedRecovery=false`；schema1/schema4 已绑定原 token、完整备份与当前原/新状态后才允许隔离断电半写 `.restore` 为正式 Operation 的 `restore-interrupted-<GUID>.bin`。本调用复制失败只清自己创建的阶段文件，目标已旧也先核阶段来源；未知未授权字节保留并阻断。该参数不进入消费请求、不扩成通用文件恢复，隔离证据属于正式恢复材料。

## SQLite Cipher 与 Privacy 默认 AES 轮换

准确请求、默认值、数据范围、配置保存、备份及恢复以 [存档维护](../../../../Docs/Editor/EditorUtil/EditorUtil.AgentActions/EditorUtil.AgentActions.PersistenceMaintenance.md) 的两个独立轮换章节为准；固定调用直接执行同三 Action，不复制转换算法或生成临时代码。宿主当前必须为 macOS Editor，标准实现、唯一普通 Prefab、稳定 EditMode 与完整源/恢复条件必须满足；不能将宿主限制解释为 Android/iOS Player 的 SQLite 支持结论。

- Cipher：先默认 SQLite 四文件检查取得 `metadata.metadataSha256`，迁移固定 `operation=rotate-sqlite-cipher/storage=SQLite/dataScope=current-editor-sqlite-managed-data` 与唯一目标/sourceSHA。自动策略 `generateNewCredential=true`；已有合法项目 handle 则省略该字段并传 `targetCredentialHandle`。拒绝明文密码、`enableAES`、全局 Save 或 Privacy 确认字段。旧/新密码均非空且不同；先 native Vault 封存重读并独立加密备份，再同库 rekey/commit、保存唯一密码字段及 Verify。schema3 恢复需原 Operation，严格全逻辑与整资产状态，不替换活库。
- Privacy：检查固定 `operation=inspect-privacy-aes/storage=PrivacyAES/dataScope=current-editor-privacy-aes-data`、唯一目标、`acknowledgeEditorOnly=true/acknowledgeDefaultAesConsumerScope=true`，其余写字段省略。检查的 Plan/Execute/Verify 均重读来源；SQLite AES 已启用且主库存在时打开既有库，执行真实 `BEGIN IMMEDIATE` 并在 `finally` 中 `ROLLBACK`，取得写事务锁且可能生成或改变 WAL/SHM。不转换/提交玩家值或保存配置不表示物理零写或不开库；缺失/停用后端不创建主库，默认 SQLite 四文件 metadata 检查仍不开库。迁移改 `operation=rotate-privacy-aes`，另加整检查 `sourceSha256`、`acknowledgeGlobalSave=true` 与同一自动/已有 handle 策略，禁止 `enableAES`。范围只有标准三存储已启用 AES 的现存数据、激活 Master 当前 Privacy 逻辑组与完整源一致的配对 Runtime；不改变开关、Cipher、App AES、资源密钥或其它独立组。

Privacy 预演若发现请求外归一变化，先保持 `blocked`，复用[配置技能](../../nova-project-configure-runtime/SKILL.md)的独立 `nova.project.config.normalize-master`，明确保留来源及完整脱敏差异，沿用已覆盖该范围的授权。整理后完成必要的配对 Runtime 正规导出与一致性核对，再重新检查 Privacy、取得新的 `sourceSha256` 和 Plan；不复用整理前摘要或执行令牌，也不把整理并入轮换。物理坐标按当前逻辑组与 mask 实际读取。

Cipher 恢复还核对独立保护的原 Prefab 全文，只允许唯一密码标量为封存旧/新值；格式边界、owned 脏态与日志隔离均按上方 Docs，不用通用字段编辑扩大恢复权限。

新手用自动生成即可，无须供应明文秘密或提前建 handle。`PersistenceCredentialVault` 是内部实现；没有正式可访问的凭据供应入口时，指定自选密码任务保持具体缺项，不用反射、任意代码或 JSON 预置。

Privacy 在独立封存、完整原/新材料与 owned 启动标记后逐存储提交，最后复用 `SaveWorkingCopy` 正式保存整组 Master、正规 `Exporter.Export` 重导原 Runtime，再核完整状态、原资源文件及清场。跨存储和配置不是原子事务；开始提交后失败保 `partial`、标记和正式材料，禁止重放。Config/Persist 即使已经加载，也在幂等短路前检查 Editor 门禁；Play/Build 同样阻断。先 Verify，再以原 `operationId/migrationRecoveryToken` 新建独立恢复 Plan；末尾门禁清理失败可以同原 token 再次计划，第三方状态不覆盖。

Privacy 恢复与后验还会核对 Master/Runtime 的独立磁盘副本完整原/新状态与身份，保留未导入的第三方修改；不把日志自填保存摘要当作覆盖权限。实际官方读取与清理边界统一见上述 Docs。

这些成功只证明本次冻结的 Editor 数据/文件及正式配置结果。PlayerPrefs Save 会提交全进程脏值，恢复不能撤销无关值的落盘；未知业务默认 AES 消费者、设备、服务端和其它独立坐标仍需明确范围与独立验证。AES CBC 严格重读不等于对历史密钥的数学鉴别。正式恢复目录、加密备份与项目 Keychain 是交付材料，单独删除须精确授权。

结果分别报告源码实现、编译、隔离 Play、真实数据迁移、目标平台耐久性。空 evidence、旧数据快照、编译或同缓存读回不计完整验收。清理本轮探针、测试分类、日志及临时备份；正式业务迁移源码与用户要求保留的恢复材料列为正式产物。
