---
id: GLO-08
title: DataMaster 分流用户属性口径（app_version / install_time 必传）
summary: 两条必传分流属性的口径：版本号 + 安装时间
category: module
status: active
date: 2026-07-07
source: cur-session
aliases:
  - GLO-08-datamaster-user-properties
keywords:
  - GLO-08
  - DataMaster用户属性
  - app_version
  - install_time
tags: [glossary, module, sdk, datamaster, abtest]
related: []
---

# GLO-08：DataMaster 分流用户属性口径（app_version / install_time 必传）

DataMaster 拉取配置（`RefreshFromServer` 的 `userProperties`）用于服务端分流与规则匹配。以下为口径约定。

## 必传字段（红线）

`app_version` 与 `install_time` 是**必传字段**，缺失会影响服务端分流命中。业务在触发拉取（登录）前经 `SetUserProperty` 设置。

| 属性 | 类型 | 口径 |
|---|---|---|
| `app_version` | number（int） | 整数版本号，见下方合成算法 |
| `install_time` | number（long） | 首次安装毫秒时间戳（13 位 = ms，非 s） |
| `country_code` | string | 国家码，如 `US`（示例分流条件，非必传） |

## app_version 合成算法（全平台通用）

把 `x.y.z` 三段版本号合成一个 int：

```
code = major * 1_000_000 + minor * 1_000 + patch
```

- `"1.0.0"` → `1000000`；`"1.10.3"` → `1010003`。
- 每段 < 1000 时唯一且可比较；`major < 2000` 时 int 不溢出。
- 非数字段按 0 计；结果 ≤ 0 时兜底 `1`。

## 被否决的合成算法

`x*1000 + y*100 + z`：minor ≥ 10 时冲突——`1.10.0` = `2000` 与 `2.0.0` = `2000` 撞车。故弃用。

## install_time 单位判定

时间戳位数区分单位：秒级 10 位、毫秒级 13 位。`1780078403000` 为 13 位 = 毫秒。取值用 `DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()`。

## 来源（Origin）
- 会话日期：2026-07-07
- 关键对话节选：
  > 用户：app_version、install_time 这些是必须属性
  > 用户：app_version应该也是一个number类型
  > 用户：x.y.z 是否可以按照 x*1000+y*100+z？→ AI 指出 1.10.0 撞 2.0.0，改用 maj×1e6+min×1e3+patch
  > 用户：还要记住 app_version、install_time 是必传字段

## 关联
- 相关 ADR：[[ADR-071-datamaster-topicid-is-params-key|ADR-071]]
