# IAPDemo

示例通过 `DemoIAPView` 登录后展示当前工程已安装的支付商店：移动官方支付、第三方支付和金券支付。入口场景为 `IAPDemo.unity`，入口流程为 `ProcedurePreload`。

## 接入与复用

- 保留 IAP Core 与 GameLogin 等 Sample 依赖，按业务需要安装 `com.solotopia.nova.framework.sdk.iap.mobile`、`com.solotopia.nova.framework.sdk.iap.thirdpay` 或 `com.solotopia.nova.framework.sdk.iap.voucher`。可选模块由各自程序集的包版本宏控制；Mobile 与 ThirdPay 不在 WebGL 编译。未安装的商店不会被发现，全部缺失时页面显示占位提示。
- 在 ConfigWindow 配置 `IAPPluginConfig.Products/StoreConfigs`，保存 Master 并导出目标 Runtime。商品维护使用 IAP 的 SKU Excel 模板与导入，不走 Luban Table 导出；网络指令、验单与商户配置按各支付包的 `Nova/Doc` 或 `Nova/Docs` 核对。
- 通过 Codex 接入时复用 `nova-project-onboard-sdk-kit` 的支付 reference、配置和网络操作；页面绑定复用已有 UI 操作。各商店统一调用 `IAPPlugin.PayAsync<T>`，专属查询通过 `TryGetCapability` 获取，不为每个 Demo 按钮新增 Skill 或直接反射调用私有方法。
- ThirdPay Panel 只展示消耗品 `1、2`，不会生成非消耗品 `5` 或订阅 `3、4` 的 Demo 支付卡；该约束只作用于示例展示，不限制 ThirdPay Runtime。额外的 `ID1 [首充+额外赠送]` 测试卡会携带首充物品 `1001 × 1` 和额外赠送物品 `1002 × 2`，普通支付卡不携带奖励参数。

用户只需描述“给商品配置价格和商店”“做一个购买页面”“检查为什么验单失败”。Codex 从实际安装 Framework 的 `Docs/START_HERE.md`、`Agents/INDEX.md` 读取共享底线与路由，再读取任务需要的支付 reference；商品 Excel 与内联配置复用确定性准备、配置保存和验证链，不需记住 Skill ID。查阅、模拟和实际下单、恢复、金券变更的授权与证据分别核对。

## 页面关闭与异步结果

每次页面会话持有一个 `DemoIAPBridge`。关闭时先使页面会话失效、释放 Bridge 并取消绑定到它的异步等待，再清理商店模块。重新打开会创建另一 Bridge；登录、查询、支付、恢复购买和金券刷新在等待后核对原 Bridge，旧任务不会改新页面的登录状态、商品缓存、余额、按钮或反馈。

应用退出时，`DemoIAPView.OnApplicationQuit` 会先标记页面失效并释放 Bridge，退订 IAP 全局事件。随后 SDK 清理期间产生的支付失败、恢复购买事件或补单结果不会再向正在销毁的反馈区创建 UI 节点；商店订单和验单仍由 IAP 核心按原有流程处理。

GameLogin 当前没有页面取消参数，关闭页面不会取消其身份请求，只会丢弃旧页面的展示结果。关闭也不等于撤销商店订单或支付；交易、验单、补单与履约仍由既有 IAP 核心和业务服务处理。取消后不向关闭页面写提示；非取消异常仍保留在当前反馈或 Console，不因页面已关闭而静默丢失。

## 验证边界

各模块的 Refresh 含义不同：Mobile 更新平台商品信息，ThirdPay 查询当前支付快照，Voucher 拉取远端钱包，不能把统一按钮当作统一商店就绪证明。Bridge 展示的 InitResult、PaySuccess、PayFailed、SubscriptionRestored、NonConsumeRestored 是可重放事件，订阅时可能立即收到缓存；展示回调不负责给玩家发货。错误应同时保留 ErrorSource 与 ErrorCode，避免把不同域的同值错误码混为一类。

自定义 Receipt 参数原样交给编码链，输入框的占位文字不构成校验或可信订单信息；订单号、回执参数和钱包调试输出应脱敏。模块只从当前已加载程序集发现实现，缺失模板或模块实例创建失败可能留下部分面板，不提供安装、资源加载或跨模块初始化的事务保证。

示例价格按 invariant decimal 读取并要求大于零，再乘 1000、以 AwayFromZero 取整为 long；此处 mills 表示千分之一货币单位，不是“分”。溢出或取整后非正会拒绝。取得 IAPPlugin 或显示平台名称也不代表对应商店已连接；可选模块发现只反映已编译程序集，不自动安装缺失包。导入和辅助工具的共同边界见 Framework `Docs/Onboarding/PROJECT_STRUCTURE.md` 的 Sample 小节。

编译和 Editor 模拟只能验证本地调用与展示，真实支付、Restore、服务端验单、幂等发货和商户资格仍需相应平台及业务服务验收。金券测试发放会修改服务端测试资产，仅在明确授权的测试环境使用。生命周期验收需实际覆盖请求进行中关闭、立即重开及旧任务结束三个阶段。
