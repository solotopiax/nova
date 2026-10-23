# Changelog

## [Unreleased]

## [0.1.2] - 2026-10-23

### Changed

- 对齐依赖版本：com.solotopia.nova.framework 0.7.0 → 0.7.3。

- 兼容声明调整为 Unity 6000.4.2f1，配合 Nova Framework 的 Unity 6000.4.x 支持范围。

## [0.1.1] - 2026-10-23

### Changed

- 补全 MemoryProtection 本体与 Anti-Cheat Toolkit 的安装步骤，说明 NovaSpark 默认安装本体，实际防篡改仍需单独安装兼容插件。

### Important

新增内存数据防护，为金币、钻石等敏感整数提供读写时的防篡改校验。发现异常时隔离相关数据，便于业务阻止异常值参与计算或保存。

使用前需安装 **Nova Framework - MemoryProtection**（`com.solotopia.nova.framework.memory.protection`）。NovaSpark 会默认安装本包；已有项目可打开 **Nova → Open PlugPals**，找到本包并安装。

实际防篡改还需安装兼容的 **Anti-Cheat Toolkit**。NovaSpark 不会自动安装该内网商业插件；已获授权的内网项目，可打开 **Nova → Open PlugPals → 内部云仓库**，安装 `com.codestage.anti.cheat.tookit`。未安装兼容插件时，普通数值读写仍可使用，但不提供内存防篡改能力。

## [0.1.0] - 2026-10-23

### Added

- 新增独立 MemoryProtection 核心扩展包，迁入受保护整数、可选 ACTk 适配、异常隔离、密钥轮换、脱敏埋点及裁剪规则；不包含付费源码或 ACTk 强制依赖。

### Changed

- 通过通用隐私扩展保存与迁移原检测/换钥开关，沿主 Framework Manager 生命周期自动接入；提供独立 MemoryProtectionDemo 和包内 Tracks 权威源。

- 标记 Framework 新隐私 API 的正式发布基线 0.6.40；开发态继续本地源码联调，正式发布前必须提升依赖声明并确认主框架配对，错误组合由发布器阻断。

- 最低依赖对齐至Nova Framework 0.7.0，与本轮发布的框架及 SDK 接口配套。

### Breaking

- 原主 Framework 使用者需另行安装本包，并在调用方 asmdef 显式引用 NovaFramework.MemoryProtection.Runtime；公开命名空间 NovaFramework.Runtime 与数值、事件和属性契约保持。

- 最低支持 Unity 6000.6.7f1，与 Nova Framework 基线一致。

### Important

新增内存数据防护，为金币、钻石等敏感整数提供读写时的防篡改校验。发现异常时隔离相关数据，便于业务阻止异常值参与计算或保存。

实际防护需安装兼容的 **Anti-Cheat Toolkit**，内网包名为 `com.codestage.anti.cheat.tookit`。已获授权的内网项目，可打开 **Nova → Open PlugPals → 内部云仓库**，找到该包并安装。

