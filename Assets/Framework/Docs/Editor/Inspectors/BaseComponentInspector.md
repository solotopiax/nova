# BaseComponentInspector

**类签名**：`public abstract class BaseComponentInspector : UnityEditor.Editor`  
**命名空间**：`NovaFramework.Editor`

## 作用

所有 Framework 组件 Inspector 的基础父类。  
它负责 Inspector 的公共准备工作，但不负责具体模块的配置绘制，也不提供统一 RuntimeDrawer 调度框架。

## 当前职责

- 维护编译状态检测：`OnCompileStart()` / `OnCompileComplete()`
- 缓存并设置 `EditorGUIUtility.labelWidth`
- 在 `OnInspectorGUI()` 开头执行 `serializedObject.Update()`
- 在 Inspector 顶部统一绘制只读 `DevelopMode` 场景快照标签，并在其下绘制一条分隔线
- 提供 `FinalRefreshInspectorGUI()`，统一 `ApplyModifiedProperties()` 与 `Repaint()`
- 提供模板路径辅助方法，如 `InitializeTemplatePath()`、`DrawTemplatePathHint()`

## 关键成员

| 成员 | 说明 |
|---|---|
| `InspectorLabelWidth` | 子类可覆写的默认 label 宽度，默认 `180f` |
| `TemplateFileName` | 需要模板功能的子类可覆写 |
| `TemplateLabelWidth` | 模板路径提示行的标签宽度 |
| `m_FeishuDocumentUrl` | 预留文档链接字段，当前基类按钮逻辑处于注释状态 |
| `m_TemplatePath` | 模板路径的 `SerializedProperty` |

## OnInspectorGUI 的当前事实

基类 `OnInspectorGUI()` 当前会做：

1. 缓存并设置 `labelWidth`
2. 处理编译状态切换
3. 鼠标按下时清理焦点
4. 调用 `serializedObject.Update()`
5. 若目标对象存在 `m_DevelopMode` 序列化字段，则绘制只读标签：`开发模式：Xxx（随 Config 导出自动回写，不可手动修改）`
6. 运行中或对象有修改时请求 `Repaint()`

它**不会**：

- 自动调用子类的配置绘制
- 自动遍历 RuntimeDrawer 列表
- 自动 `ApplyModifiedProperties()`

因此子类通常需要在自己的 `OnInspectorGUI()` 末尾显式调用 `FinalRefreshInspectorGUI()`。

## DevelopMode 头部

- 基类通过 `serializedObject.FindProperty("m_DevelopMode")` 自动探测场景快照字段
- 该展示是只读标签，不提供编辑入口
- 组间分隔线只由基类在头部下方绘制一次；业务 Inspector 内部是否画线仍由各子类自行控制

## 与 RuntimeDrawer 的关系

`BaseComponentInspector` 当前不持有 `m_RuntimeDrawers` 一类统一字段。  
如果某个 Inspector 需要运行时附加面板，应由该 Inspector 自己维护列表并自行调用。`EventComponentInspector` 是当前示例。

## 常见子类模式

```csharp
protected override void OnEnable()
{
    base.OnEnable();
    // 绑定 SerializedProperty
}

public override void OnInspectorGUI()
{
    base.OnInspectorGUI();
    // 绘制当前 Inspector 的配置与运行时信息
    FinalRefreshInspectorGUI();
}
```

## 关联

- [IEditorRuntimeDrawer.md](../Definitions/IEditorRuntimeDrawer.md)
- [Editor.md](../Editor.md)

## 数据来源与失败边界

InitializeTemplatePath 在已有值为空时写默认模板路径并 Apply，已有非空值原样保留；只读模板提示只是不编辑模板字段，创建按钮仍可建目录、覆盖目标同名 Excel 并触发刷新。属性版 GenerateFromTemplate 要求目标目录已经存在，ByPath 版会先建目录；没有覆盖预览、备份或多文件事务，也不因复制成功自动登记 Unit 或完成正式导出。

Packages 与项目相对路径解析用于定位，不是目录归属安全门，不限制所有 `..` 或任意绝对路径。模板标签默认宽度为 95，普通 Inspector 为 180，可被覆写；DevelopMode 颜色/多值只是序列化场景快照，不校验当前配置或 Runtime 生效。

FinalRefreshInspectorGUI 依次 Apply、Repaint、恢复 labelWidth，不含 SaveAssets 或运行时 setter；Apply/Repaint 抛错会跳过后续恢复。基类与子类绘制序列没有统一 finally 保证，编译状态 hook 也不等同于触发编译或确认编译通过。
