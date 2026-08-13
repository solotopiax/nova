# Nova Framework - SDK - Firebase

> 包名：`com.solotopia.nova.framework.sdk.firebase`
> 当前版本：`0.1.2`
> Firebase Unity SDK：`13.14.0`

Firebase 聚合插件，统一接入分析、崩溃、推送、远程配置

## 安装

通过 Nova 私域 UPM 注册表以 UPM 依赖形式接入（注册表地址向 Nova Framework 内部开发人员索取）：

```json
"dependencies": {
  "com.solotopia.nova.framework.sdk.firebase": "0.1.2"
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

Firebase 桌面（Editor）核心原生库 `FirebaseCppApp-*`（macOS `.bundle` / Linux `.so` / Windows `.dll`，位于 `Firebase/Plugins/x86_64/`）现由 **Git LFS** 承载、随开源仓分发。正常 `git clone` 会自动 smudge 还原为真实内容，无需额外操作。

- **真机构建（Android / iOS）不依赖这些桌面库，不受任何影响。**
- Firebase 官方将桌面支持定位为「仅开发期 beta、不用于发布」，仅在 **Editor 播放模式**调试 Firebase 时需要。
- 兜底：若未安装 Git LFS 客户端导致 clone 只拿到指针文件，`FirebaseDesktopLibraryGuard` 会在 Console 与弹窗提示补齐——执行 `git lfs pull`，或从 [Firebase 官方 Unity SDK](https://firebase.google.com/download/unity) 下载解压后通过 `Assets > Import Package > Custom Package` 导入 / 手动拷回同名目录。

---

The Firebase desktop (Editor) core native libraries `FirebaseCppApp-*` (`.bundle` / `.so` / `.dll` under `Firebase/Plugins/x86_64/`) are now managed by **Git LFS** and shipped with the open-source repo. A normal `git clone` auto-smudges them into real content — no extra steps required. Device builds (Android / iOS) do not depend on them. Fallback: if you cloned without Git LFS installed and only got pointer files, `FirebaseDesktopLibraryGuard` will prompt — run `git lfs pull`, or import from the [official Firebase Unity SDK](https://firebase.google.com/download/unity).
