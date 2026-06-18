# ObjectPoolComponentInspector

**类签名**：`[CustomEditor(typeof(ObjectPoolComponent))] internal sealed partial class ObjectPoolComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.ObjectPoolComponent`

对象池组件的 Editor Inspector，运行时展示所有已创建对象池的状态信息，并提供释放与 CSV 导出操作。

---

## § 2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `ObjectPoolComponentInspector.cs` | `sealed partial ObjectPoolComponentInspector` | 主体：`OnEnable` 绑定属性、`OnInspectorGUI` 绘制入口 |
| `ObjectPoolComponentInspector.Visitors.cs` | `partial ObjectPoolComponentInspector` | 字段：`m_CurManagerTypeName`、`m_ManagerTypeNames`、`m_SearchText`、`c_PageSize`、`m_PageIndices` |
| `ObjectPoolComponentInspector.Methods.cs` | `partial ObjectPoolComponentInspector` | 私有方法：`DrawConfigs`、`DrawRuntimeInfos`、`DrawObjectPool`、`ExportObjectPoolCsv`、`DrawSearchBox`、`DrawObjectListPagination` |

---

## § 3 继承关系

```
UnityEditor.Editor
 └── BaseComponentInspector (abstract)
      └── ObjectPoolComponentInspector (sealed partial)
```

---

## § 4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_CurManagerTypeName` | `SerializedProperty` | — | 绑定 `ObjectPoolComponent.m_CurManagerTypeName` 序列化字段 |
| `m_ManagerTypeNames` | `List<string>` | — | 通过反射收集所有 `IObjectPoolManager` 实现类的类型名 |
| `m_SearchText` | `string` | `""` | 池列表搜索框输入文本，按 FullName 大小写不敏感过滤 |
| `c_PageSize` | `const int` | `50` | 对象列表分页大小；单池对象数超过此值时启用分页 |
| `m_PageIndices` | `Dictionary<string, int>` | `{}` | 记录每个池当前页码，键为池 FullName，值为 0-based 页索引 |

---

## § 5 完整公开 API

继承自 `BaseComponentInspector`，无额外 public 方法。

```csharp
// --- 生命周期（override） ---
protected override void OnEnable()     // 绑定 SerializedProperty，收集 Manager 类型名列表
public override void OnInspectorGUI()  // 绘制：DrawConfigs → DrawRuntimeInfos → FinalRefreshInspectorGUI
```

---

## § 9 关键算法

### DrawRuntimeInfos 流程

```
DrawRuntimeInfos()
  ├── 非 Playing 状态 → 直接返回
  ├── 显示"对象池数量"标签（t.Count）
  ├── 分割线
  ├── DrawSearchBox()                          // 搜索框 + 清除按钮
  ├── 获取 t.GetAllObjectPools(sort: true)
  ├── 搜索过滤：m_SearchText 非空时
  │     └── 保留 FullName.Contains(m_SearchText, IgnoreCase) 的池
  └── 遍历过滤后的池列表
        └── DrawObjectPool(objectPool)
              ├── Foldout 展开判断（以 FullName 为缓存键）
              ├── 折叠时直接返回
              └── 展开时绘制 box 面板：
                    ├── 名称、类型、自动释放间隔、容量、对象数量、可释放数量、过期时间、优先级
                    ├── 有对象时：
                    │     ├── 表头行（AllowMultiGet 决定显示 RefCount 还是 IsInUse）
                    │     ├── 分页逻辑（对象数 > c_PageSize 时）：
                    │     │     ├── 从 m_PageIndices 读取当前页码（缺省为 0）
                    │     │     ├── 计算 startIndex = page * c_PageSize
                    │     │     ├── 仅渲染 [startIndex, startIndex + c_PageSize) 范围的 ObjectInfo
                    │     │     └── DrawObjectListPagination(fullName, currentPage, totalPages)
                    │     ├── 无分页时：渲染全部 ObjectInfo 行
                    │     ├── [释放] 按钮
                    │     ├── [释放所有未使用] 按钮
                    │     └── [导出 CSV] 按钮 → ExportObjectPoolCsv
                    └── 无对象时：显示"对象池为空 ..."
```

### DrawSearchBox 流程

使用 `EditorUtil.Draw` 绘制 `toolbarSearchField` 样式输入框；右侧"×"按钮在 `m_SearchText` 非空时可用，点击后清空文本并还原焦点。

### DrawObjectListPagination 流程

```
DrawObjectListPagination(string fullName, int currentPage, int totalPages)
  ├── 显示"第 {currentPage + 1}/{totalPages} 页"
  ├── [上一页] 按钮（currentPage > 0 时可用）→ m_PageIndices[fullName]--
  └── [下一页] 按钮（currentPage < totalPages - 1 时可用）→ m_PageIndices[fullName]++
```

### AllowMultiGet 对展示内容的影响

| 字段 | `AllowMultiGet = true`（多次获取池） | `AllowMultiGet = false`（单次获取池） |
|------|--------------------------------------|---------------------------------------|
| 使用情况列 | `RefCount`（引用计数） | `IsInUse`（是否在使用，RefCount > 0） |
| CSV 列名 | `RefCount` | `In Use` |

---

## § 11 使用示例

Inspector 自动挂载，无需手动调用。运行时点击 ObjectPoolComponent 即可查看：

```
[运行时 Inspector 面板]
对象池数量：3
─────────────────────────────────────
[搜索... ________________] [×]          ← DrawSearchBox
─────────────────────────────────────
▶ NovaFramework.Runtime.UIViewInstanceObject.
▼ NovaFramework.Runtime.SomeObject.Main
  名称：Main
  类型：NovaFramework.Runtime.SomeObject
  自动释放间隔：60
  容量：16
  对象数量：55                           ← 超过 c_PageSize(50)，触发分页
  可释放数量：3
  过期时间：0
  优先级：0
  名称    Locked  In Use  Flag  Priority  Last Use Time
  obj_1   False   True    True  0         2026-03-05 10:00:00
  obj_2   False   False   True  0         2026-03-05 09:59:00
  ... （共 50 行，第 1 页）
  第 1/2 页  [上一页（灰）]  [下一页]    ← DrawObjectListPagination
  [释放]  [释放所有未使用]  [导出 CSV]
```

---

## § 13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [ObjectPoolComponent.md](../../../Runtime/Modules/ObjectPool/ObjectPoolComponent.md)
- [ObjectPoolManager.md](../../../Runtime/Modules/ObjectPool/ObjectPoolManager.md)
