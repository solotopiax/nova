---
id: PAT-169
title: Google Play 上传按产物身份与远端阶段验收
summary: 锁定成功产物，区分验证、保存、审核与可下载
category: editor
type: pattern
status: active
date: 2026-10-10
source: Pipify Google Play 上传实现、fastlane supply 与 Google API 文档
aliases:
  - PAT-169-google-play-upload-artifact-and-remote-stage-boundary
  - Google Play 自动上传边界
keywords:
  - googleplay.upload_aab
  - Google Play
  - fastlane supply
  - BuildReport
  - versionCode
  - ValidateOnly
  - CredentialsPath
  - changesNotSentForReview
  - 服务账号应用权限
  - 上传前重载检查点
tags: [pattern, editor, pipify, android, google-play]
related:
  - "[[MOC-Pipify|Pipify图谱]]"
  - "[[PAT-21-inspector-helpbox-multiline|PAT-21]]"
  - "[[PAT-36-git-tracked-paths-relative-to-project-root|PAT-36]]"
  - "[[PAT-49-pipify-step-no-batch-lock-assumption|PAT-49]]"
---

# PAT-169：Google Play 上传按产物身份与远端阶段验收

## 适用场景

为 Nova Pipify 接入 Google Play AAB 上传、解释上传选项、维护 CI 重试或验收发布结果。

## 核心做法

- 首次接入分别核对 Google Cloud 的 API 与服务账号凭据、Play Console 的目标应用权限，以及首包与上传签名。Cloud 项目角色不能替代 Play 应用授权，服务账号 JSON 也不能替代 AAB 上传签名；服务账号出现在用户列表中不等于已获目标应用权限。
- 上传只能使用当前 Batch 最新明确成功的 AAB 和构建时包名；手动指定时，AAB 路径与应用包名必须成对填写。不能从当前 PlayerSettings 猜已有文件的身份，也不能在新构建失败后沿用旧产物。
- 验证、保存变更、Google 审核、目标用户可下载是不同阶段。界面使用“保存草稿”“开放下载”“后台手动提交审核”描述结果，执行与验收分别核对每个阶段。
- `ValidateOnly` 仍真实上传 AAB，并按所选轨道、状态和适用比例验证 edit，最后不 commit；不能当成本地 dry-run，也不能保证再次上传相同 versionCode 不冲突。
- 草稿不向轨道用户开放下载；全部或部分用户选项仍受审核和后台设置约束。内部测试的用户是符合条件的测试人员；部分用户是正式轨道的比例开放，不自动增大比例。
- `changesNotSentForReview` 是请求稍后在后台手动提交审核，不会自动将版本改为草稿；仅验证时禁用且不传参。不能把该选项当作所有场景都不会触发审核的保证。
- Nova 在草稿时禁用手动审核，属于工具使用限制，不是 Google 的固定互斥规则。UI 灰显并显示未勾选时，执行也必须忽略旧保存值；仅在非验证、开放下载用途下恢复该选择。当前默认值与具体参数留在 Docs，不用截图中的配置代替后台规则。
- 凭据配置只保存项目根相对路径，执行时转成绝对路径；不把凭据正文写进配置。公开交付分别过滤文件与脱敏路径，源码默认值也属于路径脱敏面；根目录 `Docs/` 与包内技术文档须区别处理。
- 审核参数被直接拒绝后的兼容提交，与重新上传 AAB 分开处理。未有效请求手动审核时，可按 Google 返回的明确参数建议重试同一个 edit 的 commit；有效请求手动审核时，禁止自动改变用户的审核意图。CLI 参数、工具默认值与最终 HTTP 查询参数须分别核对，`false` 不等于省略查询参数。
- commit 附近失败时，先查 Play Console 中版本和变更的实际状态，再决定重试；本地退出码不能证明远端未保存。进程取消、超时或编辑器重载后终止本次上传并清理临时目录，保留正式 AAB。
- 上传前的重载检查点仅恢复尚未启动的上传：核对工作区、原 Batch 参数、平台和实际 AAB 指纹后继续，并在启动上传前消费检查点。已开始的上传中断后不能自动重放，也不能重新构建或猜测产物身份来替代核对。

## 原因

构建配置可以在构建后变化，远端上传也可能先产生副作用再返回失败。仅凭当前配置、成功日志或“没有 commit”推断包身份、可下载状态和可安全重试，会把不同层面的结果混为一谈。

## 反模式

- 任意 Unity Build 完成后自动上传，未显式加入上传 Step。
- 将验证上传解释为无网络、无副作用、可重复使用同一版本号。
- 将 fastlane 的 `completed` 解释为 Google 审核通过或用户立即可下载。
- 把 Cloud Owner/Editor 当成 Play 上传权限，或把服务账号 JSON 当成 AAB 签名。
- 只在界面灰显手动审核，却仍让旧保存值进入上传参数；把工具互斥说成 Google 固定规则。
- 只脱敏配置路径，却把实际凭据文件复制进公开产物。
- 错误发生后自动重新执行整条上传链，未核对后台是否已有变更。

## 来源、影响与验证

- 后台准备与授权分工：`Assets/Framework/Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.GooglePlay.Setup.md`；当前配置、用途说明与 CI：同目录 `PipifySteps.GooglePlay.md`。
- 实现：`PipifySteps.Build.cs`、`PipifySteps.GooglePlay.cs`、`PipifySteps.Definitions.cs`；界面约束位于 `PipifyWindow.RightPanel.cs` 与 `PipifyWindow.RightPanel.Execute.cs`；上传前交接与指纹核对位于 `EditorUtil.Pipify.WindowUpload.cs`。
- [fastlane supply](https://docs.fastlane.tools/actions/upload_to_play_store/)：核对的 2.240.1 `Uploader.perform_upload` 先上传文件，再进入 validate 或 commit 分支；`Client.commit_current_edit!` 对明确审核参数错误的兼容分支只重试同一个 edit 的 commit，升级工具时重新核对。
- [Google API 接入](https://developers.google.com/android-publisher/getting_started?hl=zh-cn)、[Play Console 用户和权限](https://support.google.com/googleplay/android-developer/answer/9844686?hl=zh-Hans)：API 准备与应用授权分属两个后台，测试轨道发布权限包含上传草稿与创建修改版本。
- [Google edit 工作流](https://developers.google.com/android-publisher/edits)、[轨道状态](https://developers.google.com/android-publisher/api-ref/rest/v3/edits.tracks)、[commit 审核行为](https://developers.google.com/android-publisher/api-ref/rest/v3/edits/commit)。版本号占用风险另有[未提交上传的社区案例](https://github.com/willowtreeapps/vocable-android/issues/700)，不能据此声称本项目已经真实复现。
- 本地定向测试使用占位 AAB、空凭据和假 fastlane，覆盖默认参数、路径解析、真实 argv 与临时目录清理；该证据不证明 Google 接受真实 AAB、审核完成或用户实际可下载。
- 本次补强依据当前 Docs、UI/执行约束与 fastlane 源码核对；未访问实际后台权限，也未重传 AAB。
- 影响 Google Play Step 的 UI、CLI、重试与公开交付；不改变 Runner 的职责边界，详见 [[MOC-Pipify]] 与 [[PAT-49-pipify-step-no-batch-lock-assumption|PAT-49]]。
