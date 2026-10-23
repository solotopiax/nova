# 游戏业务 Service 与流程复用

仅在游戏控制用户目标命中时读取本页。这个分支实现“实际入口→项目业务Service→既有Procedure/FSM→结果与退出”，复用现有Skill，不为开始、暂停、结算、重开或每个Service新增Skill。

## 从项目读取与冻结

先查真实入口、当前状态、项目自有Service/控制器、public接口、程序集、数据拥有者和已注册Procedure。当前Framework不存在经核实的`Nova.Game`门面，不靠这个名称造调用。冻结一个目标与精确source/既有入口、合法起点/终点、输入/结果/错误、重复点击和并发策略、局/账号/操作身份、离开/销毁/取消、资源/事件/存档与最低Play探针。路径、类型、当前值可从项目读取；玩法规则、输赢/奖励、保留进度或重开取舍有歧义时只补必要业务决定。

复用当前项目已确认Service的真实公开调用；它不存在且创建自有实现属于已授权业务目标时，保持项目既有组织，在最小业务源码范围实现明确输入/输出契约，不创建另一套Framework Manager、Service Locator、全局FSM或新注册机制。缺少业务规则或外部协议时仍可完成不依赖缺项的已授权本地入口/生命周期，整体partial并列出具体缺项。

## 真实接口与生命周期

`ProcedureBase`继承`FsmState<IProcedureManager>`。业务override按已安装版本和当前程序集的访问规则保留`base`，进入先由base创建本次`CancellationToken`。OnLeave只Cancel；下次进入和OnDestroy才Dispose。关键await后检查本次取消及局/账号/请求身份，避免旧局完成回调覆盖新局。

状态内复用`ChangeState<TProcedure>(procedureOwner)`或`ChangeState(procedureOwner,type)`，当前同步触发新状态OnEnter，放在已确认OnUpdate转换点的最后并停止旧状态逻辑。跨状态数据使用已有Service或`IFsm<IProcedureManager>.SetData/GetData<T>/RemoveData`，明确Key/type/所有者和清理时机；黑板GetData不存在返回default，不把默认值当已完成业务。`Nova.Procedure.CurrentProcedure/HasProcedure<T>/GetProcedure<T>/GetProcedure(Type)`用于查询，不臆造`Nova.Procedure.ChangeState`门面，也不通过反射强制跳转。

局/页面Service拥有的长任务、资源句柄、监听和计时由同一拥有者释放；Procedure仅取消其拥有的等待。暂停若涉及Time.timeScale、输入、音频、动画、计时或联网，按用户目标明确哪些跟着暂停，不能从“暂停”自动推断全局时间缩放。结算/奖励/发货复用真实业务操作ID及幂等台账，失败或断线不盲目重放；重开重置哪些数据、存档还是跨局状态分别核对。

## 条件复用与交付

按钮/UIView接`nova-project-update-ui-view`，事件接`nova-project-integrate-event`，进度接`nova-project-integrate-persistence`，通信接`nova-project-integrate-network-api`，广告/支付/账号接`nova-project-onboard-sdk-kit`；仅读取当前命中分支的contract。表驱动数据沿权威源→正式导出→Config→运行消费；Prefab/Scene绑定经Unity MCP，资源加载沿已有资源Operation，不在本workspace-only Operation悄悄扩大序列化写集。

交付可达业务入口与真实Service调用、输入/结果/错误、有效状态转换、取消/退出、监听/资源清理、重复触发/局身份及数据持久性。先Unity编译，再从原入口Play验证目标前后行为与重复/取消探针；只写方法、只查询当前Procedure或只看到日志不证明游戏控制可用。外部账号、交易、云写等按精确独立授权验收；没有真实服务证据保持partial，Mock只证明本地替身。

清理本轮探针、测试状态、runner/meta和日志，必要Refresh并核对原入口/场景恢复及目标diff。按需读匹配版本`Docs/Runtime/Modules/Procedure/ProcedureBase.md`与`Docs/Runtime/Core/Fsm/{FsmState,IFsm,Fsm}.md`，只展开实际Service/source与调用方。当前分支契约源于源码查阅，不是某消费游戏已执行验收。
