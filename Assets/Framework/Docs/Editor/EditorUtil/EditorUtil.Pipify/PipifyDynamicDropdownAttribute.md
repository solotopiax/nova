# PipifyDynamicDropdownAttribute

## §1 文件头

| 项 | 值 |
|---|---|
| 类签名 | `[AttributeUsage(Field)] public sealed class PipifyDynamicDropdownAttribute : Attribute` |
| 命名空间 | `NovaFramework.Editor` |
| 全局访问 | `PipifyDynamicDropdownAttribute` |
| 功能描述 | 字段渲染特性：string 字段以动态选项下拉框形式编辑（每帧调用 Provider 取选项数组） |

## §2 文件表

| 文件 | 类 | 说明 |
|---|---|---|
| `EditorUtil.Pipify/Definitions/PipifyDynamicDropdownAttribute.cs` | `PipifyDynamicDropdownAttribute` | 唯一实现 |

## §5 完整公开 API

| 成员 | 类型 | 说明 |
|---|---|---|
| `ProviderType` | `Type` | 选项提供者所在类型 |
| `MethodName` | `string` | 静态无参方法名（返回 `string[]` 作为下拉选项） |
| `PipifyDynamicDropdownAttribute(Type providerType, string methodName)` | 构造器 | 设定提供者方法 |

PipifyWindow 解析规则：仅作用于 `string` 字段；每帧调用 `ProviderType.MethodName()`（无参 static，公私皆可）取 `string[]` 渲染下拉框；存储值为选中选项字符串本身（非 FullName）。选项数组为空 / null / 调用异常时退化为普通 TextField，避免阻塞参数填写。

与 `PipifyDropdownAttribute` 区别：后者按接口/基类反射收集实现类型 FullName 作为选项；本特性允许 Provider 返回任意 `string[]`，适合需要从工程实际配置动态生成选项的场景（如包名列表）。

## §11 使用示例

```csharp
[PipifyDynamicDropdown(typeof(AssetBundleBuildArgs), nameof(GetPackageNameOptions))]
public string PackageName = "Default";

public static string[] GetPackageNameOptions()
{
    // 从当前活动场景的 AssetComponent 中收集已配置包名
    Nova nova = UnityEngine.Object.FindAnyObjectByType<Nova>(FindObjectsInactive.Include);
    if (nova == null) return new[] { "Default" };
    AssetComponent asset = nova.GetComponentInChildren<AssetComponent>(true);
    // ... 反射读 m_Packages
    return new[] { "Default", "Patch1" };
}
```

## §13 关联文档

- [EditorUtil.Pipify.md](./EditorUtil.Pipify.md)
- [PipifySteps.md](./PipifySteps.md)
- [PipifyDropdownAttribute.md](./PipifyDropdownAttribute.md)
- [PipifyDynamicDefaultAttribute.md](./PipifyDynamicDefaultAttribute.md)
