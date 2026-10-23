# INativeAppInstaller

`public interface INativeAppInstaller` 是 `NovaFramework.Runtime` 的可选自身 APK 安装扩展；内置 `NativeManager` 实现，旧 `INativeManager` 不增加必实现成员。

```csharp
UniTask<bool> CanRequestPackageInstallsAsync(CancellationToken ct = default);
UniTask<bool> OpenPackageInstallSettingsAsync();
UniTask<NativeAppInstallResult> InstallApkAsync(string apkPath, CancellationToken ct = default);
```

`NativeManagerConfig.EnableAndroidAppInstall` 默认 `false`。安装返回 `Disabled`；启用后 Editor、iOS、WebGL、Android API 28 以下返回 `Unsupported`，不导入或调用 Android 原生安装层。来源查询以系统 `canRequestPackageInstalls()` 为准；未获来源许可时安装返回 `PermissionRequired`，业务可以在用户明确操作后调用来源设置入口，返回后再次安装。

安装接受 persistentDataPath 内既有、非空、无符号链接的文件绝对路径，最大 1 GiB。原生层先复制到自有 cache 唯一暂存件，再对同一份字节使用官方 `ApkVerifier.isVerified()` 进行密码学验证；要求包名等于当前应用、`versionCode` 严格递增、验签得到的当前 signer 证书集合与已安装应用完全一致，并拒绝 split APK。首版不支持 signer 轮换；解析归档元数据不替代验签，也没有绑定远端 `CheckAsync()` 命中的具体版本。

使用 `PackageInstaller` 完整安装 session。Android API 31 以上显式要求系统用户操作，所有版本均处理 `STATUS_PENDING_USER_ACTION`，该回调只启动系统确认界面；只有最终 `STATUS_SUCCESS` 对应 `Installed`。系统取消对应 `Cancelled`，验证、准备及平台失败对应 `Failed`。安装过程最多等待 15 分钟，超时取消清理。自身升级可能先由系统终止旧进程，因此不能保证旧进程收到成功回调，不主动 kill、不承诺自动重启。

同一 Manager 与 Java 桥接均拒绝并发安装。调用者取消抛取消异常，重新 Initialize 与 Shutdown 取消等待并放弃未完成 session；已完成安装不可回滚。取消先失效 callback、放弃未完成 session；后台复制/验签 worker 在检查点退出，不保证取消返回时 worker 已退出。终态取消 PendingIntent、解除 callback，并尝试删除暂存和自有 session marker；文件 delete 和清理 marker 的返回值不构成已逐项验证清理成功的回执；进程重启初始化只清理自有遗留，不弹界面。

`NativeAppInstallResult.IsInstalled` 仅在 `Status == Installed` 为 true。下载返回路径仅是文件提交完成，系统确认发起、权限设置跳转及元数据解析均不代表安装成功。

平台桥接与依赖随 Framework 的 `Plugins/Android/NovaNativeAppInstaller.androidlib` 发布，纯 Java `com.android.tools.build:apksig:8.13.2` 不使用构建主机 `apksigner.jar`，consumer ProGuard 保留 JNI/receiver 与验签库。依赖与 JNI 保留规则的源码存在不等于构建或设备通过；Gradle/R8、最终 Manifest 和 Android 真机安装须按本次目标验证。

相关入口：[NativeComponent](../../NativeComponent.md)、[NativeManagerConfig](../Definitions/NativeManagerConfig.md)、[ProcedureAppDownload](../../../Procedure/Procedures/ProcedureAppDownload.md)。

安装库的 Manifest 只声明不导出的 `ResultReceiver`，不自行声明来源安装权限；中央构建规则根据实际 Build Scene 的 Native 配置决定权限，必须核对最终合并 Manifest。安装库与评价库的 Gradle namespace 分别为 `com.solotopia.nova.nativeappinstaller`、`com.solotopia.nova.nativereview`，Java JNI 类位于 `com.solotopia.nova.nativebridge`，三者不能混作同一个名称。consumer rules 保护各自精确 JNI 类/Callback，安装库另保护 receiver 和 `com.android.apksig.**`；静态 keep 规则不证明最终 R8 结果。
