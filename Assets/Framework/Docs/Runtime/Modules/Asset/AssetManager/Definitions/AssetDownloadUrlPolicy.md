# AssetDownloadUrlPolicy

`AssetDownloadUrlPolicy` 是 AssetManager 为每个 YooAsset 包独立持有的候选 URL 与失败重试策略，同时实现 `IDownloadUrlPolicy` 和 `IDownloadRetryPolicy`。

候选去重、最近成功优先排序、不可变计划、执行游标和偏好存储均复用 Core 的 `HttpFallbackPlanner`、`HttpFallbackExecutionPlan`、`HttpFallbackExecutionCursor` 与 `HttpFallbackPreferenceStore`；本类只适配 YooAsset 的 URL/失败回调和 Asset 专属错误码规则。

- 每个文件按 URL 路径拥有独立计划；A/B/C 并发失败不会互相推进候选。
- 候选先去重，再按 `C × R × (K + 1)` 计算最大物理尝试数；传给 YooAsset 的额外物理重试数为该值减一。
- `FallbackRoundCount` 的一轮会完整尝试全部候选；`RetryDownloadCount` 是下载重试次数，每次重试重新执行完整轮次组合。
- 404/408/416/429、5xx、无响应可继续；401/403 和其他 4xx 立即停止。
- 传输成功后的内容校验失败会继续使用该文件已冻结计划中的下一候选。
- 成功会更新同类请求的最近成功域名，新文件可从该域名开始；完整失败不会清除此偏好，候选配置变化且旧域名已不存在时才清除。
- URL 查询串和 fragment 不参与文件身份匹配，因此带时间戳的 `.version` 回调仍可正确收口。

白名单命中时，候选顺序由 `AssetRemoteService` 提供：白名单元数据主、白名单元数据备用、常规主、常规备用。全部逻辑组合失败后才进入 AssetManager 回退。WebGL 首包元数据回退也复用同一计划、错误分类和埋点，但候选临时收口为首包同源地址；Bundle 地址不受影响。同步加载行为不变，WebGL 仍受 YooAsset 异步加载边界约束。

Bundle 超时按平台互斥：WebGL 使用 `WebGLBundleRequestTimeout` 作为单次物理请求总超时，其他平台使用 `IdleTimeout` 监测连续无字节流入；两者都不表示整条候选链的共享总时限。
