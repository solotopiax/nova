---
name: nova-project-integrate-persistence
description: 在 Nova 项目中接入业务存储或关键 int/long 内存防护、诊断保存，或受控维护当前 Editor 的值 AES、SQLite Cipher 与 Privacy 默认 AES 密钥时使用。
---

# Nova 接入业务存储与内存防护

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在当前决策分支按需读取：确认三种入口、加载顺序和现有组件配置时读 `Docs/Runtime/Modules/Persist/PersistComponent.md`；选 PlayerPrefs、FileFragment 或 SQLite 时只读对应的 `Docs/Runtime/Modules/Persist/PlayerPrefsManager.md`、`FileFragmentManager.md` 或 `SQLiteManager.md`；确认自动保存时读 `Docs/Runtime/Modules/Persist/PersistManagerConfigBase.md`；核验 AES 前置时读 `Docs/Runtime/Modules/Config/PrivacyConfigs.md`。仅在排查现有设置时读 `Docs/Editor/Inspectors/PersistComponentInspector/PersistComponentInspector.md` 或 `Docs/Editor/Windows/ConfigWindow.md`；不要递归加载全部 Persist、Config 或加密文档。

检查旧存档、清理测试分类、设计数据结构迁移或处理 AES/Cipher 变化时，只追加读 [日常存档维护](references/maintenance.md)。先区分只读诊断、业务源码实现、运行测试与真实数据维护；自然语言“存不下来”“清缓存”“换存档结构”不能自动转成全盘删除或加密开关变更。热更资源缓存由该参考路由到资源能力，避免与业务存档混用。

“保护金币/体力等关键数值”“接入 ProtectedInt/ProtectedLong”“检测内存修改”“未安装 ACTk 能否正常运行”进入 [内存防护接入](references/memory-protection.md)。先发现独立核心包 `com.solotopia.nova.framework.memory.protection` 的 `Nova/Docs/INDEX.md`；缺包复用已有包管理 Operation，不能从 Framework 或本 Skill 原有版本下界推断已安装。本分支编辑已授权业务源码，并只增加必要的 `NovaFramework.MemoryProtection.Runtime` 程序集引用和隔离探针；公开命名空间仍为 `NovaFramework.Runtime`，不创建模块或场景组件，不调用存档加密维护 Action。独立示例为本包 `MemoryProtectionDemo`，MainDemo 不再承载内存防护页。

## 冻结输入与阻断门

先按 `Docs/Onboarding/DEVELOPMENT_CONSTRAINTS.md` 的 NA-001/002 核对真实消费入口。当前 `PersistComponent.PlayerPrefs/FileFragment/SQLite` 直接返回 Manager 接口，尚无对应业务 CRUD/保存门面；以下存储调用说明用于理解既有实现和诊断，不授权新增、持有、传递或包装 Manager 的普通业务代码。需要这类入口的新增/修改分支保持 `blocked`，列出所需行为、已查接口与影响范围，待框架补齐合规入口后再继续；已经完成的独立部分可报 `partial`。普通 DTO/业务规则、只读诊断、不依赖存储的内存防护，以及合同已开放的类型化维护可按原边界继续；不因此修改存量调用或扩大维护权限。

先区分内存防护接入与下述存储/维护分支。仅做内存防护时，按参考冻结业务字段及 owner、可信初值来源、当前 Config 加载责任、失败后的结算/保存边界、允许写集和隔离验证；代码路径、类型、现有调用点及配置事实由 Agent 查询，不要求用户手填。没有实际存档调用时，不要求 `classify/item`、存储选型、耐久性/删除探针或 AES/Cipher 凭据；不能为补这些输入扩大到真实存档或配置维护。缺少玩法规则或可信恢复来源时，仅阻断依赖它们的步骤。

先冻结唯一项目根、当前 Platform/Channel/DevelopMode 与已导出的 `ConfigRuntimeSO`、现有 PersistComponent 可用性、业务数据模型与 `classify/item` 命名、选择的存储实现、支持平台、保存时机、清理范围、AES/SQLite 密码前置、允许写入集，以及可观察独立读写和清理结果的 Play 探针。任一项不唯一时返回 `blocked`。

只读诊断无需新增数据模型或探针，冻结真实平台/宿主、数据归属、可读位置、时间范围与脱敏边界即可；结果记录已有证据和缺口，静态诊断最高为 `partial`，不沿用接入任务的 Play 成功结论。业务迁移实现冻结源/目标版本、类型转换、冲突规则、旧数据保留、恢复与重复运行策略；仅编辑已授权项目源码，运行真实数据维护是另一个明确阶段。

值 AES 维护走参考中的存储分支，复用同 `inspect-data`、`migrate-encryption`、独立 `recover-maintenance`；冻结唯一自有普通 Prefab PersistComponent、真实 scope、源摘要及精确 Plan。FileFragment 是 `current-editor-global-data` 共享全集；PlayerPrefs 是 `current-editor-managed-indexed-data` 已索引字符串，并显式接受 `acknowledgeGlobalSave=true` 的全局 Save 副作用；SQLite 是 `current-editor-sqlite-managed-data` 标准库，在同库真实事务内转换。恢复绑定原 `operationId/migrationRecoveryToken`，不重放迁移；各后端备份和失败语义见参考。此分支不要求业务模型或 Play 探针，不表示设备耐久性，也不承担密码轮换或业务 schema 迁移。

SQLite Cipher 与 Privacy Key/IV 分别复用同三 Action 的独立类型化分支，执行宿主当前限 macOS Editor。Cipher 是标准库非空密码到不同非空密码的同库轮换，仅保存目标组件的唯一密码字段；Privacy 是当前 Editor 的标准三存储、激活 Master 当前 Privacy 逻辑组与配对正式 Runtime 的协调轮换。新手默认选择自动生成，不要求准备明文秘密或凭据 handle；指定 handle 只引用当前项目已经合法存在的凭据，不通过内部 API、临时代码或反射预置。先按参考冻结各自完整范围、来源摘要、明确副作用及恢复条件，缺项只阻断对应依赖。Keychain 与恢复材料须先封存并重读，提交后失败保留 `partial`，先 Verify 再以原 Operation 建独立恢复计划。

默认只读元数据复用同 `inspect-data`：标准目标、`storage=PlayerPrefs/SQLite`、`dataScope=current-editor-global-data`，不传 `enableAES/sourceSha256`。不取 Master/Privacy、不读玩家值、不 Save/开库；成功仅限索引/HasKey 或四文件观察。其 `metadataSha256` 不是 FileFragment 迁移 SHA；SQLite 值迁移只用它作 Plan 前变化探测，真实事务另冻结逻辑摘要。PlayerPrefs 值检查使用独立 indexed scope，会内部读取已索引字符串但不输出值。各观察和写维护不能相互冒充。

- 既有实现的初始化顺序是先完成 `await Nova.Config.LoadAsync();`，再完成 `await Nova.Persist.LoadAsync();`，之后才可能访问三个存储实例。不能把两者并行、倒置，或以 `Awake`/`Start` 偷跑初始化替代既有启动链；顺序正确不消除上方普通业务消费入口缺口。
- 选型必须明确：PlayerPrefs 用于小体量键值设置；FileFragment 按 `classify` 分成 `.dat` 存档；SQLite 适用于更大、结构化的键值缓存，但 WebGL 不可用，也不向业务开放任意 SQL。SQLite 的 `CipherPassword` 是数据库级密码，与 `UseAESEncrypt` 的值级 AES 不是同一件事。
- 启用 `UseAESEncrypt` 时，当前坐标的 Privacy AES Key/IV 必须已经随 `ConfigRuntimeSO` 可用；它们与 App AES Key/IV 独立，按 UTF-8 各为 16 字节。不得在业务源码、日志、探针或本 Skill 输入中写入明文 Key/IV。初始缺配、组件设置或配对 Runtime 导出由已授权配置 Operation 推进；已有有效 Key/IV 轮换走本 Skill 的独立 Privacy 分支，不能用普通配置 patch 代替。缺唯一来源、权限或真实旧数据范围时保持准确 `blocked`。
- `SetXxx()` 只更新缓存并标脏，不能单独当作落盘证据；按冻结的耐久性要求使用 `Save()`、`Save(classify)`、既有自动保存或正常 Shutdown。PlayerPrefs 的分类保存底层仍是全局 Save；FileFragment 的 `RemoveAll` 和删空分类要到后续 `Save` 才物理删除；SQLite `RemoveAll` 会删除对应表。任何真实存档清理都是删除操作，必须逐一确认目标分类/条目和测试数据边界。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已授权关键整数 owner、字段身份、可信初值及失败边界 | `workspace-edit`；`ProtectedInt/ProtectedLong.TryRead/TryWrite/Dispose` | 业务字段声明、异常阻断和对称释放源码 | 读取失败的输出 0 不进入结算或保存；异常字段不以覆盖写重新建立可信基线；仅 Unity 主线程访问 |
| 当前独立包 API、`MemoryProtectionConfigs` 隐私扩展和后端可用性；隔离测试数据 | `MemoryProtection.IsActive`、字段 `IsProtected/IsQuarantined`、`Util.Json.Serialize/Deserialize`；既有 Unity 编译/Play 通道 | 当前防护状态、普通整数 JSON 和隔离验证结果 | 无商业插件可正常存取但明确未防护；有插件验收实际状态与异常阻断；API 不是可调度 MCP Action，不读写真实存档 |
| 已冻结的 ConfigRuntime 坐标、PersistComponent 与存储可用性 | Unity Editor 自动化通道 只读检查与现有 Inspector/ConfigWindow 事实 | 已确认的运行时加载前置 | 当前坐标、存储实现和 AES 前置一致；不修改序列化配置 |
| 已冻结的数据模型、classify/item、选型、保存和清理语义 | `workspace-edit` | 目标业务持久化调用和本地 Play 探针 | 写入仅覆盖业务源；不手改 Config、Prefab、Scene 或生成物 |
| 既有加载、读写和耐久性调用的诊断 | 只读核对 `Nova.Config.LoadAsync()`、`Nova.Persist.LoadAsync()` 及现存存储接口调用；实际行为查当前源码/XML | 已有调用与能力缺口说明 | 初始化和落盘事实分别判断；不把 Manager 访问视为普通业务可用 Adapter |
| 已冻结的运行验证路径 | Unity Editor 自动化通道 的脚本刷新、编译与 Play Mode 观察 | 可重复的读写/清理验证 | 不只验证同一内存缓存；保存后的独立读取或等价耐久性证据成立 |
| 当前 Editor 默认 PlayerPrefs 托管索引或 SQLite 固定四文件及唯一目标 | 同一 `nova.project.persistence.inspect-data` 的精确 storage 分支 | 计数/存在性/大小与 `metadataSha256`，无玩家值或配置写入 | 有界双次观察、范围与漂移检查；元数据成功不证明落盘、完整数据、数据库逻辑或写维护就绪 |
| 当前 Editor 共享 FileFragment 全集与唯一配置目标 | `nova.project.persistence.inspect-data` → `nova.project.persistence.migrate-encryption`；独立 `nova.project.persistence.recover-maintenance` | 完整原字节备份、持久 journal、集合摘要与字段保存收据 | 严格解码与逐值重读、原 Operation 绑定、全集后验；真实 Player/设备耐久性单独报告 |
| 当前 Editor 已索引 PlayerPrefs 字符串或标准 SQLite 库 | 同三项维护 Action 的精确 storage/scope | 受保护原/新值备份、SQLite 原厂逻辑备份、schema2 journal、原单字段收据 | PP 全局 Save 副作用显式接受；SQLite Nova lease 与真实事务重核、不替换主库；独立恢复与第三方漂移拒绝，成功限定 Editor 本地 |
| macOS Editor 标准 SQLite 非空 Cipher、源摘要和唯一组件 | 同 `migrate-encryption(operation=rotate-sqlite-cipher)`；独立 `recover-maintenance` | 独立加密 `before-cipher.db`、schema3 journal、项目 Keychain 与唯一字段收据 | 同库 rekey/完整逻辑重读/精确字段保存；原 Operation 与备份独立恢复；未证明设备耐久性 |
| macOS Editor 当前标准三存储、Privacy 逻辑组、配对 Master/Runtime | 同 `inspect-data(operation=inspect-privacy-aes)` → `migrate-encryption(operation=rotate-privacy-aes)`；独立恢复 | 受保护完整原/新配置与值、原分片字节、独立加密库、schema4 journal 与启动标记 | 全局 Save 显式接受；存档转换→正规 Master 保存→正规 Runtime 导出→完整后验与门禁清场，最高只证明冻结的当前 Editor 范围 |

## 实施与验证边界

内存防护分支遵循参考的独立验收：业务源码与编译、当前后端实际状态、失败阻断、普通整数 JSON、owner 释放及探针清场分别取证；插件缺失可完成普通存取接入，但不能宣称防护已生效。涉及正式存档读写时仍叠加原持久化边界。下述五项适用于存储接入，不因内存分支跳过而削弱真实存档维护的权限与验证。

1. 先通过上方消费入口门，再确认当前 ConfigRuntime 坐标已可加载、PersistComponent 已配置并且选择的实现支持目标平台。入口仍缺失时不得继续新增 Manager 调用或以 Play 通过报告接入成功。SQLite 目标为 WebGL 时返回 `not_applicable`；缺少当前坐标 Privacy AES 前置或需要改配置资产时返回 `blocked`。
2. 只编辑冻结的业务 C# 源码和本地 Play 探针；不修改 PersistComponent、ConfigMaster/ConfigRuntime、`Nova.prefab`、Prefab、Scene、存储实现、插件、密钥或生成物。任何 Unity 资产设置变更均属于独立且需确认的配置任务。
3. 在业务读取前等待 Config 再等待 Persist；按选择的实现使用统一 `classify -> item -> value` 调用面。把保存时机写清楚：需要耐久检查点才显式 Save，不能把同一进程内的 Set 后 Get 当作落盘成功。
4. 清理仅在已确认的测试数据范围执行：先验证 `RemoveItem` 或 `RemoveAll` 对目标分类/条目的语义，再执行该实现要求的保存或独立读取。不得以清理真实用户存档作为普通 Play 探针。
5. 只有加载顺序、选型、编译、选定存储的写入/保存后独立读取，以及本次范围内的清理语义均有 Play 证据时报告 `success`。已完成允许的源码步骤但缺少独立耐久性 Play 证据时最高为 `partial`；前置、范围或保存/清理策略不唯一时为 `blocked`；目标依赖 SQLite WebGL 或要求 Framework 存储实现改造时为 `not_applicable`。

业务接入按冻结业务写集执行。明确授权的值 AES、Cipher、Privacy 维护分别只按当前真实 Plan 写入各自限定范围，保留正式恢复材料；Plan token 仍需匹配。默认 metadata 与内部值检查分开；SQLite 写维护 Plan/Verify 可能改变伴随文件。Privacy 检查的 Plan/Execute/Verify 也会在 SQLite AES 已启用且主库存在时打开既有库，执行真实 BEGIN IMMEDIATE/ROLLBACK，取得写事务锁且可能改变 WAL/SHM；不转换/提交玩家值或保存配置不表示物理零写或不开库。默认 SQLite 四文件元数据检查仍不开库。Privacy 跨存储及配置不是真正的跨资源原子事务，未完成时启动门禁和原材料保留。已有授权不重复询问；删除恢复材料、范围扩大、设备或外部写入与 Git 按精确授权执行。
