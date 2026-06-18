---
id: PAT-135
title: SerializeReference 跨格深拷贝陷阱
summary: boxedValue 跨格须 JsonUtility 深拷贝
category: editor
type: pattern
status: active
date: 2026-06-03
aliases:
  - PAT-135-serializeref-crosscell-deepcopy-trap
keywords:
  - PAT-135
  - SerializeReference 跨格深拷贝陷阱
  - PAT-135-serializeref-crosscell-deepcopy-trap
tags: [pattern, methodology, editor, serialization]
related:
  - "[[ADR-059-serializeref-deepcopy-boxedvalue|ADR-059]]"
  - "[[ADR-058-per-panel-dimension-mask|ADR-058]]"
---

# PAT-135：SerializeReference 跨格深拷贝陷阱

## 适用场景（When）

- 在同一 `SerializedObject` 内，把某个 `[SerializeReference]` 多态字段从一个 propertyPath 拷贝到另一个 propertyPath（维度分裂 / 同组广播 / 跨格同步）。
- 编辑期内存态（尚未 `CopySerialized` 存盘）需要源格与目标格的实例**彼此独立**。

## 核心做法（What & How）

**禁用** 别名赋值：

```csharp
dstProp.boxedValue = srcProp.boxedValue;          // ❌ 内存态共享同一引用
dstProp.managedReferenceValue = srcProp.managedReferenceValue;  // ❌ 同上
```

**改用** JsonUtility round-trip 产生内存态独立实例：

```csharp
private static object DeepCloneManagedRef(object src)
{
    if (src == null) return null;
    System.Type type = src.GetType();
    object copy = System.Activator.CreateInstance(type);     // 保留运行时多态类型
    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(src), copy);
    return copy;
}
// 调用：dstProp.boxedValue = DeepCloneManagedRef(srcProp.boxedValue);
```

**约束**：被拷贝的配置类须为可 `JsonUtility` 序列化的**叶子数据**（`[Serializable]` 简单值字段）；**禁内嵌 `[SerializeReference]` 多态字段**——`JsonUtility` 不保留嵌套多态，会丢失子引用类型。

## 为什么这么做（Why）

- `boxedValue` 对 managed reference 的 getter/setter 在**编辑期内存态**返回/写入的是**同一对象引用**，并非深拷贝（实测 `ReferenceEquals(src, dst) == True`）。直接赋值令同组各格共享同一实例，跨格编辑互相污染（改 Publish 的字段，Debug 跟着变）。
- **第二层（更普适的教训）**：序列化深拷贝的正确性验证**必须覆盖编辑期内存态**，不能只用 `CopySerialized` / 存盘 round-trip 验证——存盘必然触发一次完整重序列化、天然分裂出独立实例，会**掩盖**内存态的共享 bug。

## 反模式（Anti-patterns）

- **「round-trip 测过实例独立就收工」**：用 `EditorUtility.CopySerialized` 存盘往返验证「实例独立 PASS」，漏测内存态共享。本次 `ADR-DRAFT-serializeref-deepcopy-boxedvalue` 正是栽在这——P1 硬门 PASS=3 却埋下跨格污染 bug，错误结论一度写入知识库误导后续。
- **「以为 boxedValue / managedReferenceValue 赋值即深拷贝」**：直接 `dst.boxedValue = src.boxedValue` 当成「保留多态 + 实例独立」，结果同组格共享对象，改一格污染全组。

## 跨项目复用提示

- 任何 Unity 项目用 `[SerializeReference]` 做多态配置、且需要跨字段 / 跨格 / 跨集合元素拷贝时通用。
- 非 Unity 技术栈无关；但「深拷贝验证须覆盖运行时内存态、而非仅存盘往返」这条二层教训跨栈通用。

## 关联

- 规范落点：建议补入统一工程约束或 C# 风格规则的 Editor 序列化小节。
- 落地位置：Nova `EditorUtil.Config.DimensionProjector.FillGroupSerializedRef`，已用 `DeepCloneManagedRef` 修复。
- 相关 ADR：[[ADR-059-serializeref-deepcopy-boxedvalue|ADR-059]]（其「boxedValue 实例独立」结论被本 PAT 部分证伪）、[[ADR-058-per-panel-dimension-mask|ADR-058]]
- 相关 Pattern：[[PAT-136-symptom-driven-debug-trap|PAT-136]]
