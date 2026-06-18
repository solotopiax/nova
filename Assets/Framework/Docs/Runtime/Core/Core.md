# Runtime/Core

框架基础层，不依赖任何 Component，可在任意层级使用。

## 子目录

| 目录 | 说明 |
|------|------|
| [`Definitions/`](Definitions/Definitions.md) | 业务枚举（渠道类型、地区类型） |
| [`Extensions/`](Extensions/Extensions.md) | C# 和 Unity 扩展方法 |
| [`Interfaces/`](Interfaces/Interfaces.md) | 基础层公共接口 |
| [`Collections/`](Collections/Structures.md) | 自定义数据结构（链表/有序字典/多值字典等） |
| [`Table/`](Table/DataReceiver.md) | DataReceiver：异步加载 AB 数据的通用基类 |
| [`Log/`](Log/Log.md) | Log 静态日志门面 |
| [`Reference/`](Reference/ReferencePool.md) | ReferencePool 引用池 |
| [`Txt/`](Txt/Txt.md) | Txt 文本格式化工具 |
| [`Fsm/`](Fsm/FsmState.md) | 有限状态机（FsmState 状态基类 + IFsm/Fsm 实现） |
