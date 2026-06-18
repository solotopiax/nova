# Nova Framework - SDK - Facebook 文档索引

> 本包当前只包含一个 Facebook 登录插件骨架。
> 代码尚未接入真实 Facebook SDK，且当前公开行为仍停留在占位阶段。

## 业务侧公开 API

| 类型 | 说明 | 文档 |
|---|---|---|
| `FacebookAuthPlugin` | Facebook 登录插件骨架，公开登录/登出接口，但默认返回“未实现” | [FacebookAuthPlugin.md](./FacebookAuthPlugin.md) |

## 当前状态

- `IsLoggedIn` 固定返回 `false`
- `CurrentUserId` 固定返回 `null`
- `LoginAsync(...)` 固定返回 `Success = false`
- `LogoutAsync(...)` 直接完成，不做真实 SDK 清理

## 相关

- [FacebookAuthPlugin.md](./FacebookAuthPlugin.md) — Facebook 登录插件骨架
