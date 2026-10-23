# Util.Json

JSON 序列化 / 反序列化封装。

当前实现直接依赖 Unity 官方包 `com.unity.nuget.newtonsoft-json` 提供的 `Newtonsoft.Json`。

## 文件

`Util.Json/Util.Json.cs`

## API

```csharp
// 泛型（编译期类型已知）
string json = Util.Json.Serialize(obj);
T obj       = Util.Json.Deserialize<T>(json);

// 非泛型（仅运行期获得 Type，例如反射 / Pipify Runner）
string json2 = Util.Json.Serialize(obj, typeof(MyParams));
object inst  = Util.Json.Deserialize(json2, typeof(MyParams));
```

`Serialize(object, Type, ...)` 与 `Deserialize(string, Type, ...)` 成对使用，用于编译期类型不可知的运行期序列化场景。

## 性能说明

默认 `JsonSerializerSettings` 缓存为 `private static readonly` 字段（`NullValueHandling.Ignore` + `ReferenceLoopHandling.Ignore`），避免每次调用创建新实例。

## 参数与失败边界

序列化默认使用 `Formatting.Indented`。传入非 null 的 `JsonSerializerSettings` 会整体替换默认设置，不与默认项合并。四个入口均直接委托 Json.NET；null、空文本、目标 Type 不匹配、转换器及循环等行为由实际设置和 Json.NET 决定，Nova 不另做参数校验、异常捕获、文件读写或对象生命周期管理。默认忽略 null 和循环引用，因此序列化结果不保证保留对象图中的每条引用。
