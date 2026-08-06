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
| `c_MenuOpenFolderPersistentDataPath` | `"Nova/Open Folder/Persistent Data Path (Unity)"` | 菜单路径 |
| `c_MenuOpenFolderPersistentDataPathYooAsset` | `"Nova/Open Folder/Persistent Data Path (YooAsset)"` | 菜单路径 |
| `c_MenuOpenFolderBundleGeneratedPath` | `"Nova/Open Folder/Bundle Generated Path"` | 菜单路径 |
| `c_MenuOpenFolderStreamingAssetsPath` | `"Nova/Open Folder/Streaming Assets Path"` | 菜单路径 |
| `c_MenuOpenFolderCachingWritingPath` | `"Nova/Open Folder/Caching Writing Path"` | 菜单路径 |
| `c_MenuOpenFolderTemporaryCachePath` | `"Nova/Open Folder/Temporary Cache Path"` | 菜单路径 |
| `c_PriorityOpenIdeProject` | `1010` | 排序优先级 |
| `c_PriorityOpenFolder*` | `1021–1027` | 排序优先级（Data Path 1021 起逐项 +1） |

---

## §5 完整公开 API

```csharp
[MenuItem("Nova/Open IDE Project")]                          public static void OpenIdeProject()
[MenuItem("Nova/Open Folder/Data Path")]                     public static void OpenFolderDataPath()
[MenuItem(".../Persistent Data Path (Unity)")]               public static void OpenFolderPersistentDataPath()
[MenuItem(".../Persistent Data Path (YooAsset)")]            public static void OpenFolderPersistentDataPathYooAsset()
[MenuItem(".../Bundle Generated Path")]                      public static void OpenFolderBundleGeneratedPath()
[MenuItem(".../Streaming Assets Path")]                      public static void OpenFolderStreamingAssetsPath()
[MenuItem(".../Caching Writing Path")]                       public static void OpenFolderCachingWritingPath()
[MenuItem(".../Temporary Cache Path")]                       public static void OpenFolderTemporaryCachePath()
```

---

## §9 关键行为（带回退的路径解析）

| 方法 | 目标路径 | 回退规则 |
|------|----------|----------|
| `OpenFolderPersistentDataPathYooAsset` | `{项目根}/{YooFolderName}/{PackageName}` | `YooFolderName` 为空 → 项目根；包目录不存在 → `YooFolderName` 目录；再不存在 → 项目根 |
| `OpenFolderBundleGeneratedPath` | `{项目根}/Bundles/{Platform}/{PackageName}` | 包目录不存在 → `{Platform}`；平台目录不存在 → `Bundles`；再不存在 → 项目根 |

默认包名 `GetDefaultPackageName()`：从 `Assets/Framework/Prefabs/Nova.prefab` 的 `AssetComponent.m_DefaultPackageName` 读取，为空时回退 `m_Packages[0]`，仍读不到返回空字符串（此时只打开到上一级已存在目录）。

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
