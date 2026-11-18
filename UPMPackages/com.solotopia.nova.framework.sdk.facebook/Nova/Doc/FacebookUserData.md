# FacebookUserData

`FacebookUserData` 保存 `FacebookPlugin.CurrentUserData` 暴露的当前登录数据。

```csharp
public string UserId { get; }
public string AccessToken { get; }
public string AvatarPath { get; }
public FacebookUserData WithAvatarPath(string avatarPath);
```

`WithAvatarPath` 返回一个替换头像路径后的新数据对象。

`AvatarPath` 是本地头像文件路径，登录自动下载关闭或下载失败时可以为空；它不是头像 URL。初始化恢复登录凭据时不会自动补下载头像，调用 `LoginAsync` 且启用 `AutoDownloadAvatarOnLogin` 后才执行该自动下载流程。

此对象不保存昵称。读取昵称、头像 URL 和资料中的头像路径时，使用 `FacebookPlugin.Profile.GetCurrentProfileAsync()` 返回的 `FacebookProfile`；FacebookDemo 在登录成功后调用该接口刷新展示。不要把 `AuthResult.Success` 或 `CurrentUserData` 非空当作资料获取成功。

权限与头像失败的日志查看方式见 [调试登录与接口调用](FacebookPlugin.md#调试登录与接口调用)。
