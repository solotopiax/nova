# WebGL BuildEvents 当前自然分支

只支持当前已加载的准确 UnityEditor.CoreModule 与 UnityEditor.WebGL.Extensions。只读 ModuleManager 现存 `s_PlatformModules["WebGL"]`，要求它为准确 TargetExtension、原 WebGLBuildTarget、已自然初始化的模块和准确空派生表；不调用平台注册、模块 getter 或派生 provider。原生只读模块名映射必须返回 WebGL，活动目标/平台 GUID、现存对象/字典身份及文化配置进入完整写前检查。

已审 TargetExtension 经空派生表回到原目标；BaseBuildTarget 的 BuildPlatformProperties 是新 WebGLPlatformProperties `as IBuildPlatformProperties`。此精确类型不实现该接口，正常 getter 只创建无字段内存对象，所以自然 null 返回，不调用任何权限上报实现。派生表非空、类型/指纹/实际 backing 改变均继续 blocked，不伪造空表或改变设置。

SceneView 只枚举已有对象和 in2DMode，冻结完整运行ID名单及默认2D设置。官方代码仅在旧统计不匹配或尚未发送时设置 s_EventSent=true 和两个当前计数，再按当前配置进入 native sceneViewInfo。合同只投影这三个正常字段；执行前仍比较真实旧值和对象引用，执行后必须实际读到准确正常计数，原窗口 roster 或模块/配置变化均 partial，不重置缓存。序列化回执保留运行身份数字，live Scope 额外核对真实引用。

包列表严格从真实 BuildReport `role=="ManagedLibrary" || role=="dll"` 选择，已审只读 PackageInfo.GetForAssemblyFilePaths 仅读 asmdef 映射及已注册包列表。Registry 保留官方 packageId，其余来源仅 `name@source.ToLower()`；只保存路径列表和净化ID整体 SHA/数量。回执不输出本地路径、原 URL 或包ID正文，也不把文件扩展名当 role。

原生 sceneViewInfo/buildPackageList 的当前配置注册、限流/队列与诊断效果是正常开发构建效果；这里没有任意 Library 写权限。原代码丢弃 native 结果，本合同不宣称发送成功、远端接收、所有 telemetry 队列结束或原生后台任务退出。真实报告缺失、窗口变化、原模块被替换或新回执材料缺失保持 partial；不重发事件。schema4 增加 buildEventsBefore/After/Report/Error；本地纯 DTO 测试不调用任何真实构建回调。

`PlayerPackageCallbacksContract` 独立覆盖 `UnityEditor.UI.Analytics.UGUIBuildAnalytics` 的真实 Player 三阶段，不能借用 SBP 中 buildEvent 为 null 的场景早退。精确 ugui 2.0.0 DLL/三份源码与当前引擎 DLL 冻结，官方 pre 创建准确 UGUIBuildEvent，scene 只读 root 下 Canvas 的 renderMode 与 UIBehaviour 的五项 TMP 类型，增加 WorldSpaceCanvas、ScreenSpaceCanvas、OverlayCanvas、TMPElements 四项整数；不调用业务虚拟属性。

官方 post 将本次 BuildReport GUID/type 赋入事件，并沿原 Analytics 配置调用 `UGUIAnalytics.Send` / `EditorAnalytics.SendAnalytic`。`analyticSent` 必须原本为空且后验仍空，不移除订阅；固定 payload 的 Gather 只复制 GUID/type/Counter，允许正常 native usage 注册、限流/排队、诊断与固定失败日志，不宣称 native ack、远端送达或队列退出。Action 的 Complete 只复制真实报告与已有缓存，Dispose/Verify 要求同一 GUID/type、四项非负计数、准确缓存结构和配置保持；没有观察实际场景次数，不按计数推测场景是否重跑。

schema 4 另增独立 `packageCallbacksBefore/After/Report/Error`，不替代 BuildEvents 原材料。完整写前值受保护，官方 pre 可以替换原固定统计对象；after/current 和真实报告材料必须全部吻合。同域身份变化、外部订阅、旧或缺失材料仍 partial，不能在 Verify 中补写 GUID、统计或重发事件。
