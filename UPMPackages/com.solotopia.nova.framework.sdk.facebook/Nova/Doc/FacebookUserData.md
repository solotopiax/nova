# FacebookUserData

`FacebookUserData` 保存 `FacebookPlugin.CurrentUserData` 暴露的当前登录数据。

```csharp
public string UserId { get; }
public string AccessToken { get; }
public string AvatarPath { get; }
public FacebookUserData WithAvatarPath(string avatarPath);
```

`WithAvatarPath` 返回一个替换头像路径后的新数据对象。
