---
name: nova-project-upload-google-play
description: Use when Nova 项目组需要配置 Pipify 的 Google Play AAB 上传、上传已有或同批次构建的 AAB，或只读排查 fastlane supply 的权限、版本号及提交失败时使用。
---

# Nova 上传 Google Play AAB

触发后先读取当前 Framework 的 `Docs/START_HERE.md`，作为所有 `nova-project-*` Skill 的共同底线。

## 渐进式披露

先读 `references/contract.json`，确定本次是配置、上传还是只读排障。仅在核对参数或一次性准备时读取当前包的 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.GooglePlay.md`；执行 Batch 时再读 Pipify 与 AgentActions 对应文档；失败时读 [排障参考](references/troubleshooting.md)。消费项目只依赖自身事实和已安装包，不依赖 Nova 主仓的账号、凭据默认值或目录。

## 范围与输入

- **配置**：为指定活动 `PipifySettingsSO` 的指定 Batch 添加或调整 `googleplay.upload_aab`，保存并核对参数；不因此运行 Batch。
- **上传**：执行用户已授权的已保存 Batch。单独上传已有 AAB 时使用仅含上传步骤的 Batch；构建后上传仅在用户授权整条流水线时运行完整 Batch。
- **排障**：读取已有日志、当前配置和必要的文件元信息；不修改后台权限、不重建或重试。

按模式收集必要输入：配置只需目标活动 Settings/Batch 和已知参数，允许保存待配置状态；没有包名、AAB 或凭据时将缺项留空、列出后续运行前置，不套用主仓凭据默认路径，也不触发上传。排障只需原始失败证据和可取得的配置，不以无关必填输入阻塞诊断。

上传须冻结项目根、活动 Settings GUID/路径、精确 Batch 名称和有序步骤、目标应用包名、轨道、用途、验证模式、手动审核选项、凭据相对路径、fastlane 入口、超时及适用的更新说明/比例。已有 AAB 上传还要核对实际包名、versionCode、签名来源、路径与 SHA-256；无法从已有构建证据或只读产物工具确认时说明缺口，不从当前 PlayerSettings 猜已有包的身份。构建后上传则在执行前冻结构建参数、预期包名/versionCode、签名和输出，执行后核对新 BuildReport 与 AAB 身份/哈希；新包不存在时不假称已取得其哈希，Batch 内的产物选择由现有 Step 校验。

## 配置与一次性准备

1. 配置先核对当前 Framework 注册了 `googleplay.upload_aab`；上传前再核对构建机已有可启动的 fastlane。安装工具不属于上传的隐含步骤。
2. 上传前核对 Google Play Console 的应用与开发者账号、首包和必要声明已准备；Google Cloud 同一项目已启用 Google Play Developer API 并创建服务账号密钥。尚无后台访问时根据用户提供的证据说明已确认项与缺口。
3. 提供管理员操作指引：在 **Play Console → 用户和权限** 添加凭据的 `client_email`，授权实际目标应用。内部测试核对“查看应用信息（只读）”和“将应用发布到测试轨道”；正式轨道再核对正式版发布权限。Cloud IAM 角色不替代 Play Console 应用权限。此 Skill 不代执行账号、密钥或后台权限变更。
4. 通过 **Nova/Open Pipify** 的现有参数界面配置、保存资产；Unity 序列化资产只经当前可用的 Editor 自动化或人工操作，不手写 YAML，也不创建任意 C# runner 绕过执行层。

凭据只填写相对 Unity 项目根的 JSON 路径，例如 `Docs/evidences/googleplay_upload/credentials.json`，Step 执行时转为绝对路径，允许项目内外文件。只检查必要的 `type`、`project_id`、`client_email`；不要输出整个 JSON、私钥或 token，也不把凭据复制到日志、Skill、公开产物或客户端中。

已有 AAB 的 `AabPath`、`PackageName` 必须成对填写；AAB 可用绝对路径或项目根相对路径。两者同时留空时，只允许使用同一 Batch 最新明确成功的非 Development Android AAB 和构建时包名。独立上传 Batch 不能引用另一条 Batch 的上下文。`FastlanePath` 可留空自动探测，也支持绝对路径、项目根相对路径或 PATH 命令名。

新配置未指定用途时，沿用 `internal + Draft + ValidateOnly=false + ChangesNotSentForReview=false + en-US + 1800 秒`；凭据必须使用当前项目自己的路径。保留既有配置；改用途、轨道或验证模式须符合用户选择。

## 上传语义与确认

“保存草稿”不开放下载；“向该轨道全部用户开放下载”仍受审核和后台设置约束，内部测试只面向符合条件的测试人员；部分用户开放仅支持 `production + InProgress + 0 < Rollout < 1`。

`ValidateOnly=true` 仍向 Google 真实上传并验证 AAB，最后不 commit，不保存到轨道；versionCode 可能被占用。用途与适用比例仍参与校验；手动审核选项在此模式禁用且不传参。本工具在 `Draft` 时也禁用手动审核并显示未勾选，执行时按 false 处理，即使原保存值为 true；原值仅在非验证的开放下载用途下生效。这是工具使用限制，不是 Google 对所有草稿的固定互斥规则。有效的 `ChangesNotSentForReview=true` 是请求稍后手动提交审核，不保证所有情形都不会触发审核或影响其他在审变更。

配置、诊断和本地检查可先完成。真实上传（包括仅验证）前展示目标应用、已有 AAB 身份或待构建参数/versionCode、轨道、用途、审核行为、凭据账号及完整 Batch 副作用；用户既有明确授权已绑定这些值时沿用，不重复询问。缺少授权时只对这份具体结果请求确认。仅调用 Skill、要求制作 Skill 或提供失败日志不等于授权上传。输入或已有文件内容漂移后重新核对授权。

## 执行与验收

交互 Agent 复用 **`nova.project.pipify.run-batch`**。先从当前 Unity MCP 确认项目根与 Editor 就绪，再 `describe` 核对该 Action 的实际 Schema；`plan` 使用活动 Settings GUID 与精确 Batch 名称。核对计划里的 Settings SHA-256、BuildTarget 和全部 Step/参数，不把非目标 CDN、Git 或其他外部步骤混入上传授权。

已授权且快照一致时执行一次 `execute`，使用返回的 `recovery_token` 轮询 `verify`。Action 的成功只证明 Runner 完整结束；还需核对上传步骤的 fastlane 成功结果及本次模式。不要把参数 Summary、普通日志或旧 AAB 存在当作 Google 接受证据。Action 不冻结 AAB/凭据文件内容，调用前须复核文件与账号未变化，并避免同一应用同时进行另一份上传或后台编辑；跨构建机互斥需由项目 CI 保证。

已有 CI 可按当前 Pipify CLI 文档调用已配置 Batch，省略 `-quit` 并等待 CLI 自行返回退出码。MCP 缺失或目标 Action 未开放时，不用任意 C#、直接 shell supply 或另起 Unity 绕过；报告缺口并给出现有界面/CI 的人工执行方式。

任何失败、超时、断线、取消或重载都不自动重新 `execute`。MCP 断线不证明已登记 Job 停止；连接恢复后只用既有 `recovery_token` 继续 `verify`。任务仍运行时不清理其临时目录；失败或重载后先核对任务与 Play Console 的实际版本/轨道状态，再决定重试。仅验证也不是可安全重用版本号的本地 dry-run。任务结束后保留正式 AAB，核对本轮上传临时目录和诊断临时文件已清理，不动用户已有构建、配置和证据。

## 结果

- `success`：配置已保存并核对、诊断结论有直接证据，或上传模式获得匹配的 fastlane 成功结果且清场完成；必须说明是哪一种。仅验证成功、草稿保存成功、审核完成和可下载分别报告。
- `partial`：已执行或已产生副作用，但上传证据、远端状态或清场未确认；列出实际已完成项和最小下一步。
- `blocked`：执行前缺少必填配置、工具、权限前提、授权或安全入口；给出具体缺口。
- `not_applicable`：核心任务是本地 Player/Bundle 构建、UPM 发布、其他应用商店或厂商后台权限代配置，路由到对应能力。

若用户目标是审核通过或用户可下载，只有已核实对应后台状态/实际下载证据才能对此返回成功；单纯上传成功仍不足。默认不执行 Git 提交推送、UPM 发布、账号授权或密钥创建。
