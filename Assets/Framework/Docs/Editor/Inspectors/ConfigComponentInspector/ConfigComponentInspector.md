# ConfigComponentInspector

**类签名**：`[CustomEditor(typeof(ConfigComponent))] internal sealed partial class ConfigComponentInspector : BaseComponentInspector`
**命名空间**：`NovaFramework.Editor`
**目标组件**：`NovaFramework.Runtime.ConfigComponent`

配置组件 Inspector；提供 Config 管理器类型选择器、Asset 地址的 Inspector 绘制，以及 Play Mode 下运行时已加载配置的只读展示。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `ConfigComponentInspector.cs` | `sealed partial ConfigComponentInspector` | 主体：`OnEnable`（绑定 SerializedProperty、收集管理器类型名）/ `OnDisable` / `OnInspectorGUI` |
| `ConfigComponentInspector.Visitors.cs` | `partial ConfigComponentInspector` | 字段：`m_CurManagerTypeName` / `m_AssetLocationSP` / `m_ManagerTypeNames` |
| `ConfigComponentInspector.Methods.cs` | `partial ConfigComponentInspector` | 私有方法：`DrawConfigs` / `DrawRuntimeInfo` / `DrawPluginConfig` / `DrawReadonlyKeyValue` / `DrawDllAssetEntryList` |

---

## §3 继承关系

```
UnityEditor.Editor
  └── BaseComponentInspector (abstract)
        └── ConfigComponentInspector (sealed partial)
```

---

## §4 关键字段表

| 字段 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| `m_CurManagerTypeName` | `SerializedProperty` | — | 绑定 `ConfigComponent.m_CurManagerTypeName` |
| `m_AssetLocationSP` | `SerializedProperty` | — | 绑定 `ConfigComponent.m_AssetLocation` |
| `m_ManagerTypeNames` | `List<string>` | — | 反射收集所有 `IConfigManager` 实现类名称，供类型选择器下拉 |

---

## §5 完整公开 API

继承自 `BaseComponentInspector`，无额外 public 方法。

```csharp
// 模板文件名（override）
protected override string TemplateFileName => "ConfigTemplate.xlsx";
protected override float TemplateLabelWidth => EditorUtil.Draw.SourceFileTree.c_DirLabelWidth;

// 生命周期（override）
protected override void OnEnable();
    // 绑定 m_CurManagerTypeName / m_AssetLocationSP；
    // 收集 IConfigManager 实现类名到 m_ManagerTypeNames

private void OnDisable();   // 空实现

public override void OnInspectorGUI();
    // base.OnInspectorGUI → DrawConfigs → FinalRefreshInspectorGUI
```

---

## §9 关键算法

### DrawConfigs 绘制流程

```
DrawConfigs()
  ├── EditorUtil.Draw.TypesSelector("Config 管理器", m_ManagerTypeNames, m_CurManagerTypeName)
  ├── EditorUtil.Draw.Line()
  ├── EditorUtil.Draw.Button("打开 Config 全局配置中心", false, ConfigWindow.Open, GUILayout.ExpandWidth(true))
  ├── EditorUtil.Draw.Line()
  ├── if EditorUtil.Draw.Foldout("配置数据位置", null, true):
  │     ├── EditorUtil.Draw.IncreaseIndentLevel()
  │     ├── EditorUtil.Draw.Property("Asset 地址", m_AssetLocationSP, true, GUILayout.Width(175))
  │     └── EditorUtil.Draw.DecreaseIndentLevel()
  ├── EditorUtil.Draw.Line()
  └── DrawRuntimeInfo()

DrawRuntimeInfo()  [仅 Play Mode]
  └── if EditorUtil.Draw.Foldout("运行时已加载配置", null, true):
        ├── if !IsLoadOver → Label("（配置尚未加载完成）")
        └── DrawReadonlyKeyValue × 8:
              DevelopMode / AppID / AppAesKey / AppAesIV /
              Namespace / Platform / Channel / GameEntranceProcedureName
        ├── DrawDllAssetEntryList("AOT 元数据 DLL", AotMetadataDlls)
        ├── DrawDllAssetEntryList("业务 DLL", GameDlls)
        └── 已启用 SDK 配置 [Label + forEach DrawPluginConfig]
```

---

## §11 使用示例

Inspector 自动挂载，无需手动调用。

```
[编辑器 Inspector 面板]
Config 管理器:  [ConfigManager                             ▼]
─────────────────────────────────────────────────────────────
[打开 Config 全局配置中心                                  ]
─────────────────────────────────────────────────────────────
▼ 配置数据位置
    Asset 地址:     [configs/runtime/ConfigRuntime           ]
─────────────────────────────────────────────────────────────

[Play Mode 下额外显示]
▼ 运行时已加载配置
    DevelopMode                 [Debug                      ]
    AppID                       [com.example.app            ]
    AppAesKey                   [***                        ]
    AppAesIV                    [***                        ]
    Namespace                   [Game.Runtime               ]
    Platform                    [Android                    ]
    Channel                     [Default                    ]
    GameEntranceProcedureName   [Preload                    ]
  ▼ AOT 元数据 DLL
      [0]                       [mscorlib.dll               ]
      [1]                       [System.Core.dll            ]
  ▼ 业务 DLL
      [0]                       [Game.Runtime.dll           ]
    已启用 SDK 配置
  ▼ MySDKConfig
      ...
─────────────────────────────────────────────────────────────
```

---

## §13 关联文档

- [BaseComponentInspector.md](../BaseComponentInspector.md)
- [ConfigComponent.md](../../../Runtime/Modules/Config/ConfigComponent.md)
- [ConfigManager.md](../../../Runtime/Modules/Config/ConfigManager.md)
- [IEditorRuntimeDrawer.md](../../Definitions/IEditorRuntimeDrawer.md)
