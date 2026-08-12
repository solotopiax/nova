---
id: PAT-159
title: iOS Player 导出后处理必须以产物级门禁验收
type: pattern
status: active
date: 2026-08-12
summary: iOS 后处理以产物与编译闭环验收
category: workflow
aliases:
  - PAT-159-ios-player-export-postprocess-artifact-gate
  - iOS 导出后处理产物门禁
  - iOSExportPostprocessArtifactGate
keywords:
  - PAT-159
  - legacy-PostProcessBuild
  - iOSExportPostprocessArtifactGate
  - TGA-PBX-plist-injection
  - MAXSwiftSupport.swift
  - HybridCLR-DEVELOPMENT-ABI
  - unsigned-xcodebuild
tags:
  - pattern
  - nova
  - ios
  - xcode
  - build
  - postprocess
  - validation
  - hybridclr
  - sdk
related:
  - "[[ADR-028-hybridclr-copy-aot-after-buildplayer|ADR-028]]"
  - "[[PAT-58-pipeline-fail-fast-no-silent-skip|PAT-58]]"
  - "[[PAT-88-detection-script-failure-mode-exhaust|PAT-88]]"
  - "[[PAT-152-runtime-initialize-unitask-cross-stage|PAT-152]]"
---

# PAT-159：iOS Player 导出后处理必须以产物级门禁验收

## 适用场景

- iOS Player 导出启用了第三方 SDK 的 legacy `[PostProcessBuild]` 回调。
- SDK 后处理会修改 `project.pbxproj`、`Info.plist`、entitlements、framework/link 配置，或创建并注册 Swift/原生源码。
- 同一份导出工程曾出现“Unity 导出成功、Xcode 才因缺文件或缺配置失败”的情况。
- HybridCLR Player 构建同时依赖预生成桥接代码与最终 `DevelopmentBuild` 档位。

## 核心做法

### 1. 先为每个后处理定义可观察的产物契约

不能以“程序集已编译”“回调方法存在”或“Pipify 已完成”为成功依据。每个 SDK 都要列出它在导出工程中必然留下的物理产物，并注明所属 Xcode target 与 build phase。

当前 TGA/MAX 的具体契约是：

- TGA：`UnityFramework` 的 build settings、framework/lib 链接，以及 `Info.plist` 的 `TDDisPresetProperties`。
- MAX：`Classes/MAXSwiftSupport.swift` 物理文件、PBX file reference、PBX build file 和 `UnityFramework` Sources membership。
- HybridCLR：生成目录中的 `MethodBridge.cpp` 必须含独立的 `// DEVELOPMENT=0` 或 `// DEVELOPMENT=1`，并与最终 Player 的 `DevelopmentBuild` 一致。

SDK 升级、替换或删除时同步更新该 SDK 的契约；不要把当前 TGA/MAX 的字段误当作所有 SDK 的固定字段。

### 2. 静态验收必须定位到正确 target

1. 解析 workspace 与 `project.pbxproj`，确认 FileRef、BuildFile、BuildPhase、target build configuration 的引用闭环，不只检索同名字符串。
2. 对 plist、entitlements 和 SDK plist 执行解析校验；对 PBX 引用核对本地文件存在性。
3. 对 TGA 这类注入，确认设置和 framework/lib 实际属于 `UnityFramework`，而不是碰巧存在于其他 target。
4. 对新增 Swift/原生文件，确认文件存在、已被 project 引用，并进入实际编译 target 的 Sources phase。
5. HybridCLR 仅认生成目录中的桥接标记；通用运行时实现中没有该标记不是失败证据。

### 3. 静态通过后执行无签名 Xcode 编译

使用 workspace 的 app scheme、目标 iPhoneOS SDK 和隔离 DerivedData 执行 `xcodebuild`，以 `CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO` 排除证书和描述文件干扰。只有 exit code 为零，才可称“导出工程已通过编译/链接”。

无签名编译不替代签名、安装或真机运行；这些属于后续平台验证，不能被静态检查或 CLI 编译冒充。

## 为什么这样定

- Unity 的 legacy attribute 回调与 Nova 的共享 `BuildPlayer` 回调是不同链路；后者成功不证明前者被收集和调用。
- 回调瞬态遗漏时，Unity 仍可能生成看似完整的工程，缺失只会在 Xcode 编译、链接或运行时暴露。
- 物理文件、PBX target membership、plist 内容和最终编译分别覆盖“有没有写出”“有没有注册”“有没有配置”“能不能消费”四个失败边界。
- HybridCLR 的 Development ABI 是生成物与最终 Player 之间的契约，必须与第三方后处理产物一并验收，但不能以其中任一项代替另一项。

## 反模式

- 只凭 `[PostProcessBuild]` 方法、asmdef、编译宏或 Unity Console 无报错判定后处理成功。
- 只检查 Swift/原生文件存在，不检查它是否属于正确 target 的 Sources phase。
- 只在整个 PBX 文本中搜到 framework/build setting，就认定 `UnityFramework` 已获得注入。
- 把 `xcodebuild -list` 或工程能在 Xcode 打开，当作编译、链接成功的证据。
- 在没有定位产物契约失败位置前，新增平行 Processor 复制厂商后处理逻辑；这会掩盖回调链问题并制造双写风险。
- 把无签名编译成功表述为签名、安装或真机通知/广告行为已验证。

## 来源与验证

- 2026-08-10 的一次 iOS 导出同时缺失 TGA callback order 88 的 PBX/plist 注入，以及 MAX callback order 90 创建并注册的 `MAXSwiftSupport.swift`。当次源码、编译条件和 Nova 最终后处理均已排除为直接原因；证据只能确认 legacy 回调收集或调用发生了瞬态失效，不能再细分到缓存、收集或反射调用的具体环节。
- 随后的新导出恢复了上述 TGA/MAX 产物，并以无签名 iPhoneOS `xcodebuild` 通过编译，证明验收应落在导出产物与编译闭环，而非只看源代码。
- 当前实现依据：`TDPostprocessBuild.cs` 的 TGA legacy 回调、`EditorUtil.Build.BuildPlayer` 的 HybridCLR Development 对齐与 fail-fast 校验、`PipifyHybridClrDevelopmentBuildTests` 的回归覆盖。

## 关联

- HybridCLR BuildPlayer 时序：[[ADR-028-hybridclr-copy-aot-after-buildplayer|ADR-028]]
- Pipeline 禁止静默跳过：[[PAT-58-pipeline-fail-fast-no-silent-skip|PAT-58]]
- 检测必须穷举失败模式：[[PAT-88-detection-script-failure-mode-exhaust|PAT-88]]
- 相邻的 iOS 回调顺序问题：[[PAT-152-runtime-initialize-unitask-cross-stage|PAT-152]]
