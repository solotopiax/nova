# BestHTTP 包自动迁移

消费项目升级 Framework 后，Editor 会从 `Packages/manifest.json` 自动移除：

- `com.solotopia.nova.framework.besthttp`
- `com.tivadar.best.http`
- `com.tivadar.best.tlssecurity`
- 两个历史包名 `com.solotopia.best.http`、`com.solotopia.best.tlssecurity`
- `testables` 中对应的旧包名

写入 manifest 后触发 UPM Resolve；`packages-lock.json` 和 Package Manager 缓存由 Unity 自行重建。迁移器不修改 Prefab、Scene、HybridCLR、`link.xml`、DLL、脚本宏或 `Library`，也不处理 TGA/ThinkingAnalytics 上游 DoH。

## 升级编译桥梁

旧 adapter `0.1.8` 引用了新版 Framework 已删除的网络接口。为避免它先编译失败、导致 Editor 迁移器无法执行，Runtime asmdef 仅在检测到旧 adapter 包时启用 `NOVA_LEGACY_BESTHTTP_MIGRATION`，临时编译旧 ABI。

桥梁只满足旧包的首轮编译，旧传输注册为空操作，Framework 始终使用 UWR。Resolve 删除旧 adapter 后，宏和桥梁自动退出编译。

Unity 6000.4.2f1 独立夹具已验证：旧 adapter `0.1.8`、Best HTTP `3.0.20` 与 Best TLS 可以完成首轮编译，随后三包从 manifest 与 lock 中自动移除；再次启动无重复写入。
