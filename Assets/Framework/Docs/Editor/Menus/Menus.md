# Editor — Menus

**目录**：`Assets/Framework/Scripts/Editor/Menus/`
**命名空间**：`NovaFramework.Editor`

Nova 顶级菜单下的所有菜单项实现，按功能分组拆分为独立文件。每个文件内部就近定义菜单路径常量与优先级常量。

---

## 子模块

| 文件 | 类 | 说明 |
|------|-----|------|
| [`FolderMenuItems.cs`](FolderMenuItems.md) | `FolderMenuItems` | Open IDE Project 与 Open Folder 系列菜单项 |
| [`EnableLogsMenuItems.cs`](EnableLogsMenuItems.md) | `EnableLogsMenuItems` | Enable Logs 脚本宏定义管理菜单项 |

---

## 菜单结构

```
Nova/
├── Open IDE Project              (priority 1010)
├── Open Folder/
│   ├── Data Path                 (priority 1020)
│   ├── Persistent Data Path      (priority 1021)
│   ├── Streaming Assets Path     (priority 1022)
│   ├── Caching Writing Path      (priority 1023)
│   └── Temporary Cache Path      (priority 1024)
├── Enable Logs/
│   ├── Disable All Logs          (priority 1040)
│   ├── Enable All Logs           (priority 1041)
│   ├── Enable Debug And Above    (priority 1042)
│   ├── Enable Info And Above     (priority 1043)
│   ├── Enable Warning And Above  (priority 1044)
│   ├── Enable Error And Above    (priority 1045)
│   └── Enable Fatal And Above    (priority 1046)
```

---

## 新增菜单项步骤

1. 在对应功能文件内部添加路径常量（`c_Menu*` 前缀）和优先级常量（`c_Priority*` 前缀）
2. 添加 `[MenuItem(...)]` 静态方法
3. 优先复用 `EditorUtil.FileSystem.*` 等已有框架接口
4. 更新本文件的"子模块"表与"菜单结构"图

---

## 关联文档

- [Editor.md](../Editor.md)
- [EditorUtil.ScriptingDefineSymbols.md](../EditorUtil/EditorUtil.ScriptingDefineSymbols/EditorUtil.ScriptingDefineSymbols.md)
- [EditorUtil.FileSystem.md](../EditorUtil/EditorUtil.FileSystem/EditorUtil.FileSystem.md)
