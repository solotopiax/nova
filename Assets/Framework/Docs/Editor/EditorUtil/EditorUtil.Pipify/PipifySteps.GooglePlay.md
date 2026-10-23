# Google Play AAB 自动上传

Pipify Step `googleplay.upload_aab`（发布 → Google Play 上传 AAB）使用构建机上的 fastlane supply 上传 Android App Bundle。默认 `internal` 轨道、`Draft` 草稿、英语（美国）更新说明；仅验证和手动审核默认关闭，草稿时手动审核灰显。普通 Unity Build 不会自行上传。

首次配置先查看 [Google Play 自动上传：后台配置说明](PipifySteps.GooglePlay.Setup.md)，按应用首包、API、凭据和应用权限完成一次性准备；只需查找凭据来源时，可查看 [凭据 JSON 从哪里获取](#凭据-json-从哪里获取)。

项目组可以直接描述“配置 Google Play 上传”“上传已有 AAB”或“排查 supply 权限错误”，由 [nova-project-upload-google-play](../../../../Agents/Skills/nova-project-upload-google-play/SKILL.md) 处理。该 Skill 随 Framework 分发，区分配置、真实上传和只读排障，执行复用 `nova.project.pipify.run-batch`，失败后不自动重试。

## 一次性准备

1. 在 [Google Play Console](https://play.google.com/console/) 创建应用、上传首包并完成应用资料及必要声明。API 用于后续更新，不能替代首次创建与法律声明。
2. 按 [凭据 JSON 获取步骤](#凭据-json-从哪里获取) 启用 Google Play Developer API，创建服务账号并下载 JSON，再在 Play Console 授予对应应用所需的上传/发布权限。
3. 构建机安装 fastlane（macOS 可使用 `brew install fastlane`），或填写 `FastlanePath`。解析顺序为显式路径、PATH、常见 macOS/Linux 安装路径；Windows 只接受可直接启动的 `.exe`，不经 cmd 执行 `.bat/.cmd`，推荐使用 macOS/Linux 上传机。
4. 在 Step 的“凭据 JSON 路径（相对 Unity 项目根目录）”中填写项目根相对路径，例如 `Docs/evidences/googleplay_upload/credentials.json` 或 `../GooglePlayKeys/credentials.json`。项目内外的文件均可，Step 转成绝对路径后传给 fastlane。配置资产仅保存路径，不读取、复制或保存 JSON 正文。

### 凭据 JSON 从哪里获取

凭据 JSON 是 **Google Cloud 服务账号的密钥文件**，在 [Google Cloud Console](https://console.cloud.google.com/) 创建并下载。Google Cloud Console 与 Google Play Console 是两个独立网站，可以直接使用下列链接进入；无需在 Play Console 中寻找 Google Cloud 的菜单入口。

1. **选择或创建 Cloud 项目**：打开 [Google Cloud Console](https://console.cloud.google.com/)，在顶部选择项目；没有可用项目时，打开 [创建项目页面](https://console.cloud.google.com/projectcreate)。后续启用 API 和创建服务账号都在同一个 Cloud 项目中完成。
2. **启用 API**：打开 [Google Play Developer API 页面](https://console.cloud.google.com/apis/library/androidpublisher.googleapis.com)，确认顶部项目正确，点击“启用”；已启用时无需重复操作。
3. **创建服务账号**：打开 [服务账号页面](https://console.cloud.google.com/iam-admin/serviceaccounts)，对应菜单为 **☰ → IAM 和管理 → 服务账号**。点击“创建服务账号”并完成创建，记下该账号的邮箱，例如 `play-uploader@YOUR_PROJECT_ID.iam.gserviceaccount.com`。
4. **下载凭据 JSON**：在服务账号列表中点击刚创建的账号，进入 **密钥（Keys）→ 添加密钥（Add key）→ 创建新密钥（Create new key）**，选择 **JSON**，点击“创建（Create）”。浏览器会下载 `.json` 文件，这就是 Step 所需的凭据文件；已有密钥不能再次下载，文件丢失时需创建新密钥。
5. **授予应用权限**：打开 [Google Play Console](https://play.google.com/console/)，选择对应开发者账号，进入 **用户和权限（Users and permissions）→ 邀请新用户（Invite new users）**，填写第 3 步的服务账号邮箱，在 **应用权限 → 添加应用** 中选择目标应用。内部测试核对“查看应用信息（只读）”和“将应用发布到测试轨道”；正式轨道另核对“发布为正式版，排除设备以及使用 Google Play 应用签名功能”，保存权限并完成邀请。这里填写的是服务账号邮箱，不是开发人员的 Google 登录邮箱；Google Cloud IAM 角色不能替代 Play Console 应用授权。具体范围见 [Google 官方权限定义](https://support.google.com/googleplay/android-developer/answer/9844686?hl=zh-Hans#release_testing)。

上述 Cloud 直达链接需要登录，并以当前选中的 Cloud 项目为准；Play Console 的“用户和权限”页面地址包含各自开发者账号 ID，因此使用后台入口加菜单路径定位。

下载后，在上传 Step 的“凭据 JSON 路径（相对 Unity 项目根目录）”中填写该文件的项目根相对路径，例如 `Docs/evidences/googleplay_upload/credentials.json`。

官方操作说明：[创建和删除服务账号密钥](https://docs.cloud.google.com/iam/docs/keys-create-delete?hl=zh-cn)、[Google Play Developer API 接入](https://developers.google.com/android-publisher/getting_started?hl=zh-cn)。

## 配置 Batch

展开上传步骤的参数配置后，HelpBox 下方的“官方参数说明”链接会打开 [fastlane 官方参数文档](https://docs.fastlane.tools/actions/upload_to_play_store/)，点击不会修改或保存 Batch 参数。

### 新建步骤的默认配置

| 配置 | 默认值 |
|---|---|
| AAB 路径 / 应用包名 | 同时留空，使用本次 Batch 的成功构建 |
| 目标轨道 | 内部测试（`internal`） |
| 上传后用途 | 保存为草稿（不开放下载） |
| 凭据 JSON 路径 | 主仓沿用当前项目 `Docs/evidences/googleplay_upload/` 中已配置的 JSON；公开副本为 `YOUR_GOOGLE_PLAY_CREDENTIALS_PATH` |
| fastlane 路径 / 更新说明 | 留空 |
| 更新说明语言 | 英语（美国）（`en-US`） |
| 仅验证上传 | 不勾选（`false`） |
| 稍后在后台手动提交审核 | 不勾选（`false`）；默认草稿时灰显，切换为开放下载后可自行勾选 |
| 超时 | 1800 秒 |

默认值只用于创建参数，既有步骤已保存的值不回写。凭据默认路径是参数源码的固定初值，不会自动查找账号或读取其他 Batch 的配置；消费项目须填写自己的文件路径。`Rollout` 的初值为 `0.1`，只有选择部分用户开放下载时才显示和传入。

### 构建产物与手动路径

沿用项目现有 Config 导出、HybridCLR、资源构建链，在 `build.package` 后添加 `googleplay.upload_aab`。Player 构建须为 Android、`BuildAppBundle=true`、`DevelopmentBuild=false`，关闭 Android 工程导出，使用 Google Play 接受的上传签名并递增 `versionCode`。

上传参数 `AabPath` 和 `PackageName` 留空时，使用同一 Batch 最新成功构建的确切 `BuildReport.summary.outputPath` 及构建时包名。记录只在本次 Batch 内共享；新构建开始即清空，构建取消/失败/Unknown 会停止流水线，最新产物为 APK 或工程时不能沿用先前的 AAB。

指定已有 AAB 重试时，必须同时填写 `AabPath` 和 `PackageName`。相对路径按项目根解析；检查 `.aab` 后缀与 `BundleConfig.pb`、`base/manifest/AndroidManifest.xml` 结构，不从当前 PlayerSettings 猜包名。实际签名、包内包名、版本号、政策与兼容性仍由 Google 校验。

两项必须同时填写或同时留空，空白视为未填写。只填其中一项时，界面在缺失字段绘制红框和补填错误提示，并禁用运行（折叠步骤同样检查）；补全或同时清空后解除。UI 和 CLI 执行前共用同一规则，禁止只填包名再搭配自动构建路径。

| 参数 | 默认值 / 行为 |
|---|---|
| `Track` | 默认 `internal`；下拉提供内部测试 `internal`、封闭测试 `alpha`、开放测试 `beta`、正式发布 `production`；“自定义…”可填写后台实际轨道标识 |
| `ReleaseStatus` | 界面标题“上传后用途”：`Draft` 显示“保存为草稿（不开放下载）”；`Completed` 显示“向该轨道全部用户开放下载”；`InProgress` 显示“向该轨道部分用户开放下载”，仅 production，`0 < Rollout < 1`，比例标题为“开放下载比例（0～1）”；仅验证时标为“上传后用途（仅校验）” |
| `CredentialsPath` | 默认沿用当前项目已配置的凭据路径；只接受相对 Unity 项目根目录的路径，执行前转成绝对路径；文件须存在且为 `.json`，允许项目内外 |
| `FastlanePath` | 空时自动探测；支持绝对路径、相对项目根的路径（如 `Tools/fastlane`），或 PATH 中的命令名；不会自动安装 |
| `ReleaseNotes` / `ReleaseNotesLanguage` | 可选更新说明；语言默认 `en-US`，下拉提供中文、英语、日语等常用语言及“自定义…”；正文最多 500 字符，留空不更新说明 |
| `ValidateOnly` | false；界面标题“仅验证上传（不保存到轨道）”；true 会真实上传 AAB，并按轨道、版本用途及适用比例验证 edit，最后不 commit，不在目标轨道保存新版本，也不开放下载；versionCode 可能被占用，再次上传同一版本号可能失败，仍需网络与凭据 |
| `ChangesNotSentForReview` | 默认 false；界面标题“稍后在后台手动提交审核”。本工具在 `Draft` 或 `ValidateOnly` 时灰显并显示未勾选；草稿执行时按 false 处理，验证时不传该选项。原保存值不回写，切换为非验证的开放下载用途后恢复；有效勾选时请求先保存变更，之后在 Console 手动提交审核，Google 可能拒绝该请求。有效值为 false 时允许按 Google 要求兼容同一次 edit 的审核参数，不重传 AAB |
| `TimeoutSeconds` | 1800，整个步骤超时；允许 1～86400 秒 |

轨道和语言仍保存为原有字符串，旧配置及 CLI 覆盖格式不变；预设外的已有值显示为自定义并原样保留，取消自定义弹窗不修改配置。路径不展开 `~` 或 shell 环境变量。

### 怎么选择用途与审核

“上传后用途”决定这份版本是否向所选轨道的用户开放下载；内部测试只面向符合条件的测试人员。“稍后在后台手动提交审核”是请求把审核操作留到后台，审核指 Google 检查本次更新。选择开放下载仍需满足审核和后台发布设置，不代表上传结束后立即可下载；想先保留包而不供用户下载，应选择草稿。

“向该轨道部分用户开放下载”表示按比例分批开放。例如在正式轨道填写 `Rollout=0.1`，Google 会向约 10% 的符合条件用户提供新版，其余用户暂时继续使用旧版；不能在这里指定具体用户，比例也不会自动增大。内部测试获取新版应选择“向该轨道全部用户开放下载”。

手动提交审核选项对应 Google `changesNotSentForReview`；官方参数说明主要描述变更被拒绝后的场景，不能据此保证所有提交都不会触发审核。仅验证也不是无风险重试模式，不能保证验证后再次上传相同 versionCode 会成功；需要先保留包再决定用途时，可上传草稿并在后台使用已有包。

为简化配置，本工具在“保存为草稿”时禁用手动审核选项，并显示原因；UI、CLI 和 Job 执行时共用同一限制，旧配置即使保存为 true 也不请求延后审核。这是工具的使用限制，不是 Google 对所有草稿的固定互斥规则。草稿仍不开放下载，是否触发审核由 Google 后台决定；切换到开放下载用途且关闭仅验证后恢复原保存选择。

凭据仅从 `CredentialsPath` 读取，不再提供或读取凭据环境变量配置。旧配置中的 `CredentialsPathEnvironmentVariable` 不再生效，须补填相对路径；已有绝对路径须改为相对路径。非空凭据路径在公开导出副本中替换为 `YOUR_GOOGLE_PLAY_CREDENTIALS_PATH`，覆盖默认及重命名的 Pipify 配置资产和参数源码默认字段；空值保留为待配置状态，主仓配置不回写。

只有显式添加并执行上传 Step 才上传；界面与 CLI 使用同一注册与参数解析路径，不修改现有 Batch 资产，也不创建带假包名的发布配置。

### 凭据与外网物料边界

仓库根目录 `Docs/` 与包内 `Assets/Framework/Docs/` 是两类目录。GitHub 公开同步使用共享 `PUBLIC_EXCLUDE` 的 `Docs/` 前缀，排除根目录全部内容，包含 `Docs/certificates/` 和 `Docs/evidences/`；即使这些文件被 Git 跟踪也会过滤。内网保留与使用不受该公开同步规则限制。

UPM 仅打包 `Assets/Framework` 和所选 `UPMPackages` 包目录，不打包仓库根目录 `Docs/`。包内配置资产及参数源码的凭据路径在公开副本中替换为占位符；路径脱敏与凭据文件过滤分别检查，不能相互替代。包内技术文档正常保留。

规则入口：`.agents/skills/_shared/public_exclude.py`、`.agents/skills/nova-publish/scripts/publish_packages.py` 和 `.nova/rules/RELEASE.md`。

## CI

使用 [Pipify CLI](EditorUtil.Pipify.Cli.md) 执行已配置的 Batch，**省略 `-quit`**。`Cli.Run` 异步等待 Runner，结束后自行 `Exit(0/1)`；编辑器内直接调用它也会最终退出 Unity，自动化查询应使用 `StartBatchJob`。

覆盖格式与其他 Step 相同：

```json
{
  "googleplay.upload_aab.Track": "internal",
  "googleplay.upload_aab.ReleaseStatus": "Draft",
  "googleplay.upload_aab.CredentialsPath": "../GooglePlayKeys/credentials.json",
  "googleplay.upload_aab.ValidateOnly": "true"
}
```

## 窗口构建与上传的重载交接

窗口 Batch 已有成功构建产物时，进入上传前先显示“等待编译与重载（上传尚未开始）”。Unity 完成编译和重载后，核对原工作区、参数指纹、平台和 AAB 文件指纹，再从原上传步骤继续，不重复构建。检查点只在本次 Editor 会话保留构建身份和尚未执行的游标，不保存参数正文或密码；上传启动前立即消费，已启动上传中断后不会重传。CLI / Job 不自动恢复。

异步上传时 Unity 可以继续更新，进度显示等待秒数；界面可操作不代表失败。重载、退出、取消或失败均清理进度条；已经启动的上传发生重载时终止进程、明确记录中断，并提示先核对 Play Console。

## 失败、审核与清理

遇到 `Changes are sent for review automatically. The query parameter changesNotSentForReview must not be set` 时，Google 拒绝本次手动审核参数；AAB 上传和轨道更新可能已完成，commit 未获得成功结果。先核对后台版本记录；若允许按 Google 后台规则处理审核，取消“稍后在后台手动提交审核”并保存。用途仍保持原选择，草稿不会开放下载。fastlane 2.240.1 即使传 `false` 仍带查询参数，因此取消勾选会启用仅针对同一 edit 的审核参数兼容提交；保持勾选则不会自动取消手动审核要求。

遇到 `The caller does not have permission` 时，先核对当前凭据 `client_email` 是否被授权访问实际包名对应的 Play 应用、权限是否过期，以及应用是否属于当前开发者账号。仅有 Cloud 服务账号和 JSON 不足以证明 Play 授权；不要通过切换草稿、审核或仅验证来修复权限。更多直接错误与最小检查见 [Skill 排障参考](../../../../Agents/Skills/nova-project-upload-google-play/references/troubleshooting.md)。

fastlane 使用独立临时工作目录和元数据目录；只上传 AAB 及本次明确填写的更新说明，不复用项目 Fastfile/Appfile、图标、截图或商店描述。父进程 `SUPPLY_*`、DEBUG 和原始凭据环境选项被隔离，凭据路径只传入子进程。

非零退出、取消、超时会中断 Batch，保留正式 AAB，清理临时元数据；域重载与 Editor 退出也终止上传进程并清理。日志只保留限长脱敏诊断。不由 Pipify 自动重新执行整个步骤，关闭 supply 外层 AAB 上传重试。勾选手动审核时关闭审核参数自动兜底，避免擅自改成自动审核；取消勾选时允许 supply 按 Google 错误建议调整同一次 edit 的 commit 参数（省略参数或设为 true），不重新上传 AAB。Google API SDK 仍有自身内置重试。

若错误发生在远端 commit 附近，状态可能未知。先检查 Play Console 中 versionCode 和 edit 的实际结果，再决定重试，不能以本地报错断言后台未提交。

自动提交遵循 Google 审核和托管发布设置。Google `edits.commit` 当前省略 `changesInReviewBehavior` 时默认 `CANCEL_IN_REVIEW_AND_SUBMIT`，fastlane supply 暂未暴露该参数，因此提交可能影响同一应用正在审核的变更；已有审核时先核对后台，不承诺草稿上传与其他审核互不影响。

## 验证范围

本地定向测试覆盖 Step 注册、参数默认值、Batch 产物隔离、非法输入、项目内外凭据的项目根相对路径解析、缺失/绝对路径拒绝且不从环境变量兜底、CLI 路径覆盖、真实 argv 转义、凭据环境隔离、更新说明、进程成功/失败/取消/超时与临时目录清理。测试使用假 fastlane、结构占位 AAB 和空凭据，不证明 Google 接受真实 AAB；首次真实联调还需工具、应用、凭据及有效签名产物。

## 官方来源

- [Google API 接入](https://developers.google.com/android-publisher/getting_started?hl=en)
- [Google edit 工作流与首次 Console 上传](https://developers.google.com/android-publisher/edits?hl=en)
- [Google AAB 上传](https://developers.google.com/android-publisher/api-ref/rest/v3/edits.bundles/upload?hl=en)
- [Google 轨道与发布状态](https://developers.google.com/android-publisher/api-ref/rest/v3/edits.tracks?hl=en)
- [Google commit 审核行为](https://developers.google.com/android-publisher/api-ref/rest/v3/edits/commit?hl=en)
- [fastlane supply 参数与配置](https://docs.fastlane.tools/actions/upload_to_play_store/)

## 本地校验与远端结果范围

显式 AAB 包名只校输入格式，未解析 AAB manifest 的真实 applicationId/versionCode 或签名；ZIP 必需项、非 Development 同 Batch 产物与凭据文件存在/.json 不证明服务账号授权。fastlane 直接进程执行不走 shell，清 SUPPLY_ 环境并只传 JSON_KEY 路径、不复制凭据；支持 timeout 1..86400，retries=0。

取消、超时、重载与退出按本次 PID 尝试停止子树/根，枚举与等待仍可失败，停止/Dispose/临时目录删除异常可能遮原上传结果。已提交 edit 不回滚，不自动重试；ValidateOnly 不提交 edit 仍真实访问服务。日志按固定规则脱敏并保尾部，不能承诺所有厂商输出秘密均被识别，exit0也不证明审核或用户下载分发完成。
