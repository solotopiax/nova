# NetResponse&lt;T&gt;

## 1. 简介

`NetResponse<T>` 是业务层网络请求结果的泛型包装，业务侧通过 `IsSuccess` / `ErrorCode` / `Data` 三字段读取请求结果，无需感知底层 HTTP 或加解密过程。

**所在文件：** `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetResponse.cs`
**命名空间：** `NovaFramework.Runtime`
**类签名：** `public sealed class NetResponse<T> where T : IMessage<T>`

---

## 2. 公开 API

### 属性（只读）

| 签名 | 说明 |
|---|---|
| `public bool IsSuccess { get; }` | 请求是否成功 |
| `public int ErrorCode { get; }` | 错误码；成功时为 `NetErrorCode.SUCCESS`（0） |
| `public string ErrorMessage { get; }` | 错误描述；成功时为空字符串 |
| `public T Data { get; }` | 业务响应 Proto 数据；失败时为 `default`（通常为 `null`） |

### 静态工厂方法

| 签名 | 说明 |
|---|---|
| `public static NetResponse<T> Success(T data)` | 创建成功响应；`ErrorCode` 固定为 `NetErrorCode.SUCCESS`（0），`ErrorMessage` 为空串 |
| `public static NetResponse<T> Fail(int errorCode, string errorMessage)` | 创建失败响应；`Data` 固定为 `default` |

> **构造函数为 `private`**，外部只能通过 `Success` / `Fail` 创建实例。

---

## 3. 使用示例

```csharp
// 业务侧接收 Login 结果
var resp = await Nova.Network.Kit<Login>().Async(cmdRow, ChannelType.Google, openId);

if (resp.IsSuccess)
{
    string uid = resp.Data.Uid;
    // 继续业务逻辑
}
else
{
    // 根据错误码做分支处理
    int code = resp.ErrorCode;
    string msg = resp.ErrorMessage;
    if (code == NetErrorCode.NETWORK_ERROR)
    {
        // 网络不可用提示
    }
}
```

---

## 4. 内部约束

- **不可变**：所有属性均为 `{ get; }` 无 setter，实例创建后状态固定。
- **失败时 `Data` 为 default**：业务侧读取 `Data` 前必须先判断 `IsSuccess`，否则可能触发 NullReferenceException。
- **泛型约束 `where T : IMessage<T>`**：确保 `T` 为 Protobuf 消息类型。

---

## 5. 关联

- 同包：[NetService.md](./NetService.md) — 通过 `NetResponse<T>.Success / Fail` 构造并返回结果
- 同包：[NetErrorCode.md](./NetErrorCode.md) — `ErrorCode` 的值域
- 同包：[NetworkComponent.md](./NetworkComponent.md)
