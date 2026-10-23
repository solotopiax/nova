# NativeComponentInspector

**类签名**：`[CustomEditor(typeof(NativeComponent))] internal sealed partial class NativeComponentInspector : BaseComponentInspector`  
**命名空间**：`NovaFramework.Editor`  
**目标组件**：`NovaFramework.Runtime.NativeComponent`

Native Inspector 只管理 Manager 实现选择与模块边界说明；它不查询、不请求通知权限，也不触发系统设置跳转。`NativeManagerConfig.EnableAndroidAppInstall` 默认关闭；Inspector 暴露显式 APK 安装总门，不自动修改项目现有配置。

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
| `m_NativeManagerConfig.EnableAndroidAppInstall` | `m_EnableAndroidAppInstall` | `EditorUtil.Draw.PropertyField`，“允许 Android APK 安装”，默认关闭 |

`BaseComponentInspector` 先负责 `serializedObject.Update()` 和只读 DevelopMode 头部；子类随后依次绘制 Manager 类型、安装开关、三条编号 HelpBox，最后调用 `FinalRefreshInspectorGUI()`。

HelpBox 说明通知与 Token 边界、Android 9+ 来源许可和系统用户确认，以及下载/安装分离与当前 signer/递增版本限制。开启安装总门后，实际 Build Scene 的生效 Native 配置使构建声明来源安装权限；关闭时不声明此权限。它不获得静默安装能力。

## 使用与验证

- 正常情况下，类型选择器应包含默认 `NovaFramework.Runtime.NativeManager`。
- Missing 类型、空配置与多选场景应保持 Inspector 的普通序列化行为，不得在绘制回调中写入运行时状态。
- 修改 Inspector 时必须同时检查 Runtime 声明、`SerializedProperty` 绑定与绘制顺序，并在 Unity 中实际打开 Inspector 验证布局和持久化。

## 关联文档

- [NativeComponent.md](../../../Runtime/Modules/Native/NativeComponent.md)
- [INativeManager.md](../../../Runtime/Modules/Native/Managers/Interfaces/INativeManager.md)
- [BaseComponentInspector.md](../BaseComponentInspector.md)
