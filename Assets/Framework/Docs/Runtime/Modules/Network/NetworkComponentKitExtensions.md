# NetworkComponentKitExtensions

## 1. 简介

`NetworkComponentKitExtensions` 是 `NetworkComponent` 的 Kit 扩展方法集合，将 `NetService.SetDebugMode` 以扩展方法形式挂载到 `NetworkComponent` 上。本类已随 Kit 基础包下沉至框架主程序集 `NovaFramework.Runtime`，与 `NetService` 同程序集，无额外依赖。

**所在文件：** `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetworkComponentKitExtensions.cs`
**命名空间：** `NovaFramework.Runtime`
**类签名：** `public static class NetworkComponentKitExtensions`

---

## 2. 公开 API

| 签名 | 说明 |
|---|---|
| `public static void SetDebugMode(this NetworkComponent network, bool debugMode)` | 设置 `NetService` 全局调试模式开关；等同于直接调用 `NetService.SetDebugMode(debugMode)` |

---

## 3. 使用示例

```csharp
// 在业务 Procedure 或初始化代码中设置调试模式
// 调试模式下跳过 AES 加解密，HTTP 请求带 X-Debug-Plain 头
Nova.Network.SetDebugMode(true);

// 关闭调试模式（生产环境）
Nova.Network.SetDebugMode(false);
```

> `Nova.Network` 是 `NetworkComponent` 的全局访问入口。扩展方法通过 `this NetworkComponent network` 接收者参数自动附着，业务侧无需感知扩展方法的存在。

---

## 4. 内部约束

- **已下沉至主框架程序集**：本类物理位于 `Assets/Framework/Scripts/Runtime/Modules/Network/Kit/NetworkComponentKitExtensions.cs`，与 `NetService` 同属 `NovaFramework.Runtime`，无独立 Kit 程序集依赖。
- **调用方无需额外引用**：业务侧调用 `Nova.Network.SetDebugMode` 时，只需正常引用框架主程序集，无需额外安装 Kit UPM 包。
- **功能完全等价**：`Nova.Network.SetDebugMode(x)` 与 `NetService.SetDebugMode(x)` 完全等价，区别仅在于调用符合主框架门面风格。

---

## 5. 关联

- 同包：[NetService.md](./NetService.md) — 被本扩展方法包装的目标
- 同包：[NetworkComponent.md](./NetworkComponent.md) — 扩展方法接收者
- ADR-020（程序集依赖方向单向，本类设计动因）
