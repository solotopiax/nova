# 平台构建、CDN 部署与缓存清理

用户要保存可重复使用的平台构建/部署 Batch 时读取本页；只配置不执行。仍用 `nova.project.pipify.inspect-settings` / `configure-batch` 读真实 Step Schema 和保存目标 Batch，明确要求运行后用 `nova.project.pipify.run-batch`。只有已有 Action 可调度；下面的 Step/C# 方法不是独立 MCP Tool。

## 本地准备与平台闭包

用户说明“Android/iOS/WebGL 全套部署”时，Agent 从消费项目读取活动平台、Master 当前坐标、已保存 Batch、Scene、平台选项、Bundle/Player 目录与工具；先形成具体可审阅的组合。生成依赖按配置/表/资源密钥源码 → 导入/编译 → HybridCLR → Bundle → Player → 请求的设备/浏览器 → 已授权远端部署执行。`export.excel.all` 不含 `export.config` 和 `export.network.proto`；资源密钥由 ConfigWindow 或 Pipify `export.config` 导出，不能用 `nova.project.config.export-runtime` 假称已生成。

平台串行切换且稳定后重新计划；Step 的 Platform/Target 运行时取 Active BuildTarget，Batch 内旧数字不构成三平台切换。`build.package` 用真实 `PackageParams`，区分 `DevelopmentBuild`、Runtime 的 `DevelopMode`、Android APK/AAB/工程与签名选项，实际 BuildReport 才证明产物。Batch Plan 没有完整冻结 Scene/Master/PlayerSettings/动态展开参数；Agent 按 [build-player 平台准备](../../nova-project-build-player/references/platform-validation.md) 单独保存基线并前后复核。构建、设备与外部层入口见 [平台 CLI](../../nova-project-build-player/references/platform-cli.md)。

## 精确远端写集

| StepId | 当前真实参数 | 权威来源与默认 | 实际副作用 |
|---|---|---|---|
| `cdn.deploy` | `VersionCheckLocalFilePath`、`VersionCheckRemoteFilePath`、`AutoLinkLatestVersion`、`LocalDirectory`、`RemoteDirectory` | AutoLink 默认 true；其余字符串无初始化。连接/凭据及 `PresetOSSPath` 来自当前维度 Master 的 CDN 配置 | 上传所选完整 YooAsset 版本的运行文件，可能附版本检查文件；同 Object Key 覆盖 |
| `cdn.whitelist.deploy` | `DeviceIDs`、`WhitelistRemoteFilePath`、`AutoLinkLatestVersion`、`ManifestBytesLocalFilePath`、`ManifestHashLocalFilePath`、`PackageVersionLocalFilePath`、`RemoteDirectory` | AutoLink 默认 true；设备按行，最终 Trim/去空/去重；OSS 来源同上 | 生成并上传白名单 JSON 与 `.bytes/.hash/.version`；不代替全部 Bundle 部署 |
| `cdn.purge` | `ZoneID`、`Token`、`CachePaths` | 三字符串无初始化；Token 密码框遮罩但资产中明文。执行时覆盖维度快照，不回写 Master | Cloudflare 对指定完整 URL 去重、最多 100 条一批顺序清缓存 |

`cdn.deploy` / 白名单路径的 `{Platform}/{Channel}/{Package}/{Version}` 大小写敏感；Platform 为本次 Active BuildTarget 映射，Channel/DevelopMode 为当前 Master。包名前缀按当前维度 YooAsset 配置解析；含 `{Time}` 时应核对实际已构建前缀，不拿本轮新时间猜产物。AutoLink 会在执行前再次挑选最新完整版本，配置/Plan 中的旧路径不等于实际冻结输出；若任务要求精确版本，应选明确目录/文件、关闭 AutoLink 并核对已保存有效值。

上传前展示实际本地文件/hash、bucket/对象 Key、版本检查文件完整 Key、白名单 JSON 完整 Key、Cloudflare Zone 和完整 URL 清单；秘密仅报告 hasValue，不输出密钥、Token 或签名 URL。覆盖远端、白名单设备集合和缓存清理各自属于外部写入授权，保存 Batch 不提供该授权；同一会话已经覆盖精确目标时继续执行，无需重复询问。凭据缺失或远端目标不能唯一解析时阻断该层，不把公网猜成测试环境。

`WhitelistRemoteFilePath` 是配置文件的完整对象位置，`RemoteDirectory` 是三个版本文件目录；配置文件位置为空或非法时当前实现仅跳过 JSON，不回退到版本文件目录。若任务要求白名单部署，缺 JSON 必须判未完成，不能用 Batch 成功掩盖。上传版本目录中的正式运行文件；`OutputCache`、`.encrypt`、构建日志和中间产物不是发布物。

## 验收与失败

上传成功只证明 OSS 调用完成；须从客户端实际请求的域名/路径读取本次白名单、version、manifest/hash 与至少任务需要的资源，核对来源版本及 HTTP/MIME/压缩/CORS。不能只读 bucket 内对象就宣称 CDN 已传播。Cloudflare API 的 success 证明接受清理请求，继续检查对应 URL 实际响应和缓存状态；传播、设备弱网、厂商服务仍是外部证据。

OSS 顺序覆盖不是远端事务：中途失败可能已写入前面的对象。保持 `partial`、记录已知完成/失败 Key，不自动重放整批、不猜旧值回滚。任何实际上传或 Cloudflare 参数/请求/业务失败会中断 Batch；Job Succeeded 不代替所有预期对象、正式产物、CDN 读取或设备验收。白名单临时 JSON 清理警告仍须核对并清理本轮目录，不视作完成。

保存正式 Batch；运行后恢复本轮 Target/development、场景/Runtime/签名与测试态设置，清理可归属临时日志、密码文件、缓存探针与对象，不删除正式交付物或用户缓存。报告“已保存 / 已执行 / 已验证 / 外部条件未验证”和清场结果。实际参数与行为来源为随包 `Docs/Editor/EditorUtil/EditorUtil.Pipify/PipifySteps.md` 的 CDN 三节及对应 `PipifySteps.CDN.cs`，不存在专用 CDN Project Action 的承诺。
