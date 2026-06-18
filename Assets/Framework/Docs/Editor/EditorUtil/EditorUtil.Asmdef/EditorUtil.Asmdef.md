# EditorUtil.Asmdef

**类签名**：`public static class EditorUtil.Asmdef`
**命名空间**：`NovaFramework.Editor`
**全局访问**：`EditorUtil.Asmdef.ResolveNamespace(path)`

Assembly Definition 命名空间解析工具，从指定路径向上逐级查找 `.asmdef` 文件并提取命名空间。

---

## 文件

| 文件 | 类 | 说明 |
|------|----|------|
| `EditorUtil/EditorUtil.Asmdef/EditorUtil.Asmdef.cs` | `EditorUtil.Asmdef` | 命名空间解析工具完整实现 |

---

## 继承关系

```
EditorUtil (public static partial class)
  └── Asmdef (public static class，嵌套工具类)
```

---

## 关键字段表

无公有字段，纯静态工具类。

---

## 完整公开 API

### 命名空间解析

```csharp
/// <summary>
/// 从指定路径所在目录开始向上逐级查找 .asmdef 文件，
/// 优先返回 rootNamespace 字段，若为空则返回 name 字段作为命名空间。
/// </summary>
/// <param name="path">起始文件或目录路径。</param>
/// <returns>解析到的命名空间字符串。</returns>
/// <exception cref="InvalidOperationException">未找到 .asmdef 文件时抛出。</exception>
public static string ResolveNamespace(string path)
```

### 私有辅助方法（仅供内部实现参考）

```csharp
// 向上查找包含 Assets 目录的项目根路径
private static string FindProjectRoot(string dir)

// 解析 .asmdef 文件，优先取 rootNamespace，其次取 name
private static string ParseNamespaceFromAsmdef(string asmdefPath)
```

---

## 关键算法（§9）

`ResolveNamespace` 算法：

```
1. 将 path 规范化为目录路径（若 path 是文件则取其父目录）
2. 调用 FindProjectRoot 找到包含 Assets/ 目录的项目根
3. 从当前目录向上遍历（终止于项目根）：
   ├─ 若目录下存在 *.asmdef 文件 → 解析命名空间并返回
   └─ 否则上移一层目录
4. 遍历完毕未找到 → 抛出 InvalidOperationException
```

`ParseNamespaceFromAsmdef` 优先级：`rootNamespace` > `name`，两者均为空时抛出。

---

## 使用示例

```csharp
// 在代码生成器中，根据输出路径动态解析目标命名空间
string outputPath = "Assets/Game/Scripts/Tables/HeroData.cs";
string ns = EditorUtil.Asmdef.ResolveNamespace(outputPath);
// ns 例如 "Game.Runtime"（取自对应 .asmdef 的 rootNamespace）

// 生成的类代码使用动态命名空间
string code = $"namespace {ns}\n{{\n    [Serializable]\n    public class HeroData : ITableData {{ ... }}\n}}";
```

---

## 注意事项（§12）

- **必须存在 .asmdef**：若目标目录及其所有父目录均无 `.asmdef`，抛出 `InvalidOperationException`。确保代码导出目录在有效的 asmdef 覆盖范围内。
- **项目根检测**：以存在 `Assets/` 目录为判断条件。标准 Unity 项目均满足此条件。
- **路径分隔符**：内部统一使用正斜杠 `/`，兼容 Windows/macOS。
- **仅 Editor 使用**：此工具类位于 Editor 层，运行时代码不可调用。

---

## 关联文档

- [EditorUtil.md](../EditorUtil.md)（工具集概览）
