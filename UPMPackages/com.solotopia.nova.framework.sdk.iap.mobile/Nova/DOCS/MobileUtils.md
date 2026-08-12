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

uid + tableId + receiptParam 三值与 GUID 字符串互转工具，用于购买时把三者编码为 UUID 写入平台账号字段（Android: `ObfuscatedAccountId` / `ObfuscatedProfileId`；iOS: `AppAccountToken`），随平台票据回传，回调 / 补单 / 恢复时解码还原以精确路由订单并把透传数据带回业务（跨重启不丢）。设计对齐 IAP3Helper 的 `IAP3StoreParameterCodec`。相关决策见 [[ADR-072-iap-mobile-passthrough-param-layout]]。

### §2 文件表

| 文件 | 类型 | 说明 |
|---|---|---|
| `Utils/MobileStoreParameterCodec.cs` | `internal static class` | 编解码工具，无状态 |

### §3 GUID 布局（32 hex = 16 字节，8/8/16）

| 段 | hex 范围 | 字节 | 内容 | 编码方式 |
|---|---|---|---|---|
| uid | `[0,8)` | 4 | 用户 UID | **转大写后左补 0**（支持字母数字，业务约束 ≤8 字符） |
| tableId | `[8,16)` | 4 | 商品配置表行 ID | **数值左补 0**（业务约束 ≤8 位十进制） |
| receiptParam | `[16,32)` | 8 | 业务票据透传参数 | **转大写后左补 0**（支持字母数字，业务约束 ≤16 字符） |

> uid / receiptParam 允许包含字母，不再要求能解析为 long；编码前统一转大写，tableId 仍按数值写入。三段组合后正好是标准 `8-4-4-4-12` GUID 字符串。

### §5 完整公开 API

```csharp
// 将 uid + tableId + receiptParam 编码为 GUID（8-4-4-4-12）
// uid / receiptParam 转大写后写入定长槽；tableId 按数值写入 8 hex
internal static string Encode(string uid, long tableId, string receiptParam)

// 解码 tableId（hex [8,16) 数值）；失败返回 0
internal static long DecodeTableId(string guid)

// 解码 receiptParam（hex [16,32) 归一化字符串）；无透传/失败返回 null
internal static string DecodeReceiptParam(string guid)

// 解码 uid（hex [0,8) 归一化字符串）；客户端不依赖，供服务端按同布局对齐
internal static string DecodeUid(string guid)
```

### §9 关键算法

**编码（Encode）**：

```
uidHex     = uid.ToUpperInvariant().PadLeft(8, '0') → 8 字符
tableHex   = tableId.ToString("X").PadLeft(8, '0') → 8 hex
receiptHex = receiptParam.ToUpperInvariant().PadLeft(16, '0') → 16 字符
raw    = uidHex + tableHex + receiptHex（共 32 字符）
result = raw[0..8]-raw[8..12]-raw[12..16]-raw[16..20]-raw[20..32]
```

**解码**：`DecodeTableId` 取 `raw[8..16]` 按 hex 解析；`DecodeReceiptParam` / `DecodeUid` 取对应字符串槽位，去掉左侧补 0，全 0 返回 null。`ReceiptParam` 会参与 Mobile 未完成订单键，空值保持旧 tableId-only 语义，非空值按 codec 的大写归一化结果参与匹配。

**iOS `AppAccountToken`**：iOS 要求 UUID 格式的 `Guid`，`Encode` 结果恰好是标准 GUID 字符串，`PurchaseService.ApplyPurchaseContext` 中直接 `Guid.TryParse(uuid, out Guid)` 后写入。

**校验**：范围/格式校验在 `MobilePurchaseService.TryValidatePassthroughParams`，由 `PayAsync` 入口调用——tableId 超 8 位、uid 超 8 位或 receiptParam 超 16 位、uid / receiptParam 含非十六进制字符、或非空值以 `0` 开头时，都直接拒绝支付（`IAPMobileErrorCode.InvalidPassthroughParam`），不会发起平台购买。后两项避免 iOS `AppAccountToken` 不是合法 GUID，或固定槽位解码去除补零后丢失原值。空值仍表示不透传。`ApplyPurchaseContext` 只做纯编码，不再重复校验；codec 自身同样只做纯编解码。

### §10 常见误区

**误区 1：receiptParam / uid 需要能解析为数字**

二者是**十六进制字符串槽位**，不做 `long.TryParse`，因此支持 `A-F` 与数字混合；只有 tableId 是数值。为区分真实值和左侧补零，非空值不能以 `0` 开头。业务传参超字符上限、包含非十六进制字符或以 `0` 开头都会被拒绝支付。

**误区 2：uid 完整 64 位会进透传参数**

uid 槽只有 8 个字符；完整 uid 仍由服务端另行同步（见 [[ADR-072-iap-mobile-passthrough-param-layout]]）。客户端从不依赖解码出的 uid。

**误区 3：解码结果为 0 / null 但不判断**

`DecodeTableId` 返回 0 表示解码失败或无效，`PurchaseService.TryParseTableId` 检查 `> 0` 才继续；`DecodeReceiptParam` 返回 null 表示无透传。直接使用会导致路由或业务数据错误。

**误区 4：旧格式（uid16+tableId16）在途单**

历史版本布局为 uid(16)+tableId(16)，本布局改为 8/8/16，属破坏性 on-wire 变更；升级前发起、升级后回来的在途单可能解错 tableId，靠 `ResolveTableIdFromTable`（productId 反查）+ 服务端优先 `serverTableId` 兜底。

---

## §13 关联文档

- MobileStore 类规格：`./MobileStore.md`
- 内部服务架构总览：`./MobileIAP-Architecture.md`
