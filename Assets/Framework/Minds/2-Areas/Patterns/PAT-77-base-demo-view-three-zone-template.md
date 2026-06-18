---
id: PAT-77
title: BaseDemoView 三段式 Demo View 模板
summary: 标题栏+交互区+反馈区三段式+Nova门面API演示
category: docs
type: pattern
status: active
date: 2026-05-23
aliases:
  - PAT-77-base-demo-view-three-zone-template
keywords:
  - BaseDemoView 三段式 Demo View 模板
  - PAT-77
tags:
  - pattern
  - methodology
  - sample
  - demo
  - ui
  - template
related:
  - "[[PAT-65-demo-coverage-standard|PAT-65]]"
  - "[[ADR-033-maindemo-isolated-topology|ADR-033]]"
  - "[[PAT-64-business-ui-view-suffix|PAT-64]]"
---

# PAT-77：BaseDemoView 三段式 Demo View 模板

## 适用场景（When）

- Nova 框架 / SDK / Kit 的演示 sample UI 子页面。
- 跨多个模块批量产出 demo 时需要统一基线。
- 任何“演示 API 门面 + 交互入口 + 滚动反馈”的 UI 场景。

## 核心做法（What & How）

### 三段式骨架（竖屏 768×1666 设计分辨率，match-by-width=0）

> R3-R4 已将画布从横屏 1920×1080 改为竖屏 768×1666；CanvasScaler 由 UIComponent.ApplyInstanceRootCanvasScaler 运行时注入，Prefab 不写死画布尺寸。

```text
┌──────── TitleBar 顶部 120px ────────────────────────────────┐
│   [API 副标题：Nova.Xxx.Yyy(...)]   <居中标题>   [X 关闭]   │
├──────── InteractionArea 中段 anchor(0,0.4)~(1,1) 拉伸 ──────┤
│   各 demo 自填：按钮 / 输入框 / 下拉 / 单复选 / 滑块        │
│   只读模块在此渲染信息卡片（不附按钮）                       │
│   交互模块在此放真按钮触发 API                               │
├──────── FeedbackArea 底部 anchor(0,0)~(1,0.4) 占 40% ──────┤
│   > Nova.Sound.PlaySound("BGM_001") → SerialID=1003         │
│   > Nova.Event.Fire(this, eventArgs) → handlers=2           │
└──────────────────────────────────────────────────────────────┘
```

**竖屏画布关键参数：**

| 参数 | 值 |
|---|---|
| 设计分辨率 | 768 × 1666 |
| ScreenMatchMode | MatchWidthOrHeight（m_ScreenWidthHeightMatchValue = 0，全 width-match） |
| TitleBar 高度 | 120px（顶部固定，anchorMin=(0,1) anchorMax=(1,1)，sizeDelta=(0,120)） |
| InteractionArea | anchor 比例式：anchorMin=(0,0.4) anchorMax=(1,1)，pivot=(0.5,1)，anchoredPos=(0,-120)，sizeDelta=(0,-120)（顶留 120px 给标题） |
| FeedbackArea | anchor 比例式：anchorMin=(0,0) anchorMax=(1,0.4)，pivot=(0.5,0)，anchoredPos=(0,0)，sizeDelta=(0,0)（占父容器高度 40%） |
| RootBackground | 深灰 RGBA (0.05, 0.05, 0.08, 1)，Prefab index 0，35 个 Variant 自动继承 |
| CanvasScaler | 运行时由 UIComponent.ApplyInstanceRootCanvasScaler 注入，Prefab 不写死 |

### 抽象基类 `BaseDemoView : UIView` 强制 API

| 成员 | 修饰符 | 职责 |
|---|---|---|
| `SetTitle(string text)` | protected | 顶部居中标题文本 |
| `SetApiHint(string apiSig)` | protected | 标题栏左侧 API 副标题，例如 `Nova.UI.OpenUIViewAsync<T>()` |
| `InteractionRoot` | protected RectTransform | 子类挂自己的交互区子元素 |
| `AppendFeedback(string line, FeedbackLevel level)` | protected | 反馈日志（强制带 `>` 前缀 + level 颜色） |
| `ClearFeedback()` | protected | 清空反馈区（用户手动按钮触发） |
| 右上角 X | 默认行为 | 调 `Nova.UI.CloseUIView(this)` |

### 每个 DemoXxxView 的强制约束（与 PAT-65 8 维矩阵协同）

| 约束 | 说明 |
|---|---|
| 必须演示 `Nova.<Module>.<Api>` 门面 | 禁止直接使用内部 Manager 引用（如 `IUIManager.GetUIView`），必须走 `Nova.UI.GetUIView` |
| 顶部 API 副标题强制 | 每个 demo 一进入页面用户就看到"日常代码长什么样" |
| 反馈日志强制带 API 前缀 | 形如 `> Nova.Sound.PlaySound(...) → SerialID=1003`，把按钮点击与 API 调用绑死 |
| 单一 API 主题 | 一个 demo 只演示一个 API 主题（含其重载折叠代表，参考 PAT-65） |
| 关闭通过 X 按钮 | 不允许 ESC / Android 返回键作为唯一退出，X 必须可见 |
| `PauseCoveredUIView=false` | 子页面打开时主菜单（DemoNavTreeView）保持可见、不暂停，被压在底层 |

### 两类变体

| 变体 | 适用模块 | 交互区形态 |
|---|---|---|
| **只读快照型** | Config / Localization / Table 数据展示、HybridCLR 启动期事实、Procedure FSM 状态 | 渲染信息卡片，不附按钮 |
| **交互触发型** | UI / Event / Sound / Vibrate / Network / Persist / SDK / Asset / Prefab 等 | 真按钮触发 Nova 门面 API，反馈区实时打印结果 |

### Prefab 制作约束

| 约束 | 说明 |
|---|---|
| 单一 prefab + 单一 MB | 一个 DemoXxxView 一个 prefab + 一个 MonoBehaviour 子类 |
| 命名 `DemoXxxView` | 与 PAT-64「业务 UI 一律 View 后缀」对齐 |
| 路径分离 | 代码在 `Scripts/Runtime/UIs/DemoXxxView/`，prefab 在 `Prefabs/UIs/DemoXxxView/` |
| 纯色块 + TMP | R5 起弃用 SpriteAtlas；按钮白底黑字 fontSize=28 不换行居中；标签白字 fontSize=24（详 PAT-79） |
| Asset 地址格式 | 裸文件名（不含目录前缀），如 `DemoNavTreeView`；BundleCollectorSetting 统一使用 `AddressByFileName`，AssetLocation 与生成地址一一对齐 |

## 为什么这么做（Why）

- 三段式骨架让 demo 切换时不用重新理解布局。
- API 副标题把 demo 和日常代码直接绑定。
- 反馈日志带 API 前缀，能把 UI 点击和实际调用对齐。
- sample 必须演示真实使用方式，而不是内部 Manager 绕法。
- `PauseCoveredUIView=false` 让导航树保持可见。

## 反模式（Anti-patterns）

- **直接调内部 Manager**：`IUIManager.OpenUIView(...)` 演示出来用户也学不会日常用法 → 必须 `Nova.UI.OpenUIViewAsync<T>()`
- **API 副标题缺失**：用户进入 demo 看不到"对应代码长什么样" → 顶部必须有
- **反馈日志不带 API 前缀**：`> 播放成功` 这类日志没有教学价值 → 必须 `> Nova.Sound.PlaySound("BGM_001") → SerialID=1003`
- **打开子页面时关闭主菜单**：用户每次返回都要重新打开导航树 → `PauseCoveredUIView=false`
- **三段式硬塞**：把交互+反馈塞同一区滚动 → 反馈区必须独立、固定底部 320px
- **一个 demo 多个 API 主题**：`DemoSoundView` 既演示 `PlaySound` 又演示 `LoadSoundDataAsync` → 拆成两个 demo
- **使用 SpriteAtlas / 图片资源**：R5 起全部弃用；UI 一律纯色块 + TMP 文字，不引用任何 Sprite（详 PAT-79）

## 订正：BaseDemoView 去 abstract + Variant 单一真相源（统一 8 sample）

上文「基类 `BaseDemoView : UIView` 强制 API」原记为「抽象基类」，现订正：`BaseDemoView` 已改为**非 abstract**。

- `BaseDemoView.prefab` 根节点挂 `BaseDemoView` 组件并绑好 7 个三段式字段（`m_TitleText`/`m_CloseButton`/`m_InteractionRoot`/`m_FeedbackContent`/`m_FeedbackLineTemplate`/`m_ClearFeedbackButton`/`m_FeedbackScrollRect`），作字段注入**单一真相源**。
- 每个 `DemoXxxView` prefab 是该 base 的 **Prefab Variant**：`AddComponent` 子类 → 从继承的 base 组件复制 7 字段 → 删继承的 base 组件（Unity 禁改 variant 继承组件的脚本，故删而非换）→ 建白底黑字示例按钮绑 `m_SampleButton`。
- 由 `nova-create-sample` 阶段 2.5 全自动生成（`execute_code` + `PrefabUtility`，禁手搓 YAML）。
- 全 8 个 sample（MainDemo + Firebase/IAP/GameSave/Ad/Login/TGA/Appsflyer）已统一此范式；MainDemo 33 个 + 6 业务入口 view 均为 Variant，GUID 保留。
- 三段式骨架 / API 副标题 / 反馈日志带前缀等本 PAT 核心契约不变。
