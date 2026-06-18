---
id: PAT-67
title: 全局禁 UnityEngine.UI.Text，UI 文字一律 TMP
summary: UI 文字一律 TMP，禁 UGUI Text
category: runtime
type: pattern
status: active
date: 2026-05-22
aliases:
  - PAT-67-no-ui-text-only-tmp
keywords:
  - PAT-67
  - PAT-67-no-ui-text-only-tmp
  - 全局禁 UnityEngine.UI.Text，UI 文字一律 TMP
tags:
  - pattern
  - ui
  - text
  - tmp
  - unity
related:
  - "[[PAT-66-no-handcraft-prefab|PAT-66]]"
---

# PAT-67：全局禁 UnityEngine.UI.Text，UI 文字一律 TMP

## 适用场景（When）

- 新建任何 UI prefab / UI 节点需要显示文字
- 旧 prefab / 旧脚本里出现 `UnityEngine.UI.Text` 字段或组件
- 业务脚本声明 UI 文字字段（`[SerializeField] private Text m_Title` 之类）
- 触发信号：本能想 `manage_components add UnityEngine.UI.Text` 或 `using UnityEngine.UI; ... Text m_Xxx`——这就是该停下换 TMP 的时刻

## 核心做法（What & How）

| 操作 | 做法 |
|---|---|
| 新建 UI 文字组件 | `manage_components action=add component_type=TMPro.TextMeshProUGUI`，**禁** `UnityEngine.UI.Text` |
| 业务脚本字段类型 | `using TMPro;` + `[SerializeField] private TMP_Text m_Title;`（接口型）；强类型可写 `TextMeshProUGUI` |
| asmdef 引用 | references 数组追加 `"GUID:6055be8ebefd69e48b49212b09b47b2f"`（Unity.TextMeshPro 包），否则 CS0246 找不到命名空间 |
| 设置文本/字号/颜色 | `m_text` / `m_fontSize` / `m_fontColor` / `m_HorizontalAlignment` / `m_VerticalAlignment` |
| 关闭自动换行 | `m_TextWrappingMode = 0`（**注意**：旧字段 `m_enableWordWrapping` 已废，新 API 是 TextWrappingMode） |
| 字体资产 | 默认 LiberationSans SDF；中文工程必须配中文 TMP SDF + Fallback Chain（后续单独规范） |

**铁律：**
1. 旧 prefab/脚本发现 `UnityEngine.UI.Text` 即换，**不允许**新增
2. 改 prefab 通过 `manage_prefabs` + `manage_components`，**禁**手搓 YAML（见 [[PAT-66-no-handcraft-prefab|PAT-66]]）
3. asmdef 引用补全后必须 `refresh_unity` 触发编译，再 `read_console` 确认无 CS0246

## 为什么这么做（Why）

- **渲染质量**：Text 走 dynamic font 动态贴图，缩放/旋转/小字号下采样质量差；TMP 走 SDF，任意缩放保持锐利、原生支持 Outline/Underlay/Glow
- **包体与性能**：Text 每个字号一份位图，中文 dynamic font 在 IL2CPP 下首次渲染抖动明显；TMP 一份 SDF 资产服务所有字号
- **多语言一致性**：Text + Fallback 在中英混排、Emoji、CJK 表现不稳定；TMP Fallback Chain 是项目级配置，统一可控
- **维护成本**：mix Text + TMP 会让 Outline / Layout / RectTransform 适配脚本两套都要写，统一 TMP 一套就够

## 反模式（Anti-patterns）

- **"先用 UI.Text 跑通再换 TMP"**：永远不会换。Text → TMP 涉及字段类型重写 + prefab 引用重绑 + asmdef 改动，临时凑合一定会留下来
- **"小图标/版本号这种地方用 Text 无所谓"**：哪怕单字符，包体里一旦同时存在 dynamic font 资产与 TMP SDF，包就大一截；规则一刀切才好执行
- **复制旧 prefab 改字号忘记换组件**：旧资产带 Text 组件直接 Duplicate 出来——必须删 Text 加 TextMeshProUGUI，不能"反正字段对就行"
- **asmdef 不引 TMP**：脚本里用 `TMP_Text` 编译报 CS0246 后注释掉 using 改回 Text，**回退方向错了**——正确做法是在 asmdef 加 `GUID:6055be8ebefd69e48b49212b09b47b2f`

## 跨项目复用提示

- Unity 项目通用规则。新项目第一天就该立这条铁律，别等积累遗产再迁移
- TMP 需要 Essential Resources（首次菜单 `Window > TextMeshPro > Import TMP Essential Resources`）
- 中文项目额外需要中文 SDF 字体资产 + Fallback Chain 配置；规则本身不依赖具体字体方案
- 对应到其他引擎：UE UMG 的 `TextBlock` vs `TextBlock with Slate Font Sets`、Godot 的 `Label` vs `RichTextLabel` 也有类似分歧——核心原则是"动态位图字体 vs SDF 字体二选一，全局统一一种"

## 关联

- 规范落点：拟更新统一工程约束或新增 `framework-ui-text-tmp-only.md`，列入零容忍清单
- 相关 Pattern：[[PAT-66-no-handcraft-prefab|PAT-66]]（同属 UI 工作流铁律组）
- 相关铁律：UI 文本统一使用 TMP 体系
