# AIHelpDemo

入口场景为 [AIHelpDemo.unity](AIHelpDemo.unity)，业务页面为 `DemoAIHelpView`。已有按钮展示帮助中心、在线客服、演示用户资料同步和未读查询；“退出账号并重置客服资料”按钮在运行时克隆登录按钮外观，独立绑定登出回调，不改写 Prefab。

该按钮先调用 `Nova.SDK.EndLoginSession()`，清除 SDKManager 当前账号及待补交的登录资料，再对状态为 `Ready` 的现存 `AIHelpPlugin` 调用无参数的 `ResetUserInfo()`。AIHelp 尚未就绪时，统一账号会话仍会结束，页面会提示本次未调用客服资料重置；就绪后可再次点击。调用异常分别提示已执行到哪一步，不自动重试。每次有效点击各调用一次，重复点击不会新增按钮、监听或自动登录。

四种操作的职责独立：

- “关闭客服”只调用 `AIHelpPlugin.Close()`，关闭原厂客服页面。
- 关闭本 Demo UIView 只退订页面消息事件并执行基类关闭逻辑，不结束账号会话。
- “退出账号并重置客服资料”只结束 SDK 的账号会话并清理 AIHelp 用户资料；不注销服务端账号，不销毁插件，也不关闭上述两个页面。
- SDK 整体释放由框架生命周期持有，本页面不调用释放或重新初始化。

登录按钮仍使用固定演示用户调用 `AIHelpPlugin.Login(...)` 和 `UpdateUserInfo(...)`，并非游戏后端登录。真实游戏登录应复用既有 `Nova.SDK.Login(...)` 或已通知 SDK 的 GameLogin 成功路径；本 Demo 的登出入口不代替服务端登出接口。

本地验证应确认：只出现一个独立登出按钮，点击不会触发登录或“关闭客服”；关闭、重开 UIView 不登出且不新增监听；插件未就绪时准确提示部分完成；再次显式登录后仍可再次登出。上述两个调用均同步返回，没有可取消的后台等待，关闭页面不会撤回已执行的资料重置。Editor 原厂方法为空操作，调用反馈不证明厂商资料已清除。

真实验收须固定已获授权的 Android/iOS 应用、设备与测试身份，先同步演示资料，再显式登出并重新打开客服，核对原厂身份状态以及重新登录后的隔离情况。不要发送工单或修改真实用户资料来代替此项验证；日志和截图不得包含原始用户内容或凭据。当前配置还启用了 TGA 和广告聚合，启动场景可能初始化其他外部服务，须一并核对授权范围。初始化、账号和重置语义以已安装 AIHelp 包的 [Nova/Doc/INDEX.md](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.aihelp` 包内 `Nova/Doc/INDEX.md`）、[AIHelpPlugin.md](README.md#跨包文档定位)（`com.solotopia.nova.framework.sdk.aihelp` 包内 `Nova/Doc/AIHelpPlugin.md`） 为准；本地编译或 Editor 调用不证明外部客服验收通过。

导入路径重写、旧版本目录清理、Sample 基类与启动流程的共同边界，见已安装 Framework 的 [Docs/Onboarding/PROJECT_STRUCTURE.md](README.md#跨包文档定位)（`com.solotopia.nova.framework` 包内 `Docs/Onboarding/PROJECT_STRUCTURE.md`） 中“Sample 的导入、辅助工具与页面边界”；本页只补充当前示例的差异。

## 启动配置等待

`ProcedurePreload` 在网络表加载成功后，等待本次启动的同一轮 GM 应用配置拉取，再继续 SDK 等后续步骤；拉取失败、超时或未配置时继续使用当前配置。仍保留原有八步顺序，预加载重试和重复等待不会重新发起启动拉取。Network Inspector 的“启动时GM配置请求超时时间（秒）”默认每次请求 5 秒，主备请求和重试分别计时，整轮总耗时可能超过 5 秒。

## 跨包文档定位

本页链接相对于当前文件；独立包之间不假定相邻安装。先用 `UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()` 按下表精确包名查找，以该包的 `resolvedPath` 为根读取文档入口，再按包内相对链接进入目标。无 Editor 通道时使用当前有效 manifest 或解析工具给出的实际包位置。

| 包名 | 包内文档入口 | 本文引用的包内位置 |
|---|---|---|
| `com.solotopia.nova.framework` | `Docs/INDEX.md` | `Docs/Onboarding/PROJECT_STRUCTURE.md` |
| `com.solotopia.nova.framework.sdk.aihelp` | `Nova/Doc/INDEX.md` | `Nova/Doc/AIHelpPlugin.md`<br>`Nova/Doc/INDEX.md` |
