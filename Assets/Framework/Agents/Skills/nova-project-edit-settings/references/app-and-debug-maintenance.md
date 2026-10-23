# App 更新、游戏运行、调试器与日志维护

用户说明希望改变的行为后，复用本 Skill 的对象发现、字段编辑和精确保存分支。场景中的 Nova 组件、项目自有启动 Prefab 与 Framework 包快照分别识别；不修改已安装包、Framework 真源或生成配置。以下是当前源码口径，实际执行前核对目标版本和真实字段。没有 AppUpdate SDK 包可安装，App 大版本更新属于 Framework App 模块。

## App 大版本更新

按需读取 `Docs/Runtime/Modules/App/AppComponent.md`、`Definitions/AppManagerConfig.md` 和当前启动 Procedure。目标类型为 `NovaFramework.Runtime.AppComponent`；字段经已有 Scene/Prefab 编辑入口保存，不新建 App 专属 Action。

| 用户意图 | 当前字段及声明默认值 |
|---|---|
| 开启大版本检查 | `m_EnableAppUpdate=false`；关闭时不影响 Asset 模块热更新 |
| Debug / Release 检查主备地址 | `m_AppDownloadCheckUrlDebug`、`m_AppDownloadCheckUrlFallbackDebug`、`m_AppDownloadCheckUrlRelease`、`m_AppDownloadCheckUrlFallbackRelease`；默认未赋值 |
| 请求超时与主备重试 | `m_TimeoutSeconds=5` 秒、`m_VersionCheckFallbackRoundCount=1`、`m_RetryRequestCount=1`（额外完整重试） |
| 保留最近成功域名优先 / 本链 UWR 埋点 | `m_PreferLastSuccessfulHost=true`、`m_EnableUWRTracks=true` |
| 商店路由与地址 | `m_DownloadRoute=Store`；`m_AndroidStoreUrl`、`m_AppStoreUrl` 默认未赋值 |
| APK 地址 | `m_PrimaryDownloadUrl`、`m_FallbackDownloadUrl` 默认未赋值；内置 `DownloadAsync` 仅 Android Player 按 HTTPS 主备流式下载并返回本地路径 |
| 推荐 / 强制更新规则 | `m_UseRecommendedDownloadRule=false`、`m_UseForcedDownloadRule=false` |

检查 URL 仅替换大小写敏感的 `{Platform}/{Channel}/{Package}/{Version}`，取启动期快照。`m_Channel/m_DefaultPackageName` 由正式 Config 导出同步，不通过通用字段编辑绕过权威源。当前节点的 `DevelopMode` 选择 Debug/Release 地址；`Start()` 仅注入配置，不自动检查。候选数 C、完整轮数 R、额外重试 K 的最多请求数为 `C × R × (K + 1)`，默认有效主备顺序为主、备、主、备。

业务编排复用 `Nova.App.CheckAsync(ct)`，按真实 `AppVersionResult.NoDownload/RecommendedDownload/ForcedDownload` 分支；推荐取消才调用 `RecordRecommendedDownloadDismissed()`。`OpenStoreAsync(ct)` 返回是否已发起跳转，不证明下载或安装；WebGL 当前跳过 App 大版本检查。不为一个参数修改重写启动链；下载仅验证文件结构，严格验签与显式系统安装属于独立 Native 能力。检查请求、商店跳转及自动上报按本任务实际授权和环境执行，字段保存不包含这些运行副作用。

只改字段时验证实际目标文件保存即可；要求完整更新功能时，再核对现有 `ProcedureCheckVersion`、更新弹窗、地址规则、目标平台与允许范围内的实际分支证据。源码或本地配置不能证明 CDN、商店或设备已接受更新。

## 游戏暂停、恢复、速度与退出业务

只读 `Docs/Runtime/Modules/Nova/Nova.md` 和当前 `Scripts/Runtime/Modules/Nova/Nova.cs/Nova.Visitors.cs`。真实类型为 `NovaFramework.Runtime.Nova`，业务运行门面为 `Nova.Self`；它在 Awake 赋值，其它静态 Component 门面在 Start 收集，不能在尚未就绪或已关闭时调用。

| 目标 | 已有字段/公开调用与语义 |
|---|---|
| 初始帧率、速度、后台运行、常亮 | `m_FrameRate=60`、`m_GameSpeed=1f`、`m_RunInBackground=true`、`m_NeverSleep=true`；字段在 Awake 写入 Unity 状态。保存初始配置与改变当前运行值分别验收 |
| 设置当前运行值 | `Nova.Self.FrameRate/GameSpeed/RunInBackground/NeverSleep` 的属性 setter 同步 Unity 状态；只写 SerializedProperty 不保证执行 setter，Play 内存变化不保证已保存初始配置 |
| 暂停 / 恢复 | `PauseGame()` 缓存当前速度后设 0，重复暂停不覆盖；`ResumeGame()` 仅暂停时恢复缓存，重复恢复无操作。不能用直接设 `GameSpeed=0` 冒充缓存暂停 |
| 回到正常速度 | `ResetNormalGameSpeed()` 设 1，不能冒充恢复暂停前倍速；Inspector 的 0～8 区间与 Runtime setter 不同，业务目标值核对为有限非负数，不能把 Inspector 限制当公共 API 上限 |
| 退出 | `QuitApplication()` 发起 `Application.Quit()`，Editor 同时停止 Play；本方法不等待项目存档、云同步、网络或交易完成，不把调用发起当已退出或存档成功 |

自然语言要求“加暂停按钮”“恢复原倍速”“退出前保存”时，直接在已冻结的项目自有业务入口接入这些公开方法，复用已有 UI 持久绑定、Procedure、Event 与 Persistence Operation，不新增 Skill/Action，不修改框架源。初始字段配置仍经本 Skill 对象编辑；正式业务 C# 与运行临时控制不是同一种写入。已存在适用业务入口时复用，没有时按授权范围新增正式实现；不通过任意 C# 或反射作为消费端稳定执行器。

冻结速度/暂停拥有者与退出策略：单个 `m_GameSpeedCache` 不是可嵌套的暂停栈，多窗口必须复用项目现有统一拥有者，关闭一个窗口不能恢复另一窗口仍要求暂停的游戏。`Time.timeScale=0` 不阻断 `Nova.Update()` 的 Manager 调度，不证明网络、SDK、unscaled time 或异步请求停止；不要把暂停当取消交易。业务离开/销毁后停止 UI 回写、释放订阅并恢复本业务持有状态，不能误改其它流程速度。

退出前保存只调用已有 Persistence/业务服务，并按真实存储方式等待其必要验证，再发起退出；`OnApplicationPause/OnApplicationQuit` 当前仅记录日志，不承诺自动保存。云请求、发货或已有数据覆盖继续遵守对应 Operation 的真实副作用门。Play 验证冻结原速度及恢复条件，从实际入口证明两次暂停、倍速恢复与正常速度分支；退出测试会结束 Play/应用，须明确授权范围，不作为普通字段维护的默认测试。源接入、文件保存、Play 行为、Player/设备生命周期各自记录证据；清理本轮探针并恢复临时运行状态。

## RuntimeDebugger

按需读取 `Docs/Runtime/Modules/Debug/DebugComponent.md`、`Definitions/DebuggerActiveType.md` 和 `Windows/DiskCheckingConfig.md`。目标类型为 `NovaFramework.Runtime.DebugComponent`，当前字段声明在 `Scripts/Runtime/Modules/Debug/Windows/DebugComponent.Visitors.cs`。

- `m_DebuggerActiveType=OnlyEnableWhenDevelopment`。精确枚举为 `AlwaysEnable/OnlyEnableWhenDevelopment/OnlyEnableInEditor/AlwaysDisable`；Development 判断使用 `UnityEngine.Debug.isDebugBuild`。
- `m_MaximumConsoleEntries=3000`。Inspector 提供 0 至 20000 范围；说明内存保留数量和淘汰影响，不把它当编译日志级别。
- `m_RuntimeDebuggerFont` 默认未赋值，保持 Prefab 原字体。Player 中文日志应绑定实际包含中文字形的 `UnityEngine.Font`，不能把 Editor 系统字体回退当 Player 验证。
- `m_DiskCheckingConfigs` 默认含 Editor、Android、iOS 配置；读取真实嵌套字段后只修改目标项。`m_CurManagerTypeName` 默认为 `NovaFramework.Runtime.DebugManager`，普通调试开关不另建 Manager。

`DebugComponent` 持有自动初始化入口，不在业务页面重复调用 `RuntimeDebugger.Init`。`NOVA_DISABLE_RUNTIME_DEBUGGER` 宏会裁剪自动初始化；Inspector 绘制把 `AlwaysDisable` 同步为加宏，其它策略同步为移除宏，并修改 Standalone、iOS、Android、WindowsStoreApps、WebGL 五个 `NamedBuildTarget`。通用字段写入不会自动执行这个私有 Inspector 方法。修改策略须同时核对字段和宏，不能只读回字段就称与 Inspector 全效一致。

## 固定日志菜单与共享宏入口

`Docs/Runtime/Core/Log/Log.md` 与 `Docs/Editor/Menus/EnableLogsMenuItems.md` 区分三层：RuntimeDebugger 显示过滤、框架 `Log` 调用的编译宏、各 SDK 自身日志配置。`LogLevel` 为 `Debug/Info/Warning/Error/Fatal`，`LogTag` 是字符串常量；没有已核实的全局 Runtime 日志等级字段。Editor 调用始终保留，Player 按 `ENABLE_LOG`、精确等级及 `ENABLE_*_AND_ABOVE_LOG` 裁剪。

固定切换优先复用当前 `mcpforunity://menu-items` 中存在的菜单，用 `execute_menu_item(menu_path=<exact>)` 执行；先核对源码和菜单存在性，不用每轮临时代码：

| 意图 | 精确菜单后缀（统一前缀 `Nova/Enable Logs/`） |
|---|---|
| 关闭框架日志宏 / 开启全部 | `Disable All Logs` / `Enable All Logs` |
| 保留某级及以上 | `Enable Debug And Above Logs`、`Enable Info And Above Logs`、`Enable Warning And Above Logs`、`Enable Error And Above Logs`、`Enable Fatal And Above Logs` |

这些菜单清除既有框架日志宏后设置目标宏，修改上方五个平台，保留无关宏；不会阻止直接 `UnityEngine.Debug` 或厂商自有日志。用户只授权一个平台时，不调用五平台菜单。

平台脚本宏增删、替换或清空优先使用 `nova.project.settings.patch-build-defines`，完整最终数组支持 `[]`，精确输入、草稿门和本次编译/重载验收见 [平台宏编辑](build-defines.md)。当前共享 `manage_build(action="settings", property="defines", target=<explicit>)` 可只读宏，带非空 `value` 可设置对应 `NamedBuildTarget` 完整分号列表；它没有 Nova Plan/Receipt/漂移锁，不能把空 value 当可靠清空。日志菜单仍是固定业务入口，其跨五目标写入范围必须独立授权；单平台宏 Action 不会自动调用菜单或改其他平台。

调试器策略需要与 Inspector 全效一致时冻结五组宏；仅一组写入只报告该组。最后一个宏移除会得到空字符串，但当前工具把空 `value` 判为读取，不能用占位宏、手写 YAML 或任意执行代码规避，报告该具体 `partial`。字段修改与宏修改跨多个保存目标，逐层记录已保存内容；失效或漂移后先读回，不自动重放。

宏后验、编译成功与 Player 日志行为分别记录；宏存在不证明特定 Player 已应用，Editor 日志输出不证明 Player 保留。日志上传回调、外部接收、设备采集不因本地维护默认执行。清理本轮探针、日志和测试对象，保留已授权正式配置。


## 显式 Android 自身 APK 安装总门

对象为启动场景已有 `NovaFramework.Runtime.NativeComponent`，持久字段为 `m_NativeManagerConfig.EnableAndroidAppInstall`，默认 false。复用已有对象 inspect/patch/verify 或共享 Unity 场景/Prefab 编辑入口，精确保存项目自有资产；不自动开启原配置、不改包快照、不增加原生操作的 MCP Action。开启后重新构建，NativeBuildProcessor从实际启用Build Scene读取同字段并声明REQUEST_INSTALL_PACKAGES；运行时API28+及来源许可与系统用户确认仍独立。

业务文件链为 `Nova.App.DownloadAsync(ct)` → 返回受控本地路径 → `Nova.Native.InstallApkAsync(path,ct)`，安装能力通过可选INativeAppInstaller，默认关闭、旧自定义Manager不实现时明确Unsupported。下载仅文件结构验证，安装对原生暂存同件严格验签、同包名/当前signer集合与递增versionCode，不绑定远端Check具体版本。来源许可不足时由用户显式选择打开来源设置，再次确认安装；pending user action不是安装完成。安装失败/系统取消后内置Procedure复用已保存APK，流程退出取消未完成session，已成功安装不可回滚。设备安装与最终Manifest仍需目标平台验证，不能将字段保存或Editor Unsupported作为真机通过。
