# EditorUtil.Asset.Operator

**类签名**：`public static class Operator`（嵌套于 `EditorUtil.Asset`）
**命名空间**：`NovaFramework.Editor`

任意 ScriptableObject 资产的查找、创建、按路径加载的通用入口。泛型版本，适用于所有 `.asset` 资产；ConfigMasterSO、PipifySettingsSO 等均通过本类读写，不再提供业务特化 facade。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|----|------|
| `Editor/EditorUtil/EditorUtil.Asset/EditorUtil.Asset.Operator.cs` | `EditorUtil.Asset.Operator` | 静态工具类定义 |

---

## §3 继承关系

```
EditorUtil (public static partial class)
  └── EditorUtil.Asset (public static partial class)
        └── Operator (public static class)
```

---

## §5 完整公开 API

```csharp
// 查找工程内第一个 T 类型资产
// 通过 AssetDatabase.FindAssets("t:TypeName") 过滤；不存在时返回 null
public static T Find<T>() where T : ScriptableObject;

// 在指定路径创建 T 类型空白资产
// 目标目录不存在则自动 Directory.CreateDirectory；完成后 SaveAssets + Refresh
public static T CreateAt<T>(string assetPath) where T : ScriptableObject;

// 按 Assets 相对路径加载 T 类型资产（LoadAssetAtPath）
// 路径无效或类型不匹配时返回 null
public static T LoadAt<T>(string assetPath) where T : ScriptableObject;
```

---

## §11 使用示例

```csharp
// 查找
PipifySettingsSO settings = EditorUtil.Asset.Operator.Find<PipifySettingsSO>();

// 创建（示例路径，实际可按团队资产目录规范调整）
settings = EditorUtil.Asset.Operator.CreateAt<PipifySettingsSO>("Assets/Nova/PipifySettings.asset");

// 按路径加载（同样是示例路径）
settings = EditorUtil.Asset.Operator.LoadAt<PipifySettingsSO>("Assets/Nova/PipifySettings.asset");
```

---

## §13 关联文档

- [ConfigMasterSO.md](../../../Runtime/Modules/Config/ConfigMasterSO.md) — 本类的主要消费资产之一
- [PipifySettingsSO.md](../EditorUtil.Pipify/PipifySettingsSO.md) — 本类的主要消费资产之一
