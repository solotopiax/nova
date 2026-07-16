# 项目与场景结构

本页帮助 Agent 在不了解项目背景时识别 Nova 的最小结构。结论应来自当前项目，而不是从 MainDemo、Starter 或其它 Sample 反推。

## 先建立事实清单

按项目检查以下内容：

1. 用户从哪个场景进入游戏，Build Settings 中有哪些启用场景。
2. 哪个场景或启动流程承载 Nova。
3. 是否存在运行时 Additive 加载的业务场景，以及谁负责加载和卸载。
4. 是否存在测试、插件 Demo、工具或美术预览场景；它们不应自动被视为产品场景。
5. 项目是否声明了 Nova 托管范围；未声明内容不做合法性推断。

## Nova 根节点

只要场景选择承载 Nova，就使用完整的 canonical `Assets/Framework/Prefabs/Nova.prefab`。不要把 `Nova` 与若干 `FrameworkComponent` 手工挂到普通 GameObject 上，因为这样很容易遗漏组件、引用和初始化关系。

同一时刻共同运行的场景拓扑只应有一个有效 Nova。单独编辑的测试场景或不会共同加载的替代入口，不应仅因仓库中还存在另一个 Nova 场景就被判为冲突。

## 单场景与多场景拓扑按项目选择

Nova 不要求统一的场景数量或命名。以下都是可行结构：

- 单场景：Nova 与业务内容位于同一入口场景。
- 入口加 Content：入口承载 Nova，业务场景按项目流程 Additive 加载。
- 自定义启动链：项目自己的 Bootstrap 决定何时进入承载 Nova 的场景。
- 多个替代入口：开发、测试或不同产品入口分别存在，但不会在同一运行拓扑同时激活。

不要为了满足文档而拆场景。先确认项目的生命周期、加载成本和协作方式，再选择结构。

## Content 的边界

Content 是项目可选的场景角色，不是所有项目必须存在的结构。项目一旦明确把某场景声明为 Content：

- 场景不再承载 Nova 或其它 Nova 根副本。
- 场景不会自动加载；Nova 不会自动加载 Content。
- 加载时机、Additive 参数、失败处理和卸载责任由项目代码明确实现。
- 使用 YooAsset 场景句柄时，生命周期结束应调用 `UnloadAsync()`。

资源化场景可从 [AssetComponent](../Runtime/Modules/Asset/AssetComponent.md) 与 [ISceneHandle](../Runtime/Modules/Asset/AssetManager/Interfaces/ISceneHandle.md) 继续阅读。项目若选择 Unity 原生场景流程，也应明确持有者和卸载边界。

## 不应被普遍强制的结构

- 固定的 Starter 目录、namespace、场景名或场景数量。
- 每个 Scene 都必须登记角色。
- Contract、representative asset 或分目录 `AGENTS.md` 必须存在。
- Content 自动补载或自动写入 Build Settings。
- 插件 Demo、测试场景和未参与产品闭包的 Sample 必须满足产品场景规则。

这些能力可由具体项目显式采用，但不能用来判断所有 Nova 项目是否合法。
