# 存档检查与受控加密维护

通过现有 `nova-project-integrate-persistence` Skill 使用三个强类型 Action；不增加第二套存档接口或顶层 Skill。文件转换复用 `FileFragmentItemGroup` 的真实版本化二进制与 AES 编解码，配置保存复用 `SerializedTargetEditing`。

## 本机恢复材料位置

维护备份、配置快照、阶段日志、Privacy 活动标记及写入锁统一位于项目 `Library/Nova/AgentActions/PersistenceMaintenance/`；每次操作使用独立 `<operationId>/`。它们属于本机过程与恢复材料，不放入可版本管理的 `.agents/`，不作为 Framework 源码或随包资料分发。成功或恢复后按原契约保留恢复材料；清理 Library 会同时失去这些本机恢复依据，未结束维护前必须先完成恢复或妥善保留材料。

## 支持范围

- 支持标准 `NovaFramework.Runtime.FileFragmentManager` 的现存分片全集、标准 `PlayerPrefsManager` 的完整已索引字符串值和标准 `SQLiteManager` 的已有标准表值 AES 开关切换。值 AES 开关操作的密钥、IV 从唯一持久激活 `ConfigMasterSO` 的当前坐标取得，不接受用户传入或轮换；Privacy 全域操作另有独立契约。
- FileFragment 使用 `dataScope="current-editor-global-data"`，数据位于当前 Editor 的 `Application.persistentDataPath/Persist/FileFragment`；PlayerPrefs 与 SQLite 值维护使用下方各自固定 scope。数据由当前 Editor 项目身份共享，不是某个 Prefab、账号或存档槽的专属数据；不会读取 Player、真机或浏览器 IndexedDB。
- 配置目标必须是项目自有、已存在的普通 Prefab 中唯一 `PersistComponent`，以 `targetGuid` 与组件 `globalObjectId` 精确定位。值 AES 操作仅保存对应初始布尔字段；Cipher 操作另见下方。两者均不处理场景 override、嵌套/变体 Prefab或自定义 Manager。
- 维护要求稳定 EditMode、没有运行实例、编译、Persist Inspector 或未保存配置草稿。不擅自关闭界面或 Manager。
- FileFragment 文件仅接受规范单层 `.dat`、`.dat.bak`，已有备份的主文件必须存在；拒绝符号链接、损坏文件、残留 `.tmp`/`.corrupt`、其他维护 stage。最多 128 文件，单文件 16 MiB，全集 64 MiB。
- 同一 `inspect-data` 另支持标准 `PlayerPrefsManager` 的默认 Unity provider 托管索引、标准 `SQLiteManager` 的固定文件元数据；不需要 Master/Privacy、不读取 Cipher 密码或玩家值。默认元数据检查继续不写；值 AES 维护使用下方精确独立 scope。

## 输入与执行顺序

| Action | 请求 | 写入与证据 |
|---|---|---|
| `nova.project.persistence.inspect-data` | `targetGuid`、`globalObjectId`、`storage="FileFragment"`、`dataScope="current-editor-global-data"` | 严格只读解码全部主/备文件，输出 AES 状态、计数、集合 `beforeSha256`；不恢复坏档、初始化 Manager、创建备份或输出玩家值 |
| 同一 `inspect-data`：PlayerPrefs | 相同身份与 scope，`storage="PlayerPrefs"` | 仅读取 `__pp_classifies__`、`<classify>::__items__` 索引及已索引键的 HasKey；输出分类/条目计数、缺失索引/条目数量、索引 UTF8 字节数和 `metadataSha256`，不读取条目值或枚举未知键 |
| 同一 `inspect-data`：SQLite | 相同身份与 scope，`storage="SQLite"` | 仅读取当前 Editor 固定 `Persist/SQLite/game.db`、`game.db-wal`、`game.db-shm`、`game.db-journal` 的存在性、大小与 SHA256；未知目录项只计数，不建目录、开库或生成伴随文件 |
| 同一 `migrate-encryption`：FileFragment | 相同身份与 scope，再加显式 `enableAES` 和上一步 `sourceSha256=beforeSha256` | Plan 冻结整个数据集、资产、Master/meta/坐标；Execute 备份完整原字节、阶段转换并独立逐值重读，然后提交全集并保存唯一字段；使用返回的原 Action `recoveryToken` 做 Verify |
| `nova.project.persistence.recover-maintenance` | 原维护收据的 `operationId`，原迁移 Action 的 `migrationRecoveryToken` | 新 Plan 绑定原 Operation、持久日志、原/新数据和资产状态；FileFragment 恢复原主/备字节，PlayerPrefs/SQLite 恢复下方限定值，并保存原 AES 字段；不重放原迁移 |

三个 Action 均不接受目录、明文存档值、密钥、SQL 或任意方法。普通 `inspect-data` 不接受 `enableAES` 或 `sourceSha256`；Privacy 检查使用下方独立类型化请求。值 AES 状态已满足时迁移不写数据和配置；任何集合、配置或凭据来源漂移均要求重新检查和 Plan。

三个 Action 都需要确认当前精确 Plan：`inspect-data` 保留共同 Credential 声明，因为 FileFragment 分支严格解码仍会内部使用当前 Privacy AES；PlayerPrefs/SQLite 元数据分支不取得这些凭据或数据库密码。确认不回显或传递 Key/IV。当前任务已明确授权的同一项目/数据集不重复询问，实际 Plan 的执行 token 仍按 Gateway 流程传递。

## 元数据检查的结果边界

两类元数据分支复用同一强类型请求和只读 Action，无新 Skill、任意 SQL、目录参数或临时执行代码。目标仍须是唯一标准 PersistComponent 的自有普通 Prefab，保持稳定 EditMode，无运行实例、编译、Persist Inspector、目标未保存草稿或 PrefabStage；不主动关闭、保存或初始化它们。`Plan/Execute/Verify` 都重新读取当前观察，收据不作为一致快照或后续写维护授权。

- PlayerPrefs 只支持当前编译配置下的默认 `UnityEngine.PlayerPrefs` provider；`NOVA_DYSDK/NOVA_WXSDK/NOVA_ALIPAYSDK` 返回 `not_applicable`。严格拒绝空项、重复、`::`、保留条目名 `__items__`、超过 1024 字符的名称；分类及总条目分别最多 4096，全部索引 UTF8 最多 1 MiB。索引读取两次，期间变化阻断。缺失分类索引仍返回其不存在的观察；不解释为没有玩家数据。引用缺项返回 `partial`，不会创建或修复索引。
- SQLite 只处理四个固定文件；目录最多 32 项，单文件和四文件合集最多 64 MiB，拒绝固定路径上的目录、符号链接和重解析点。整文件有界读取核对前后大小/修改时间，再双次核对固定集合摘要；变化、读取占用、格式或容量不满足返回 `blocked`。未知目录项、孤立伴随文件或零字节主库返回 `partial`，不读取或删除未知项。不打开数据库，因此不验证格式、schema、AES/Cipher 或逻辑记录；文件全缺失也只表示本次固定文件观察为空，不证明玩家没有存档。
- 输出不包含分类/条目名称、玩家值、密文或凭据；PlayerPrefs 元数据检查只报告索引总字节数，无法报告未读取值的大小。`metadataSha256` 是限定元数据变化探测，与 FileFragment `beforeSha256` 不同；SQLite 值迁移只能把它作为 Plan 前变化探测，不能当一致快照或逻辑摘要。两类默认元数据输出明确 `playerValues=not_read`、`durableState=unknown`、`writeMaintenanceSupported=false`，这些字段只描述本次元数据 scope，不能替代或否定下方值维护的独立前置检查。PlayerPrefs 的完整键空间/全局 dirty、SQLite 的逻辑一致性/活动连接仍为 `unknown`，数据库完整性为 `not_checked`。`success` 只表示限定元数据检查完成，不表示完整存档、原子快照或迁移前置通过。

## FileFragment 的失败与恢复

跨多个文件及 Unity 资产不是真事务。先完整备份，再把 `committing` 阶段持久写入 `Library/Nova/AgentActions/PersistenceMaintenance/<operationId>/journal.json`，随后才替换正式数据。提交点后不因取消自动回滚；异常返回 `partial`，保留原字节备份、日志和未完成阶段文件，先 Verify 再独立恢复。

恢复先核对所有备份及正式文件；只接受本操作的原或新整体字节与原/新完整配置状态，拒绝第三方修改。原迁移 Operation 凭据丢失、日志/备份不匹配或存在无法确认的临时文件时阻断，不能用更宽的字段 patch、文件覆盖或 Inspector 切换绕过。

成功后保留上述目录下的 `backup/` 与 journal，作为正式恢复材料；临时 stage 应清理。数据目录保留固定 `.nova-persistence-maintenance.lock`，当前持有者在同一独占句柄内写回 `idle-v1` 再关闭，避免解锁删除与下一写者接手竞态；它是长期锁载体，不是遗留活跃锁。恢复材料不属于测试垃圾，单独删除需要确认。报告列出真实保留路径、当前 phase、全集摘要和字段保存证据，不回显凭据或玩家内容。内部 codec 遇到损坏内容仍可能向 Unity Console 输出既有错误摘要；维护 Action 的请求、收据和错误消息不回显这些底层异常，不承诺框架日志已全面脱敏。

本地严格转换和后验不代表 Player/真机重启耐久性。值 AES、SQLite Cipher 和 Privacy Key/IV 各用下方独立分支；业务 schema 迁移与任意数据批量编辑不属于固定维护接口。

## PlayerPrefs 与 SQLite 的边界

普通项目业务读写仍可由自然语言驱动 `nova-project-integrate-persistence`：在项目自有源码中接入或复用已有业务方法，等待 Config 后 Persist 加载，调用 `Nova.Persist.PlayerPrefs` 或 `Nova.Persist.SQLite` 的公开存储接口，再验证保存后的独立读取。这是 `workspace-edit` 与公开 C# API 的业务接入能力；不是已开放的 Project Action，也不会默认转换原地已有存档。

值 AES 维护复用同一三个 Action。数据目标仍是唯一标准 PersistComponent 的自有普通 Prefab，要求稳定 EditMode、无 Persist Inspector/运行实例/配置草稿；当前 Privacy AES Key/IV 只在服务内部取得。

| 存储 | 检查与迁移请求 | 提交及恢复范围 |
|---|---|---|
| PlayerPrefs | `inspect-data` 使用 `storage="PlayerPrefs"`、`dataScope="current-editor-managed-indexed-data"`，输出完整已有 indexed strings 的 `sourceSha256`；迁移相同 scope 加 `enableAES/sourceSha256/acknowledgeGlobalSave=true` | 缺失索引/引用、非字符串键、重复或非法名称阻断；只改已索引键，Save 会提交整个进程 PlayerPrefs 缓存。恢复只还原这些键，无法撤销其它工具脏值随 Save 落盘的副作用 |
| SQLite | 保持默认只读四文件检查，取得 `metadata.metadataSha256`；迁移使用 `storage="SQLite"`、`dataScope="current-editor-sqlite-managed-data"`、`enableAES`、`sourceSha256=<metadata SHA>` | Plan 再核物理摘要，取得包自有维护门并用真实 `BEGIN IMMEDIATE` 冻结 schema/值；回滚后记录逻辑 SHA 和伴随文件新摘要。该 Plan/Verify 可能改变 WAL/SHM，不能称物理只读。Execute 完成原厂一致逻辑备份后，在同库新写事务重核并逐行转换/commit，不删除或替换主库 |

PlayerPrefs 完整键空间、全局 dirty 仍未知；明确接受全局 Save 副作用使限定键维护可执行，不把未知键写入/恢复权扩大。默认元数据检查仍不读玩家值；新 indexed-value 检查读取值但仅报告计数和摘要。

SQLite 只接受当前库中标准 `Name TEXT PRIMARY KEY, Value TEXT` 两列、无额外约束/触发器/自定义索引的 Nova 表，列值必须 TEXT，未知结构或坏数据阻断。不存在主库、零字节或未知伴随项阻断迁移；已有合法空数据库/空表只切换初始 AES 状态。schema 与空表也纳入逻辑摘要。最多 16384 个行/结构记录与 64 MiB 内容；受保护 JSON 备份另作同容量限制。

`SQLiteMaintenanceLease` 仅约束参与协议的 Nova 连接。`Util.SQLite` factory 返回登记连接，Runtime `SQLiteManager` 使用同 factory，Inspector 现 factory 自然覆盖；Close/Dispose 释放登记。已有连接时拒绝维护，不自动提交关闭。未参与协议的 raw SQL 写者由实际 SQLite 写事务隔离，未声称操作系统绝对排他；外部 schema/值变化会在实际事务内摘要检查时阻断。普通连接 Create/BeginTransaction、Close/Commit/checkpoint 的既有语义保持。

AES 解密走严格字节与 UTF8 校验，坏 Base64/PKCS7/UTF8 不改为空值，不跳过。原/新全集写入固定 `before-values.bin`、`after-values.bin`，随机 IV 加密，Key/IV不保存；SQLite 另用原厂 `ATTACH ... KEY` 与 `sqlcipher_export` 在真实事务内生成 `before-sqlite.db`，以当前 Privacy AES Key 加密并独立重读。维护源连接保留无 Create 的只打开既有库契约；共享 `BackupSQLite` 先校验目标路径并以 `FileMode.CreateNew` 独占创建空备份，再附加该目标，避免 ATTACH 继承源连接的无 Create 限制。已有目标、路径风险或创建失败均阻断；后续失败残留由各维护调用方按本次所有权清理或保留恢复证据。API不直接复制活库文件；当前原厂 backup API不支持明文与 Cipher 之间的备份，所以不把该路径用于跨格式保护。导出后再取得转换写事务并比对冻结逻辑，漂移即阻断。

固定 `values-journal.json` 仅保存 schema=2、精确目标、原单字段收据、原/新逻辑摘要、恢复文件摘要、计数与阶段；玩家值和密码不进入外部 Request/Receipt。数据提交后 Prefab 单字段保存失败返回 `partial`，保留正式恢复材料；独立 `recover-maintenance` 仍需原 `operationId/migrationRecoveryToken`。PlayerPrefs 部分写仅接受各已知键原/新值；SQLite 只接受完整原或新逻辑，并用新的同库事务恢复原值后保存原字段，拒绝第三方修改。

旧 Inspector `DecryptString` 可失败返回空串，`RefreshCipherEncrypt` 先删库再建库，均不作为本维护实现。CipherPassword 与 Privacy Key/IV 通过下方独立 typed operation 轮换，不用通用字段 patch、私有回调、临时反射或任意 SQL 绕过。成功只证明 Editor 本地限定维护/保存与恢复材料核验，设备重启耐久性单独验证。正式恢复目录保留，不当测试垃圾删除。

## FileFragment 与值 AES 的中断恢复补强

schema1 FileFragment 与 schema2 PlayerPrefs/SQLite 值维护继续复用同三个 Action。新版提交前增加 `before-aes-asset.bin`，使用冻结 Master 的当前 AES Key 保护原 Prefab 全文，再以原迁移 token 绑定的 `plannedFileSha256` 认证完整来源；只读解析精确组件 localId 对应存储的唯一 `0/1` AES 布尔标量，其余文本完整不变。当前限定 LF Unity 文本 Prefab；格式不符、第三方字段或文件变化均阻断，恢复不手写资产。

领域内部的单布尔恢复允许本操作完整原/新内存及已认证磁盘状态，即使字段 Apply 后保存失败留下脏态，或再次恢复时字段已旧但仍有本操作脏态，也会真正保存并核验完整原状态。通用字段编辑的草稿、只读、Inspector、PrefabStage 等门禁仍保持；只有原 token 已绑定完整来源的独立恢复可使用该入口。

本轮创建日志时写失败会精确清自己创建的半写 `.tmp`。跨崩溃留下的空或截断临时日志，只在原 token 及正式日志完整归属核实后隔离为 `*.interrupted-<GUID>.bin` 保留；完整异域日志、超限或未知内容不能删除。FileFragment 已处于 `recovered` 时可重新计划核对原字节并只清本操作 stage，不重复改数据和字段。

FileFragment 的内部 `Restore(..., CancellationToken token, bool authorizedRecovery=false)` 默认拒绝未知 `.restore`。本次 `CreateNew` 复制失败只清自建半写文件；跨断电已有半写文件，只有 schema1 或 schema4 服务先用原 token 核完整备份及当前原/新字节后才传 `true`，隔离到正式 Operation 目录的 `restore-interrupted-<GUID>.bin` 保留，不删除证据。即使目标已是旧字节也先处理这个阶段文件，然后只清自有 stage；未授权调用保持保守阻断，不把该内部参数暴露为用户通用文件恢复开关。

旧日志没有 `before-aes-asset.bin` 时，只允许磁盘整文件仍精确等于原 `plannedFileSha256` 的恢复；已经保存过新 AES 布尔值的旧日志会保守阻断，不能声称全部历史日志可自动恢复。原 Master 凭据或原 token 丢失也不能由日志自填保存摘要补授权。历史十一项隔离 EditMode 结果仅对应当时实际执行的源冻结；后续十项中断/脏态/日志/第三方文件回归的执行范围须查各自原证据。此次文档归并仅核源码，不续签任何旧测试结果，真实数据完整恢复与设备耐久性分别验收。


## SQLite CipherPassword 安全轮换

同一 `migrate-encryption` 使用 `operation="rotate-sqlite-cipher"`，不是另一套 Action。没有 `operation` 或使用 `operation="toggle-value-aes"` 时保持前述 AES 开关语义。Cipher 请求固定 `storage="SQLite"`、`dataScope="current-editor-sqlite-managed-data"`、唯一 `targetGuid/globalObjectId`、`sourceSha256=<inspect-data 的 metadata.metadataSha256>`，并只选择以下一种策略；拒绝 `enableAES/acknowledgeGlobalSave`、密码 JSON、SQL 或路径。

| 策略 | 请求字段 | 凭据来源与默认 |
|---|---|---|
| 自动轮换 | `generateNewCredential=true` | 新手无需先建 handle。Plan 只冻结生成策略及 opaque handle，不生成密码、不创建 Keychain 项。Execute 在写库前用安全随机数生成 32 字节 Base64 密码并保存到当前项目的 Keychain 命名空间 |
| 指定既有凭据 | `targetCredentialHandle="nova-keychain:<N格式GUID>"`，不提供 `generateNewCredential` | 从当前项目 Keychain 的 credential namespace 读取现有条目；Plan 内存随机 HMAC 绑定其值，Execute 再读必须相同。仅引用已经存在的合法项目 credential 条目；当前 bridge 是内部 Editor 服务，消费项目没有直接供应入口。没有既有条目时使用自动生成策略。指定密码的独立授权供应入口另需正式能力，不通过临时代码、反射或明文密码 JSON 建项；不接受任意系统 Keychain service/account |

旧密码只从冻结 `PersistComponent.m_SQLiteCipherPassword` 取得。源必须已有、合法、密码非空；新密码必须非空且不同。源 null/空、明文库、缺库、零字节、未知结构或额外文件均 `blocked`，不调用旧 Inspector 的先删库再重建路径。值 AES 原内容与开关保持，按标准 schema 完整读核 raw Name/Value 内容，轮换仅改变库级 Cipher。

凭据 bridge 只支持 **macOS Editor 宿主**。这是工具执行环境的限制，不表示 Android/iOS Player 目标不支持 SQLite。Windows/Linux Editor 当前没有可封存的 provider，轮换 `blocked`，不会写玩家数据。macOS 使用原生 `SecItemAdd/CopyMatching/Delete`，不执行 shell、不把密码放在命令参数；项目绝对路径整体 SHA 作为 service namespace，credential/recovery 分域，account 仅 opaque GUID。Keychain 条目使用 `AfterFirstUnlockThisDeviceOnly` 且不弹认证 UI；锁定、权限拒绝、API 不可用或无法 readback 均在玩家数据写之前阻断。移动项目目录、换宿主用户/机器、删除或丢失 Keychain 条目会使恢复不可用。

Execute 先封存旧密码、新密码及随机独立备份密码到 recovery namespace，并完整 readback；保护不依赖旧/新 Cipher 或 Privacy AES Key/IV。随后原厂 `sqlcipher_export` 生成固定 `before-cipher.db`，使用独立密码加密并完整读核 schema/值及整文件摘要。取得真实 `BEGIN IMMEDIATE` 后再次核对备份与当前完整逻辑，持久写入 `committing`，通过自有 Editor P/Invoke 调用 vendor 原生 `sqlite3_rekey`，密码只经短生命周期 byte[] 参数传入，成功后 COMMIT，再用受控字段服务保存唯一密码字段。不修改 vendor、不删除或替换活库。配置字段仍按现框架序列化行为写入 Prefab，工具收据不会输出字段内容。

数据库与 Unity 资产不构成跨资源真事务。rekey/COMMIT 后字段保存失败、崩溃或日志写失败返回 `partial`；固定 `cipher-journal.json`（schema=3）保留身份、策略、整资产/整逻辑/备份摘要、计数、阶段与两个 opaque handle，不含密码、玩家值或逐密码摘要。Plan 自身也不持有密码。数据开始提交后恢复材料保留，禁止自动重放原迁移。提交前失败只清理本轮创建的固定备份/日志和自有 Keychain 项，不覆盖或删除同 handle 下已有条目；无法安全清理返回 `partial`。

同一 `recover-maintenance` 继续要求原 `operationId/migrationRecoveryToken`，绑定原 `migrate-encryption` Operation。跨 domain reload 从 recovery Keychain 取旧/新/备份密码，验证完整日志与加密备份，并把日志中的两种整资产状态重新绑定到实际封存凭据。当前库必须以本操作旧或新密码可读、全体原值与备份一致，组件必须是完整原或新状态；第三方数据、字段、脚本/meta 或备份修改均阻断。恢复用新的同库写事务 rekey 回旧密码后恢复原组件字段，仍不替换主库。Verify 只核当前库、保存收据和独立恢复材料；没有 committed/recovered 全链证据返回 `partial`。

成功后保留 `Library/Nova/AgentActions/PersistenceMaintenance/<operationId>/before-cipher.db`、`cipher-journal.json` 和对应项目 Keychain 项作为正式恢复材料；清理它们需独立明确授权。Editor 本地 rekey/读核通过不证明 Player、Android/iOS 真机重启耐久性。长期测试使用唯一临时 fixture 与测试专用 Operation store/Prefab，测试后的 Keychain 项、数据库、日志、资产与编译目录均精确清理。

Cipher 独立恢复允许字段已 Apply、Prefab 保存失败留下的本操作脏状态，前提是原 token、唯一 GUID、脚本/meta、整个内存原/新状态与磁盘本操作完整字节均一致；通用字段编辑继续拒绝脏态。日志写失败会清本轮新建半写 `.tmp`；跨崩溃留下的坏临时日志仅在原 token 已绑定正式日志后隔离为 `cipher-journal.interrupted-<GUID>.bin` 保留，再继续独立恢复，不丢弃未知内容。

Cipher 的磁盘恢复另保存 `before-cipher-asset.bin`，独立加密并绑定原 token 的整 Prefab 文件 SHA；仅允许精确组件的唯一密码标量变为封存旧/新值，其它文本逐字节保持。当前限 LF 的 Unity 文本 Prefab 与可解析单行密码标量；多行、未知转义或 Unity 保存引发其它文本变化会阻断，不覆盖第三方内容。这一保存格式边界需实际 Unity 验证，源码编译不代表恢复测试已通过。

## Privacy 默认 AES Key/IV 协调轮换

复用相同三项 Action。该分支固定 `storage="PrivacyAES"`、`dataScope="current-editor-privacy-aes-data"`，只处理 macOS **当前 Editor** 的标准默认 AES 数据；与值 AES 开关、SQLite Cipher、`AppConfigs.AppAesKey/AppAesIV`、ThirdPay 支付 URL 随机 Key/IV及 YooAsset 资源密钥分别管理。

| 阶段 | 请求与默认 | 实际范围 |
|---|---|---|
| `inspect-data` | 唯一 `targetGuid/globalObjectId`、`operation="inspect-privacy-aes"`、上述 storage/scope、`acknowledgeEditorOnly=true` 与 `acknowledgeDefaultAesConsumerScope=true`；`sourceSha256/enableAES/acknowledgeGlobalSave/generateNewCredential/targetCredentialHandle` 均省略 | Plan/Execute/Verify 均重新严格检查三后端、完整配置源与已配对 Runtime，内部读取数据但仅返回整体 `sourceSha256`、计数与状态；不转换或提交玩家值、不保存配置、不生成新秘密、Keychain 项、备份或正式导出。已启用且存在的 SQLite 会开库并执行真实事务，可能改变 WAL/SHM |
| `migrate-encryption` | 相同目标/scope与双确认，`operation="rotate-privacy-aes"`，检查取得的整体 `sourceSha256`，`acknowledgeGlobalSave=true`；仅选择 `generateNewCredential=true` 或省略它并提供已有合法项目 `targetCredentialHandle`，拒 `enableAES` | 先执行期生成/读取新 pair并完整预演，独立封存与恢复材料重读后逐后端提交，最后正式保存 Master与重导原 Runtime。`generateNewCredential=false` 不接受；省略并无 handle 也不接受 |
| `recover-maintenance` | 仍只有原 `operationId/migrationRecoveryToken` | schema4协调日志、原 Operation、Keychain、原/新材料、当前全体数据与配置状态绑定后，以新独立计划还原；不重放原迁移，不覆盖第三方变化 |

Privacy 检查的三个阶段都经 `ReadPrivacyBackends` 读取后端，再由 `AssertPrivacyBackendSource` 重核来源。SQLite 仅在 AES 已启用且主库存在时，以无 Create 的读写连接打开既有库；两处逻辑读取均使用真实 `BEGIN IMMEDIATE` 并在 `finally` 中 `ROLLBACK`，取得写事务锁，可能生成或改变 WAL/SHM。因此“不转换/提交玩家值、不保存配置”不表示物理零写或不开库。缺失/停用后端不创建主库；默认 `storage="SQLite"/dataScope="current-editor-global-data"` 的四文件元数据检查仍不开库。

两个 scope 确认必须为 `true`，默认 `null` 不接受；它们表示调用者明确接受当前 Editor 及已经冻结的默认 AES 消费者边界，不能解释为全设备、全玩家或全部业务密文已经覆盖。Privacy 迁移另要求全局 Save 确认，即使本次没有待转换 PlayerPrefs 也必须显式声明。PlayerPrefs Save 会提交进程中无关脏值；恢复只能还原完整已索引范围，不能撤销这些旁效应。

目标仍为唯一项目自有普通 Prefab的标准 `PersistComponent`，三个 Manager必须分别是标准 FileFragment、PlayerPrefs、SQLite实现；小游戏 provider、自定义 Manager、Runtime实例、Persist Inspector、编译或用户草稿均阻断。当前激活 Master的 `Platform/Channel/DevelopMode`、`PrivacyConfigsMask` 所决定的完整物理同组、有效 UTF-8 各16字节旧 pair、已保存配对 `ExportTarget` 都必须唯一且合法。同组所有格必须保持相同旧 pair；其它独立组如果也复用该 pair 则拒绝，不能漏掉共享本机数据的消费者。只读预验不自动补齐矩阵或切换 Unity BuildTarget。配对 Runtime须整体与源正规解析一致；过期的其它配置不能借轮换顺手刷新。

只迁移已经启用 AES且现存的后端数据，保持三个开关与 Cipher不变；缺失/停用后端只冻结其状态，不创建数据库或索引。FileFragment严格核全 `.dat/.dat.bak` 与历史条目，孤立备份、残留stage/tmp/corrupt、坏档、链接和容量超限阻断。PlayerPrefs只读完整合法托管索引字符串，未知键不扫描。SQLite现存主库要求标准Name/Value/schema与完整TEXT逻辑，先Nova登记lease再真实事务核对，未知结构或坏数据不跳过；外部raw连接不被该lease宣称为绝对排除。AES CBC严格Base64/块/PKCS7/UTF8与新密文完整重读不构成对历史密钥的数学鉴别。

自动策略在 Execute写入前阶段以无偏密码学随机可打印ASCII生成新 Key与IV，各16字节。Plan只冻结生成策略、源范围和opaque handle，既有handle使用随机内存HMAC绑定；Plan不长期保存秘密或新 pair。已有合法项目credential可引用，`PersistenceCredentialVault` 为内部实现，消费项目无法直接调用；指定自选秘密没有正式供应入口时保留该缺项，不生成临时反射或将明文传入JSON。

执行先完整预演新的Master组、Runtime和数据；Keychain recovery项封存旧pair、新pair与随机独立保护秘密并完整readback。固定恢复目录保存受保护原/新Master、Runtime、Prefs与SQLite值正文、原FileFragment完整字节，以及原厂`sqlcipher_export`独立加密的`before-privacy.db`；保护不依赖随后变更的旧/新Privacy Key。JSON与外部计划/收据只含opaque handles、完整摘要、范围、阶段与计数，CLR临时字符串不能保证原地清零。

`privacy-journal.json`使用schema4协调，恢复材料与本项目固定owned标记 `Library/Nova/AgentActions/PersistenceMaintenance/privacy-aes-active.json` 必须先于提交落盘。随后逐FileFragment、SQLite同库值事务、PlayerPrefs提交，再复用`DesignTimeEditing.SaveWorkingCopy`保存当前Privacy整组Master，使用冻结时间的正规`Exporter.Export(..., saveOnlyTouchedAssets:true, exportResourceKeys:false)`重导原Runtime，保持原YooAsset同值干净文件的完整字节/meta及其他配置。完整后验同时核数据、正式保存状态与文件摘要、配对源一致，最后清stage与owned标记，保留正式恢复目录及Keychain。

这不是跨文件、存储与Unity资产的原子事务。开始提交后异常/取消/崩溃只报告`partial`，保留阶段、门禁和材料，不自动重放或回滚。Runtime的Config/Persist在幂等短路前检查Editor门禁；Editor Play和Player构建也会检查，坏标记、未知schema或无法核归属同样阻断。该 Runtime guard只在`UNITY_EDITOR`编译，不读取Player/设备文件。

恢复严格绑定原迁移Operation与schema4完整来源，读取封存原/新正文并对全数据、配置整状态/meta和独立备份再核；只接受本操作已知原/新范围、完整owned脏态，不覆盖额外用户草稿、第三方值/索引/文件或脚本变化。严格 JSON 读取保留 ISO 时间字符串原文（`DateParseHandling.None`），不把 `placeholderTime` 自动转为日期后改写格式；原 token、完整来源、重复字段与尾随内容校验保持。正规还原存储、Master与Runtime后再Verify并清stage/标记。终态数据已核但门禁/清场失败仍为`partial`，可以原token新建恢复计划重试；坏临时日志按原token与固定归属隔离保留，不能以清理名义直接删除未知内容。

允许原/新状态的恢复与后验还会用 Unity 官方 `LoadSerializedFileAndForget` 读取 Master、Runtime 的独立非持久磁盘副本，核对实际类型、名称/隐藏标志身份及完整原/新字段状态，并比较读取前后的整文件摘要；不 Refresh、Import 或先覆盖正式资产。日志自填的 `savedFileSha256` 不能把未导入的第三方修改变为可恢复状态。临时副本精确销毁；终态仍要求真实 Master、Runtime 无脏态及正确保存/导出摘要。历史二十项 Privacy 隔离 Unity 测试（含两项第三方磁盘回归与故障阶段）只对应原执行版本及 Editor 隔离范围；此次静态归并未重新运行，不能作为当前全部消费者或设备验收保证。

成功仅证明本次冻结的当前Editor标准范围与正规配置链。设备重启、其它独立坐标、服务端协议及未知默认AES业务消费者都须另有明确迁移及验证；不从函数审查、managed编译、隔离tests或Action成功推定这些已完成。正式恢复材料与Keychain不能当测试垃圾清掉，删除需独立精确授权。
