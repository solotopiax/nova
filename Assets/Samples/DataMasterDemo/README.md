# Nova Framework - SDK - DataMaster — DataMasterDemo

DataMaster（远程配置 / ABTest / 事件上报）SDK 插件的演示示例：如何取用 `DataMasterPlugin` 并调用其读参 / 曝光 / 上报 API。

## 启动方式

导入后 `SampleVersionGuard` 会提示清理旧版本；清理前核对自己的改动。本 Sample 不自动接管启动场景，运行时打开实际导入目录中的 [DataMasterDemo.unity](DataMasterDemo.unity)。先核对实际安装的插件、ConfigMaster 中的启用配置、目标 Runtime 和编译结果；清理提示不代表初始化或服务端拉取已成功。

## 通过 Codex 接入

可以描述“帮我解释远程配置”“把这个开关接到开始按钮”“检查为什么没有拉到实验参数”。Codex 从已安装 Framework 的 [Docs/START_HERE.md](README.md#跨包文档定位)（`com.solotopia.nova.framework` 包内 `Docs/START_HERE.md`）、[Agents/INDEX.md](README.md#跨包文档定位)（`com.solotopia.nova.framework` 包内 `Agents/INDEX.md`） 复用 SDK/Kit 接入、配置、UI 和启动诊断能力；插件细节从已安装 DataMaster 包内文档和源码读取，不要求消费项目拥有主仓 Minds。

示例按已落库主题查找 `show_start_button`，读 Boolean 参数以 `false` 兜底，并分别演示曝光、事件、属性和清理运行缓存。主题名从实际内容读取，不当作固定业务 ID。模拟登录按钮使用真实 GameLogin 且 `forceNewAccount=true`，可能创建新测试账号并触发远程拉取；缓存清理和上报也有实际副作用，查阅示例不自动授权执行。默认值、本地缓存、刷新触发、网络响应、实验分桶和后台接收须分别核验。

页面在打开/关闭时订阅和退订刷新事件，但登录等待没有绑定页面关闭令牌，关页不会撤销已经发出的登录或远程拉取；SDK 已由 GameLogin 通知，不重复广播。导入路径、版本清理与基类页面的共同边界见 Framework [Docs/Onboarding/PROJECT_STRUCTURE.md](README.md#跨包文档定位)（`com.solotopia.nova.framework` 包内 `Docs/Onboarding/PROJECT_STRUCTURE.md`） 的 Sample 小节。

读参选取第一个包含该参数的本地主题；返回 `false` 无法区分真实分组值与兜底值。按钮只显示读取结果，不实际控制游戏开始按钮；曝光按钮用于演示显式调用，不能替代玩家真正看到实验内容的曝光时机。调试输出可能包含设备标识和整份配置，不应用于生产玩家日志。

## 升级注意

UPM 标准行为下旧版本 sample 不会自动删除。SampleVersionGuard 会在 Editor 启动时检测并提示清理。

## 启动配置等待

`ProcedurePreload` 在网络表加载成功后，等待本次启动的同一轮 GM 应用配置拉取，再继续 SDK 等后续步骤；拉取失败、超时或未配置时继续使用当前配置。仍保留原有八步顺序，预加载重试和重复等待不会重新发起启动拉取。Network Inspector 的“启动时GM配置请求超时时间（秒）”默认每次请求 5 秒，主备请求和重试分别计时，整轮总耗时可能超过 5 秒。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework` | `Docs/INDEX.md` | `Agents/INDEX.md`<br>`Docs/Onboarding/PROJECT_STRUCTURE.md`<br>`Docs/START_HERE.md` |
