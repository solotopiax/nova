# LubanDataReceiver

**类签名**：`public sealed class LubanDataReceiver : DataReceiver`  
**命名空间**：`NovaFramework.Runtime`

面向 Luban JSON 的通用接收器。它把一个资源里的多张 Sheet 数据拆进 `LubanDataCache.DataMap`，并在 `Map` 模式下执行主键冲突检测。

## 文件组成

| 文件 | 作用 |
|---|---|
| `LubanDataReceiver.cs` | Luban JSON 拆分、合并、冲突校验实现 |

## 关键字段

| 字段 | 类型 | 说明 |
|---|---|---|
| `m_Cache` | `LubanDataCache` | 共享数据缓存，持有 `DataMap` 与 `SourceTracker` |
| `m_UnitSourceName` | `string` | 当前数据单元来源，通常等于 `unit.AssetLocation` |
| `m_Mode` | `DataTableMode` | 表模式，决定是否开启主键冲突检测 |
| `m_IndexField` | `string` | `Map` 模式下的主键字段名 |

## 构造函数

```csharp
public LubanDataReceiver(
    LubanDataCache cache,
    IDataTableUnitSetting unit,
    LoadAssetAsyncFunc loadAssetAsyncFunc,
    ReleaseAssetAction releaseAssetAction)

public LubanDataReceiver(
    LubanDataCache cache,
    IDataTableUnitSetting unit,
    LoadAssetSyncFunc loadAssetSyncFunc,
    ReleaseAssetAction releaseAssetAction)
```

## 公开重写

```csharp
public override bool OnParseDataAsset(string contentString)
public override bool OnParseDataAsset(byte[] contentBytes)
```

## 核心规则

### 1. 缓存键生成

每个 Sheet 的缓存键都按下面规则生成：

```csharp
string cacheKey = ("tb" + sheetName).ToLower();
```

示例：

| Sheet 名 | cacheKey |
|---|---|
| `Hero` | `tbhero` |
| `Sound` | `tbsound` |

### 2. JSON 拆分

- 顶层 JSON 的每个属性视为一张 Sheet。
- 属性值如果本身是 `JArray`，直接写入缓存。
- 属性值如果不是数组，会被包装成单元素 `JArray` 再写入缓存。

### 3. 跨资源合并

如果同一个 `cacheKey` 在多个 unit 中重复出现：

- `List` / `One` 模式：直接追加到已有数组
- `Map` 模式：若声明了 `IndexField`，会先扫描已有数据与新增数据的主键是否重复
- 一旦发现主键冲突，立即 `Log.Error` 并返回 `false`

### 4. 来源追踪

`SourceTracker[cacheKey]` 会记录这个表数据来自哪些 unit，主要用于冲突报错时定位来源。

## 使用示例

```csharp
LubanDataCache dataCache = new LubanDataCache();

DataReceiver.LoadAssetAsyncFunc loadFunc = async location =>
{
    IAssetHandle<TextAsset> handle = await assetManager.LoadAsync<TextAsset>(location);
    TextAsset asset = handle.Asset;
    handle.Release();
    return asset;
};

DataReceiver.ReleaseAssetAction releaseFunc = _ => { };

bool success = await new LubanDataReceiver(
    dataCache,
    unit,
    loadFunc,
    releaseFunc)
    .ReadDataAssetAsync(unit.AssetLocation);
```

## 注意事项

- `Map` 模式的冲突检测依赖 `IndexField`；若字段为空，则不会做主键去重。
- `OnParseDataAsset(byte[])` 的实现只是把 UTF-8 字节流转成字符串，再复用字符串解析逻辑。
- 这个类只负责“拆数据到缓存”，不负责反射构造 `*Tables` 实例；那一步由 `LubanTablesLoader` 和对应模块管理器完成。
- JSON 构造器保存 unit 的来源、Mode、IndexField 当时值，空字符串字段归空，之后修改 unit 不更新该 Receiver。字符串入口直接 JObject.Parse，无效文本异常传播；字节入口按 UTF-8/BOM 读取，捕获异常并返回 false。
- Map 检查只比较已有数组的键与新增批次，不检查第一批内部、新批内部的重复或缺失字段。缓存 key 的 ToLower 使用当前文化。逐 Sheet 合并没有事务或统一锁，后段冲突/异常会保留前段写入；base 再回退 text 时可能重复处理已成功 Sheet。DataMap/SourceTracker 都是原可变字典引用，Clear 不释放已被其他引用持有的数据，也不保证与解析并发互斥。

## Binary Unit 接收路径

`LubanRuntimeData.CreateReceiver` 仅在 format 恰为 Binary 时创建 `LubanBinaryDataReceiver`，其余枚举值走 JSON；它只选接收器，不读取资源或构造 Tables。Binary 构造拒绝 null cache/unit/AssetLocation，但不拒绝空地址，不读取 Mode/IndexField。协议为 ASCII `NLBP`、版本 1、小端表数量，再逐个读取长度前缀 UTF-8 名称和原始 payload；读取长度拒绝负值、超过剩余或短读，UTF-8 默认替代坏序列，不验证名称与 Binding 一致。

Binary 在 DataMap 锁内逐表保存独立字节数组和来源，现有或同包重复名称、截断、负数量、尾随字节等错误记录 Error 后返回 false；零表包也返回 false。已经加入的表不回滚，没有额外总表数/总字节预算。字符串解析固定 false，不能用文本代替 Binary；magic 比较是普通逐字节比较，既不恒定时间，也不是认证或加密校验。

## 关联文档

- [DataReceiver.md](DataReceiver.md)
- [LubanTablesLoader.md](LubanTablesLoader.md)
- [DataTableMode.md](DataTableMode.md)
