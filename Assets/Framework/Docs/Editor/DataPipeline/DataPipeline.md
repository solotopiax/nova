# DataPipeline — Excel 预过滤管线

**命名空间**：`NovaFramework.Editor`

DataPipeline 目录当前保留两个 Excel 预过滤器实现类，负责在 Luban CLI 处理之前对原始 Excel 做最小必要预处理，输出符合 Luban 输入格式要求的临时 Excel。

> **Config 预过滤器已移除（批 1）**：`ConfigExcelPreFilter` 已删除，Config 导出流程迁移至 ConfigWindow + `EditorUtil.Config.Exporter` SO 骨架方案。

---

## 目录结构

```
DataPipeline/
└── Implements/
    ├── Localizations/
    │   ├── LocalizationExcelPreFilter.cs
    │   └── LocalizationTextExporter.cs
    └── Networks/
        └── NetworkExcelPreFilter.cs
```

---

## 两个预过滤器概览

| 类 | 文件 | 调用方 | 作用 |
|----|------|--------|------|
| `LocalizationExcelPreFilter` | `Implements/Localizations/LocalizationExcelPreFilter.cs` | `LocalizationTextExporter` | 将多语言列 Pivot 为按语言的 Name+Value 临时 Excel |
| `NetworkExcelPreFilter` | `Implements/Networks/NetworkExcelPreFilter.cs` | `NetworkComponentInspector` | 将有效 Sheet 原样复制到 `_temp/`，仅跳过注释 Sheet / 无效 Sheet / 临时文件 |

---

## 预过滤在 Luban 流水线中的位置

```
原始 Excel
  ↓ XxxExcelPreFilter（DataPipeline）
    输出 _temp/ 目录下的标准 Luban 格式临时 Excel
      ↓ EditorUtil.Luban.Pipeline（Luban CLI）
        生成 C# 类型代码 + JSON 数据文件
```

---

## 关联文档

- [LocalizationExcelPreFilter.md](Implements/Localizations/LocalizationExcelPreFilter.md)
- [NetworkExcelPreFilter.md](Implements/Networks/NetworkExcelPreFilter.md)
- [EditorUtil.Luban.Pipeline.md](../EditorUtil/EditorUtil.Luban/EditorUtil.Luban.Pipeline.md)
