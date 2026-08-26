---
id: RES-005
title: PSD2UGUI 商业插件内部 UPM 改造与升级重放记录
summary: PSD2UGUI 内部 UPM 改造与升级重放依据
category: external
status: active
type: resource
date: 2026-08-26
source: nova-internal-cn-efunstudio-psd2ugui
author: Nova team
aliases:
  - RES-005-psd2ugui-internal-upm-migration
  - PSD2UGUI 内部 UPM 改造记录
keywords:
  - PSD2UGUI
  - PSD2UIForm
  - cn.efunstudio.psd2ugui
  - unitypackage
  - file UPM
  - MonoScript
  - Psd2UIFormConfig
  - Nova-internal
tags: [resource, nova, unity, upm, editor, third-party]
related:
  - "[[PAT-41-upm-package-layout-and-manifest|PAT-41]]"
  - "[[PAT-119-upm-private-fork-local-diff-marking|PAT-119]]"
  - "[[PAT-141-vendor-source-readonly|PAT-141]]"
---

# RES-005：PSD2UGUI 商业插件内部 UPM 改造与升级重放记录

## 来源与边界

- 官方交付物：`PSD2UGUI_PRO_2026.8.6.unitypackage`。
- 内部包：`../Nova-internal/UPMPackages/cn.efunstudio.psd2ugui`。
- 包名：`cn.efunstudio.psd2ugui`，版本：`1.0.0`，核心版本：`release-2026.8.6`。
- 该插件是商业资产，只允许保存在 `Nova-internal`，不进入公开 Nova 仓库或公开镜像。
- 官方原始 unitypackage 原样归档在包内 `Nova/Docs/`，作为升级对比和来源证明；官方输入 SHA-256 为 `6d360e3ace9043e090d8a08b82665f125f6626f57c509aa5ec736dda07ceba48`。

当前实现事实以内部包的 `Nova/Docs/UPGRADE_GUIDE.md`、源码和 DLL 为准，本文负责记录改造原因、稳定契约和重放顺序。

## 为什么不能直接把源码搬进 UPM

官方 unitypackage 默认位于 `Assets/Plugins/PSD2UIForm`。改成外置 file UPM 后，源码程序集身份、资源路径和 `Psd2UIFormConfig.asset` 中的 `MonoScript` 引用都会变化。配置 asset 即使文件内容仍在，也可能因为脚本 GUID/fileID 不匹配而在 Inspector 中无法显示字段。

最终采用 Editor-only 预编译 DLL：

- 可执行程序集位于 `Core/PSD2UIForm/Libs/cn.efunstudio.psd2ugui.dll`。
- 可维护源码位于 `Core/PSD2UIForm/Source~/`，通过 `.npmignore` 排除，不参与消费端编译。
- DLL `.meta` GUID 固定为 `a6c1e4c70b6d4cf5b4c7b8f61a236f90`。
- `UGUIParser` 的 DLL MonoScript fileID 固定为 `-231125866`。
- 包内默认 `Psd2UIFormConfig.asset` 必须引用上述 GUID/fileID。
- 当前 DLL 由 Unity `6000.4.2f1` 编译，SHA-256 为 `af8f3e0273a0bc246382fc30b36e3be9631c610eb1b326bdc76b5f4e7191e098`。

## 包装与依赖

- 官方 `Assets/Plugins/PSD2UIForm` 内容归入内部包 `Core/PSD2UIForm`。
- 包根补充 Nova 标准 `package.json`、README、CHANGELOG、许可说明与 `Nova/Docs`。
- Nova 工程使用本地依赖：

```json
"cn.efunstudio.psd2ugui": "file:../../Nova-internal/UPMPackages/cn.efunstudio.psd2ugui"
```

- 插件同时使用 `UnityEngine.UI` 与 `Unity.TextMeshPro`；Unity 6000.4 中均由 `com.unity.ugui@2.0.0` 提供，因此不额外依赖已停止维护的独立 `com.unity.textmeshpro` 包。
- file UPM 的真实物理位置必须通过 `UnityEditor.PackageManager.PackageInfo.resolvedPath` 解析，不能把 `Packages/...` 简单拼接到工程根目录。

## 配置资产契约

包内 `Psd2UIFormConfig.asset` 只作为只读模板，不作为消费工程的可编辑配置。用户生成的配置必须位于消费工程 `Assets/`，才能正常编辑、保存并提交到项目版本库。

配置查找顺序：

1. 当前场景位于 `Assets/Samples/` 时，从场景目录逐级向上寻找最近的 `Editor/Psd2UIFormConfig.asset`。
2. 未命中场景配置时，仅在 `Assets/` 中存在唯一有效配置时使用该配置。
3. 存在多份配置时不依赖 `FindAssets(...).FirstOrDefault()` 的不稳定顺序，也不持久化额外的项目 GUID。
4. 都未命中时保持未配置，不回退到包内模板。

安装、升级和 Unity 启动阶段不主动提示。只有用户右键 PSD/PSB 并进入 `Psd2UIForm Editor` 时才检查配置；缺少配置时询问是否从包内模板生成，取消后终止本次转换。不额外增加 Nova 菜单项或项目设置 JSON。

手动复制配置时只复制 `.asset` 内容，不复制包内 `.meta`，避免重复 GUID。旧配置若 MonoScript GUID/fileID 与当前 DLL 不一致，也不会被识别为有效 `UGUIParser`。

## 导出行为修正

- “导出配置 json”默认从当前生效配置 asset 的同级目录开始选择。
- “导出 PS 脚本工具”只读取包内 JSX 模板，在内存中注入当前配置后输出，不回写 UPM 包。
- 检测到 Photoshop 时输出到其脚本目录；没有检测到 Photoshop 时允许用户选择导出目录。

## TMP 动态字体注册修正

`TMP_FontAsset.CreateFontAsset` 会预先缓存字体与材质 hash。插件随后按 padding 重命名字体资源并替换材质时，旧实现没有刷新缓存，导致同一源字体生成 P16、P32 等资源后仍可能用同一个旧材质 key 注册，并在 `MaterialReferenceManager.AddFontAsset` 抛出重复 key 异常。

本地 fork 在动态字体最终命名和材质替换后刷新 `hashCode`、`materialHashCode`，材质名使用最终字体资源名保证不同 padding 唯一；注册前再次刷新，并先调用 `TryGetFontAsset` 避免同一字体重复注册。该修复不改变公开 API、配置结构或已有字体资源格式，现有已生成字体会在下次注册时使用修正后的 hash。

## 本地 fork 维护规则

- 修改过的源码文件使用 `// modify: local fork` 标记。
- 所有行为变化同步记录到包级 `CHANGELOG.md` 和 `Nova/Docs/UPGRADE_GUIDE.md`。
- 已删除没有任何调用入口的临时 `Psd2UIFormConfigRepair.cs`；不得重新引入消费端自动创建、重写或保存包内配置的逻辑。
- 新手引导保留手动入口，但移除 `[InitializeOnLoad]`，避免安装、升级或启动时自动弹窗。

## 官方升级重放顺序

1. 原样归档新的官方 unitypackage，并记录 SHA-256。
2. 在临时目录解包，对比文件清单、asmdef、许可和依赖，不直接覆盖现有内部包。
3. 保留内部包外壳、`Nova/Docs`、本地 fork 标记和配置契约，再替换官方核心内容。
4. 重新确认 uGUI/TMP 依赖和目标 Unity 版本下的程序集引用。
5. 将可维护源码放入 `Source~/`，逐项重放路径解析、项目配置、右键检查、场景配置路由与导出修正。
6. 确认没有重新引入配置自修复死代码或安装/升级自动提示。
7. 用目标 Unity 版本重新编译 DLL，恢复既有 DLL `.meta`，保持 GUID/fileID 不变。
8. 更新 DLL SHA-256、CHANGELOG 与升级指南。
9. 验证配置 Inspector、右键 PSD 流程、Demo 就近配置、JSON/PS 脚本导出和 Console。

## 验证依据

- Unity `6000.4.2f1` batchmode 编译成功，日志包含 `Exiting batchmode successfully now!`。
- Unity 重载后确认 DLL 包含 237 个类型及新增注册方法；现有 P16、P32 字体注册成功，材质 hash 分别为 `-939643037` 与 `449737189`，未再触发重复 key，且两份字体资产均未被标记为 dirty。
- 当前 DLL、包内配置 asset 的 GUID/fileID 已静态核对一致。
- 安装提示相关入口、版本状态与 `UserSettings/Nova/Psd2UIForm.json` 已移除。
- 内部包 `git diff --check` 通过。

## 关联

- UPM 布局：[[PAT-41-upm-package-layout-and-manifest|PAT-41]]
- 私有 fork 留痕：[[PAT-119-upm-private-fork-local-diff-marking|PAT-119]]
- 第三方源与授权边界：[[PAT-141-vendor-source-readonly|PAT-141]]
