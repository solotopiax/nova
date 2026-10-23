# Player Facebook Resources 正常绑定合同

此合同只覆盖 `Facebook.Unity.Editor.XCodePostProcess.OnPostProcessBuild` 的真实 WebGL 分支。完整 Editor/Settings DLL 均使用准确 SHA；原 `PostProcessBuild_iOS` 是另一个非 iOS 早退合同，不按同类型泛化。

官方回调先读 `FacebookSettings.IsValidAppId`，再判断平台，所以缺少 static instance 不能直接当作无副作用早退。Plan 可以通过 Unity AssetDatabase 合法只读加载唯一已有的 Resources/FacebookSettings 主资产；完整 Settings DLL 无 Awake、OnEnable、OnValidate、序列化回调，ctor 仅默认列表/标量，cctor 仅空变更委托表。Plan 不调用 Instance / NullableInstance / AppId getter，不修改 static instance 或创建配置；直接 Load 前后保留真实 static 引用，核对原文件/meta未变。

Resources 查找冻结全部 AssetDatabase 同名 key，包括其他类型和大小写冲突。不是只找 Settings 类型；不唯一、不是准确主类型、原 singleton 已属另一对象、索引越界或原选中字符串为 null 时 blocked。AppId 有效性按官方 Trim/非空/非"0"条件读取 backing；ID、ClientToken、配置正文只在内存中用于整体 SHA，不进入 Plan、receipt、日志或测试样本。

官方 WebGL 回调可以自然将 null singleton 绑定到上述同一已有资产，并在 AppId 无效时输出固定缺配置 warning。它不登录 SDK、不调用 iOS/Android业务分支、不联网、不改设置。实际 SDK账号、认证和真机业务仍需各自证据；这里不把固定 warning 或自然绑定当作 SDK业务验收。

Execute 写前核对完整 before 及当前域真实资产/cached引用。只对准确 null→原资产绑定作独立不变量投影，其他 JSON、asset/meta/导入依赖、dirty、候选集、变更委托保持不变。Scope 成功和失败都读真实 after，receipt保存原 before/after/error；正常链完成后还必须实际绑定到原 GUID/fileID/当前域对象。任何意外官方 fallback CreateInstance 仅保留为 partial 失败材料，不手动创建/重绑/销毁来制造后验。

Verify 不预调用 getter、Resources.Load、CreateInstance，也不重载对象补证据；域重载或对象替换使原身份无法对应时 partial。schema4增加此组必须的材料，缺组的旧回执 partial，不刷新旧执行SHA。测试覆盖正常投影与写前保护、未知同名资源、输入漂移、错误owner/非持久fallback和真实已加载对象只读检查；最后一项若尚未由正式 Plan 加载则明确 inconclusive，不能计作通过。
