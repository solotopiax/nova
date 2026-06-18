# DebuggerActiveType

**类签名**：`public enum DebuggerActiveType : byte`
**命名空间**：`NovaFramework.Runtime`

Debugger 激活策略枚举，用来决定 `DebugComponent` 是否初始化 RuntimeDebugger。

---

## 文件

| 文件 | 类 | 说明 |
|---|---|---|
| `Runtime/Modules/Debug/Definitions/DebuggerActiveType.cs` | `DebuggerActiveType` | Debugger 激活条件枚举 |

---

## 枚举值

| 值 | 名称 | 说明 |
|---|---|---|
| `0` | `AlwaysEnable` | 始终启用 |
| `1` | `OnlyEnableWhenDevelopment` | 仅 `UnityEngine.Debug.isDebugBuild == true` 时启用 |
| `2` | `OnlyEnableInEditor` | 仅 `Application.isEditor == true` 时启用 |
| `3` | `AlwaysDisable` | 始终禁用 |

---

## 使用示例

```csharp
switch (m_DebuggerActiveType)
{
    case DebuggerActiveType.AlwaysEnable:
        return true;
    case DebuggerActiveType.OnlyEnableWhenDevelopment:
        return UnityEngine.Debug.isDebugBuild;
    case DebuggerActiveType.OnlyEnableInEditor:
        return Application.isEditor;
    case DebuggerActiveType.AlwaysDisable:
    default:
        return false;
}
```

---

## 关联文档

- [DebugComponent.md](../DebugComponent.md)
