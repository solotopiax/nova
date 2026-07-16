# EditorUtil.ProjectGuard

`EditorUtil.ProjectGuard` 是 Nova 项目结构检查的唯一规则层。Play、Unity Build 和 ProjectGuardWindow 只调用它，不各自维护规则。

## 公开入口

```csharp
EditorUtil.ProjectGuard.ValidateQuick();
EditorUtil.ProjectGuard.ValidatePlay();
EditorUtil.ProjectGuard.ValidateBuild(buildTarget);
EditorUtil.ProjectGuard.ValidateRelease(buildTarget);
```

报告包含按严重性、规则编号、路径和消息稳定排序的 `Issues`；只有 `Error` 会令 `HasErrors` 为 `true`。

## 当前范围

- Quick / Play：当前活动 Scene，以及该 Scene 所在目录的资源。
- Build / Release：Build Settings 中启用的 Scene，以及这些 Scene 所在目录的资源。
- 未进入上述范围的 Scene、UPM 内容和第三方插件资源不由 ProjectGuard 推断是否合法。

Scene 检查使用已加载 Scene 或只读 Preview Scene，不保存、不修复、不自动加载 Content。首个启用 Build Scene 作为本次构建入口检查；其它 Build Scene 可以是不含 Nova 和 FrameworkComponent 的 Content 或自定义 Scene。

## 当前规则

| Rule | Severity | 含义 |
|---|---|---|
| `NOVA-SCENE-000` | Error | Scene 无法通过只读 Preview 打开或检查 |
| `NOVA-SCENE-001` | Warning | 首个启用 Build Scene 不含 Nova，需确认是否为自定义 Bootstrap |
| `NOVA-SCENE-002` | Error | Scene 有 FrameworkComponent 但没有 Nova |
| `NOVA-SCENE-003` | Error | 单个 Scene 中存在多个 Nova |
| `NOVA-SCENE-004` | Error | Nova 不是 canonical `Nova.prefab` 的 connected instance |
| `NOVA-RES-001` | Warning | 当前范围发现归属待确认的非 `Resources/BuiltIn` Resources；先确认所有权，再决定是否迁移 Bundle |

`Resources/BuiltIn/**` 合法；UPM 与识别为第三方插件所有的 Resources 被忽略。资源归属不能确定时只给 Warning，不阻断 Play 或 Unity Build。

## 集中位置

规则、scope 和报告模型全部位于 `Scripts/Editor/EditorUtil/EditorUtil.ProjectGuard/`。外围只保留：

- Play gate：同目录中的薄事件适配器。
- ProjectGuardWindow：只展示 Quick / Build 报告。

ProjectGuard 不注册全局 Build preprocessor。Unity Build、显式 `BuildPlayerOptions.scenes` 和项目自定义 BuildPipeline 均保持原行为；若项目希望发布门禁，应在自己的流水线中显式调用并解释报告。

相关阅读：[验证与构建](../../Onboarding/VALIDATION.md)、[资源工作流](../../Onboarding/RESOURCE_WORKFLOW.md)。
