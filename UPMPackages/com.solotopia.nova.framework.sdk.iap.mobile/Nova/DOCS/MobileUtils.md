# Mobile IAP 工具类

> 本文档覆盖 `com.solotopia.nova.framework.sdk.iap.mobile` 包内两个内部工具类：
> `MobileReceiptParser`（票据解析）和 `MobileStoreParameterCodec`（购买透传参数编解码）。

---

## §1 MobileReceiptParser

**类签名**：`internal static class MobileReceiptParser`
**命名空间**：`NovaFramework.SDK.IAP.Mobile.Runtime`
**文件**：`Utils/MobileReceiptParser.cs`

Unity IAP 5.x 票据（`order.Info.Receipt`）JSON 解析工具，带 productId 级别的缓存，避免对相同票据重复反序列化。

### 关联数据结构

同文件中定义的三个 `internal sealed class`：

| 类 | 说明 |
|---|---|
| `MobileReceiptInfo` | 顶层票据：Store 类型、TransactionID、Payload 原始字符串；提供 `OrderId` / `GoogleToken` 计算属性 |
| `MobileGooglePlayload` | Google Payload 反序列化结构：`Json`（inappPurchaseData）/ `Signature`（inappDataSignature）/ 解析后的 `PayloadJson` |
| `MobileGooglePayloadJson` | Google Payload 内 json 字段：`OrderId` / `PackageName` / `ProductId` / `PurchaseToken` |

### §2 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Utils/MobileReceiptParser.cs` | `internal static class` | 解析工具（带缓存） |
| `Utils/MobileReceiptParser.cs` | `internal sealed class MobileReceiptInfo` | 顶层票据 DTO |
| `Utils/MobileReceiptParser.cs` | `internal sealed class MobileGooglePlayload` | Google Payload 反序列化结构 |
| `Utils/MobileReceiptParser.cs` | `internal sealed class MobileGooglePayloadJson` | Google Payload 内 json 字段 |

### §4 关键字段表

#### MobileReceiptInfo

| 字段 / 属性 | 类型 | 序列化 key | 说明 |
|---|---|---|---|
| `Store` | `string` | `"Store"` | 商店类型标识（`"GooglePlay"` 或 `"AppleAppStore"`） |
| `TransactionID` | `string` | `"TransactionID"` | 平台交易 ID |
| `Payload` | `string` | `"Payload"` | 平台原始 Payload 字符串 |
| `ReceiptJson` | `string` | — | 缓存用原始 JSON 字符串，用于 Parse 时比对是否需要重新解析 |
| `GooglePayload` | `MobileGooglePlayload` | — | Google Payload 解析结果；Apple 时为 null |
| `OrderId` | `string`（只读属性） | — | Google = `PayloadJson.OrderId`；Apple = `TransactionID` |
| `GoogleToken` | `string`（只读属性） | — | Google = `PayloadJson.PurchaseToken`；Apple = `string.Empty` |

#### MobileReceiptParser 常量

| 常量 | 值 | 说明 |
|---|---|---|
| `GoogleStore` | `"GooglePlay"` | Google Play 商店标识 |
| `AppleStore` | `"AppleAppStore"` | Apple App Store 商店标识 |

### §5 完整公开 API

```csharp
// 解析票据，命中缓存且票据未变化时直接返回缓存结果；失败时返回 null
internal static MobileReceiptInfo Parse(string productId, string receiptJson)

// 清除所有缓存（由 MobileStore.DisposeAsync 调用）
internal static void ClearCache()
```

### §9 关键算法

**缓存命中逻辑**：

```
Parse(productId, receiptJson):
  1. s_Cache.TryGetValue(productId, out cached)
  2. cached != null && cached.ReceiptJson == receiptJson → 命中，直接返回
  3. 未命中 → ParseInternal(receiptJson)
               → JsonConvert.DeserializeObject<MobileReceiptInfo>(receiptJson)
               → Google: 解析 Payload → MobileGooglePlayload
                          解析 GooglePayload.Json → MobileGooglePayloadJson
               → s_Cache[productId] = info
```

**缓存粒度**：per-productId，同一商品的 Receipt 变化（如补单后重新拉取）会触发重新解析。

### §10 常见误区

**误区 1：MobileReceiptParser 是 MobileStore 级别缓存**

`s_Cache` 是 `static readonly` 字段，生命周期等同于进程。`MobileStore.DisposeAsync` 时会调用 `MobileReceiptParser.ClearCache()` 清除，确保新的 store 生命周期不会读到上轮缓存的旧数据。若忘记清除，多次初始化时可能读到过期票据。

**误区 2：Apple Receipt 有 GoogleToken**

Apple 票据中 `GoogleToken` 属性恒为 `string.Empty`（计算属性判断 `Store != GoogleStore`）。验单代码中若未按平台分支处理，会将空 token 发往服务端。

**误区 3：Google OrderId 会持久化到 MobileOrderRecord.TransactionId**

Android 运行期可以把 Google `OrderId` 写入 `MobileOrderRecord.TransactionId`，用于结果和打点回填；但该字段在 Android 下不会序列化进本地存档。Google 验单凭据和本地支付成功打点去重仍使用 `GoogleToken`。

---

## §1 MobileStoreParameterCodec

**类签名**：`internal static class MobileStoreParameterCodec`
**命名空间**：`NovaFramework.SDK.IAP.Mobile.Runtime`
**文件**：`Utils/MobileStoreParameterCodec.cs`

uid + tableId 与 GUID 字符串互转工具，用于购买时将 UID 和 tableId 编码为 UUID 写入平台透传字段（Android: `ObfuscatedAccountId` / `ObfuscatedProfileId`；iOS: `AppAccountToken`），回调时解码还原 tableId 以精确路由订单。对齐 IAP3Helper 的 `IAP3StoreParameterCodec` 设计。

### §2 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Utils/MobileStoreParameterCodec.cs` | `internal static class` | 编解码工具，无状态 |

### §5 完整公开 API

```csharp
// 将 uid + tableId 编码为 GUID 格式字符串（8-4-4-4-12）
// uid 转 long 失败时 uid 部分填 0，tableId 仍正常编码
// 返回形如 "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" 的字符串
internal static string Encode(string uid, long tableId)

// 从 GUID 字符串中解码 tableId（取去掉连接符后的后 16 个 hex 字符）
// 解码失败或 GUID 格式非法时返回 0
internal static long DecodeTableId(string guid)
```

### §9 关键算法

**编码（Encode）**：

```
uid → long（失败填 0）→ X16 hex（16 字符）
tableId → X16 hex（16 字符）
raw = uidHex + tableHex（共 32 字符）
result = raw[0..8] + "-" + raw[8..12] + "-" + raw[12..16] + "-" + raw[16..20] + "-" + raw[20..32]
```

**解码（DecodeTableId）**：

```
raw = guid.Replace("-", "")   // 去掉连接符，得 32 字符 hex 串
tableHex = raw[16..32]        // 后 16 位
tableId = long.Parse(tableHex, HexNumber)
```

**iOS `AppAccountToken`**：iOS 要求 UUID 格式的 `Guid`，`Encode` 结果恰好是标准 GUID 字符串，`PurchaseService.ApplyPurchaseContext` 中直接 `Guid.TryParse(uuid, out Guid)` 后写入。

### §10 常见误区

**误区 1：uid 为非数字字符串时编码失败**

`Encode` 对 uid 做 `long.TryParse`，非数字字符串会使 uid 部分编码为 `0000000000000000`。tableId 编码不受影响，`DecodeTableId` 仍可正确还原。

**误区 2：解码结果为 0 但不判断**

服务端和客户端都可能返回全零 tableId，`DecodeTableId` 返回 0 表示解码失败或 tableId 本身无效。`PurchaseService.TryParseTableId` 检查 `> 0` 才继续，直接使用 0 会导致商品匹配失败。

---

## §13 关联文档

- MobileStore 类规格：`./MobileStore.md`
- 内部服务架构总览：`./MobileIAP-Architecture.md`
- 本文仅记录当前工具类事实；历史设计说明见 `MobileStore-Design.md`。
