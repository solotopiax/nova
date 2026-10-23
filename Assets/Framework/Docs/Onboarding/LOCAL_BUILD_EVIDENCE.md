# 本地构建与外部结果的验收边界

本地构建完成以本次正式产物、报告、冻结输入及领域 Verify 为依据。构建回调附带的 Unity Editor Analytics 或已审 vendor telemetry 仍属于外部副作用，必须保留其范围、凭据、确认、回调检查和失败记录；本地产物成功不代表这些统计已送达。

以下三个既有 Operation 在 `references/contract.json` 中声明 `externalCompletionPolicy: "incidental-build-telemetry"`，避免把附带统计的远端收件误作本地交付的必需证据：

| Operation | 主要交付证据 |
|---|---|
| `nova-project-build-bundles` | 本次 Bundle、正式报告及完整构建回执 |
| `nova-project-build-player` | 本次本地安装包或平台工程、BuildReport 及必要恢复结果 |
| `nova-project-generate-hybridclr-artifacts` | 本次编译与完整生成物、回执、报告及裁剪输入一致性 |

该字段只用于无依赖 Operation 的明确本地构建目标；缺省或未知外部效果仍要求独立外部证据。任务或分支显式要求的 `external` 不会被移除；同一 Workflow 中上传、支付、发布等其他外部节点也不会继承豁免。副作用列表和执行授权不因此改变，不关闭统计设置、不吞回调错误、不补造外部收件。

策略只纠正证据推导，不自动提高任何历史验收状态。产物缺失、报告失败、回执不完整、输入漂移或清场未完成仍需按原契约保留 `partial` 或 `blocked`；三平台启动、设备反馈和服务端结果依其实际任务分别验收。

已有 AAB 的设备安装使用 `nova-project-build-player` 的 `android-aab-device-install` 任务画像。它复用原有 bundletool CLI，分别要求 `apk-set-build` 和 `device`：前者绑定源 AAB、真实工具与设备、成功生成的完整 APK 集、每个 APK 的签名及清场；后者核对安装读回和真实入口。该证据级别只属于这个精确画像，不进入普通 Skill 或 Catalog 的最低证据选项。普通 Player 构建仍要求 `player-build` 和真实 Unity BuildReport；APK 集转换不能被写成 Unity 重构建或 Google Play 分发成功。
