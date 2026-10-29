# WebSocket 业务接入

仅在长连接业务分支读取本页。HTTP的HostKey/NetCmd、AES业务Service、UWR主备和`network.export`不能证明WebSocket已接入；已存在的表/Proto仅在当前项目确实用它提供地址、命令或消息时复用对应源与正式导出。

## 最少输入与真实调用

从项目读出NetworkComponent、活动平台、当前业务Service/流程/页面、程序集和已有消息处理，冻结精确`ws://`或`wss://`地址、`WebSocketScope.NetChannelType.Tcp`或`TcpPb`、命令ID与请求/响应格式、账号认证规则、心跳响应、服务端ack、重放/幂等、连接拥有者、断开时机、允许写集及测试服务器/账号。用户名、Token和载荷敏感值不进入Prompt、日志或确认Diff。业务规则未知时不按字符串/Proto示例猜协议。

直接使用授权项目自有业务源的`workspace-edit`实现以下公开调用；API保持C#调用面，不伪装成可调度Action：

| 目标 | 当前公开入口 | 返回/证据含义 |
|---|---|---|
| 连接、手动重连、退出 | `Nova.Network.ConnectServer(type,address,autoReconnect)` / `ReconnectServer(type,address)` / `DisconnectServer(type,address)` | void只表示发起；主动断开终止自动重连，退出时由同一连接拥有者断开 |
| 状态 | `IsWebSocketConnected` / `IsWebSocketAuthenticatedSuccess` / `WebSocketNetChannels` | 连接与认证分开；按类型+地址核对通道，int回调参数是当前通道索引，不是持久身份 |
| 消息 | `CreateMessage(type)` / `SendMessage(type,address,message)` / `RecycleMessage(message)` | SendMessage=true只表示进入发送或离线缓冲，需服务端ack证明业务完成 |
| 观察 | `OnWebSocketBeginConnect/ConnectSuccess/ConnectFail/Disconnect/ReconnectFailed/AuthenticateSuccess/AuthenticateFail/ReceiveMessage/SendMessage` | 生命周期成对订阅/注销，不依赖未证实会发出的单一事件 |

`TcpMessage.Initialize(channel,commandID,List<string>)`会按现有帧格式编码字符串；`TcpPbMessage.Initialize(channel,commandID,byte[])`消费已确认正式Proto字节，序列号由通道分配。初始化需要同一真实`NetChannelBase`，不能造临时通道或随便写序列号。消息帧含4字节CommandID、4字节SequenceID与body，不等于任意JSON WebSocket服务；确认服务端协议兼容后才能使用。

## 生命周期与消息所有权

主线程连接/发送，事件由现有Manager轮询回主线程。连接先订阅再发起，迟到事件按通道、地址、账号及本次请求匹配。界面关闭未必意味着全局长连接结束，先核实Service还是页面拥有连接，避免每次开页面创建一条；当前`ConnectServer`只复用同类型同地址的未连接通道，重复调用已连接目标可能创建额外通道。

`CreateMessage`取得池对象。`SendMessage=true`后由通道持有，禁止业务马上回池、修改或另发同一对象；false且当前源码确认未入队时由创建者回收。异常/断线导致归属不确定时先查本次调用与队列事实，不重复回池或盲目重发。发送和接收事件参数是借用对象：当前通道在回调返回后自动回收，业务订阅者不再次`RecycleMessage`，也不将消息或其可变载荷留给异步消费者；回调内拷贝业务需要的CommandID、SequenceID和载荷，转换业务事件时使用独立数据，捕获业务处理异常避免中断通道收尾。

离线缓冲可能在恢复认证后发送；网络断开不等于请求未到达。领奖、保存或交易复用项目已确认业务ID/服务端ack与去重台账，不以SequenceID自动代替业务幂等身份。Service/Procedure退出取消本地等待、注销同一handler，按拥有者主动断开；公开连接API没有CancellationToken参数，仅取消本地等待不会关闭Socket。主动`DisconnectServer`会取消该通道正在进行的握手，连接超时也会取消握手并释放Socket；两者都不证明已发业务请求的服务端工作已停止。显式重连采用新连接轮次，旧连接/认证续体及旧Socket排队响应不能代替新连接认证；业务仍须匹配自己的账号与请求身份。

关闭或显式连接准备只清理在线队列中的旧认证/心跳消息，普通业务队列保留既有发送/重放规则；发送中关闭仍可能已经到达服务端，不自动重试敏感业务。通道终止等待实际异步收发循环并回收剩余队列，但`WebSocketManager.Shutdown()`公开返回仍只表示异步终止已发起，不能据此宣称所有平台生命周期已清场；测试连接须按实际断开、主线程收尾与探针资源验证。

## 配置与当前实现缺口

当前`WebSocketSettings`声明的默认值为ConnectTimeout=10秒、AuthenticateTimeout=10秒、HeartBeatTimeInterval=20秒、HeartBeatTimeout=10秒、EnableAutoReconnect=true、AutoReconnectMaxCounter=5、AutoReconnectTimeInterval=3秒。已保存值优先；通过`nova-project-edit-settings`的目标定位与共享Unity MCP修改授权组件字段，等待重启生效，不重新Initialize已运行Manager。`AutoReconnectFailedUIAssetLocation`当前只透传，不能声称自动显示失败弹窗；需要提示时接已有UI业务入口。

需要真实认证/心跳时先核实已安装版本初始化：`NetworkComponent.Start`当前没有注入`WebSocketManagerConfig.SpecialMessageCreator`，公开门面没有对应动态setter；不靠反射、二次Initialize、直接改通道状态绕过。已有项目公开初始化入口明确负责该委托时复用，否则报告具体缺口。Tcp认证按5002帧内JSON的success判定；TcpPb认证按6002及`AuthenticateResponseValidator`判定，该validator为空会接受对应认证消息，不能作为游戏服已校验账号的证据。

当前认证状态链为：通道初始化不等待响应，连接成功设置需要认证；真实认证消息入队前清除需要发送标记并开始等待，成功响应结束等待；无委托或返回null时跳过账号认证，完成的是免认证通道状态，不是服务端账号校验。每次自动重连成功后重新启动认证和心跳。首次连接失败当前只触发连接失败事件，尚未启动自动重连协程；业务需要初连重试时复用自己的有界重试入口，不把设置开启视为初连自动重试保证。`OnAuthenticateFail`当前声明中未找到调用点，不能仅等该事件。缺少真实认证创建入口时，免认证业务可按其约定验收；要求账号认证的业务保持partial/blocked并指出初始化缺口。Framework修复不在本消费Operation写集。

正常关闭控制帧已在解包前处理；畸形业务帧仍可能在池对象创建后触发解析异常并遗漏回收。若当前协议来源或失败证据命中此边界，按Framework传输实现缺口报告，不能通过消费项目反射修改或把正常帧测试当作畸形输入健壮性验收。

## 验证、产物与恢复

交付真实业务Service/入口、订阅与注销、连接身份、消息编码/结果分发、错误/超时/退出、ack及幂等消费；必要配置/Proto依赖按对应Operation维护权威源→正式导出→生成访问→Config→运行链。先编译，再从真实入口观察目标通道唯一、连接及认证、请求/响应/ack、主动退出与注销。范围含重连时在明确授权测试服务器上使用`TestDisconnectServerAbnormally`，验证次数、离线重放与去重；该测试不用于生产，不能拿主动断开代替异常断开。

Play只证明该平台与冻结服务器；Editor成功不证明WebGL浏览器桥或移动网络。真实账号、状态写入、权限、费用及自动认证/心跳请求需精确外部授权，缺设备/服务器/协议或安全初始化入口明确标partial/blocked，不用本地Mock验收真服务。执行后断开本轮拥有的测试连接、注销探针、清理runner/meta/日志/测试状态与必要Refresh，恢复本轮临时配置，保留正式业务源与用户原有工作。

仅按命中问题读取匹配版本`Docs/Runtime/Modules/Network/NetworkComponent.md`、`WebSocketManager/WebSocketManager.md`、`WebSocketManager/IWebSocketManager.md`与必要消息/通道源码；本页依据源码查阅，不是当前消费项目live验收。

## 已有 MainDemo echo 分支

开发仓或已导入 MainDemo 的项目可直接查阅其 `DemoNetworkView` 和 Sample README，复用现有 URL 输入与 Connect 按钮。有效 ws/wss 输入覆盖目标，非 WebSocket 输入保留原 echo 默认值；先读取并冻结目标，不能把打开页面视为授权公网请求。示例以免认证二进制 Tcp 帧完整回显为显式 ack：`commandID=790001`、通道真实分配的 sequence 和唯一随机 nonce 必须全部匹配，不能据此推定游戏服鉴权或生产协议。

该页面当前直接使用 `IWebSocketManager`，属于存量 Manager 依赖示例，不是新业务可复制的入口。新增或修改普通业务时使用 `Nova.Network` / `NetworkComponent` 已有的连接、订阅与发送门面；所需状态或终态缺少合规访问入口时报告具体缺口，不取 Manager 或复制底层释放判断。示例的时序策略可单独查阅：先订阅再连接，等待认证就绪再发送；已连接后重复点击是新 echo，连接或待 ack 期间不创建第二个请求；发送超时不重放，退出只处理自己拥有的等待、订阅和通道。实际接入仍须本次验证。

页面不接管其它会话的活动通道，也不接管未知归属的已断开通道；普通业务缓冲可能仍存在。重开只复用本 Sample 先前标记并释放的通道，且还须公开底层释放终态：非 WebGL 的 `WebSocket==null`，WebGL 的 `WebSocketInstanceID==0`。内部 `IsClosing` 既不向业务程序集公开，关闭完成后也不立即清零，不能用反射或假设替代此边界。真实控制服务器回显可验证本分支，结果仅覆盖该平台、目标与 nonce 约定，不能填补其它业务的账号、心跳、幂等或设备证据。
