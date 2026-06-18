---
id: PAT-122-sample-devpathprefix-no-trailing-slash
title: sample devPathPrefix 不得带尾斜杠 + Python 兜底
summary: devPathPrefix 无尾斜杠加 rstrip 兜底
category: workflow
type: pattern
status: active
date: 2026-05-29
aliases:
  - PAT-122-sample-devpathprefix-no-trailing-slash
  - PAT-122
keywords:
  - PAT-122-sample-devpathprefix-no-trailing-slash
  - sample devPathPrefix 不得带尾斜杠 + Python 兜底
  - PAT-122
tags: [pattern, workflow, sample, path-rewrite, defensive-coding]
related:
  - "[[PAT-63-upm-sample-readonly-prefab-path-override|PAT-63]]"
  - "[[PAT-78-sample-demo-full-flow-sop|PAT-78]]"
  - "[[PAT-121-publish-sample-rewrite-symmetric|主/子包路径重写对称]]"
---

# PAT-122：sample devPathPrefix 不得带尾斜杠 + Python 兜底

## 适用场景（When）

- Nova 任意 UPM 包根目录 `nova-samples.json` 描述符的 `devPathPrefix` 字段编辑/复制时
- C# `SamplePathRewriter.LocateSampleRoot` 通过 `Path.GetFileName(devRoot)` 反推 sample 末段目录名以匹配外部工程 import 后真实根
- 发版脚本 Python `_load_pkg_samples_descriptor` 加载描述符时

## 核心做法（What & How）

**铁律**：`devPathPrefix` 字段值必须以 sample 末段目录名结尾，**禁带任何尾随分隔符**。

| 形式 | 是否合法 | 备注 |
|---|---|---|
| `Assets/Samples/MainDemo` | ✅ | 推荐写法 |
| `Assets/Samples/LoginDemo` | ✅ | |
| `Assets/Samples/LoginDemo/` | ❌ | `Path.GetFileName` 返回空串 → rewriter 静默退出 |
| `Assets/Samples/LoginDemo\\` | ❌ | 同上 |
| ` Assets/Samples/LoginDemo` | ❌ | 前导空格也禁止 |

**双层防御**（源数据 + 加载兜底）：

1. **源数据守门**（首要）：编辑 `nova-samples.json` 时按上表写
2. **Python 加载兜底**：`_load_pkg_samples_descriptor` 加载时强制归一

```python
def _load_pkg_samples_descriptor(pkg_dir: Path) -> list[dict]:
    raw = json.loads((pkg_dir / "nova-samples.json").read_text(encoding="utf-8"))
    samples = raw.get("samples", []) or []
    for d in samples:
        if "devPathPrefix" in d and isinstance(d["devPathPrefix"], str):
            d["devPathPrefix"] = d["devPathPrefix"].rstrip("/").rstrip("\\")
    return samples
```

**自检步骤**：

1. PR 含 `nova-samples.json` 改动时，reviewer 必须人眼扫 `devPathPrefix` 字段确保无尾斜杠
2. 发版 dry-run 时观察 Python 输出 `[INFO] sample <name> devPathPrefix=<value>`，目视确认末字符不是 `/`
3. 外部工程 import sample 后，主动跑测试场景验证 SamplePathRewriter 是否实际重写——若 SO 路径仍指向开发态前缀，定为本 bug

## 为什么这么做（Why）

C# `Path.GetFileName` 的行为是**末尾分隔符敏感**：

```csharp
Path.GetFileName("Assets/Samples/LoginDemo")   // → "LoginDemo"  ✅
Path.GetFileName("Assets/Samples/LoginDemo/")  // → ""           ❌
```

下游链路：

```csharp
// SamplePathRewriter.LocateSampleRoot 简化版
string devTail = Path.GetFileName(devRoot);  // 空串
if (string.IsNullOrEmpty(devTail)) return;   // 静默退出，不重写
```

后果：外部工程 import 后，sample 内 scene / SO / prefab 内的硬编码路径**仍指向开发工程绝对前缀**`Assets/Samples/LoginDemo/...`，运行时 `LoadAsync` 直接找不到资源，**而 Console 不会报错**——因为 rewriter 执行了，只是没改任何东西。

**这是最难调的一类 bug**——

- 不报异常
- 不抛警告
- 表象是"sample 看起来 import 成功了"
- 只在跑业务流程触发资源加载时才暴露

`devPathPrefix` 是 0.0.10 LoginDemo 首次发版引入的字段，复制 MainDemo 模板时手滑加了尾斜杠 → 0.0.11~0.0.13 三个版本全踩，直到 0.0.14 才定位。**双层防御**确保即使源数据再次手滑，Python 也会兜底归一，不会再让这类隐式失败漏到外部工程。

## 反模式（Anti-patterns）

- **「字符串末尾加个斜杠看着对称」**：路径字段美学优先级低于语义正确性，宁可不对称也不能加
- **只在源数据守门，不加 Python 兜底**：人会犯错；文档约定容易在 1 年后被新人忽略；机器层兜底零成本
- **只在 Python 兜底，不在 C# 兜底**：本 Pattern 主张优先 Python（修源头）；但若决定额外加 C# 防御也合理，需要在 `LocateSampleRoot` 加 `devRoot.TrimEnd('/', '\\')` 后再 `GetFileName`——但这是次优解，因为问题已经传到运行时
- **修了一处样本忘了其他样本**：本铁律对**所有** sample 描述符生效，不限主包/子包；新建 sample 用模板时必须复检
- **改 C# rewriter 时不验证 sample 路径生效**：SamplePathRewriter 改动后必须在外部工程 import 后跑一次完整 sample 流程，不能只看 import 是否成功

## 跨项目复用提示

任何「跨项目共享元数据 + 路径字符串前缀替换」的发版工具都有此风险：

- **C# / Java / Go / Rust** 的 path API 普遍有"末尾分隔符敏感"行为（`Path.GetFileName` / `File.getName` / `filepath.Base` / `Path::file_name`）——具体语义需查文档
- **通用模式**：「字符串路径」字段的格式约束应在**最贴近源数据**的层加防御性归一（加载层、解析层、反序列化后归一化），让下游链路接到的总是规范化形式
- **配套工程实践**：路径字段加 schema 校验（如 JSON Schema `pattern` 排除尾斜杠），CI 检查不通过即拒 PR

