# ProcedureDataKeys

`ProcedureDataKeys` 是流程黑板键名常量表。

它的定位很简单：

- 给内置流程之间的 `SetData / GetData` 提供稳定字符串键

## 什么时候先看这页

优先看这页的场景：

- 你要排查某个流程黑板值是谁写的、谁读的。
- 你要确认启动链里跨流程传递了哪些最关键的状态。
- 你要避免手写字符串键导致拼写漂移。

## 当前键语义

### 1. `AppVersionResult`

- 值类型：`AppVersionResult`
- 写入者：`ProcedureCheckVersion`
- 读取者：`ProcedureAppDownload`

表示大版本检查的结果。

### 2. `HasAssetPatch`

- 值类型：`bool`
- 写入者：`ProcedureCheckVersion`
- 当前读取者：无；进入 `ProcedureAppDownload` 或 `ProcedureHotfix` 后会被清理

表示非 WebGL 启动检查是否存在资源补丁；WebGL 不执行 Downloader 差异检查，因此固定为 `false`。

它不等于“必须进入 `ProcedureHotfix`”：WebGL `TagsOnLaunch` / `AllOnLaunch` 的启动预热由 `RequiresStartupAssetWork` 独立表达。

### 3. `RequiresStartupAssetWork`

- 值类型：`bool`
- 写入者：`ProcedureCheckVersion`
- 读取者：`ProcedureAppDownload`

表示本次启动是否还必须进入 `ProcedureHotfix`：非 WebGL 时表示存在待处理补丁；WebGL 时还包含 `TagsOnLaunch` / `AllOnLaunch` 的 Warmup。

## 风险点 / 易错点

- 这是“键名表”，不是黑板值生命周期管理器；值什么时候写、什么时候删，仍由具体流程决定。
- `ProcedureAppDownload` 会先读取启动路由键，再在离开流程时统一清理三个键；`ProcedureHotfix` 进入时也会清理它们。不要把这些键当成后续长期状态。

## 继续阅读

关键源码：

- [ProcedureDataKeys.cs](../../../../Scripts/Runtime/Modules/Procedure/Definitions/ProcedureDataKeys.cs)

相关文档：

- [ProcedureCheckVersion.md](ProcedureCheckVersion.md)
- [ProcedureHotfix.md](ProcedureHotfix.md)
- [Procedures/ProcedureAppDownload.md](Procedures/ProcedureAppDownload.md)
