# Changelog

本文件记录 Nova Framework 各版本的团队聚合变更内容，遵循 [Keep a Changelog](https://keepachangelog.com/) 格式。

> 详细包级变更见 `Assets/Framework/CHANGELOG.md`，本文件仅作团队聚合视图，不进 npm tarball。

---

## [0.5.26] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.26]` 节。

主要内容：
- 主包 framework 升级到 0.5.26：Asset 远端清单不可达时回退使用当前已激活清单或内置清单，PlugPals 缺失依赖检测跳过 Unity 包与已注册 scope。
- 子包 `com.solotopia.nova.framework.besthttp` 升级到 0.0.3：刷新 BestHTTP Runtime asmdef 配置。
- 子包 `com.solotopia.nova.framework.sdk.ad` 升级到 1.0.9：AdDemo sample 字体资源随包发布同步刷新。
- 子包 `com.solotopia.nova.framework.sdk.iap` 升级到 0.0.9：IAPDemo sample 字体资源随包发布同步刷新。

## [0.5.25] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.25]` 节。

主要内容：
- 主包 framework 升级到 0.5.25：进一步移除 PlugPals 安装按钮同步路径上的远端 package metadata 拉取，避免点击安装/升级时阻塞 Unity 主线程。

## [0.5.24] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.24]` 节。

主要内容：
- 主包 framework 升级到 0.5.24：修复 PlugPalsWindow 点击安装/升级后同步触发 UPM Resolve 导致消费端 Unity 卡顿/无响应的问题，改为下一帧合并解析。
- 子包 `com.solotopia.nova.framework.besthttp` 升级到 0.0.2：同步更新包级授权文件。

## [0.5.23] - 2026-06-15

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.23]` 节。

主要内容：
- 主包 framework 升级到 0.5.23：补发 `upm-release-2026.06.10-01` 后累计变更，覆盖 BestHTTP 可选后端解耦、内部云仓库展示、缺失依赖提示与 Util.Json 依赖迁移等内容。
- 同步补发所有 eligible 且有变更的 UPM 子包；禁发名单内包仍保持不发布。

## [0.5.22] - 2026-06-10

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.22]` 节。

主要内容：
- 主包 framework 升级到 0.5.22：Network / DoH 链路增强，新增预热后自动缓存 IP 直连与更清晰的模块文档说明；ConfigWindow 的 Luban 相关提示文案同步收敛。
- MainDemo 示例场景随主包同步刷新。

## [0.5.21] - 2026-06-09

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.21]` 节。

主要内容：
- 主包 framework 升级到 0.5.21：新增场景级 `DevelopMode` 序列化回写与 Inspector 只读彩色展示；App / Asset 改为使用节点本地开发模式在 Debug / Release 主备地址间路由；版本检查增加主备地址失败回退并在双路都不可用时返回 `NoDownload`。
- 子包 `kit.network.gamelogin@0.0.2`、`kit.network.gamesave@0.0.9`、`sdk.appsflyer@0.0.16`、`sdk.firebase@0.0.14`、`sdk.tga@0.0.14`：同步收敛配置提示文案与包内文档说明。
- 子包 `sdk.ad@1.0.6`、`sdk.admob@0.0.3`、`sdk.iap@0.0.6`、`sdk.iap.mobile@0.0.2`、`sdk.max@0.0.6`：刷新包内文档索引与使用说明。

## [0.5.20] - 2026-06-05

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.20]` 节。

主要内容：
- 主包 framework 升级到 0.5.20：优化启动流程，Splash / 进度面板的销毁时机由启动流程内部移交业务入口统一回收，避免首屏衔接闪帧；同步刷新框架 L0/L1/L2 文档与代码注释。

---

## [0.5.17] - 2026-06-01

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.17]` 节。

主要内容：
- 主包 framework 升级到 0.5.17：新增 Kit 配置体系（`IKitConfig` + ConfigWindow「Kit 配置」面板 + `Nova.Config.GetKitConfig<T>()`），Asset 模块支持启动期按 tag 切片预热、运行期刷新清单与缓存治理接口，Hotfix 阶段新增热更提示弹窗与完成后自动清缓存开关。
- 子包 `kit.network.gamesave` 升级到 0.0.5：存取接口收口为 6 个零 cmd 参数极简入口，指令名由 ConfigWindow「Kit 配置」统一管理（新增 `GameSaveKitConfig`）。
- 子包 `kit.network.login` 升级到 0.0.16：登录入口简化为只传 `openId`，`Async` 签名新增首位 `uid` 参数，指令名与渠道由 ConfigWindow「Kit 配置」统一管理（新增 `LoginKitConfig`）。

---

## [0.5.16] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.16]` 节。

主要内容：
- 主包 framework 升级到 0.5.16：把对 `com.solotopia.yooasset` 的依赖从 `1.0.0` 提升到当前 Verdaccio 最新版 `1.0.3`，框架默认随附最新资源系统封装层。

---

## [0.5.15] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.15]` 节。

主要内容：
- 主包 framework 升级到 0.5.15、登录 Kit 升级到 0.0.15：两包均通过 `.npmignore` 排除 `nova-samples.json` 与 `.meta`，发版描述符仅留开发期源工程使用，不再随 npm tarball 落到外部工程的只读 `Packages/<pkg>/` 区，消除 `immutable folder` 警告与 `SamplePathRewriter` 的重复 RunRewrite。

---

## [0.5.14] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.14]` 节。

主要内容：
- 主包 framework 升级到 0.5.14：发版流水线 `publish_packages.py` 重构为统一流水线——主包 `MainDemo` 与所有子包 sample 走完全对称的 Stage 1 / Stage 3，删除主包专属分支，新增 `Assets/Framework/nova-samples.json` 描述符，所有 sample 等量复制 `Docs/Excels` + `Docs/Protos`、注入 `Nova.prefab` 的 `*SourceDirPath` PrefabInstance override、写入 `SamplePathManifest`；脚本对 `devPathPrefix` 末尾斜杠做防御性 `rstrip("/")` 归一。
- 登录 Kit（kit.network.login@0.0.14）：修正源 `nova-samples.json` 的 `devPathPrefix` 末尾斜杠 bug，外部工程 import LoginDemo 后 `SamplePathRewriter` 不再静默放弃路径重写，sample scene 与 SO 路径正确指向 import 后真实根目录。

---

## [0.5.13] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.13]` 节。

主要内容：
- 主包 framework 升级到 0.5.13：`WorkspaceActive` 增加多 sample 切换感知——当前活跃 scene 在 `Assets/Samples/<sampleRoot>/` 下且与 `Globals.json` 缓存的 ConfigMaster 所在 sample 根不一致时，自动按 scene 重新推断 ConfigMaster 并覆盖 `Globals.json`，根除外部工程同时 import 多个 sample 时打开次级 sample 却读到首个 sample 配置的玄学。
- 登录 Kit（kit.network.login@0.0.13）：发版脚本与主包对称——为子包 sample 复制 `Docs/Excels` + `Docs/Protos` 副本，注入 `Nova.prefab` 的 `*SourceDirPath` PrefabInstance override 到 sample scene；修正 `sampleManifestRelative` 路径错配；`package.json` 备份从包目录移到系统临时目录，消除外部工程 Console 持续刷的 `package.json.publish.bak` immutable folder 报错。

---

## [0.5.12] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.12]` 节。

主要内容：
- 主包 framework 升级到 0.5.12：修复 `WorkspaceActive` 的 sample scene 路径推断，外部工程 import sample 后打开 ConfigWindow 不再提示"未检测到激活的 ConfigMaster"——从 scene 所在目录起逐级向上递归扫 `Editor/ConfigMaster.asset`，同一逻辑兼容开发态扁平结构与 UPM 导入态三段嵌套结构。
- 登录 Kit（kit.network.login@0.0.12）：修正 `nova-samples.json` 的 `displayName` 为 `LoginDemo`（原为带空格的 `Login Demos`），与 `sampleName` / `sourceDir` 末段对齐，确保 Unity Package Manager 落盘的最末层文件夹名与开发态一致。

---

## [0.5.11] - 2026-05-29

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.11]` 节。

主要内容：
- 主包 framework 升级到 0.5.11：ConfigMaster 新增 `YooAssetSettingsPath` / `BundleCollectorSettingPath` 显式路径字段，YooAsset 全局设置与 Bundle 收集器加载改由 ConfigWindow 配置驱动，避开 Editor 启动期 Resources 多副本与全工程 `AssetDatabase.FindAssets` 扫描；ConfigMasterSO 仅 Editor 期消费的字段补齐 `#if UNITY_EDITOR` 包围。
- yooasset 升级到 1.0.3：暴露 `YooAssetConfiguration.SetSettings`（internal）注入点与 `SettingLoader.LoadSettingDataAtPath<T>` 按路径加载重载，配合主包路径注入链路。
- 登录 Kit（kit.network.login@0.0.10）：新增 `nova-samples.json` 元数据，声明 `LoginDemo` 示例工程，预留 `/nova-create-sample` 与发版流水自动化能力。

---

## [0.5.10] - 2026-05-28

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.10]` 节。

主要内容：
- 主包 framework 升级到 0.5.10：清理 `Assets/Framework/Tests/` 临时回归脚本、SDKInspector 插件条目绘制层调整、`Nova.Visitors.Version` 同步、`Assets/Samples/MainDemo/` 资源刷新。
- gamesave Kit 升级到 0.0.4：上传请求新增 `GameVersion / AppVersion / LastDeviceId` 三项元数据自动填充，新增 `SetGameVersion` 实例方法供业务侧注入；拉取响应新增最近一次存档版本 / 设备 / 时间戳元数据。
- sdk.ad 升级到 1.0.3：调整 UPM displayName 为 "Nova Framework - SDK - AD"，与其它 SDK 子包命名风格统一。

---

## [0.5.9] - 2026-05-27

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.9]` 节。

主要内容：
- 声音 / 振动模块新增「按名称播放」入口，业务侧可直接传数据表 Name 字段触发播放。
- Editor 新增 PlugPals 私有 Verdaccio 仓库 UPM 包管理工具，支持远程包列表、安装/卸载与按版本查看更新日志。
- 网络 Kit（kit.network@0.0.10）：响应公共头新增 `app_id` 与 `uid` 字段，便于多产品/多账号场景识别。
- 登录 Kit（kit.network.login@0.0.9）：登录接口新增「按命令名调用」重载，并修复 `openId` 为 null 时底层异常。
- 存档 Kit（kit.network.gamesave@0.0.3）：拆分全量与非全量接口为独立入口，并新增「按命令名调用」重载（破坏性：旧版"keys 为空即全量"隐式回退取消）。

---

## [0.5.8] - 2026-05-27

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.8]` 节。

主要内容：
- UI 视图新增「对象池开关」，可按视图选择关闭后缓存还是直接销毁（破坏性：UIView 子类覆写 OnInit 需补回参数）。
- UIView 默认不再带淡入淡出，淡入淡出能力移交业务自行实现。
- Asset / UI 模块 L2 文档同步刷新，强化 LoadXxx 必须经 Handle 释放铁律。
- Vault 沉淀本期 UI 深度因子、Asset Load API 等决策与通用模式。

---

## [0.5.7] - 2026-05-26

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.7]` 节与各 UPM 子包 CHANGELOG。

主要内容：
- 启动期 UI 改为多语言驱动，弹窗显示接口签名简化（破坏性）。
- 启动期新增独立本地化能力，可在资源系统就绪前安全使用本地化文本。
- 网络 / 登录 / 数据上报三个 UPM 子包跟版升级。

---

## [0.5.6] - 2026-05-22

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.6]` 节与各 UPM 子包 CHANGELOG。

主要内容：
- 网络 / 声音模块对外接口与 DTO 调整。
- MainDemo 演示工程切换为基于 Nova.UI 的树形导航 + TMP 文字渲染。
- 7 个 UPM 子包跟版升级。
- Vault 沉淀本期演示拓扑、UI 命名、Demo 覆盖标准、prefab 制作等多条规范。

---

## [0.5.5] - 2026-05-22

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.5]` 节。

主要内容：
- 修复外部工程导入 sample 后 Inspector 业务字段为空与 SDK `[Missing]` 提示。
- 修复多版本 sample 共存时新旧版本识别不准的问题。

---

## [0.5.4] - 2026-05-22

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.4]` 节。

主要内容：
- 放弃此前的桥接式生命周期方案，回归 HybridCLR 原生 MonoBehaviour + Prefab 直挂。
- 发版流程支持把项目根 Docs 资源（表格 / 协议）随 sample 一起打包，外部工程导入后 Inspector 路径自动对齐。
- HybridCLR 约束规则全面重写为原生方案口径。

---

## [0.5.3] - 2026-05-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.3]` 节。

主要内容：
- 修复 0.5.2 演示工程改名后命名空间 / 配置残留导致的启动报错。

---

## [0.5.2] - 2026-05-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.2]` 节。

主要内容：
- 演示工程更名 Demo → MainDemo，目录 / asmdef / 命名空间 / 子框架脚手架同步对齐。
- qa 测试改为按需就近建测试脚本，不再依赖固定测试入口。

---

## [0.5.1] - 2026-05-21

主要内容：
- 包内结构调整与冗余资源优化。

---

## [0.5.0] - 2026-05-21

汇总：见 [Assets/Framework/CHANGELOG.md](Assets/Framework/CHANGELOG.md) `[0.5.0]` 节。

主要内容：
- 接入 UPM 标准 Samples 机制，演示工程改作 sample 分发；导入后自动检测旧版本残留并询问设置启动场景。
- 各 UPM 包补齐 CHANGELOG / LICENSE / README 三件套，发版脚本强制校验。
- 主框架版本 0.4.2 → 0.5.0。
- 发布工具沉淀为 Claude Code skill。
- 废除 bootstrap.zip 机制。
