# Nova Framework - SDK - DataMaster 文档索引

> 本包为 Nova 框架 DataMaster 远程配置 / ABTest / 事件上报插件。
> `DataMasterPlugin` 继承 `SDKPluginBase`，不实现业务能力接口；ABTest 能力经具体类型公开方法暴露，业务通过 `SDKManager.Get<DataMasterPlugin>()` 调用。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| DataMasterPlugin | DataMaster SDK 插件：读参 / 曝光打点 / 实验事件上报 / 分流属性 | DataMasterPlugin.md |
| DataMasterPluginConfig | 插件配置：AppId / AesKey / 默认配置文本 | DataMasterPlugin.md |

## 学习资料

- ABTest扫盲.md — 从零讲清 ABTest 是什么、客户端工作流程、结果如何比较（新人向）。
- 参考/ — 服务端接口 swagger（params/evaluate、dm/events）与后台操作流程图，联调时对照。

## 相关

- 依赖框架核心：com.solotopia.nova.framework（`SDKPluginBase` / `ISDKManager` / `SDKEventData.UserLogin`）
