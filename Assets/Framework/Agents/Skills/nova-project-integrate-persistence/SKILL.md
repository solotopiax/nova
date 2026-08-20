---
name: nova-project-integrate-persistence
description: Use when 项目组要在现有 Nova 项目中接入或调整业务本地持久化数据，并需完成存储选型、加载顺序、保存清理语义和运行时读写验证时使用。
---

# Nova 接入业务持久化存储

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

再读取 `references/contract.json`。仅在当前决策分支按需读取：确认三种入口、加载顺序和现有组件配置时读 `Docs/Runtime/Modules/Persist/PersistComponent.md`；选 PlayerPrefs、FileFragment 或 SQLite 时只读对应的 `Docs/Runtime/Modules/Persist/PlayerPrefsManager.md`、`FileFragmentManager.md` 或 `SQLiteManager.md`；确认自动保存时读 `Docs/Runtime/Modules/Persist/PersistManagerConfigBase.md`；核验 AES 前置时读 `Docs/Runtime/Modules/Config/PrivacyConfigs.md`。仅在排查现有设置时读 `Docs/Editor/Inspectors/PersistComponentInspector/PersistComponentInspector.md` 或 `Docs/Editor/Windows/ConfigWindow.md`；不要递归加载全部 Persist、Config 或加密文档。

## 冻结输入与阻断门

先冻结唯一项目根、当前 Platform/Channel/DevelopMode 与已导出的 `ConfigRuntimeSO`、现有 PersistComponent 可用性、业务数据模型与 `classify/item` 命名、选择的存储实现、支持平台、保存时机、清理范围、AES/SQLite 密码前置、允许写入集，以及可观察独立读写和清理结果的 Play 探针。任一项不唯一时返回 `blocked`。

- 先完成 `await Nova.Config.LoadAsync();`，再完成 `await Nova.Persist.LoadAsync();`，之后才访问 `Nova.Persist.PlayerPrefs`、`FileFragment` 或 `SQLite`。不能把两者并行、倒置，或以 `Awake`/`Start` 偷跑初始化替代既有启动链。
- 选型必须明确：PlayerPrefs 用于小体量键值设置；FileFragment 按 `classify` 分成 `.dat` 存档；SQLite 适用于更大、结构化的键值缓存，但 WebGL 不可用，也不向业务开放任意 SQL。SQLite 的 `CipherPassword` 是数据库级密码，与 `UseAESEncrypt` 的值级 AES 不是同一件事。
- 启用 `UseAESEncrypt` 时，当前坐标的 Privacy AES Key/IV 必须已经随 `ConfigRuntimeSO` 可用；它们与 App AES Key/IV 独立，按 UTF-8 各为 16 字节。不得在业务源码、日志、探针或本 Skill 输入中写入明文 Key/IV。缺配、需要创建/轮换 Key/IV、修改 PersistComponent 或重新导出 ConfigRuntimeSO 时返回 `blocked`，交给已确认的配置流程处理。
- `SetXxx()` 只更新缓存并标脏，不能单独当作落盘证据；按冻结的耐久性要求使用 `Save()`、`Save(classify)`、既有自动保存或正常 Shutdown。PlayerPrefs 的分类保存底层仍是全局 Save；FileFragment 的 `RemoveAll` 和删空分类要到后续 `Save` 才物理删除；SQLite `RemoveAll` 会删除对应表。任何真实存档清理都是删除操作，必须逐一确认目标分类/条目和测试数据边界。

## Input → Action Adapter → Artifact → Evidence

| Input | 现有 Action Adapter | Artifact | Evidence |
|---|---|---|---|
| 已冻结的 ConfigRuntime 坐标、PersistComponent 与存储可用性 | Unity Editor 自动化通道 只读检查与现有 Inspector/ConfigWindow 事实 | 已确认的运行时加载前置 | 当前坐标、存储实现和 AES 前置一致；不修改序列化配置 |
| 已冻结的数据模型、classify/item、选型、保存和清理语义 | `workspace-edit` | 目标业务持久化调用和本地 Play 探针 | 写入仅覆盖业务源；不手改 Config、Prefab、Scene 或生成物 |
| 已冻结的加载、读写和耐久性契约 | `Nova.Config.LoadAsync()`、`Nova.Persist.LoadAsync()`、`Nova.Persist.PlayerPrefs` / `FileFragment` / `SQLite`、`IPersistManager.Save` / `RemoveItem` / `RemoveAll` | 已初始化且可读取的目标业务数据 | 加载顺序正确，写入与保存/清理语义符合选定实现 |
| 已冻结的运行验证路径 | Unity Editor 自动化通道 的脚本刷新、编译与 Play Mode 观察 | 可重复的读写/清理验证 | 不只验证同一内存缓存；保存后的独立读取或等价耐久性证据成立 |

## 实施与验证边界

1. 先确认当前 ConfigRuntime 坐标已可加载、PersistComponent 已配置并且选择的实现支持目标平台。SQLite 目标为 WebGL 时返回 `not_applicable`；缺少当前坐标 Privacy AES 前置或需要改配置资产时返回 `blocked`。
2. 只编辑冻结的业务 C# 源码和本地 Play 探针；不修改 PersistComponent、ConfigMaster/ConfigRuntime、`Nova.prefab`、Prefab、Scene、存储实现、插件、密钥或生成物。任何 Unity 资产设置变更均属于独立且需确认的配置任务。
3. 在业务读取前等待 Config 再等待 Persist；按选择的实现使用统一 `classify -> item -> value` 调用面。把保存时机写清楚：需要耐久检查点才显式 Save，不能把同一进程内的 Set 后 Get 当作落盘成功。
4. 清理仅在已确认的测试数据范围执行：先验证 `RemoveItem` 或 `RemoveAll` 对目标分类/条目的语义，再执行该实现要求的保存或独立读取。不得以清理真实用户存档作为普通 Play 探针。
5. 只有加载顺序、选型、编译、选定存储的写入/保存后独立读取，以及本次范围内的清理语义均有 Play 证据时报告 `success`。已完成允许的源码步骤但缺少独立耐久性 Play 证据时最高为 `partial`；前置、范围或保存/清理策略不唯一时为 `blocked`；目标依赖 SQLite WebGL 或要求 Framework 存储实现改造时为 `not_applicable`。

不默认修改 Framework 包、PersistComponent、ConfigMasterSO、ConfigRuntimeSO、`Nova.prefab`、Prefab、Scene、SQLite 插件、Privacy AES Key/IV、CipherPassword、真实用户存档、Bundle/Player、设备、外部服务或 Git。删除或迁移持久化数据、修改加密/密码、变更存储实现、自动保存策略或业务数据契约、外部写入、凭据使用、Git commit / push 都需要本 Skill 之外的精确确认。
