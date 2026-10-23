# Runtime/Utils — 索引

`static partial class Util` 的各子工具类，可在任意层级调用。

| 文档 | 子类 | 说明 |
|------|------|------|
| [Util.TypeCreator.md](Util.TypeCreator.md) | `Util.TypeCreator` | **DI 核心**：按类型名反射创建实例 |
| [Util.Assembly.md](Util.Assembly.md) | `Util.Assembly` | 跨程序集按名查找 `Type` |
| [Util.HybridCLR.md](Util.HybridCLR.md) | `Util.HybridCLR` | AOT 元数据补充与业务 DLL 加载入口 |
| [Util.Json.md](Util.Json.md) | `Util.Json` | JSON 序列化/反序列化 |
| [Util.Placeholder.md](Util.Placeholder.md) | `Util.Placeholder` | 显式上下文的标准文本占位符解析 |
| [Util.Convert.md](Util.Convert.md) | `Util.Convert` | 基础类型安全转换 |
| [Util.Encrypt.md](Util.Encrypt.md) | `Util.Encrypt` | AES / XOR 加解密 |
| [Util.SQLite.md](Util.SQLite.md) | `Util.SQLite` | SQLite 连接、事务与表操作（非 WebGL） |
| [Util.SysIO.md](Util.SysIO.md) | `Util.SysIO` | 文件/目录/路径（含 WebGL） |
| [Util.MD5.md](Util.MD5.md) | `Util.MD5` | MD5 哈希（字节数组/文件） |

`Util.AppStore.OpenAsync(url)` 同步调用 `Application.OpenURL` 后返回已完成 UniTask，不等待商店页面出现，也不校验 URL、商店可用性或安装结果。调用方负责平台 URL 与 Unity 线程条件。
