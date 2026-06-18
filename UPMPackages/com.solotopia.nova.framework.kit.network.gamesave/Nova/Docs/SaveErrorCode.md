# SaveErrorCode

## 1. 简介

`SaveErrorCode` 是游戏存档（云存档）业务错误码占位类，约定段位 8000~8999，与 `NetErrorCode` 通用段、`LoginErrorCode`（7000~7999）互不冲突。当前为空类，实际错误码常量随业务推进补充。

**所在文件：** `Nova/Scripts/Runtime/SaveErrorCode.cs`
**命名空间：** `NovaFramework.Kit.Network.GameSave.Runtime`
**类签名：** `public static class SaveErrorCode`

---

## 2. 公开 API

> **当前版本无已定义常量。** 以下为约定的段位规划，待后续补充。

| 段位 | 说明 |
|---|---|
| `8000~8099` | 游戏存档流程通用错误（待填充） |
| `8100~8999` | 业务扩展段（待填充） |

---

## 3. 使用示例

```csharp
// 当前仅用于 switch/case 分支预留，实际常量待后续版本填充
var resp = await gameSave.SetAsync("Bag", bagJson);
if (!resp.IsSuccess)
{
    // 8000~8999 为游戏存档业务专属段（当前无已定义常量）
    if (resp.ErrorCode >= 8000 && resp.ErrorCode < 9000)
    {
        // 游戏存档业务错误
    }
}
```

---

## 4. 内部约束

- **段位互斥**：`NetErrorCode` 使用负数（客户端）+ 1000/5000/6000/6001（服务端通用）；`LoginErrorCode` 占用 7000~7999；本类从 8000 起，避免混用。
- **不强制在此类扩展服务端通用错误**：服务端通用协议级错误（如 PARAM_ERROR/SERVER_ERROR）统一用 `NetErrorCode`；本类仅收录游戏存档语义相关的业务错误码。

---

## 5. 关联

- 同包：[Save.md](./Save.md) — 游戏存档业务 Service
