# DebugFoundation

RuntimeDebugger 依赖库，包含 MonoBehaviour 基类、服务定位器、UI 工具等基础组件。
本包为精简版，仅保留 RuntimeDebugger 所需的最小子集。

## 主要模块

| 模块 | 路径 | 说明 |
|------|------|------|
| 组件基类 | `Scripts/Components/` | `DebugMonoBehaviour`、`DebugMonoBehaviourEx`（[RequiredField] 自动注入）、`DebugSingleton<T>` |
| 服务定位器 | `Scripts/Service/` | `DebugServiceRegistry`（IoC 容器）、`[Service]`/`[ServiceSelector]` 特性 |
| 属性/方法包装 | `Scripts/Helpers/` | `PropertyReference`（getter/setter/ValueChanged）、`MethodReference` |
| UI 工具 | `Scripts/UI/` | `VirtualVerticalLayoutGroup`（虚拟滚动）、`FlowLayoutGroup`、`StyleSheet` |
| 扩展方法 | `Scripts/Extensions/` | GameObject / Transform / String / List / Float 扩展 |
| 外部依赖 | `External/` | `MiniJSON`（轻量 JSON 解析） |

## 编译归属

不再维护独立 asmdef，随 `Assets/Framework/Scripts/Runtime/NovaFramework.Runtime.asmdef` 统一编译。
