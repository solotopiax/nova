# EditorUtil.AndroidResolver

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `public static partial class EditorUtil.AndroidResolver` |
| 命名空间 | `NovaFramework.Editor` |
| 全局访问 | `EditorUtil.AndroidResolver` |
| 功能描述 | 通过反射调用 EDM4U PlayServicesResolver.ResolveSync，强制重建 Android 依赖目录 |

---

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.AndroidResolver.cs` | `partial EditorUtil.AndroidResolver` | 公有接口：`Resolve()` |
| `EditorUtil.AndroidResolver.Methods.cs` | `partial EditorUtil.AndroidResolver` | 私有方法：`ExecuteResolveSync`、`FindResolverType` |

---

## §5 完整公开 API

```csharp
/// 触发一次同步 Android 依赖解析（等价于 EDM4U 菜单 Assets → External Dependency Manager → Android → Force Resolve）。
/// 内部通过反射调用 GooglePlayServices.PlayServicesResolver.ResolveSync(true)，强制重建 Assets/GeneratedLocalRepo/**。
/// 找不到 EDM4U 类型/方法或反射调用抛异常时直接抛 InvalidOperationException，由调用方（Pipify Runner）
/// 捕获并标记 Step 失败；禁止静默跳过，避免下游 BuildPlayer 在 GeneratedLocalRepo 缺失时才崩。
public static void Resolve()
```

---

## §10 常见误区

| 误区 | 真相 |
|---|---|
| EDM4U `ResolveSync` 是双参数 `(bool, bool)` | 实际签名是 `ResolveSync(bool forceResolution)` 单参数；反射拼错签名会让 `GetMethod` 返回 null |
| 找不到 EDM4U 类型/方法时 Warning 跳过即可 | 跳过后 `Assets/GeneratedLocalRepo/**` 不会被重建，Step 3 BuildPlayer 阶段才被动触发 EDM4U，但目标目录缺失会报 `DirectoryNotFoundException` 并把 HybridCLR `GenerateStripedAOTDlls` 拖崩；当前实现一律抛异常，让 Pipify Runner 在 Step 1 就显式失败 |

---

## §11 使用示例

```csharp
// 在 HybridCLR 编译之前执行，确保 GeneratedLocalRepo 目录存在
EditorUtil.AndroidResolver.Resolve();
EditorUtil.HybridCLR.CompileDllActiveBuildTarget();

// 或通过 Pipify Batch（推荐）：
// PipifyWindow 中将 Step "edm4u.android_resolve" 拖到 "hybridclr.generate_all" 之前
```

---

## §13 关联文档

- [EditorUtil.md](../EditorUtil.md) — EditorUtil 工具集概览
- [EditorUtil.HybridCLR.md](../EditorUtil.HybridCLR/EditorUtil.HybridCLR.md) — HybridCLR 原子操作合集
- [PipifySteps.md](../EditorUtil.Pipify/PipifySteps.md) — 全 Step 清单

## 消费端 Action 的执行边界

`nova.project.android.resolve-dependencies` 复用 `Resolve()`，按当前真实资源刷新参与者和自然状态建立可执行合同。Plan 先核稳定编辑态、完整方法派发规则、源码与 DLL/MVID、现有调度和无弹窗条件，再冻结模板支路、精确写集和非目标保护；未知实现、订阅、输入、容量或状态均以具体原因 `blocked`，不通过关闭设置或预先调用 getter 制造可执行状态。公开 `describe / plan / execute / verify` 与请求 Schema 保持不变。

源码编译包重新编译后，必须核对唯一实际加载类型、磁盘 DLL/MVID、编译源/条件/引用映射及实际派发和必要调用分支，才能维护对应精确身份；源码未变本身不构成准入依据。ShaderGraph 路径按原回调实际使用的无点 `shadergraph` / `shadersubgraph` 后缀保守拒绝，同时覆盖完整冻结域中的父目录及 `.meta`；不因身份维护放宽动态对象、订阅或队列检查。

已安装的 `MemoryProtectionLegacyConfigPostprocessor` 仅在冻结域完全排除 `.asset` 时按专属自然支路核对，不进入通用外围状态表。读取既有 `s_Queued` 前，必须匹配唯一实际加载模块、完整源码、asmdef 与 Unity 编译归属，并有真实正常 `Editor.update` 或完整绑定的 PlanJobs/VerifyJobs update 闭包栈见证；通用同步上下文不作见证。原 Plan State 与执行观察器仅在快照同域且回调与调度摘要仍相同时继承该见证；Verify 的反序列化回执一律重新核当前真实调用栈，不能靠自一致摘要证明来源。两种路径均拒绝初始化/迁移重入和待处理队列。不会调用、清除或推进迁移，也不以历史迁移成功作为 Android 验收条件；已返回的历史结果属于当前非目标基线，所有配置保护保持。

该模块依赖 `NovaFramework.Editor`，完整 DLL/MVID 存在既有 Skill 真源的 `references/android-refresh-compiled-identity.json`，避免把下游编译指纹内嵌回上游后再次失效。文件仅含 `assemblySha256` 与 `mvid`，最多 512B；缺失、重复键、额外字段或无效身份均阻断。维护方须在编译后完成实际加载、磁盘、源码和编译所有者复核，再更新这个非代码 reference；Action 从不自动采纳当前 DLL。已核实际完整身份进入原回调冻结摘要，漂移仍阻断执行与核验。

完整刷新快照与解析回执仅在此 Action 内采用有界无损表示：原文最多 8MiB，gzip 最多 128KiB，专属封装最多 192KiB；Plan 另按完整信封、写集与 null 字段核算 192KiB Operation 局部预算，通用 Store 仍为 256KiB。全部对象行、顺序、dirty 标志、原摘要及前后材料保持，不更改域与写前/写后校验。公开 data 保留原字段或数组形状，并以紧凑数据 1536KiB 为局部展示预算，为原 2MiB MCP 响应保留外层余量。Plan 超限具体 blocked；写后公开展示超限明确 partial/警告；完整编码回执只有成功附入结果后才交 Core 持久化，不能提前冒称已保存。若写后回执本身越过编码界，安全返回 partial 与实际容量原因，保留此前已持久化的完整写前 Recovery，并明确完整写后回执未持久化，不二次抛出或重放。容量失败说明实际字节、对应上限和层级。Verify 兼容旧普通回执；无损解码不代表解析或验收成功。

2026-10-19 的一次后来的只读复现含 17,337 条唯一对象行，快照 896,436B、完整计划回执 1,028,428B；完整回执无损封装后 101,744B，完整 Operation 模拟 116,113B，保留原 data 的 Gateway 预算模拟 902,239B。该样本用于容量回归，不是旧失败 Plan 的补造材料，也不证明 Resolve、Gradle 或 Player 已执行。永久测试仅保留经列语法审查的完整身份/类型/dirty、公开 Sample 场景与选择 ID 行，不分发完整私密回执。

当前可接纳范围是已有 Gradle 模板和本地 SRCAAR/POM。Plan 冻结完整有序 Maven 输入、原 POM 与既有目标、`gpsr` 删除集合、工具链、可能触及的父目录及普通无 AssetBundle 名的 importer；Java 只进行无弹窗版本读取。下载、Gradle 执行、SDK 安装以及不满足当前合同的其他自然支路仍阻断。POM 验收计算完整预期 SHA-256：目标缺失或被明确删除时采用源内容，否则采用未删除的既有目标；只修改已有 packaging 节点为 `aar`，无节点或已为 `aar` 时保留原字节，保留坐标、依赖及其余内容。预期值在内存中按已核 EDM4U 1.2.188 的 XML 格式计算，不写文件或调用 vendor 修补。

执行前重新冻结完整合同并比较；确认只绑定当前 Plan。Execute 只调用一次正规 `Resolve()`，自有观察器记录实际四数组派发，并等待正常 Editor update 推进后只读核验。它不调用私有 vendor 回调、不消费或清理队列、不主动 flush、不取消厂商工作。失败、超时、取消、域重载或写后证据缺失均保留 `partial` 回执，不重放；旧 Receipt 缺完整刷新合同或跨域身份时最高为 `partial`。只有完整正常解析、刷新及输出证据一致才可 Verify `success`。

刷新等待复用绑定当前域、真实类型与 DLL/MVID 的 Version Handler 轻量调度探针，十秒轮询窗口内不反复遍历全部资产、标签和设置。自有 update 信号异步续接到 Unity 上下文，先让同轮后续回调返回；队列自然完成且 Search 事务验收就绪后，再做一次完整稳定输入与非目标校验，并复用其中实际捕获的版本快照写入回执。完整采集仍受取消控制，不计为额外的自然等待预算；超时后的单次诊断捕获即使观察到队列后来完成，也不能将超时升级为成功。全部 Search、稳定输入、脏状态、事务及最终产物要求保持不变。

这里的十秒是实际 Editor update 返回后检查的协作预算。`NextEditorUpdate` 没有独立取消注册或超时定时器；Editor 不再发出 update 时，当前 await 不会仅因十秒或取消自行完成。`Dispose` 关闭观察窗，不主动推进官方队列，也不把晚到的刷新或历史 `completed=false` 补为成功。

非目标对象行以 Ordinal 集合线性比较，仍拒绝两侧重复、原行缺失、脏状态、场景或选择变化；已审 Grid 空单例新增边界保持。新计划另以有限 UI Toolkit 角色合同识别正常引擎资源重建，所有原始对象行仍完整保存，只有实际字段引用证明归属的行在比较副本中成对处理；项目资产、输入纹理与其它用户对象不授予变化。文件/目录摘要变化与对象行变化分别报告，对象诊断仅展示移除和新增各最多四条、每条最多 240 字符及省略号。

UI Toolkit 合同绑定当前已审模块的实际加载、磁盘 SHA/MVID和既存面板根，只读现有字段，不调用创建型 getter、Reset、Repaint、Render 或初始化入口。允许的四类角色为面板直接持有的默认材质、默认浮点 Shader 信息纹理、Shader 信息存储纹理和动态 Atlas 页（包含页的 `m_Blitter.m_BlitMaterial` 复制材质）；面板拓扑、输入资源、精确类型、非持久化身份与原 dirty 仍需符合冻结合同。写前完整角色状态不变，写后只允许已证角色正常重建，未知归属继续 blocked/partial。新 Snapshot 的 `uir` 与 Receipt 的 `uirAfter` 分别进入摘要；旧字段为空时保持旧摘要和旧严格对象规则，不能补造历史 owner-map 或追溯升级 partial。

写集除本轮精确产物外还明示正常引擎派生效果：对应资产的 Library 导入登记；Search 两个属性库及字符串表、GUID 临时 merge 文件和实际四数组对应的有序事务追加；SpeedTree 指定原生派生依赖前缀维护；已审内存/UI 缓存和最多九个已有颜色偏好键的同值物化。Search 仅接纳现有已初始化的空属性缓存，联合只读锁下验证逻辑物化、稳定文件和精确事务增量，允许该派生缓存后续正常幂等维护；不把逻辑完成等同于所有被丢弃的后台 Task 退出。SpeedTree 要求属性静态方法、st9/spm 输入和现有非目标 artifact 相关依赖均为空，不把前缀维护描述成无副作用。

SpeedTree 的历史 artifact 查询按全项目累计最多 131,072 份结果限界，不再因单资产历史版本超过 128 而拒绝；仍逐项核有效性、检查全部当前依赖，并释放全部查询包装。当前 artifact 数量及每份依赖数量的原边界保持，取消不跳过释放。

外围合同依据实际已加载实现和 backing 状态核对 Preset/Sprite、UPM/AssetEvents、Grid、Graph、颜色消费者及 CodeEditor 尾部。颜色完整 pending 队列在 Plan 时冻结，不能绑定历史的六项或九项数量；只允许固定九键已存在且正规格式往返同值的物化，其他键或消费者阻断。Lighting 已存在 UI 可按计划执行固定 shader/Handles 三色更新，其余稳定状态仍核验。当前 IDE 支路限定已自然初始化的 Cursor、完成的安装发现、无写入需求的 `.vscode` 三文件和不会触发同步的真实输入角色；不调用发现、Sync、打开窗口或修改选择来放行。

普通 Unity 导入 usage 沿用 Action 已声明的 `ExternalWrite`：已启用时可调用 `assetPostProcessorsUsage` v2，字段仅为 `importActionId`、方法名、持续时间与调用数；该调用不构成外部送达证明。当前严格指纹内的 Google `Report(string,string)` 完成回调只读 `Enabled` 后返回，没有 HTTP 或持久上报队列；仍要求系统同意已请求且无进行中窗口等自然条件，冻结对象、字段、偏好及配置摘要，不调用 Report/Prompt 探测、不改同意。正规 UseJetifier=true 支路的项目 Gvh 同字节设置持久化和系统 `GooglePlayServices.UseJetifier=True` 精确物化另在 Plan 中冻结及验收。

Force Resolve 和构建场景回调的无弹窗检查复用 `TryCaptureNoPromptToolchain`，冻结 JDK 二进制与 release 摘要、版本、明确工作目录，并拒绝额外 Java 参数。构建回调 `ResolveSync(false, true)` 的无解析场景合同与此 Force Resolve 合同分别验证。测试覆盖实际方法绑定、完整 POM 字节、持久设置、刷新回执、Search 事务和正常 Editor update 等；测试通过不等于本工程 Force Resolve、Gradle、Player 或真机验收通过，实际执行必须另有当前回执。

## 调用与失败边界

底层 Resolve 与受控 Action 的身份/输出冻结是两层。原 FindResolverType 先查 Google.JarResolver / Google.VersionHandlerImpl，再扫描当前已加载程序集，遇到首个同名 Type 就返回，不加载替代程序集，也不拒绝所有同名歧义或核版本；Assembly.GetType 本身的异常没有统一吞掉。ExecuteResolveSync 只接受准确 ResolveSync(bool) 并同步传 force=true，缺类型/方法或 false 结果阻断；反射目标异常包装其 InnerException，其它反射错误仍可能传播。不能把底层函数单独当作有确认、完整写集或恢复保证的解析任务。
