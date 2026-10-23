# Nova Framework — MainDemo

本工程是 Nova Framework 主框架对外接口的**可运行参考**。不是项目模板，但对新项目有非常有价值的参考意义——你可以自由复制粘贴到自己的工程。

使用入口见本目录的 [DEMO-INDEX.md](DEMO-INDEX.md)。[DESIGN.md](DESIGN.md) 保留建设时的设计背景；实际 API、配置与演示状态以当前源码和随包 Docs 为准。消费项目无需主仓的 Minds 或开发工具。

---

异步与池化示例遵守各自所有权：`DemoNetworkView` 每次打开创建HTTP令牌源，GET/POST传入令牌末位重载，关闭或销毁时主动取消并释放令牌源；旧任务继续取得最终响应并在 `finally` 唯一归还 `HttpResponse`，旧代际不会更新重开的页面。取消驱动物理Abort，不代表服务端回滚；`DemoAssetView` 每次加载独立持有 CTS/代际，取消、替换或关闭后的结果自行释放。`DemoObjectPoolView` 按 `"DemoBullet"` 取用，纯 C# 占位对象以自身提供唯一 Target，当前对象未归还时不再覆盖；`DemoSoundView` 在 Stop、替换旧 SerialID 或关闭页面时，先调用 `StopSound(serialID, 0f)`，再调用 `ReleaseAssetBySerialID(serialID)` 结束本页面持有的声音请求。已加载声音仅 Stop 或自然结束不会释放资源，仍需显式释放；加载中的请求会登记停止，异步结果到达后由 Manager 释放且不播放。Pause/Resume 保留 SerialID 与资源，自然结束后也保留本页面的释放责任，直到显式结束、替换或关闭。声音 SerialID 只证明已发起请求，实际声音资源与播放仍需运行验证；`PlaySoundParams` 交给 SoundManager 后不自行重复归还。

`DemoNetworkView` 的 Connect 按钮另提供独占的二进制 echo 示例：URL 输入为 `ws://` 或 `wss://` 时使用该地址，HTTP 输入仍沿用原公开 echo 默认地址，不改变 GET/POST 语义。连接就绪后发送 Sample 协议号 `790001`、通道分配序列号与随机 nonce；仅收到三者匹配的完整回显才显示本次 ack，入队与 10 秒超时分别报告且不自动重放。已连接时再次点击发送新 echo，连接或待 ack 时重复点击不再发送，断线后显式点击重连。关闭和销毁先退订再只断开自己的唯一通道；本 Sample 曾释放且已完成底层关闭的实例可在重开时复用。未知归属、活动或歧义通道不接管，防止影响其它业务及其普通消息队列。接收回调仅复制值，不保留或再次回收框架池对象。此 echo ack 是明确的示例约定，不证明生产认证、心跳、服务器业务或设备网络；实际执行仍须授权目标服务器。

`DemoAppView` 的版本检查、下载和商店跳转固定发起时的取消令牌与页面打开身份；关闭、销毁或重新打开后的旧成功与旧异常不会写入新页面，取消回调异常时仍释放令牌源。内置 APK 下载由 `AppManager.DownloadAsync` 调用 `AppApkDownloader.DownloadAsync`，仅支持已启用更新、命中推荐或强制更新且采用 Apk 路由的 Android Player；冻结 HTTPS 主备地址后流式下载，经过文件大小和 APK 归档结构检查再原子提交。Editor、iOS、WebGL 明确拒绝该文件下载入口。返回路径只表示下载文件完成，不证明 APK 签名、目标版本或安装成功；商店跳转结果也不表示应用已更新。

`DemoConfigView` 的远端应用配置刷新固定页面打开身份，关闭再重开后不接收旧反馈；刷新属于全局配置能力，关闭页面不会取消或回滚它。快照只显示当前 `Nova.Config` 值，空 SDK 配置项会跳过并提示，不把配置实例数当作 SDK 初始化完成数。

`DemoNativeView` 只在页面打开且已有本次令牌时发起操作，通知查询、权限请求、评价请求和设置跳转的结果与异常都受原页面身份约束。关闭只取消本页等待，无法撤回已交给系统的通知或评价请求；设置跳转没有取消入口。`RequestDispatched` 只表示请求已交给系统，设置入口的 `true` 只表示已发起跳转，均不证明系统提示展示、权限修改、评价或提交完成。

`DemoUIView` 同时跟踪本页已打开与正在加载的 Toast、Dialog 请求；负编号不记录，加载中的请求只提示“正在加载”。关闭会关闭已打开子页面，并通过 `CloseUIView(serialID)` 取消加载中的打开；实际资源加载可能继续，但迟到结果不再打开子页面。自关闭与加载失败会移除失效编号，页面关闭时解除失败订阅。逐项关闭失败会保留编号和原 UI 门面，重开时暂停新子页面请求，可用 Close All 重试；旧 Dialog 回调不能写入重开的页面。它只处理本页拥有的子页面。

`DemoLocalizationView` 的五个示例标签在 Prefab 中持久绑定 `Demo_Greet`、`Demo_Confirm`、`Demo_Cancel`、`Demo_Toast_Tip`、`Demo_Switch_Lang`，由既有 `TextLocalizing` 在语言刷新事件后更新；未绑定的节点才保留 `GetText` 回退，非空错误 Key 不由回退覆盖。语言请求固定本次视图的 CTS/Token，关闭后旧请求不再执行该视图的成功反馈和手动刷新；这不会取消全局语言切换或撤回其刷新广播。当前演示支持简中和英文，自动字体适配关闭，两种语言复用现有 `AaBianYaKai-2 SDF`；字体表中的其他地址不代表已有资源，需按项目真实字体另行接入。

`DemoAssetView` 在既有滚动交互区动态提供明确 Package 输入、“清理未引用缓存”和“取消清理等待”。清理前读取已有公开 `Nova.Asset` 的当前清单版本，仅调用 `ClearUnusedCacheAsync`；不创建 Manager、不切模式或清单。取消或关闭只停止本页等待，实际清理继续独立观察，同一 Owner/Package 不重复提交。公开调用异常且终态不明时本会话保留占用，不自动重试。正常返回只说明操作返回，仍需核对当前引用保留和实际磁盘结果；默认 EditorSimulate 可以合法无删除，不能据此认定磁盘清理验收通过。

`DemoFrameworkComponentView` 的现有页面提供全局速度输入和应用、暂停、恢复、恢复正常速度及退出按钮。恢复使用 `Nova.Self.ResumeGame()` 的暂停前缓存，恢复正常速度使用 `ResetNormalGameSpeed()` 设为 1；重复暂停不覆盖缓存。关闭只恢复本页发起且仍持有的暂停，显式应用或重置的速度继续生效。框架只有一个暂停缓存，页面不接管已观察到的外部暂停；退出不会自动等待业务存档或请求，Editor 下停止 Play，Player 退出另验。

`DemoPersistView` 增加独立保存按钮，复用 `Nova.Persist.PlayerPrefs.Save("Demo")`。Set/Get/Has/Delete 仍使用原 `Demo` 分组；默认 Unity 后端保存会提交整个进程的偏好缓存，返回 true 只表示底层报告完成。耐久性应结合下一次独立启动的读取确认，不能只看同一内存内的 Get。

`DemoHybridClrRuntimeUpdateView` 另提供现有 Content 场景的独立加载与卸载按钮，按当前默认 `Default` Package 地址 `DemoRuntimeHotUpdateContent` 以 Additive 加载。它不要求先执行 Running DLL 流程；原增量流程的 Tag → DLL → 业务入口 → Scene 顺序保留，两条路径互斥。加载未返回时卸载按钮只请求取消，原请求继续负责清场；返回的 `ISceneHandle` 由本页唯一持有，到卸载、关闭或销毁时一次 `UnloadAsync()`，完成后不再访问。取消停止等待不保证底层立即中止；卸载失败保留阻断状态。

`DemoPrefabView` 的每次实例化固定本次打开周期的取消令牌和所有者；关闭、重开或取消后的旧结果不能接管新页面，已返回但未接管的实例仍进入统一销毁回收链。正常操作使用 `Nova.Prefab.Destroy`，框架门面已经销毁时仅用原生销毁兜底触发既有 `PrefabInstanceTag` 清理，不手动释放内部句柄。

`DemoProcedureView` 合并框架与当前已加载业务程序集的流程快照，并提供“重新预加载”。请求只在当前 `ProcedurePlaying` 接受，同一帧重复请求被拒绝，下一次轮询切回 `ProcedurePreload`，仍按 Persist → Localization → Network → SDK → Table → UI → Sound → Vibrate 的八步顺序执行。入口页面接受请求后关闭自己；主流程退出时只关闭自己拥有的导航，重入后再打开一个新导航。每次预加载固定 token 与进入/尝试代际，离开后的旧任务、重试弹窗和 finally 不接管重入状态；取消不代表底层加载立即停止。

当前 `ProcedurePreload.RunPreloadAsync()` 没有检查各加载调用的布尔结果，例如 `Nova.Table.LoadAsync()` 返回 `false` 后仍会计步并继续，最后可以设置 `m_AllLoaded=true`。进度完成只说明调用链未抛异常且任务身份仍有效；业务接入还须核对目标表、注册项与资源是否实际可用。

`DemoIntegrationTableUiView` 从已加载的 `TbDemo_Item` 按 `Id` 扫描唯一行，显示编号、名称、图标地址和价格。每次打开默认读取明确编号 `1001`；可输入 `1002` 查看另一行，输入 `999999` 或非整数查看错误处理。错误会清空上次详情，池化重开会重新读表；图标地址仅作文字展示。导航的 4.5 入口只合并本页面的重复打开，已加载时回焦，关闭后保留原导航。

## 教学页面的使用边界

跨模块页面保留了较短的教学实现：`DemoIntegrationUiAssetView` 在关闭时释放句柄，但异步续体读取共享 CTS，未固定每次请求的身份，也未在释放前清空 Image；快速重复加载或池化重开时不具备 `DemoAssetView` 的完整所有权保护。`DemoIntegrationUiLocalizationView` 只检查对象是否销毁，没有页面令牌；关页后仍可能完成全局切语言并更新池化页面。两页打开的 Toast 不由父页跟踪关闭，返回的编号也不证明子页面已打开。业务接入应采用对应独立模块页的生命周期写法。

`DemoIntegrationEventNetworkView` 的连接状态是本地布尔模拟，不能用来验收真实网络。`DemoTableView` 校验固定的四张示例表、十行物品与 `XXXXX` Map 行，不适合作为任意项目的通用表健康检查；管理器、流程与 HybridCLR 页面展示的也是当前固定列表或已加载程序集快照，目录或 namespace 名不能代替实际 DLL 身份。

`DemoEdgeCasesView` 会实际执行 `Put(null)`、重复订阅和故意不归还引用；泄漏按钮不保留可供关闭时归还的所有者，关闭页面不能消除它。FSM 按钮只输出约束说明，没有真正触发非法跳转。`DemoReferenceView` 的 Get×100 可重复累积，Put 与关闭归还没有逐项异常恢复；某次归还抛异常时，后续引用可能仍未清理。`DemoToastView` 使用受 `Time.timeScale` 影响的延时，Dialog 业务回调抛异常也可能阻止后续关闭。这些边界示例不应直接当作生产容错实现。

事件示例的池化载荷在交给 `Nova.Event.Fire` 后由事件分发链管理，回调不保留或自行归还它；入队不证明已分发。`DemoVibrateView.StopAll` 影响全局振动，关闭页面并不自动停止；数值输入只约束有限值及归一化项，时间项仍可为负。`DemoUtilView` 会显示演示明文/密文，MD5 示例也不提供身份认证；不要填真实秘密。调试器开关和日志按钮同样有全局副作用，关页不会撤回日志或恢复原调试器可见性。

## 业务进度结构迁移

`Scripts/Runtime/UIs/DemoPersistView/DemoProgressMigration.cs` 是可复用的项目业务入口，通过公开 `IFileFragmentManager` 读取旧进度、保存新版并读取、更新实际业务值，不创建框架 Manager 或维护 Action。它不自动接管存档；调用方须先明确源/目标分类并独占写入，确保既有启动链已完成 `Nova.Persist.LoadAsync()`，再注入 `Nova.Persist.FileFragment`。

```csharp
var migration = new DemoProgressMigration(Nova.Persist.FileFragment, "PlayerProgressV1", "PlayerProgressV2");
migration.Migrate();
var progress = migration.ReadProgress();
migration.SaveProgress(new DemoProgressMigration.Progress(progress.Level + 1, progress.Coins, progress.DisplayName));
```

这段调用只展示已冻结业务入口的用法，不是运行真实玩家数据的授权。两个分类须不同（不区分大小写），且各为1至64个ASCII字母、数字、下划线或短横线；目标分类只能包含本例 `progress` 条目。源同名条目的v1 JSON为 `schemaVersion/level/coins/name`，名称可缺省为空，等级至少1、金币非负且均为Int32。未知字段、重复字段、坏类型和不支持版本拒绝，JSON最多64KiB UTF8。

v2将 `schemaVersion=2`、`progress.level`、`wallet.coins`、`displayName` 与旧原文摘要 `migratedFrom` 放在同一目标条目，通过 `Save(targetClassify)` 一起保存。旧分类及原值保留。已有同来源v2时不重复保存，允许游戏随后更新进度；其他来源、坏目标或目标含其他条目均拒绝覆盖。保存返回false或抛异常后，当前服务不继续读写，需由调用方关闭并重新初始化存储后独立核查，不回滚或自动重放。

`ReadProgress()`从注入实例读取；真实落盘验收必须关闭旧实例、重新初始化另一实例后读取，不能只看同缓存值。FileFragment不提供跨分类事务；本例不删除旧档、不单独提前写版本标记，也不保证设备掉电或浏览器异步持久化已完成。`Assets/Tests/Editor/DemoPersistMigrationPlayTests.cs` 在空场景Play中使用自有临时根的真实FileFragment，覆盖迁移、独立重开、业务更新后幂等、来源/结构冲突拒写与清场。实际业务模型、真实账号数据、其他存储实现及设备耐久性仍分别冻结和验证。

## 整体拓扑

```
DemoNavTreeView（树形导航主菜单）
├─ 1.Core 核心库（9 个叶子）
│   ├─ 1.1 DemoFrameworkComponentView
│   ├─ 1.2 DemoFrameworkManagerView
│   ├─ 1.3 DemoFsmView
│   ├─ 1.4 DemoReferenceView
│   ├─ 1.5 DemoLogView
│   ├─ 1.6 DemoUtilView
│   ├─ 1.7 DemoCollectionsView
│   ├─ 1.8 DemoExtensionsView
│   └─ 1.9 DemoEdgeCasesView
├─ 2.Modules 模块库（17 个叶子）
│   ├─ 2.1  DemoAppView
│   ├─ 2.2  DemoAssetView
│   ├─ 2.3  DemoPrefabView
│   ├─ 2.4  DemoConfigView
│   ├─ 2.5  DemoEventView
│   ├─ 2.6  DemoTableView
│   ├─ 2.7  DemoLocalizationView
│   ├─ 2.8  DemoUIView
│   ├─ 2.9  DemoNetworkView
│   ├─ 2.10 DemoProcedureView
│   ├─ 2.11 DemoObjectPoolView
│   ├─ 2.12 DemoPersistView
│   ├─ 2.13 DemoSoundView
│   ├─ 2.14 DemoVibrateView
│   ├─ 2.15 DemoSDKView
│   ├─ 2.16 DemoDebugView
│   └─ 2.17 DemoNativeView
├─ 3.HybridCLR 热更新解决方案（4 个叶子）
│   ├─ 3.1 DemoHybridClrAotMetadataView
│   ├─ 3.2 DemoHybridClrGameDllView
│   ├─ 3.3 DemoHybridClrProcedureRegisterView
│   └─ 3.4 DemoHybridClrRuntimeUpdateView
└─ 4.Integration 跨模块联动（5 个叶子）
    ├─ 4.1 DemoIntegrationUiLocalizationView
    ├─ 4.2 DemoIntegrationUiAssetView
    ├─ 4.3 DemoIntegrationEventNetworkView
    ├─ 4.4 DemoIntegrationConfigHybridClrView
    └─ 4.5 DemoIntegrationTableUiView
```

共 35 个演示子页面，均继承 `BaseDemoView`（三段式：标题栏 + 交互区 + 反馈区），通过 `Nova.UI.OpenUIViewAsync<T>()` 打开，`PauseCoveredUIView=false` 保持导航菜单始终可见。3.4 固定使用启动阶段选定的 Manifest，仅下载该 Manifest 内带 `demo_runtime_hotupdate` 标签的内容，再依次加载 Running DLL、业务入口和增量场景。

另有 2 个辅助子页面（不计入树形导航 35 叶）：
- `DemoToastView`：DemoUIView / DemoIntegrationUiLocalizationView / DemoIntegrationUiAssetView 内部 spawn 的轻量子页面。
- `DemoDialogView`：DemoUIView 内部 spawn 的带按钮对话框子页面。

---

## 目录结构

| 路径 | 说明 |
|---|---|
| `Scripts/Runtime/UIs/` | 35 个 DemoXxxView 脚本 + BaseDemoView 基类 + DemoNavTree |
| `Scripts/Runtime/DataTypes/` | Tables / Sounds / Vibrates / Localizations / Networks 数据类型 |
| `Scripts/Runtime/Procedures/` | 业务侧 Procedure 实现 |
| `Scripts/Editor/` | 业务侧 Inspector / EditorWindow |
| `Prefabs/UIs/` | DemoNavTreeView prefab + 对应 DemoXxxView prefab；页面新增须完成持久绑定与 UI 注册导出 |
| `Configs/` | 框架 ScriptableObject 配置示例 |
| `Jsons/` | 多语言 / 表格 JSON 资源 |
| `Resources/BuiltIn/Prefabs/` | Launcher 启动期 UI Prefab（SplashPanel / ProgressPanel / DialogPanel） |
| `Fonts/` | 演示用字体资源 |

详细 demo 索引（编号 / 类名 / 变体 / API / 资源依赖）见 [`DEMO-INDEX.md`](DEMO-INDEX.md)。

---

## 运行方式

### 自动（推荐）

导入 Demo 后，Editor 会自动弹窗：

1. **检测旧版本** — 若工程已有其他版本的 Nova Framework Demo，提示一键清理。
2. **设置启动场景** — 询问是否将 `MainDemo.unity` 设为 Build Settings 第一项。

先核对旧目录是否含自己的改动，再决定清理或设置场景。配置、资源、编译和目标平台就绪后点 Play；弹窗处理完成不代表启动、在线服务或全部页面已验收。

### Google Play 上传示例

`Editor/PipifySettings.asset` 提供构建 AAB 后上传的组合批次，以及手动上传已有 AAB 的独立批次。组合批次使用本次成功构建产物；独立批次中的 AAB 路径是开发示例，导入后必须同时填写自己的有效 AAB 路径和应用包名。两种批次都需填写自己的服务账号凭据路径，公开副本不会附带凭据文件。配置要求见已安装 Framework 的 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.GooglePlay.md`。

保存的全量、HybridCLR 与 Bundle 组合批次，会先导出配置和网络代码/数据，再验证、生成和编译热更新 DLL，随后构建资源与 Player。修改源表后应沿用这个顺序，避免 DLL 使用上一轮导出结果。保存批次只更新配置；运行包含 CDN、商店上传等步骤的批次前，仍需核对目标和本次授权。

### 手动

1. `File > Build Settings...`，把实际导入目录中的 `MainDemo.unity` 拖到 `Scenes In Build` 第一项；不要套用其他版本的目录名。
2. 点 Play。

---

## 通过 Codex 使用

可以直接描述目标，例如“帮我运行主框架示例”“解释这个字段并改成适合移动端的配置”“照这个页面做一个由表格驱动的背包”。Codex 从实际安装的 Framework 包读取 `Docs/START_HERE.md`、`Agents/INDEX.md`，按目标复用现有配置、表格、UI、流程、资源或诊断 Skill；不需要记住 Skill 名称，也不为每个演示按钮建立新 Skill。

先读取实际工程、场景、配置坐标、输入资产和当前工具能力，再按既有入口执行并验证。示例按钮可能发起联网、账号、存档、权限或商店操作；学习和查阅示例不等于授权这些实际操作。在线结果、声音播放、DLL 加载和真机行为需要各自证据，不能由编译或页面打开推断成功。

---

## 贡献流程

新增 demo 子页面按下列交付链完成，框架主仓开发还须遵守当前仓库工作流：

1. **设计期（步 1-3）**：确认用户目标、包依赖和独立输入输出，复用本 Sample 的三段式页面；用已有 Skill、Action、CLI 或 Pipify 表达可复用操作，不把内部方法逐个变成 Skill。
2. **源数据期（步 4-6）**：在对应模块 xlsx 追加 `Demo_` 前缀数据行 → Pipify 导出 → read_console 确认编译。
3. **资源期（步 7-9）**：按实际需求维护 Prefab、图片和声音及其采集规则。当前 `Editor/BundleCollectorSetting.asset` 使用 `AddressByFileName`，地址为不带扩展名的文件名；UI 注册填写 `DemoXxxView`，不得写成 `UIs/DemoXxxView`。
4. **代码期（步 10-13）**：继承 `BaseDemoView`，正确配对打开、关闭、句柄释放和异步取消；UI Excel 追加注册行并正式导出，再更新 `DemoTreeData.cs` 叶子节点。
5. **Prefab 期（步 14-16）**：UnityMCP 串行 clone BaseDemoView.prefab → 绑定字段 → read_console 确认 Missing Reference 为零。
6. **验证期（步 17）**：Play Mode 全链路验证（树形菜单点叶子 → 子页面打开 → 反馈区出日志 → X 关闭）。
7. **文档与能力期（步 18）**：将接口说明写入源码/XML，将跨调用约束写入对应模块 Docs、本 README 与索引；已有 Skill 维护真实用户任务、输入、复用入口、产物、失败恢复和验证边界。优先补已有 Skill 的 reference/contract，只有独立目标无法容纳时才新增 Skill；自动索引负责发现入口，不再另写逐函数审查账本。运行适用的能力与 Skill 校验，未实现或未验证的部分保留显式缺口。

---

## 升级注意

UPM 标准行为：升级 Nova Framework 后，Package Manager 会把重新导入的 Sample 放入新版本目录；旧版本目录**不会**自动删除。

- 旧版 asmdef 名称与新版冲突，工程会编译报红。
- SampleVersionGuard 会在 Editor 启动时检测并提示一键清理。
- 清理前确认旧目录中的自定义改动和依赖，只移除自己确认不再使用的旧版本 Sample。

---

## 反馈

如果 Demo 内某个接口示例有错或不易理解，请反馈到 Nova Framework 主仓库 issue。

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 `Docs/Onboarding/PROJECT_STRUCTURE.md` 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。
