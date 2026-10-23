# Util.SysIO

文件 / 目录 / 路径操作，含 WebGL 平台专用实现。

## 文件

`Util.SysIO/` 下分部类，WebGL 实现在 `Plugins/WebGL/` 下通过 `#if UNITY_WEBGL` 切换。

## API

```csharp
Util.SysIO.File.ReadAllTextSync(path)
Util.SysIO.File.WriteAllTextSync(path, content)
Util.SysIO.Directory.Exists(path)
Util.SysIO.Directory.GetFiles(path)
Util.SysIO.Path.Combine(a, b)
```

## 注意事项

- `WriteAllBytesAsync` 使用 `FileMode.Create` 打开文件（自动截断旧内容），确保文件缩小时不残留旧数据。
- `ReadAllBytesAsync` 按初始 Length 分配并循环读取；非 WebGL 超过 int.MaxValue 时 Fatal 后返回 null。提前 EOF 会直接结束，尾部保留零，不证明完整快照。

## 路径与流

路径入口通常只对 null/空记录 Fatal 后返回 null/false/void；Fatal 本身不保证抛错，自定义日志 helper 的异常仍可传播。空白、相对路径、..、链接、根目录包含关系和读写权限不在该封装统一验证范围内。Exists=false 不能区分缺失与不可访问；存在检查和后续 IO 之间仍有竞争。

Path 类是 BCL 文本处理：Combine 的后续绝对路径可覆盖前缀，GetRelativePath 可返回 ..，GetFullPath 依赖进程当前目录且不证明存在。ChangeExtension 只返回字符串，extension=null 去扩展名、empty 遵从 BCL；IsPathRooted 不等于完整绝对路径。路径结果通常转正斜杠；`Directory.GetPath` 直接对 GetDirectoryName 结果 Replace，根路径可因 null 失败，而 `Path.GetDirectoryName` 保留 null。目录 GetName 不去尾分隔符；文件 GetName 默认 includeFileExtension=true。

OpenRead/Open/OpenWrite/CreateText/OpenText 返回的流由调用方 Dispose；Open 不创建父目录，OpenWrite/CreateText 会创建。OpenWrite 从位置 0 写而不自动截断旧尾；CreateText 则覆盖、默认 UTF-8 无 BOM。直接操作返回流不会自动请求 WebGL 同步。

## 读写和目录

ReadAll 系列缺文件返回 null；读文本默认 UTF-8/BOM 行为由 BCL reader 处理，指定 encoding 重载直接传入且不回退。异步入口在 WebGLPlayer 转同步 IO，其他分支 using 包住 await，无取消令牌或统一超时。ReadAllBytesAsync 不缩短提前 EOF 的数组；WriteAllBytesAsync 非 WebGL 的 null 数据只可能建目录而不写文件，WebGL 的同步路径则交 BCL 拒绝 null。空字节数组覆盖为空文件。

WriteAll 与 CreateText 会覆盖截断，父目录可在后续失败前已创建；不使用临时文件或原子替换，await 也不代表浏览器持久化完成。输入数组跨 await 不复制。文本/行写默认 UTF-8 无 BOM，显式编码由调用方决定。

Directory.Create/CreateIfNotExist 在已存在时返回 null。GetFiles/GetDirectories/GetFileSystemEntries 默认 `*`、AllDirectories，目录不存在返回 null，无匹配为空数组，无排序保证。ClearFiles 默认 `*.*`、AllDirectories，仅逐文件删除而保留目录，不汇总失败。

Directory.Copy 先创建目标再扫描源，以字符串 Replace 映射子路径，源缺失可能在枚举时失败；不防同源、嵌套或全子串替换偏差。Directory.Move 先递归删除目标再移动源，目标等于源或为源祖先可能先删除源；File.Move 同样可先删目标。同源/移动失败不回滚，副作用可已发生。复制文件默认覆盖，三参数 overwrite 原样传 BCL；主文件与 meta 分步处理，overwrite=false 也不是相邻 meta 的保护门。

## 删除与 WebGL

Directory.Delete 默认 recursive=true，成功删目录后还删除相邻 `.meta`，此段不局限 Editor。文件 Delete/Copy/Move 的相邻 meta 处理仅在 Editor；均不更新 AssetDatabase。文件 Delete 只重试 IOException/UnauthorizedAccessException：UNITY_WEBGL 一次，其余最多三次，每次失败触发 GC 与等待 finalizer，前两次再阻塞 50ms；最终仅日志和 false，公开 Delete 丢弃该结果。主文件缺失时不删孤立 meta，主文件成功后 meta 失败也可能继续发同步请求。

`WebGLSyncFs` 只有 UNITY_WEBGL 且非 Editor 调用桥接，其他分支为空。静态委托保活回调；jslib 的 NovaSyncFsState 合并重叠请求，串行执行 `FS.syncfs(false)`，不为每次 C# 调用返回独立完成结果。错误回调映射 1，托管侧仅日志；IndexedDB 不存在时只警告一次，数据限本 session，仍回调 0。因此无错误日志或写入 UniTask 完成不能证明持久化。jslib/extern 的真实绑定可从同目录 `Plugins/WebGL/WebGLExtensions.jslib` 和 `Util.SysIO.WebGL.cs` 查阅。

WebGL `OpenURL(url,target)` 直接 UTF8ToString 后交给 window.open，没有默认 target、校验或成功返回；能否打开由浏览器策略决定。

- [Util.SQLite.md](Util.SQLite.md)
