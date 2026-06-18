# SDK 异常总览

当前 `Assets/Framework/Scripts/Runtime/Modules/SDK/Definitions/Exceptions/` 下只存在一个公共异常类型。

## 当前异常

| 类型 | 说明 |
|---|---|
| `SDKUnavailableException` | `ISDKManager.Get<T>()` 请求的插件未启用、初始化失败或未注册时抛出 |

## 关键语义

- 这是“插件不可用”异常，不区分更细的厂商原因。
- 调用方应优先使用 `TryGet<T>()` 做防御性查询。
- 如果明确要求强依赖某插件，可在 `Get<T>()` 失败时把它视为启动期或业务链路错误。

## 关联文档

- [../Managers/Interfaces/ISDKManager.md](../Managers/Interfaces/ISDKManager.md)
