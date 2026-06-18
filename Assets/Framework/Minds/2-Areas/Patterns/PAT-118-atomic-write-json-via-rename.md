---
id: PAT-118-atomic-write-json-via-rename
title: 原子写 JSON 文件（临时文件 + rename）
summary: JSON 写入用临时文件+rename
category: workflow
type: pattern
status: active
date: 2026-05-28
aliases:
  - PAT-118-atomic-write-json-via-rename
  - PAT-118
keywords:
  - PAT-118-atomic-write-json
tags: [pattern, json, fileio, atomic]
related:
  - "[[ADR-047-editor-active-master-anchor]]"
---

# PAT-118：原子写 JSON 文件（临时文件 + rename）

## 适用场景（When）

任何"被多个进程或多次调用并发读"的 JSON / 配置文件，特别是：

- `ProjectSettings/Nova/Globals.json`（C# 写、Python 读）
- 任何"长期存在 + 被外部脚本读"的工程级配置 JSON

非典型场景：纯内存中转 JSON / 单进程内顺序写读 / 短生命期临时文件——这些场景直接 `File.WriteAllText` 即可，不必引入 rename 开销。

## 核心做法（What & How）

不直接 `File.WriteAllText(path, json)`，改用「写临时文件 → 原子 rename 覆盖」两步：

```csharp
public static void WriteAtomic(string path, string content)
{
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);

    var tmp = path + ".tmp";
    File.WriteAllText(tmp, content);
    File.Move(tmp, path, overwrite: true);   // POSIX rename / NTFS MoveFileEx 都是原子操作
}
```

**关键点：**
- 临时文件后缀 `.tmp` 与原文件同目录（`File.Move` 跨分区不是原子的，必须同目录）
- `overwrite: true` 是 .NET 5+ API，旧版本工程改用 `File.Replace` 或先删原文件再 Move（注意非原子）
- 写入失败时 `.tmp` 文件残留，下次写入会被覆盖，不影响数据完整性

## 为什么这么做（Why）

- **并发读安全：** 外部进程（如 nova-publish 的 Python 脚本）随时可能读这个文件；如果用直接写，读到"写半截的 JSON"会触发解析报错
- **崩溃恢复：** 写过程中 Editor 崩溃 / 强退 / 断电，原文件保持完整状态——临时文件可能残留但不影响读
- **POSIX/NTFS 原子语义：** `rename` / `MoveFileEx` 是 OS 层原子操作，文件系统保证读端要么看到旧版要么看到新版，不可能看到中间态

## 反模式（Anti-patterns）

1. **直接 `File.WriteAllText`：** 写半截被并发读 → JSON 解析报错；崩溃丢数据
2. **临时文件放别的目录：** `File.Move` 跨分区/卷会退化为"复制 + 删除"，不是原子操作；并发读端可能看到旧文件被删后新文件还没到位
3. **写完后 `AssetDatabase.Refresh`：** Editor 工具写 `ProjectSettings/` 下的文件**不需要** Refresh（Library/ProjectSettings 不在 AssetDatabase 管辖范围）；非必要的 Refresh 触发 import 风暴
4. **加文件锁（FileShare.None）替代 rename：** 写端持锁期间读端等待 → 拖慢外部脚本；崩溃时锁可能不释放
5. **JSON 序列化失败仍写入：** 必须先在内存里 `JsonUtility.ToJson` 验证不抛异常，再走 WriteAtomic；否则可能写入 partial JSON

## 跨项目复用提示

适用于任何 C# / Python / Node 多进程并发读写 JSON 的场景。各语言对应原生 API：

| 语言 | 原子 rename API |
|------|----------------|
| C# | `File.Move(src, dst, overwrite: true)` (.NET 5+) |
| Python | `os.replace(src, dst)` |
| Node | `fs.renameSync(src, dst)` |

**关键不变量：** 临时文件与目标文件**同目录**；不要跨分区。

