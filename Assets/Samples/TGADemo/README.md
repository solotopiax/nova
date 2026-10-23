# TGADemo

示例使用 `DemoTGAView` 演示登录、标识、事件、用户属性和公共属性。登录按钮仅调用 `Nova.Network.Kit<Login>().Async(...)` 并展示响应；GameLogin 按服务端 `IsNewAccount` 调用 `Nova.SDK.RegisterAndLogin` 或 `Nova.SDK.Login`，页面不重复发送登录通知。

本页登录等待没有随 UIView 关闭取消，关页不撤销已发的游戏账号或 SDK 操作。导入路径、旧版本目录清理和 BaseDemoView 的共同限制见 Framework `Docs/Onboarding/PROJECT_STRUCTURE.md` 的 Sample 小节。

表单通常把属性值作为字符串；UserAdd 另按 InvariantCulture 解析 double，但未增加有限值或正数检查。UserAppend 仅按逗号拆分并 Trim、丢弃空项，没有 JSON 数组或逗号转义协议。清空页面参数只清本页字典，静态与动态公共属性各有独立清理入口。UserDelete 会调用统计 SDK 的用户删除能力，应核对实际账号与环境；它不是清理本页显示或本地登录缓存。

`RegisterAndLogin` 已包含一次登录，`Login` 每次有效调用都会交付会话与通知。复用现有账号入口，不能靠 SDK 内部去重掩盖调用方重复。事件与属性的含义、归属及上报按已安装包 `Nova/Doc/INDEX.md` 与 Tracks 规范核对；本地调用或编译不证明统计后台已收到数据。

通过 Codex 可描述“给购买事件加一个已存在的属性说明”“接入登录统计”“检查后台为什么没有数据”。从实际安装 Framework 的 `Docs/START_HERE.md`、`Agents/INDEX.md` 复用 SDK/Kit、配置、网络与 Tracks reference，入口场景使用实际导入目录中的 `TGADemo.unity`。先核对插件、应用物料、目标 Runtime、既有表源归属和隐私启用条件；消费项目无需主仓 Minds。已有单元格文字修改与新增事件、结构治理、业务埋点和汇总生成的支持边界分别读取，不把文档入口当作全部结构操作已自动化。登录和真实上报需针对具体环境授权，后台接收仍需厂商证据。
