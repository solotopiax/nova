# Nova Framework - SDK - DataMaster - ABTest 文档索引

> 本包（`com.solotopia.nova.framework.sdk.datamaster.abtest`）是 Starlus DataMaster SDK 的 **Nova 接入层**，提供远程配置 / ABTest 实验参数 / 曝光打点 / 事件上报能力。
> `DataMasterPlugin` 继承 `SDKPluginBase`，由 `SDKManager` 统一实例化与编排；业务通过 `SDKManager.Get<DataMasterPlugin>()`（即 `Nova.SDK.Get<DataMasterPlugin>()`）获取实例后调用。

## 从这里开始

| 文档 | 内容 | 面向 |
|---|---|---|
| [DataMasterPlugin.md](./DataMasterPlugin.md) | 【主】本接入层**对外 API 完整参考**：读参 / 曝光 / 事件上报 / 分流属性 / 主题枚举 / 缓存清理 / 调试接口 / 事件回调 / 生命周期 / 构建环境宏，含使用示例与 Demo 按钮对照 | 接入本插件的业务开发 |
| [ABTest扫盲.md](./ABTest扫盲.md) | ABTest 是什么、客户端工作流程、实验结果如何比较（原理科普） | ABTest 新人 |
| [官方SDK技术文档.md](./官方SDK技术文档.md) | 附：厂商 Starlus DataMaster SDK（底层 `com.starlus.sdk.datamaster`）**官方技术文档原文**——底层数据模型 / 初始化 / 拉取流程 / 本地存储与安全 / 环境域名 / 常见问题等实现细节 | 需了解底层机制 / 排查底层问题 |
| 参考/ | 服务端接口 swagger（`params/evaluate`、`dm/events`）+ 后台操作流程图（创建主题 / 定义参数 / 上线流程），联调与后台配置时对照 | 联调 / 后台配置 |

## 读法建议

- **只做接入** → 读 [DataMasterPlugin.md](./DataMasterPlugin.md)：本层已封装厂商细节，业务只调本层公开 API，不直接碰厂商 `DataMaster` 类型。
- **想弄懂 ABTest** → 先看 [ABTest扫盲.md](./ABTest扫盲.md)。
- **排查底层（环境域名 / 加解密 / 落库 / 篡改重建等）** → 查 [官方SDK技术文档.md](./官方SDK技术文档.md)。
- **对接后台 / 联调服务端** → 查 `参考/`。

## 相关依赖

- 框架核心：`com.solotopia.nova.framework`（`SDKPluginBase` / `ISDKManager` / `SDKEventData.UserLogin`）
- 登录 Kit：`com.solotopia.nova.framework.kit.network.gamelogin`（sample 登录演示使用）
- 底层原厂包：`com.starlus.sdk.datamaster`（内部云仓库，随本包 `dependencies` 拉取）
