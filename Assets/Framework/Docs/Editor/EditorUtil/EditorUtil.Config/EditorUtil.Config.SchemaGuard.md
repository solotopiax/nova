# EditorUtil.Config.SchemaGuard

`EditorUtil.Config.SchemaGuard` 是 `ConfigMasterSO` 的 Editor-only 结构版本保护入口。最新版只接受当前结构，不再自动迁移旧资产或重导出 Runtime 快照。

## 公开入口

```csharp
public static bool Validate(ConfigMasterSO master, out string error);
```

- 当前 `ConfigSchemaVersion` 与 `CurrentConfigSchemaVersion` 一致时返回 `true`。
- 资产为空，或版本低于、高于当前版本时返回 `false` 并提供错误信息。
- Editor 脚本重载后会延迟扫描全部 `ConfigMasterSO`，只报告版本不一致，不修改资产。

旧结构必须先使用对应历史 Framework 完成升级并保存，或在最新版中重新创建配置资产。

关联文档：[ConfigMasterSO.md](../../Config/ConfigMasterSO.md)、[ConfigRuntimeSO.md](../../../Runtime/Modules/Config/ConfigRuntimeSO.md)。
