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

## Sample 的导入、辅助工具与页面边界

Sample 的源码命名空间、asmdef 名、生成 DLL、ConfigRuntime 中的入口/Binding、资源地址和实际导入目录各有独立来源。典型数据链是 Excel/schema 与生成配置 → 正式 JSON/访问代码 → 场景或 Runtime Config 引用 → 实际加载；复制出来的源码和清单只是骨架，不能由文件名推断已导出、已编译或已经加载。YooAsset 地址由有效 Collector 规则生成，字体表只提供地址/材质/比例，不创建真实字体；link.xml 的 preserve 声明也不证明实际裁剪和 AOT 已通过。

`SamplePathManifest` 的字段 getter 只是读取配置，`RewriteTargets` 返回底层列表的只读接口而非独立快照。`SamplePathRewriter` 在 Editor 域加载后延迟检查并按清单改写导入路径：它不是事务或通用安全路径工具，字符串替换不理解序列化字段，也不保证阻止相对路径越界。文件未齐时有界重试，不等于持续监听所有导入；标记只用于内部重写状态，不证明资产绑定正确。已有自动工具的资产文本写入不能成为 Agent 手工改写 YAML 的先例，实际资产维护仍经 Editor/MCP。

`SampleVersionGuard` 的版本比较使用前三段数字，不是完整 SemVer 或 UPM lock 判断。某些实现按整个旧版本目录调用 `DeleteAsset`，其中可能包含同版其他 Sample 及用户改动，范围大于弹窗中提到的单个 Demo；确认前核对完整目标，失败不提供事务恢复。旧版删除询问与启动场景询问是两个独立动作，拒绝删除不表示后续不会再询问场景；场景文件存在也不证明其可运行。专用 PrefabBuilder、配置补齐或诊断桥同样只服务自己的固定 Sample，不是可泛化的任意反射/任意资产写入口。

Sample 的 `BaseDemoView` 在首次 `OnInit` 接线，池化重开不等于重新初始化。关闭默认只清本页登记的反馈行并进入 UIView 关闭链，清反馈不结束账号、取消网络或关闭厂商页面；派生类自行管理业务等待、事件、纹理和句柄。反馈最多保留 200 行并在帧末销毁旧对象，`Success` 只是显示级别，不证明下层任务成功。模板或字段缺失的静默跳过也不证明 Prefab 完整绑定。

启动 `ProcedureLogin` 的一秒等待属于演示流程，不进行游戏服认证；具体页面按钮才可能请求真实 GameLogin。各 Sample 的预加载模块不同，应读当前 `ProcedurePreload`；完成计步只证明相应 await 返回，不保证所有 bool 结果已检查。旧复制版还可能在异常重试时保留计数，不能以进度条完成代替模块可用性。关闭或离开流程对令牌/代际的保护也不承诺所有底层全局加载立即停止，各 Demo 的独立取消和清理说明以其 README 及模块 Docs 为准。
