# FacebookAuthPlugin

## 1. 简介

`FacebookAuthPlugin` 是一个未完成的 Facebook 登录插件骨架。当前代码主要用于占位和接口预留，不代表已经具备可投产的 Facebook 登录能力。

## 2. 当前公开成员

| 成员 | 说明 |
|---|---|
| `SDKName => "Facebook"` | 插件标识 |
| `Priority => 30` | 初始化优先级 |
| `IsLoggedIn => false` | 当前固定未登录 |
| `CurrentUserId => null` | 当前固定无用户 |
| `OnInitializeAsync()` | 当前仅返回完成任务 |
| `LoginAsync(string provider, ...)` | 当前返回 `Success = false, ErrorMessage = "未实现。"` |
| `LogoutAsync(...)` | 当前直接完成 |

## 3. 当前行为边界

- 还没有接入 Facebook SDK 初始化。
- 还没有缓存登录态，也没有用户凭据桥接。
- `provider` 参数目前不参与任何逻辑。

## 4. 使用建议

如果只是想确认 SDK 模块如何挂一个“认证类插件”，可以参考本包的骨架结构；如果要直接交付 Facebook 登录功能，当前代码还需要继续补齐：

- SDK 初始化
- 登录结果映射
- 登录态持久化
- 账号登出与回收

## 5. 关联

- 包入口：[INDEX.md](./INDEX.md)
