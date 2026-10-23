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

运行列表 Drawer 构造时按 `EventTypeID` 建立名称映射，重复 ID 后项覆盖，保存的是简单类型名；它是本域构造时快照，不是动态类型身份认证。展开仅查询已注册 ID 与处理器，不主动注册、派发或清空队列；事件集合在绘制中改变仍可能导致枚举失败。

Play gate 由 Inspector 的调用层承担，Drawer 的 `Draw` 自身没有统一运行态检查。`OnDisable` 逐项 Dispose，没有单项异常隔离；一个释放失败可能阻止其余项及列表置空。Drawer 只清主 Foldout key，不能从这里推断所有子 key 都已经删除。事件池负数/互斥标志等有效性也不在 Inspector 中统一校验，默认值与真正分发语义仍以 Runtime 为准。

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [EventManager.md](../../../Runtime/Modules/Event/EventManager.md)
