# WebSocketScope.TcpChannel

**类签名**：`public sealed class TcpChannel : NetChannelBase`（嵌套于 `WebSocketScope`）
**命名空间**：`NovaFramework.Runtime`

TCP 明文通道，消息体为 JSON 字符串列表的二进制打包。负责 `TcpMessage` 的封包/解包，心跳和认证消息通过协议 ID `MessageType.HeartBeat` / `MessageType.Authenticate` 识别，认证结果通过解析 JSON 中的 `success` 字段判定。

---

## 文件列表

| 文件 | 说明 |
|------|------|
| `WebSocketScope.TcpChannel.cs` | TCP 明文通道实现（含 WebGL 和非 WebGL 两套接收逻辑） |

## 关键字段/属性

| 字段 | 类型 | 说明 |
|------|------|------|
| `IsNeedConnect` | `bool`（override） | 始终返回 `true` |

## 公开 API

```csharp
// 将 TcpMessage 封装为二进制帧：[协议ID 4B][序列号 4B][消息体 NB]
override byte[] PackageMessageToBytes(NetMessageBase message)

// 判断是否为心跳消息（CommandID == MessageType.HeartBeat）
override bool IsHeartBeatMessage(NetMessageBase message)

// 判断是否为身份认证消息（CommandID == MessageType.Authenticate）
override bool IsAuthenticateMessage(NetMessageBase message)

// 判断认证响应是否成功（解析 JSON 中的 success 字段）
override bool IsAuthenticateMessageSuccess(NetMessageBase message)

// 获取通道类型
override NetChannelType GetNetChannelType()  // => NetChannelType.Tcp

// 判断通道类型与地址是否匹配
override bool Equals(NetChannelType channelType, string serverAddress)
```

## 消息解包流程

非 WebGL 接收时先识别 WebSocket `Close` 控制帧：直接结束本次接收，不获取池化消息、不按 Nova 8 字节协议头解包；主动释放正在接收的 Socket 也作为正常结束处理。

这里的 Tcp 名称表示 Nova 明文帧格式，底层仍是 WebSocket binary，并非裸 TCP。封包使用本次局部数组，错误消息类型会因显式转换抛异常。认证只在正确 CommandID 的消息中查找第一个含 `success` 的 JSON 项并 bool.Parse；畸形 JSON 或非布尔文本可抛异常，消息 ID 本身不证明认证通过。

非 WebGL 按 EndOfMessage 汇总分片后才解包；WebGL 直接解包回调给出的数组。两分支均没有额外正文大小上限。当前解包先借池对象，再读取头与字符串长度；短头、非法长度或越界复制等异常虽然会告警并返回 null，catch 没有归还已经借出的对象。上层正常消息 finally 回池不覆盖此失败分支，不能以正常关闭测试推断任意畸形包无泄漏。

```
接收字节流 recvBytes
  ├─ CommandID  = recvBytes[0..3]（网络字节序→主机字节序）
  ├─ SequenceID = recvBytes[4..7]
  └─ bodyBytes  = recvBytes[8..]
       └─ 循环解析：[4字节长度][UTF-8字符串] → 添加到 Messages 列表
```

## 关联文档

- [NetChannelBase](NetChannelBase.md)
- [TcpMessage](../Messages/TcpMessage.md)
- [MessageType](../Messages/MessageType.md)
- [NetChannelType](NetChannelType.md)
