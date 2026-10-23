# AppleSignInPlugin

`AppleSignInPlugin` 是 Nova 的 Apple 登录插件，实现 `IAuthPlugin`，返回统一的 `AuthResult`。

```csharp
public sealed partial class AppleSignInPlugin : SDKPluginBase, IAuthPlugin
```

## 常用接口

```csharp
public bool IsLoggedIn { get; }
public AppleSignInUserData CurrentUserData { get; }
public UniTask<AuthResult> LoginAsync(string provider, CancellationToken ct = default);
public UniTask LogoutAsync(CancellationToken ct = default);
```

## 说明

- `Priority => 50` 用于插件排序与逆序销毁；初始化由依赖关系调度，无依赖插件可以并发初始化，不能据此认定 Facebook、Apple、Google 的初始化先后。
- 当前登录数据只从 `CurrentUserData` 读取。
- `CurrentUserData` 只暴露 Apple 用户 ID 和可选姓名。
- iOS 构建会自动注入 `com.apple.developer.applesignin` entitlement。
- `AuthResult.UserId` 使用 Apple 用户 ID；`AuthResult.Token` 不再填充。
- `LogoutAsync` 只清理本地状态。

## 取消、回调与失败

登录服务先调用原厂 `LoginWithAppleId`，随后在等待循环中调用 `AppleAuthManager.Update()` 并检查取消令牌。当前实现没有内置超时；业务需要限制等待时，应传入自己的取消令牌。预取消不保证阻止原厂请求启动，取消等待也不会撤销已经展示的 Apple 登录界面。同步完成的原厂回调可以直接结束等待，不经过循环内的取消检查。

`LoginAsync` 对服务调用中的 `OperationCanceledException` 继续抛出，对其他服务异常返回失败的 `AuthResult`；服务返回后的结果转换与登录状态写入位于该捕获范围之外，不能假设所有异常都会转成失败结果。`provider` 只用于统一结果标签，空值采用 Apple 默认标签，不选择另一个原厂登录服务。调用结束后没有独立的常驻回调泵。

`CurrentUserData` 是当前缓存引用，读取不会重新请求原厂状态；`LogoutAsync` 不检查传入令牌、不注销 Apple 系统账号，也不请求游戏服。账号可信校验和游戏账号绑定由业务后端及 GameLogin/GameBind 流程完成。

初始化只保存 Apple 配置并创建认证服务；配置类型不匹配或为空时使用默认 `RequestFullName=true`，不在此时检查平台支持。实际登录才创建并复用原厂 Manager，只请求可选姓名，不请求邮箱，原厂参数中的 nonce/state 当前为空。姓名可能只在首次授权返回，拼接按前缀、名、中间名、姓、后缀顺序，忽略空部件但不 Trim 空白。

`LogoutAsync` 的本地状态清理不会主动清除此前发布的 OpenID/Provider 数据槽；完整 SDK 释放的数据槽处理属于 `SDKPluginBase.DisposeAsync`，不要混同这两个入口。释放认证服务引用也不会撤销在途原生登录或调用原厂 Manager 的销毁接口。

## iOS 构建注入

构建预处理仅检查配置是否存在并记录日志，不验证 Apple ID、证书或签名服务。后处理向共享构建上下文写入 `com.apple.developer.applesignin=[Default]`，向目标加入弱链接的 `AuthenticationServices.framework`，再通过兼容构造器创建 capability；最终落盘由中央构建处理器收口。缺失 entitlements/PBXProject/目标等分支可以 Warning 后跳过，反射构造失败则继续抛出，因此“处理器运行过”不证明签名能力完整。

## AppleSigninDemo

示例的 Apple 登录只取得平台身份；绑定按钮会先按需调用 GameLogin，再请求 GameBind，发生绑定冲突时只提示继续 QueryConflict/Resolve，不代替玩家裁决或迁移存档。GameLogin 已在成功路径交付 SDK 注册/登录，页面不应再重复通知。示例平台登出不清除游戏 UID 或撤销既有绑定。

页面使用不随 UIView 关闭取消的认证等待；`OnClose` 仅清本页缓存与显示，已启动的异步调用仍可能继续更新旧页面。拿到 Plugin 引用也不证明目标平台受支持或已认证，真实 Apple 登录、游戏服校验和账号绑定须分别判断。

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 `Docs/Onboarding/PROJECT_STRUCTURE.md` 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。
