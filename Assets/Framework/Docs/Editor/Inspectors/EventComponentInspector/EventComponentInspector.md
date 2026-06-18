# EventComponentInspector

**类签名**：`[CustomEditor(typeof(EventComponent))] internal sealed class EventComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.EventComponent`

---

## 文件

| 文件 | 说明 |
|------|------|
| `EventComponentInspector.cs` | 主体：OnEnable 初始化、OnInspectorGUI 绘制流程 |
| `EventComponentInspector.Visitors.cs` | SerializedProperty 字段声明（m_CurManagerTypeName、m_EventPoolMode、m_MaxDispatchPerFrame 等） |
| `EventComponentInspector.Methods.cs` | DrawConfigs / DrawRuntimeInfos / DrawRuntimeLists 私有绘制方法 |
| `EventComponentInspector.EventListRuntimeDrawer.cs` | 嵌套类 `EventListRuntimeDrawer`：运行时已注册事件列表诊断面板 |

---

## Inspector 可配置项

| 配置项 | 控件类型 | 说明 |
|--------|---------|------|
| EventManager 类型名 | TypesSelector | `IEventManager` 实现类（DI 注入） |
| 事件池模式 | EnumFlagsSelector | `EventPoolMode` Flags 枚举：AllowNoHandler / AllowMultiHandler / AllowDuplicateHandler |
| 每帧最大事件分发数量 | Property（IntField） | `MaxDispatchPerFrame`：0 表示无限制，> 0 限制每帧分发上限，防止事件风暴卡顿 |

---

## 运行时信息

`isPlaying` 时展示：
- 已注册 handler 总数（`EventComponent.EventHandlerCount`）
- 当前待分发事件数量（`EventComponent.EventCount`）

### 已注册事件列表（EventListRuntimeDrawer）

运行时通过 `EventComponent.EventManager`（`#if UNITY_EDITOR` accessor）获取诊断数据，展示所有已注册事件的详细信息：

- **顶层 Foldout**：`已注册事件列表(N)`，N 为已注册事件类型数量
- **每个事件类型**（二级 Foldout，按类名展示）：
  - ID：事件类型编号
  - Handler 数：该事件的处理函数数量
  - Handler 列表：每个 handler 显示 `[索引] TargetType.MethodName`（静态方法显示 `(static) DeclaringType.MethodName`）

事件 ID 到类名的映射通过 `TypeCache.GetTypesDerivedFrom<EventData>()` 在构造时预构建，对每个非抽象子类调用 `EventTypeID.Get(type)` 作为 key（与 `EventData.ID` 的 `EventTypeID.Get(GetType())` 保持一致），无需反射静态字段。

---

## 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [EventManager.md](../../../Runtime/Modules/Event/EventManager.md)
