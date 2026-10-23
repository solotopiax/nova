# FirebaseDemo

示例使用 `DemoFirebaseView` 演示 Firebase 标识、事件参数及 Push Task。登录按钮调用 `Nova.Network.Kit<Login>().Async(...)`；GameLogin 在合法成功响应中已向 SDK 交付注册或登录，页面只更新反馈与 Push Task 按钮，不再次调用 `Nova.SDK.Login`。

页面提供一个默认空的 Topic 输入框，以及独立“订阅 Topic”“退订 Topic”按钮，绑定字段分别为 `m_TopicInput`、`m_SubscribeTopicButton`、`m_UnsubscribeTopicButton`。输入完整 Topic 名称后显式点击，页面裁剪首尾空白并调用现有 `FirebasePlugin.SetTopicSubscribed(topic, true/false)`；空输入只给普通提示。打开页面和关闭页面只接线或解绑按钮，不自动订阅或退订。

`SetTopicSubscribed` 返回 `void`，页面“请求已派发”不代表成功；Android/iOS 必须等 SDK 实际订阅或退订回执，Editor 的平台空操作不能作为真实验收。每次显式点击都会派发一次，应先等回执再执行反向操作；关闭页面不会取消已派发的 SDK 请求。手动操作不改默认 Topic 的 `FirebaseDefaultTopics/BaseState`、`CountryState` 自动管理快照；手动退订默认 Topic 后，不应把旧快照或恢复原 APK 当作远端订阅已自动恢复。测试清理应对本轮实际新增 Topic 显式退订并核 SDK 回执，不清除其它订阅、Token 或账号。

`RegisterAndLogin` 包含本次登录，`Login` 每次有效调用都会发送通知；重复调用会产生额外会话及上报，调用方应复用已有登录路径。Firebase 初始化、权限、Token、任务缓存及真实送达分别按已安装包的 `Nova/Doc/INDEX.md` 验收。本地编译不证明厂商数据或 Push Task 已送达。

页面登录等待没有随关闭取消；普通失败或异常也不会统一清除旧的本页登录标志，按钮是否启用不能代表当前 NetService 身份仍有效。导入重写、版本目录清理与 BaseDemoView 共同限制见 Framework `Docs/Onboarding/PROJECT_STRUCTURE.md` 的 Sample 小节。

Push Task 的发送时点重新读取当前时间，界面预览不冻结最终任务时间；排队成功也不是服务端接受或设备送达。TaskKey 表示任务身份，不能代替账号隔离，跨账号复用前应核对后端契约。

通过 Codex 可描述“接入登录后的通知”“检查为什么没有 Token”“配置测试事件”。从实际安装 Framework 的 `Docs/START_HERE.md`、`Agents/INDEX.md` 复用 SDK/Kit、配置、网络和诊断能力；入口场景使用实际导入目录中的 `FirebaseDemo.unity`。先核对已安装依赖、目标平台、应用标识、厂商配置及当前 Runtime，不依赖主仓 Minds，也不为每个按钮新建 Skill。账号登录、权限请求、上报和真实通知任务必须按具体目标授权；权限已授予、Token 已生成和消息已送达分别验收。
