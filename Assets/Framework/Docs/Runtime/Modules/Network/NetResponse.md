# `NetResponse<T>`

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
| `public T Data { get; }` | 业务响应 Proto 原引用；成功时也可为 null，失败时可携带已解析的错误业务体 |

### 静态工厂方法

| 签名 | 说明 |
|---|---|
| `public static NetResponse<T> Success(T data)` | 创建成功响应；`ErrorCode` 固定为 `NetErrorCode.SUCCESS`（0），`ErrorMessage` 为空串 |
| `public static NetResponse<T> Fail(int errorCode, string errorMessage)` | 创建失败响应；`Data` 固定为 `default` |
| `public static NetResponse<T> Fail(int errorCode, string errorMessage, T data)` | 创建失败响应并保留 data，例如绑定冲突附带的 existing_uid |

> **构造函数为 `private`**，外部只能通过 `Success` / `Fail` 创建实例。

---

## 3. 使用示例

```csharp
// 业务侧接收 Login 结果
var resp = await Nova.Network.Kit<Login>().Async(string.Empty, openId);

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

- 包装属性只有 getter，但 Data 是原消息引用，消息内容仍可修改，不是深度不可变快照。
- 成功/失败与 Data 是否为 null 分别检查；错误响应可有有效 Data，成功工厂也不保证非空。Fail 不验证错误码非零，也不把 null 描述归一化；IsSuccess 由工厂保存，不按 ErrorCode 动态计算。
- 实例是普通托管对象，不归还 ReferencePool；内部 NetResult 同样保存业务 byte[] 原引用，不校验或克隆内容。
- **泛型约束 `where T : IMessage<T>`**：确保 `T` 为 Protobuf 消息类型。

---

## 5. 关联

- 同包：[NetService.md](./NetService.md) — 通过 `NetResponse<T>.Success / Fail` 构造并返回结果
- 同包：[NetErrorCode.md](./NetErrorCode.md) — `ErrorCode` 的值域
- 同包：[NetworkComponent.md](./NetworkComponent.md)
