# Nova Framework - Kit - Network - Login 文档索引

> 本包为 Nova 框架登录业务 Kit，在主框架包 `com.solotopia.nova.framework` 的 Network Kit 公共编排层基础上，封装登录协议的发送逻辑。
> 业务侧通过 `Nova.Network.Kit<Login>().Async(...)` 完成登录，无需关心协议细节。

---

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `Login` | 登录业务 Service（Async / Clear / SetDebugMode / UID 属性） | [Login.md](./Login.md) |
| `LoginKitConfig` | 登录 Kit 固有配置（CmdName），在 ConfigWindow 一次配置后 Login.Async 内部自动取用 | [LoginKitConfig.md](./LoginKitConfig.md) |
| `LoginErrorCode` | 登录业务错误码（约定段位 7000~7999，待填充） | [LoginErrorCode.md](./LoginErrorCode.md) |

## 错误码

- [LoginErrorCode.md](./LoginErrorCode.md) — 登录段错误码（7000~7999）
- 通用网络错误码由底层公共网络层返回，本包只维护登录业务段错误码。

## Sample 演示

GameLoginDemo（`Assets/Samples/GameLoginDemo/`）是本包附带的示例工程，由 `ProcedurePlaying.OnEnter` 直接打开 `DemoLoginView`。

| 类 | 演示 API | 交互元素 |
|---|---|---|
| `DemoLoginView` | `Nova.Network.Kit<Login>().Async(uid, openId, forceNewAccount)` / `Nova.Network.Kit<Login>().Clear()` | openId 输入框（`TMP_InputField`）、forceNewAccount 开关（`Toggle`）、登录按钮、登出按钮 |

**打开方式：** 启动 GameLoginDemo 场景，流程进入 `ProcedurePlaying` 后自动打开；UI 在 `UIs.xlsx` UI1 表注册，UIGroupName=Demo，Asset 地址=DemoLoginView。

**范式说明：** `ProcedurePlaying` 作为收口，所有子 Demo 的演示 View 均在此直接通过 `Nova.UI.OpenUIViewAsync<DemoXXXView>()` 打开，无需在 Procedure 外额外触发。

## 相关

- [Login.md](./Login.md) — 登录业务 Service
- [LoginKitConfig.md](./LoginKitConfig.md) — 登录 Kit 配置
- [LoginErrorCode.md](./LoginErrorCode.md) — 登录业务错误码
