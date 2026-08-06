# AssetDownloadUrlPolicy

`AssetDownloadUrlPolicy` 是 AssetManager 为每个 YooAsset 包独立持有的候选 URL 轮换策略。

- `SelectUrl` 按当前游标选择候选地址；成功后保持在当前可用地址。
- `OnRequestFailed` 对每个连接、超时、HTTP 等传输失败独立推进候选，保留并发 Bundle 下载的主备轮换语义。
- `FailureGeneration` 记录已处理的传输/内容失败次数；版本或 Manifest 操作开始前保存该值。
- `AdvanceAfterOperationFailure(startGeneration)` 仅在操作期间没有发生传输失败时补推进一次，覆盖 HTTP 200 但 `.version` / `.hash` / `.bytes` 内容非法、损坏或反序列化失败的情况，同时避免一次失败跳过备用地址。

白名单命中时，候选顺序由 `AssetRemoteService` 提供：白名单元数据主、白名单元数据备用、常规主、常规备用。全部候选失败后才进入 AssetManager 既有离线回退。
