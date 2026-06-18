# EditorUtil.Config.SceneDevelopModeWriter

**类签名**：`public static class SceneDevelopModeWriter`（嵌套于 `EditorUtil.Config`）  
**命名空间**：`NovaFramework.Editor`

将 `ConfigWindow` 当前导出选中的 `DevelopMode` 回写到当前激活场景中的 `Nova + 所有 FrameworkComponent`。

---

## 作用

这个工具解决的是启动早期时序问题：

- `AppComponent` / `AssetComponent` 在 `Start()` 就需要决定使用 Debug 还是 Release 地址
- 但 `Config.LoadAsync()` 更晚，不能作为这一步的依赖
- 所以导出时把所选 `DevelopMode` 序列化回场景，运行时直接从组件节点本地读取

---

## 公开 API

```csharp
public static void WriteActiveScene(DevelopMode developMode);
```

---

## 行为

`WriteActiveScene(developMode)` 会：

1. 取当前激活场景 `SceneManager.GetActiveScene()`
2. 遍历所有根节点及其子孙中的 `FrameworkComponent`
3. 将每个组件的私有序列化字段 `m_DevelopMode` 写成目标值
4. 对改动过的对象 `SetDirty`
5. 若有任意变更，调用 `EditorSceneManager.MarkSceneDirty(activeScene)`

它不会自动保存场景，只负责回写并置脏。

---

## 关联

- [ConfigWindow.md](../../Windows/ConfigWindow.md)
- [EditorUtil.Config.Exporter.md](EditorUtil.Config.Exporter.md)
- [../../../Runtime/Modules/FrameworkComponent.md](../../../Runtime/Modules/FrameworkComponent.md)
