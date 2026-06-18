---
id: MOC-Sound
title: 声音系统图谱
summary: Sound 模块入口、声音组与代理结构速查
category: module
status: active
date: 2026-06-05
aliases:
  - MOC-Sound
  - 声音系统图谱
  - 音效系统图谱
tags: [moc, nova, sound, audio, runtime]
keywords: [SoundComponent, ISoundManager, SoundManager, SoundGroup, SoundAgent, PlaySoundParams, SoundGroupShell]
related:
  - "[[ADR-001-component-manager-three-layer|ADR-001]]"
  - "[[ADR-010-validation-on-consumer-side|ADR-010]]"
  - "[[ADR-011-load-unload-and-ireference-pairing|ADR-011]]"
  - "[[ADR-017-component-manager-isolation|ADR-017]]"
  - "[[ADR-042-assetmanager-load-api-all-return-handle|ADR-042]]"
  - "[[PAT-28-luban-load-release-symmetric|PAT-28]]"
  - "[[PAT-68-pool-reference-spread|PAT-68]]"
  - "[[MOC-Asset]]"
---

# MOC-Sound：声音系统图谱

## 一句话

Sound 模块负责“按声音组组织播放能力”，公开入口是 `Nova.Sound`；它既不是单纯的 AudioSource 包装，也不是资源系统本身。

## 何时查这页

- 要改声音播放、组管理、代理数量或音量控制
- 要判断播放入口该走 `name` 还是 `group + assetLocation`
- 要理解 `SoundGroup / SoundAgent / PlaySoundParams` 的关系

## 当前结构

```text
Nova.Sound
  -> SoundComponent
  -> ISoundManager
  -> SoundManagerBase
  -> SoundManager
     -> SoundGroup
     -> SoundAgent
```

组件入口：

- `Start()` 时用 `SoundSettings + AudioMixer + SoundGroupShell[]` 初始化 Manager
- 组是在组件启动时通过 `AddSoundGroup(...)` 建出来的
- 模块既支持先加载声音表再按 `name` 播放，也支持直接按 `group + assetLocation` 播放

## 高频入口

- `LoadSync / LoadAsync`
- `PlaySound(name)`
- `PlaySound(name, PlaySoundParams)`
- `PlaySound(groupName, assetLocation, PlaySoundParams)`
- `StopSound / PauseSound / ResumeSound`
- `StopGroupSound / PauseGroupSound / ResumeGroupSound`
- `StopAllLoadedSounds / StopAllLoadingSounds`
- `SetSoundGroupVolume`
- `ReleaseAllAsset / ReleaseAssetBySerialID`

## 模块边界

- `SoundGroup` 是逻辑分组，不是业务上的任意标签系统
- `SoundAgent` 是组内实际播放承载单元
- `PlaySoundParams` 是调用期参数对象，不能把它当长期配置存储
- 声音资源加载仍要遵守统一的 Load/Release 约束，Sound 只是播放编排层
- `AudioMixer` 是 `SoundComponent` 暴露的配置面，不应被误写成“完全不可见的内部细节”

## 与其他模块的关系

- `Asset`：真正的音频资源加载与释放依赖资源系统
- `ObjectPool / ReferencePool`：`PlaySoundParams` 和部分播放期数据是高频临时对象
- `Localization`：文本本地化与音频播放是两个表现系统，不要用同一套配置心智去理解

## 导航提醒

- 模块有“已加载声音”和“正在加载声音”两条状态线
- 组是显式创建的，不是按名字第一次播放时自动无限扩展
- `StopAllLoadedSounds` 只处理已加载/已播放声音；正在加载的条目和资源释放要看 `StopAllLoadingSounds / ReleaseAllAsset / ReleaseAssetBySerialID`
- 具体优先级、重载细节与资源释放路径，以 `Docs` 和源码为准。

## 常见误区

- 把声音模块当成直接播放 `AudioClip` 的薄封装
- 忽略 `SoundGroup`，把所有声音都视为同一播放池
- 把 `PlaySoundParams` 当成普通 DTO 长期持有
- 只记得播放，不记得组控制、加载中状态和资源释放是三条不同的控制线

## 先往哪看

- 改结构：[[ADR-001-component-manager-three-layer]]
- 改资源句柄语义：[[ADR-042-assetmanager-load-api-all-return-handle]]
- 改高频临时对象用法：[[PAT-68-pool-reference-spread]]

## 关联

- 图谱：[[MOC-Asset]]、[[MOC-Vibrate]]、[[MOC-Manager]]
