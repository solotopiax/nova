# Nova Framework - SDK - Firebase

> 包名：`com.solotopia.nova.framework.sdk.firebase`
> 当前版本：`0.1.13`
> Firebase Unity SDK：`13.14.0`

Firebase 聚合插件，统一接入分析、崩溃、FCM 推送、远程配置，并提供 Nova 侧默认 Topic 同步与业务 push task 缓存发送能力。

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.firebase": "0.1.13"
}
```

## 能力概览

- Analytics：`TrackEvent(...)`、`SetUserId(...)`、`SetUserProperty(...)`。
- FCM：`GetTokenAsync(...)`、`OnTokenRefreshed`、`SetTopicSubscribed(...)`。
- 默认 Topic：初始化后等待 FCM Token 就绪，再按 `IConfigManager.DevelopMode` 同步 `top_debug_*` 或 `top_release_*` 的全量、语言、平台、时区和国家 Topic；Config Manager 不存在或未加载完成时只使用 Debug 分群，并通过 `IFileFragmentManager` 记录订阅状态，变化时先退订旧 Topic 再订阅新 Topic。
- Push task：业务通过 `IFirebasePushTaskPlugin.QueuePushTaskAsync(...)` 写入本地缓存，按配置的时间阈值、数量阈值或应用恢复前台触发批量发送；发送成功后才删除缓存。
- 登录上报：收到 `SDKEventData.UserLogin` 后，上报 Firebase Push Token、Analytics Instance ID、国家码和时区偏移。

## 运行时配置

`FirebasePluginConfig` 主要配置业务服务器协议名和 push task 发送策略：

- `ReportCmdName`：登录后上报 Firebase 标识使用的 NetCmd 名称。
- `PushCmdName`：批量创建或取消服务端 push task 使用的 NetCmd 名称；为空时保留本地缓存并等待下次发送。
- `PushFlushIntervalSeconds`：push task 本地缓存后的时间阈值，默认 `100` 秒。
- `PushFlushBatchSize`：push task 缓存数量阈值，默认 `5` 条。
- `AutoRequestNotificationPermission`：是否在 Firebase 依赖初始化成功后自动请求通知权限，默认开启；如项目希望由业务自行选择交互时机，可关闭后显式调用 `Nova.Native.RequestNotificationPermissionAsync(...)`。

国家码不在 Firebase 配置中单独设置；默认国家 Topic 和登录上报会通过 `IAdPlugin.GetCountryCodeAsync(...)` 获取，等待超时和上次成功缓存兜底由 AD 模块负责。

## Sample 依赖

`FirebaseDemo` 为演示登录联动会使用 `com.solotopia.nova.framework.kit.network.gamelogin`。当前包级依赖中保留 GameLogin，是为了保证导入示例后可直接编译；Firebase 运行时代码本身不直接引用 GameLogin 类型。

## Push Task 约束

`FirebasePushTask.TaskKey` 是本地缓存主键，相同 `TaskKey` 的新任务会覆盖旧任务。协议发送必须等待 Firebase 初始化完成且 `SetUserId(...)` 已成功同步用户身份，避免缺少用户相关协议参数。

`FirebasePushTask.Cancel == true` 表示取消同 `TaskKey` 下未派发的服务端任务。此时协议层只发送 `task_key` 和 `cancel`，不会携带 `trigger_time` 或 `template_id`。

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

详细运行时说明见 [Nova/Doc/INDEX.md](./Nova/Doc/INDEX.md)。

## 当前开源状态

- 当前结论：包根第三方声明已补齐，可按“保留各 Firebase 子包许可证 + 包根说明文件”的方式进入公开仓。
- 项目私有配置文件与私有集成产物仍不属于公开仓保留范围。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- `Core/` 内随包分发的 `LICENSE`、`NOTICE`、`README` 等文件，应与对应内容一起保留。

## Firebase 桌面库说明 / Firebase Desktop Libraries

Firebase 桌面（Editor）核心原生库 `FirebaseCppApp-*`（macOS `.bundle` / Linux `.so` / Windows `.dll`，位于 `Firebase/Plugins/x86_64/`）现由 **Git LFS** 承载、随开源仓分发。正常 `git clone` 会自动 smudge 还原为真实内容，无需额外操作。

- **真机构建（Android / iOS）不依赖这些桌面库，不受任何影响。**
- Firebase 官方将桌面支持定位为「仅开发期 beta、不用于发布」，仅在 **Editor 播放模式**调试 Firebase 时需要。
- 兜底：若未安装 Git LFS 客户端导致 clone 只拿到指针文件，`FirebaseDesktopLibraryGuard` 会在 Console 与弹窗提示补齐——执行 `git lfs pull`，或从 [Firebase 官方 Unity SDK](https://firebase.google.com/download/unity) 下载解压后通过 `Assets > Import Package > Custom Package` 导入 / 手动拷回同名目录。

---

The Firebase desktop (Editor) core native libraries `FirebaseCppApp-*` (`.bundle` / `.so` / `.dll` under `Firebase/Plugins/x86_64/`) are now managed by **Git LFS** and shipped with the open-source repo. A normal `git clone` auto-smudges them into real content — no extra steps required. Device builds (Android / iOS) do not depend on them. Fallback: if you cloned without Git LFS installed and only got pointer files, `FirebaseDesktopLibraryGuard` will prompt — run `git lfs pull`, or import from the [official Firebase Unity SDK](https://firebase.google.com/download/unity).
