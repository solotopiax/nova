# EditorUtil.Environment

`NovaFramework.Editor.EditorUtil.Environment` 是 Python3、Luban 和 HybridCLR 的编辑器环境检查器集合。当前 Environment.cs 只保留 partial 容器，旧 EnvironmentData、ColumnIndices、GetEnvironmentData 和 GetColumnIndices 已移除；过滤器使用各自显式导出上下文，不应恢复旧的场景默认值回退。

## 探测与缓存

Check 优先返回 Unity SessionState 缓存；Recheck 先清 cached 标记再重新探测，环境安装或 PATH 变化不会自动刷新。SessionState 跨程序集域重载保留，到编辑器会话结束才清空。Luban 另用 cacheVersion=2 淘汰旧结构。DTO 构造只保存字段，不探测、不验证 Ready/Issue/版本互相一致；缓存还原的枚举整数也不校范围。

HybridCLRChecker 检查固定 `Packages/com.solotopia.hybridclr/package.json` 和 `HybridCLRData/LocalIl2CppData-{Application.platform}/il2cpp/libil2cpp/hybridclr` 目录；版本读取失败可仍就绪。它不核 LocalIl2Cpp 版本、文件完整性或实际构建，固定 Packages 路径也可能漏 registry 的 PackageCache 布局。Ready 只表示这一轮存在性检查通过。

Python3Checker 逐候选同步执行，单次 probe 3000 毫秒，多候选会累加阻塞。只要求首行匹配 Python 3.x，没有 3.9 下限；非超时但退出码非零且输出版本仍可能被接受。stdout 为空才读 stderr。Where 成功仅得到首行候选，后续还需运行。PyLauncher 的 DetectedPath 为 `py -3`，包含参数，不能整体当可执行文件路径。候选列表不是安装状态；失败日志可能包含 PATH。

LubanChecker 独立探测 Luban.dll 存在和 dotnet --version，后者使用 ProcessRunner 默认 120 秒。版本剥离 `-` 后缀再解析，支持区间为 8.0.127 至 10.0.203（含端点）。DLL 缺失优先作为主 Issue，同时保留独立 DotnetIssue；版本越界时 IsReady=false，Pipeline 是否允许告警继续由 Pipeline 决定。DLL 存在不证明依赖齐全或导出成功。初始化通过 delayCall 运行静默检查，延迟并没有把同步 probe 改成后台任务。

## HybridCLR 安装与验收

新建 Nova 工程必须在业务开发前核对 HybridCLR 的实际环境。默认按工程所选 HybridCLR 方案完成本地安装准备；确实不采用时须有明确范围或技术依据并告知用户，状态写为“不适用/未安装”，不能为了通过构建而临时关闭开关，也不能仅因游戏单机就推断无需该环境。

以下状态分别核验，不相互替代：

| 层级 | 最低核验内容 |
| --- | --- |
| UPM 包已解析 | 实际 package id、声明与解析版本、source 及解析物理路径；覆盖 registry、local、Git 等实际来源，不以 manifest 声明、固定 `Packages/<id>` 物理目录或缓存目录名代替解析结果。 |
| 本地安装完成 | 当前安装器的真实结果、`HasInstalledHybridCLR()`、`SettingsUtil.LocalIl2CppDir` 对应本机环境、实际文件及安装版本标记；记录所用 Unity 版本和包声明的兼容分支。目录存在只是必要条件，不足以证明完整、兼容或能构建。 |
| 启用策略正确 | HybridCLR 自身设置、Nova 热更策略、业务程序集与加载链分别读取；关闭热更不等于安装成功，关闭 HybridCLR 后普通 IL2CPP 构建通过也不能验收该工具链。 |
| 生成与正式交付 | 使用热更时按目标平台/DevelopmentBuild 核对正式导出、DLL 列表与输出、link/AOT、资源映射、Bundle 和最终 Player；Editor 直接运行源码不证明业务 DLL 已部署。 |
| 实际运行 | 沿目标 Player/设备核对所需程序集、AOT metadata 和业务入口真实加载；APK 文件存在或能启动不能单独证明热更链有效。 |

安装后重新读取安装器状态和本地版本标记，再对 Nova 环境面板执行重新检测；`Check()` 的旧 SessionState 缓存不能作为当次证据。包版本、上游分支、Unity 版本和安装标记分别报告，不能仅按名称相似推断一致。当前通用环境检查器的固定路径与存在性局限仍如上文所述，面板 `Ready` 不代替这些验收项。

安装使用当前已解析包的正式 Installer、受支持的本地源或已有受控入口，先核对实际下载/覆盖范围与授权，不复制另一工程或另一 Unity 版本的 LocalIl2Cpp 目录，也不手改版本标记制造成功。缺少安装能力、安装失败或兼容性未知时，保留具体缺口；依赖 HybridCLR 的工作停止，其他已授权准备可继续。安装请求发出、按钮点击或关闭提示，都不能写为安装完成。

只读环境评估不自动执行安装。实际安装、Generate、DLL 刷新、Bundle、Player 与设备操作分别使用对应权限和执行通道；它们是不同交付层级，不在一个局部成功后笼统报告“全部就绪”。

## 关联文档

- [Python3Checker](EditorUtil.Environment.Python3.md)
- [LubanChecker](EditorUtil.Environment.LubanChecker.md)
- [ProcessRunner](../EditorUtil.ProcessRunner/EditorUtil.ProcessRunner.md)
