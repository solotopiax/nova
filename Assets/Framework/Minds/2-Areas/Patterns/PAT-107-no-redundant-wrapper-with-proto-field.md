---
id: PAT-107
title: 禁 Proto 协议字段与外层包装容器字段重复
summary: Proto 已自带的协议字段禁再封装容器外层字段重复持有
category: arch
type: pattern
status: active
date: 2026-05-26
aliases:
  - PAT-107
  - PAT-107-no-redundant-wrapper-with-proto-field
keywords:
  - PAT-107
  - 禁 Proto 协议字段与外层包装容器字段重复
  - PAT-107-no-redundant-wrapper-with-proto-field
tags: [pattern, arch, network, proto, anti-pattern]
related:
  - "[[PAT-08-architecture-antipatterns|PAT-08]]"
  - "[[PAT-42-naming-concrete-deduplicate|PAT-42]]"
---

# PAT-107：禁 Proto 协议字段与外层包装容器字段重复

## 适用场景（When）

- 设计网络层 / 序列化层「请求容器」类型（如 `NetRequest<T>`）
- 业务 Proto 消息按协议规定**已经包含**某些公共字段（如 `Head` / `RequestId` / `Timestamp`）
- 有人在 Proto 之外**又包了一层**容器，把同名字段再放一份
- 任何"包装类持有的字段值"和"被包装数据自身字段值"在语义上重叠的地方

## 核心做法（What & How）

**铁律**：如果 Proto 协议字段已经存在并被序列化进网，外层 C# 包装容器**禁止**再持有同名/同义字段。

### 判定步骤

1. 看 `.proto` 文件：业务消息（如 `PbNetLoginReq`）是否已声明 `PbNetReqHeader Head = 1;`
2. 看序列化路径：进网的字节是 `containerWrapper.ToByteArray()` 还是 `body.ToByteArray()`？
   - 如果是**只序列化 body**，那容器外层的同名字段**永远不会进网**，是死字段
   - 如果是**序列化整个 wrapper**，则 wrapper 自身必须是 proto 类型，且 wrapper 字段不与 body 字段重叠
3. 看构建路径：填充该字段的代码（如 `BuildHeader()`）是否被调了多次？多跑 = 双填 + 日志双发

### 反模式实例（本会话踩过的坑）

```csharp
// ❌ 反模式
public sealed class NetRequest<T> where T : IMessage<T>
{
    public PbNetReqHeader Head { get; }   // 与 PbNetLoginReq.Head 重复！
    public T Body { get; }
}

// 业务侧调用：
var body = new PbNetLoginReq { Head = NetBuilder.BuildHeader(), ... };  // 第一次 BuildHeader
var request = NetBuilder.BuildRequest(body);                             // 内部又 BuildHeader 一次
                                                                          // 容器外层 Head 永远不进网
                                                                          // 配置异常时 Log.Warning 双发
```

```csharp
// ✅ 正解
// 删除整个 NetRequest<T> 容器
// SendAsync 直收裸 Body
public static async UniTask<NetResponse<TResp>> SendAsync<TReq, TResp>(
    INetworkCmdRow cmdRow, TReq request, MessageParser<TResp> parser, ...)
    where TReq : IMessage<TReq>
{
    byte[] protoBytes = NetBuilder.SerializeBody(request);   // 直接序列化 Body
    ...
}

// 业务 Service 包装方法负责把 Head 填进 Body
// Channel 已下沉到 PbNetReqHeader，由 NetBuilder.BuildHeader() 内部通过 InferChannel() 自动填充，无需在 body 中手动赋值
public async UniTask<NetResponse<PbNetLoginResp>> Async(...)
{
    var body = new PbNetLoginReq
    {
        Head = NetBuilder.BuildHeader(),   // 一次 BuildHeader，channel 也已在内部填充
        OpenId = openId, ...
    };
    return await NetService.SendAsync(cmdRow, body, PbNetLoginResp.Parser, ...);
}
```

## 为什么这么做（Why）

- **字段双填 = 数据漂移源**：两处独立填充同一字段，容易写不一致；维护时改一处忘改另一处，编译还过得去，bug 隐藏到运行期才爆。
- **死字段污染信号 / 噪声比**：永不进网的字段被 Inspector / 日志 / 调试器拿到，提供假信息。
- **Builder 双跑 = 日志噪声**：本会话 `NetBuilder.BuildHeader()` 跑两次，配置异常时 `Log.Warning` 双发，给排查制造噪声。
- **容器只剩单字段是 anti-pattern**：删完冗余字段后容器只剩 Body，等于无意义包装，留着只会诱导后人往里加东西。
- **Proto 协议是源头真相**：协议规定字段就规定了职责，C# 侧应直接消费 proto 类型，不"加工出更精致的中间形态"。

## 反模式（Anti-patterns）

- ❌ Proto 已有 Head 字段，C# 包装容器**也**有 Head 字段（同名同义）
- ❌ 包装容器持有的字段「永远不进网」（序列化路径只动 body）
- ❌ 同一逻辑数据**初始化两次**（一次填 proto body，一次填 wrapper）
- ❌ "为将来扩展"预留一个仅含一个有效字段的泛型容器
- ❌ 看到 wrapper 类型签名就心生敬意不再追问"它是不是冗余" —— 反过来：单字段 wrapper 必查冗余

