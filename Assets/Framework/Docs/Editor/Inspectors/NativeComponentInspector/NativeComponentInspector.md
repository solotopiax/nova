# NativeComponentInspector

**类签名**：`[CustomEditor(typeof(NativeComponent))] internal sealed partial class NativeComponentInspector : BaseComponentInspector`  
**命名空间**：`NovaFramework.Editor`  
**目标组件**：`NovaFramework.Runtime.NativeComponent`

Native Inspector 只管理 Manager 实现选择与模块边界说明；它不查询、不请求通知权限，也不触发系统设置跳转。当前为空的 `NativeManagerConfig` 仍由 Runtime 序列化并传给 Manager，但不在 Inspector 中显示无内容的配置行。

## 文件

| 文件 | 说明 |
|---|---|
| `NativeComponentInspector.cs` | `OnEnable` 绑定属性、收集 `INativeManager` 实现、`OnInspectorGUI` 调度 |
| `NativeComponentInspector.Visitors.cs` | `SerializedProperty` 与类型列表字段 |
| `NativeComponentInspector.Methods.cs` | `DrawConfigs()` 绘制实现 |

## 序列化绑定与绘制顺序

| Runtime 字段 | Inspector 字段 | 绘制方式 |
|---|---|---|
| `m_CurNativeManagerTypeName` | `m_CurNativeManagerTypeName` | `EditorUtil.Draw.TypesSelector`，枚举 `INativeManager` 实现类型 |

`BaseComponentInspector` 先负责 `serializedObject.Update()` 和只读 DevelopMode 头部；子类随后依次绘制 Manager 类型、两条 HelpBox，最后调用 `FinalRefreshInspectorGUI()`。

HelpBox 固定说明两项边界：

1. 通知权限只在业务显式调用 `RequestNotificationPermissionAsync` 时请求，框架启动不会自动弹窗。
2. APNs / FCM Token 与消息生命周期由对应 SDK 管理，不属于 Native 模块。

## 使用与验证

- 正常情况下，类型选择器应包含默认 `NovaFramework.Runtime.NativeManager`。
- Missing 类型、空配置与多选场景应保持 Inspector 的普通序列化行为，不得在绘制回调中写入运行时状态。
- 修改 Inspector 时必须同时检查 Runtime 声明、`SerializedProperty` 绑定与绘制顺序，并在 Unity 中实际打开 Inspector 验证布局和持久化。

## 关联文档

- [NativeComponent.md](../../../Runtime/Modules/Native/NativeComponent.md)
- [INativeManager.md](../../../Runtime/Modules/Native/Managers/Interfaces/INativeManager.md)
- [BaseComponentInspector.md](../BaseComponentInspector.md)
