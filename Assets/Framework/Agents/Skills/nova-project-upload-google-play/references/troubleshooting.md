# Google Play 上传排障

从已有错误、fastlane 参数、失败时间和本次日志定位。参数 Summary 只表示选项，不表示成功上传。不要通过重跑整条构建链获得诊断证据。

## The caller does not have permission

Google 已拒绝当前调用者的访问权限。凭据文件存在和 Cloud 服务账号已创建不能证明拥有目标 Play 应用权限；尚未访问后台时，不能断言缺少哪一项授权。

1. 只读取当前 JSON 的 `type`、`project_id`、`client_email`，核对 `type=service_account` 与实际账号；不打印 JSON 正文。
2. 由开发者账号所有者或管理员进入 **Play Console → 用户和权限**，找到或邀请上述 `client_email`。
3. 在 **应用权限 → 添加应用** 中选择与本次 `package_name` 一致的应用；核对权限是否过期，以及应用是否转移到其他开发者账号。
4. 内部测试核对 **查看应用信息（只读）**与 **将应用发布到测试轨道**。后者包含上传草稿、创建/修改版本及测试轨道发布。正式轨道另核对 **发布为正式版，排除设备以及使用 Google Play 应用签名功能**。
5. 核对应用已在该开发者账号下创建并完成首次 Console 上传；同时核对凭据所属 Cloud 项目已启用 Google Play Developer API。

Cloud IAM 的 Owner/Editor 不替代 Play Console 的应用授权。修改 `Draft`、手动审核或 `ValidateOnly` 不会消除权限错误；上传也无需财务或订单权限。

若日志只有 Summary 与权限错误，没有上传进度：可按实际安装的 fastlane 源码核对首个 API 请求。已核对的 supply 2.240.1 在 Summary 后先 `begin_edit → insert_edit`，然后才上传 AAB；可以提示“可能在建立 edit 时被拒绝”，不能仅凭截断日志断言 versionCode 未使用或后台绝无变化。

授权、包名等缺口修复后，用户可选择仅含上传步骤的 Batch，成对填写已有 AAB 路径和包名再执行；不自动修改配置、重建 AAB 或代用户重试。

## 进度条停留、Unity 仍可操作

异步上传时 Editor 可继续更新，不能仅据此判定失败。核对本次 Batch 的开始、上传、结束记录和编译/程序集重载时间。窗口已有成功构建产物时会先完成一次上传前重载，再核对配置与文件指纹继续；“等待编译与重载（上传尚未开始）”阶段未启动 fastlane。重载后参数或产物变化会停止，不重建、不上传。

已经进入上传后再重载，进程被终止、进度清理，并记录远端状态可能未知，不会自动重试。后台结果仍须核对，不能把进度消失或进程退出解释为版本未占用。旧版遗留进度仅在确认任务和进程已结束后清除，不得通过运行整条 Batch 清除。

## Google 要求省略手动审核参数

`Changes are sent for review automatically. The query parameter changesNotSentForReview must not be set` 表示 Google 拒绝 commit 中的手动审核参数。日志已出现 AAB 上传、Updating track 和 Uploading all changes 时，不能解释为未开始上传，也不能保证原 versionCode 未占用。

先核对 Play Console 的实际版本记录。当前工具在草稿用途下禁用“稍后在后台手动提交审核”，显示未勾选，执行按 false 处理；这是工具的使用限制，不是 Google 对所有草稿的固定规则。开放下载用途下若用户允许按 Google 后台规则处理审核，可取消该选项并保存；保持用户的用途选择，不代改配置或重试。草稿不会开放下载。fastlane 2.240.1 的 false 仍会传查询参数；有效值为 false 时，Pipify 允许 supply 按 Google 错误建议兼容同一 edit 的提交，不重新上传 AAB。非验证且开放下载用途下有效勾选时仍禁止自动兜底为自动审核。

## 常见其他错误

| 直接错误或证据 | 最小检查与处理 |
|---|---|
| 本地凭据路径不存在/为绝对路径 | 相对 Unity 项目根解析 JSON 路径；核对真实文件，不要求用户发送内容 |
| fastlane 找不到或无法启动 | 核对已有可执行文件、PATH/显式路径和构建机；需要安装时单独说明，不静默安装 |
| 应用/包名不存在或不匹配 | 核对 AAB 内包名、目标开发者账号、后台应用和首包状态；不能只改上传参数冒充另一应用 |
| versionCode 已使用 | 查该应用已有版本与上传记录；不删除后台版本，不自动递增或重新签包；按用户决定使用后台已有包或构建新版本 |
| 签名不一致、Development 或政策/兼容性拒绝 | 以 Google 直接错误核对实际 AAB、上传密钥、构建模式及后台声明；交给对应构建/后台任务，不改无关发布选项 |
| 审核参数被拒绝 | 核对后台在审变更及 API 行为；不自动取消手动审核选项或将草稿改为开放下载 |
| 超时、网络中断、commit 附近失败 | 远端状态可能未知；先查版本、轨道与变更实际结果，停止自动重放 |

`ValidateOnly` 仍真实上传、校验 versionCode，不能保证验证后相同版本号再次上传成功。Pipify 的“远端提交状态可能未知”是非零退出通用文案，不证明 commit 已发生。分别报告已确认事实、推断、后台尚未核实项，以及临时文件清理结果。

## 官方来源

- [Google Play Developer API 接入与服务账号邀请](https://developers.google.com/android-publisher/getting_started?hl=zh-cn)
- [Play Console 用户和权限定义](https://support.google.com/googleplay/android-developer/answer/9844686?hl=zh-Hans)
- [测试轨道发布权限](https://support.google.com/googleplay/android-developer/answer/9844686?hl=zh-Hans#release_testing)
- [Google edit 工作流与首次 Console 上传](https://developers.google.com/android-publisher/edits)
- [fastlane supply 参数](https://docs.fastlane.tools/actions/upload_to_play_store/)
