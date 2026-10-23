# EditorUtil.Environment

`NovaFramework.Editor.EditorUtil.Environment` 是 Python3、Luban 和 HybridCLR 的编辑器环境检查器集合。当前 Environment.cs 只保留 partial 容器，旧 EnvironmentData、ColumnIndices、GetEnvironmentData 和 GetColumnIndices 已移除；过滤器使用各自显式导出上下文，不应恢复旧的场景默认值回退。

## 探测与缓存

Check 优先返回 Unity SessionState 缓存；Recheck 先清 cached 标记再重新探测，环境安装或 PATH 变化不会自动刷新。SessionState 跨程序集域重载保留，到编辑器会话结束才清空。Luban 另用 cacheVersion=2 淘汰旧结构。DTO 构造只保存字段，不探测、不验证 Ready/Issue/版本互相一致；缓存还原的枚举整数也不校范围。

HybridCLRChecker 检查固定 `Packages/com.solotopia.hybridclr/package.json` 和 `HybridCLRData/LocalIl2CppData-{Application.platform}/il2cpp/libil2cpp/hybridclr` 目录；版本读取失败可仍就绪。它不核 LocalIl2Cpp 版本、文件完整性或实际构建，固定 Packages 路径也可能漏 registry 的 PackageCache 布局。Ready 只表示这一轮存在性检查通过。

Python3Checker 逐候选同步执行，单次 probe 3000 毫秒，多候选会累加阻塞。只要求首行匹配 Python 3.x，没有 3.9 下限；非超时但退出码非零且输出版本仍可能被接受。stdout 为空才读 stderr。Where 成功仅得到首行候选，后续还需运行。PyLauncher 的 DetectedPath 为 `py -3`，包含参数，不能整体当可执行文件路径。候选列表不是安装状态；失败日志可能包含 PATH。

LubanChecker 独立探测 Luban.dll 存在和 dotnet --version，后者使用 ProcessRunner 默认 120 秒。版本剥离 `-` 后缀再解析，支持区间为 8.0.127 至 10.0.203（含端点）。DLL 缺失优先作为主 Issue，同时保留独立 DotnetIssue；版本越界时 IsReady=false，Pipeline 是否允许告警继续由 Pipeline 决定。DLL 存在不证明依赖齐全或导出成功。初始化通过 delayCall 运行静默检查，延迟并没有把同步 probe 改成后台任务。

## 关联文档

- [Python3Checker](EditorUtil.Environment.Python3.md)
- [LubanChecker](EditorUtil.Environment.LubanChecker.md)
- [ProcessRunner](../EditorUtil.ProcessRunner/EditorUtil.ProcessRunner.md)
