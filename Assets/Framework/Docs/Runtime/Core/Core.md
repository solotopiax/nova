# Runtime/Core

框架基础层，不依赖任何 Component，可在任意层级使用。

编译边界以 `Scripts/Runtime/NovaFramework.Runtime.asmdef` 当前 name、references、平台、define 与引擎/unsafe 开关为准；目录和 namespace 不等于程序集身份，生成 DLL 与实际加载阶段还应分别核对。依赖与最低 Unity 版本以 Framework 的 `package.json` 以及消费项目 Resolve 后的有效包为准，不从函数说明复制一份版本表。Nova MCP 包的 `package.json` 另声明自身依赖与 `nova.requiredFrameworkApiVersion`；安装版本不等于已连接 Provider 或已暴露 Action。精确 GUID、调用位置和清单字段在命中任务时读取真实输入，本文不提供自动消费者反查或静态配置即运行成功的承诺。

## 子目录

| 目录 | 说明 |
|------|------|
| [`Definitions/`](Definitions/Definitions.md) | 业务枚举（渠道类型、平台类型、开发模式） |
| [`Extensions/`](Extensions/Extensions.md) | C# 和 Unity 扩展方法 |
| [`Interfaces/`](Interfaces/Interfaces.md) | 基础层公共接口 |
| [`Path/`](Path/Path.md) | Streaming、Persistent、Cache、Hotfix 与 Persist 路径约定 |
| [`Collections/`](Collections/Structures.md) | 自定义数据结构（链表/有序字典/多值字典等） |
| [`Table/`](Table/DataReceiver.md) | DataReceiver：异步加载 AB 数据的通用基类 |
| [`Log/`](Log/Log.md) | Log 静态日志门面 |
| [`Reference/`](Reference/ReferencePool.md) | ReferencePool 引用池 |
| [`Txt/`](Txt/Txt.md) | Txt 文本格式化工具 |
| [`Fsm/`](Fsm/FsmState.md) | 有限状态机（FsmState 状态基类 + IFsm/Fsm 实现） |

## HTTP 候选规划与执行游标

`Core/HttpFallback` 是 App、Asset、Network 共用的内部纯规划层。它不发送请求、不裁决业务成功、不 Abort UWR，也不持久化偏好；各模块持有独立游标并负责实际网络与资源生命周期。`HttpFallbackCandidate` 保存完整 URL、规范端点、原配置角色和原索引；`HttpFallbackStep` 保存候选及从零开始的重试周期、轮次、候选位置、候选总数和物理发送序号，`AdditionalRetriesUsed` 等于周期索引。值构造器仅赋值，不验证 URL、负坐标或跨字段一致性；读取这些值不证明已经发送或成功。

`HttpFallbackPlanner.Build` 跳过空白 URL，按完整字符串 Ordinal 去重，所以 path/query 的大小写不同仍是不同候选。角色由去重前索引决定：0 为 Primary、1 为 Fallback，其余为 Other；偏好只把已存在的匹配端点移首，不修改原角色。`GetEndpointId` 仅接受绝对 HTTP(S) URL，去除 path/query/fragment 与默认端口，非法输入返回 `null`；Build 不因此拒绝其非空 URL，也不证明地址可达。空/null 列表形成零候选计划，空偏好不调整顺序。

`HttpFallbackPolicy` 构造要求轮数 R≥1、额外重试 K≥0；计划按 checked long 计算 C×R×(K+1)，溢出抛出。计划直接保存候选数组并以 IReadOnlyList 暴露，不复制数组，内部调用方不能把接口视为强制不可变；`default` 策略也不会在计划构造时重新校验。`RetryCycleCount` 是 int 的 K+1，在极端值可能溢出，不能代替 long 的计划总数。`CreateCursor` 每次返回独立游标，共享计划只适合保持数组不变后读取。

游标按重试周期→轮→候选推进。`TryBeginNext` 在已有在途候选时抛异常，终止或耗尽时返回 false；`RejectCurrent` 只允许在途状态并推进下一序号，`CompleteCurrent` 同样要求在途，但 Completed 可以代表正式 HTTP 错误。`Cancel` 只修改游标状态，已 Completed/Exhausted 时跳过，不取消实际网络任务；终止后 Current 仍保留上一坐标，应联合 State 判断。auto setter 自身不验证状态转换。

`HttpFallbackPreferenceStore` 在锁内按 Ordinal scope key 隔离进程偏好：Capture 空键返回空快照，MarkSuccess 的键或端点为空时跳过；成功定义由调用模块决定。更新版本用于 ClearIfUnchanged，避免旧请求清除更新后的偏好；普通请求耗尽不应清除最近成功。ClearAll 清字典但不重置版本，版本递增无溢出保护。快照 HasValue 仅看端点非空及版本>0，不验证 scope 或当前 Store 是否仍有该版本；手工构造空 scope 的“有值”快照可能在清除时触发字典异常。真实 URL、Host/包隔离键与何时记成功，应继续读取对应模块的当前源码和配置。

## 可选核心扩展

内存数值防护由独立包 `com.solotopia.nova.framework.memory.protection` 提供，主 Framework 不包含其代码或强制依赖。请从 [MemoryProtection 包文档](../../../../../UPMPackages/com.solotopia.nova.framework.memory.protection/Nova/Docs/INDEX.md) 安装和接入；业务 asmdef 需显式引用 `NovaFramework.MemoryProtection.Runtime`。
