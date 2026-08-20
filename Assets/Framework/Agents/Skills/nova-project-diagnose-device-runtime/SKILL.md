---
name: nova-project-diagnose-device-runtime
description: Use when Nova 消费项目需要在已冻结 Android 或 iOS 设备、Bundle ID、时间窗与脱敏规则下，只读收集并诊断真机运行日志时使用。
---

# Nova 真机运行时诊断

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读取 `references/contract.json`。仅在需要确认实际 Framework 与随包 Docs 时，使用已解析 Framework 包内的 `Agents/Tools/nova_skills.py resolve --project-root <projectRoot>`。仅当日志、Bundle ID 或症状明确指向某个已安装 Nova SDK / Kit 时，才从消费项目的 `Packages/<package>/Nova/{Doc,Docs,DOCS}/INDEX.md`，或 `Library/PackageCache/<package>@<version>/Nova/{Doc,Docs,DOCS}/INDEX.md` 查找首个存在的包内 INDEX；先读 INDEX，再按它的链接读取当前错误相关页面。不要使用主仓 `.nova/`、`Minds/`、绝对路径、未命中的厂商源码或其他包的文档替代目标包事实。

## 冻结诊断会话

在连接或读取设备前，冻结唯一项目根、平台（`Android` 或 `iOS`）、单个 Android serial 或 iOS UDID、Bundle ID、带时区的起止时间窗、症状与复现入口、允许的日志源，以及脱敏规则。日志源只能是当前设备的受限 `adb` / `xcrun` 只读输出、用户提供的时间受限 Xcode / Console 导出、当前 Unity Editor.log 或当前 Player.log；没有精确设备、Bundle ID、时间窗或脱敏规则时返回 `blocked`。

脱敏规则至少覆盖 Access Token、Cookie、密码、会话值、API Key、AES Key/IV、私钥、广告或设备标识、用户 ID、手机号、邮箱与其他个人数据。最终报告只保留时间、平台、版本、进程 / tag、异常类型、已脱敏的相关字段和日志位置；serial、UDID、原始日志值与凭据不得回显。无法在输出前可靠脱敏时停止，不保存或转发原始日志。

## 严格只读采集边界

- Android 仅可使用 `adb devices -l` 核对已冻结 serial，以及 `adb -s <deviceSerial> shell getprop`、`dumpsys package <bundleIdentifier>`、`pidof <bundleIdentifier>` 和受时间窗约束的 `logcat -d -v threadtime -T <windowStart>` 等只读查询。若当前 host 的 `adb` 不支持时间边界，改用用户提供的时间受限导出，不能抓取或保存无限制全量日志。
- iOS 仅可使用当前 Xcode 可用的 `xcrun devicectl list devices`、`xcrun devicectl device info apps --device <udid>`、`xcrun devicectl device info processes --device <udid>`，以及可可靠限制时间窗的 `xcrun devicectl device log stream --device <udid>` 或用户提供的时间受限 Xcode / Console 导出。工具不支持受限 stream 时不启动 stream。
- Unity 侧只读取现有 Editor.log、Player.log、Console 或用户明确提供的设备日志；不启动或关闭 Unity、不触发编译、不进入 Play、不修改工程，也不新建日志文件、导出文件或诊断资产。
- 绝不执行或建议 `adb install`、`adb uninstall`、`adb shell pm clear`、`adb logcat -c`、`adb shell am start`、`adb shell monkey`、`adb forward`、`adb reverse`、`xcrun devicectl device process launch`、`simctl launch`，也不启动未知应用、改设备设置、访问应用沙盒、安装证书或清理数据。

`pidof` 无结果只表示未观察到已运行进程，不得为复现而启动应用；Bundle ID、进程名或日志 tag 映射不确定时停止猜测并报告 `partial` 或 `blocked`。本 Operation 只诊断，不修复；可能的源码、配置、SDK 或平台修改必须另行路由至对应写入 Skill 或获得新的明确任务。

## 证据分级与结果

按“冻结上下文 → 已脱敏的设备与包事实 → 时间窗内最早直接日志 → 包内文档相关语义 → 根因假设与未验证项”交付。优先定位第一个能解释症状的客户端异常、平台错误码、初始化失败或进程状态，不把后续级联错误当根因；如日志只支持客户端现象，应明确服务端、厂商后台、权限状态和真实用户行为仍未验证。

有足够的时间受限、脱敏直接证据并完成诊断报告时可返回 `success`，这只表示只读诊断交付完成，不表示问题已修复、厂商后台已成功或真实设备功能已成功。设备不可访问、日志不在时间窗内、脱敏失败或关键身份不唯一时为 `blocked`；已收集部分安全证据但无法定位根因时为 `partial`；非 Android / iOS 真机运行问题、要求写入、安装、清理或启动应用时为 `not_applicable`。
