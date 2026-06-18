# Nova Framework - SDK - Firebase

> 包名：`com.solotopia.nova.framework.sdk.firebase`
> 当前版本：`0.0.15`

Firebase 聚合插件，统一接入分析、崩溃、推送、远程配置

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.firebase": "0.0.8"
}
```

## 维护

变更记录见 [CHANGELOG.md](./CHANGELOG.md)。每次发版必须在 CHANGELOG 中追加对应版本节，否则发布脚本会中断。

## 当前开源状态

- 当前结论：包根第三方声明已补齐，可按“保留各 Firebase 子包许可证 + 包根说明文件”的方式进入公开仓。
- 项目私有配置文件与私有集成产物仍不属于公开仓保留范围。

## 许可与第三方声明

- 包根许可边界说明见 [LICENSE.md](./LICENSE.md)。
- 上游来源、第三方声明与当前再分发边界见 [THIRD_PARTY_NOTICES.md](./THIRD_PARTY_NOTICES.md)。
- `Core/` 内随包分发的 `LICENSE`、`NOTICE`、`README` 等文件，应与对应内容一起保留。

## Firebase 桌面库说明 / Firebase Desktop Libraries

为控制仓库体积、且 Firebase 官方将桌面支持定位为「仅开发期 beta、不用于发布」，本包**未随仓分发** Firebase 桌面（Editor）核心原生库 `FirebaseCppApp-*`（macOS `.bundle` / Linux `.so` / Windows `.dll`，位于 `Firebase/Plugins/x86_64/`）。

- **真机构建（Android / iOS）不依赖这些桌面库，不受任何影响。**
- 仅当你需要在 **Editor 播放模式**下调试 Firebase 时才需要它们；缺失时 `FirebaseDesktopLibraryGuard` 会在 Console 与弹窗给出提示。
- 补齐方式：从 [Firebase 官方 Unity SDK](https://firebase.google.com/download/unity) 下载解压，通过 `Assets > Import Package > Custom Package` 导入对应 `.unitypackage`；或手动将 SDK 内 `Firebase/Plugins/x86_64/` 下的 `FirebaseCppApp` 桌面库拷回本包同名目录。

---

To keep the repository size down — and because Firebase officially treats desktop support as "development-only beta, not for shipping" — this package does **not** ship the Firebase desktop (Editor) core native libraries `FirebaseCppApp-*` (`.bundle` / `.so` / `.dll` under `Firebase/Plugins/x86_64/`). Device builds (Android / iOS) do not depend on them. To debug Firebase in the Editor, import them from the [official Firebase Unity SDK](https://firebase.google.com/download/unity); `FirebaseDesktopLibraryGuard` will prompt when they are missing.
