# FolderMenuItems

## §1 文件头

```csharp
public static class FolderMenuItems
namespace NovaFramework.Editor
// Assets/Framework/Scripts/Editor/Menus/FolderMenuItems.cs
```

打开 IDE 工程文件（.sln）与各 Unity 系统路径文件夹的菜单项集合。内部复用 `EditorUtil.FileSystem.OpenFolder` / `EditorUtil.FileSystem.OpenFile`。

---

## §2 文件表

| 文件 | 类 | 说明 |
|------|-----|------|
| `FolderMenuItems.cs` | `FolderMenuItems` | 唯一文件 |

---

## §3 继承关系

```
(无继承，public static class)
```

---

## §4 关键字段表

| 常量 | 值 | 说明 |
|------|----|------|
| `c_MenuOpenIdeProject` | `"Nova/Open IDE Project"` | 菜单路径 |
| `c_MenuOpenFolderDataPath` | `"Nova/Open Folder/Data Path"` | 菜单路径 |
| `c_MenuOpenFolder*` | 见代码 | 其余 4 个 Open Folder 子菜单路径 |
| `c_PriorityOpenIdeProject` | `1010` | 排序优先级 |
| `c_PriorityOpenFolder*` | `1020–1024` | 排序优先级 |

---

## §5 完整公开 API

```csharp
[MenuItem("Nova/Open IDE Project")]       public static void OpenIdeProject()
[MenuItem("Nova/Open Folder/Data Path")]  public static void OpenFolderDataPath()
[MenuItem("...Persistent Data Path")]     public static void OpenFolderPersistentDataPath()
[MenuItem("...Streaming Assets Path")]    public static void OpenFolderStreamingAssetsPath()
[MenuItem("...Caching Writing Path")]     public static void OpenFolderCachingWritingPath()
[MenuItem("...Temporary Cache Path")]     public static void OpenFolderTemporaryCachePath()
```

---

## §11 使用示例

```csharp
// 菜单触发：Unity 菜单栏 → Nova → Open IDE Project
// 代码调用（如在自定义工具中打开某个目录）：
EditorUtil.FileSystem.OpenFolder(Application.persistentDataPath);
```

---

## §13 关联文档

- [Menus.md](Menus.md)
- [EditorUtil.FileSystem.md](../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.md)
