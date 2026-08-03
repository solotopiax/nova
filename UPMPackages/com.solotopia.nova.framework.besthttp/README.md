# Nova Framework - BestHTTP

Nova Framework 的 BestHTTP 可选 HTTP 后端适配包。

安装本包后，仍需要安装原厂商业包：

- `com.tivadar.best.http`
- `com.tivadar.best.tlssecurity`

内部成员可从 PlugPalsWindow 的“内部云仓库”安装原厂包；外部使用者可自行购买并导入原厂包。原厂包安装完成后，`NovaFramework.BestHTTP.Runtime` 程序集会直接编译启用。

本包只包含 Nova 适配代码，不包含 BestHTTP/TLS 原厂内容。

安装支持结构化遥测的商业库版本后，本包会自动把 BestHTTP 网络事件转发给 Nova 中所有可用的 `ITrackPlugin`。可在 Network Inspector 的 HTTP 区域通过“启用 BestHTTP 网络埋点”统一开启或屏蔽；完整事件与字段定义见 [Nova/Docs/INDEX.md](Nova/Docs/INDEX.md)。

## 目录结构

- `Nova/`：Nova 自有适配代码。
- `Core/`：第三方源码槽位。本包不分发 BestHTTP/TLS 原厂源码，因此当前为空。
