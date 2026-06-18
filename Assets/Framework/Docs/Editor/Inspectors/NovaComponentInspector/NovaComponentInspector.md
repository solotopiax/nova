# NovaComponentInspector

**类签名**：`[CustomEditor(typeof(Nova))] internal sealed partial class NovaComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.Nova`

---

## 文件

| 文件 | 说明 |
|------|------|
| `NovaComponentInspector.cs` | 主体：`OnEnable`（绑定 Property）、`OnInspectorGUI`（绘制所有配置） |
| `NovaComponentInspector.Visitors.cs` | 所有 `SerializedProperty` 字段 + 静态游戏速度数组 |
| `NovaComponentInspector.Methods.cs` | `GetGameSpeed(index)`、`GetSelectedGameSpeed(value)` 辅助方法 |

---

## Inspector 可配置项

| 配置项 | 控件类型 | 对应 Nova 字段 |
|--------|---------|--------------|
| 帧率 | IntSlider (1~120) | `m_FrameRate` |
| 游戏速度 | FloatSelectionGrid | `m_GameSpeed` |
| 后台运行 | Toggle | `m_RunInBackground` |
| 屏幕常亮 | Toggle | `m_NeverSleep` |
| 引用检查模式 | EnumSelector | `m_ReferenceStrictCheckType` |
| TxtHelper 类型名 | TypesSelector | `m_CurTxtHelperTypeName` |
| LogHelper 类型名 | TypesSelector | `m_CurLogHelperTypeName` |
| ReferenceHelper 类型名 | TypesSelector | `m_CurReferenceHelperTypeName` |

**游戏速度预设值**（FloatSelectionGrid）：
`0x / 0.01x / 0.1x / 0.25x / 0.5x / 1x / 1.5x / 2x / 4x / 8x`

---

## Visitors 字段

```csharp
// 静态数据
private static readonly float[] s_GameSpeeds;     // { 0f, 0.01f, 0.1f, 0.25f, 0.5f, 1f, 1.5f, 2f, 4f, 8f }
private static readonly string[] s_GameSpeedTexts; // { "0x", "0.01x", ..., "8x" }

// SerializedProperty（在 OnEnable 中 FindProperty 绑定）
private SerializedProperty m_FrameRate;
private SerializedProperty m_GameSpeed;
private SerializedProperty m_RunInBackground;
private SerializedProperty m_NeverSleep;
private SerializedProperty m_ReferenceStrictCheckType;
private SerializedProperty m_CurTxtHelperTypeName;
private SerializedProperty m_CurLogHelperTypeName;
private SerializedProperty m_CurReferenceHelperTypeName;

// Helper 类型名候选列表（反射程序集中 ITxtHelper / ILogHelper / IReferenceHelper 的所有实现）
private List<string> m_TxtHelperTypeNames;
private List<string> m_LogHelperTypeNames;
private List<string> m_ReferenceHelperTypeNames;
```

---

## 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [Nova.md](../../../Runtime/Modules/Nova/Nova.md)
