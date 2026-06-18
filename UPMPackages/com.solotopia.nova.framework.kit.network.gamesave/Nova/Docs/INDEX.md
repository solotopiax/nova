# Nova Framework - Kit - Network - GameSave 文档索引

> 本包为 Nova 框架游戏存档（云存档）业务 Kit，在主框架包 `com.solotopia.nova.framework` 的 Network Kit 公共编排层基础上，封装存档节点的获取与上传协议。
> 业务侧通过 `Nova.Network.Kit<Save>()` 获取实例，无需关心协议细节。
>
> **边界提示：** 本包负责存档数据在客户端与服务端之间的网络传输（云存档）；本机持久化（PlayerPrefs / SQLite / 文件分片）由主框架 `PersistManager` / `FileFragmentManager` / `SQLiteManager` 负责，二者职责不重叠。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `Save` | 游戏存档业务 Service（GetAsync / GetFullAsync / SetAsync / SetFullAsync 6 个极简入口，SetDebugMode） | [Save.md](./Save.md) |
| `SaveKitConfig` | 游戏存档 Kit 固有配置（GetCmdName / SetCmdName，在 ConfigWindow「Kit 配置」中填写） | [SaveKitConfig.md](./SaveKitConfig.md) |
| `SaveErrorCode` | 游戏存档业务错误码（约定段位 8000~8999，待填充） | [SaveErrorCode.md](./SaveErrorCode.md) |

## 错误码

- [SaveErrorCode.md](./SaveErrorCode.md) — 游戏存档段错误码（8000~8999）
- 通用网络错误码由底层公共网络层返回，本包只维护游戏存档业务段错误码。

## 相关

- [Save.md](./Save.md) — 云存档业务 Service
- [SaveKitConfig.md](./SaveKitConfig.md) — 云存档 Kit 配置
- [SaveErrorCode.md](./SaveErrorCode.md) — 云存档业务错误码
