# Util.SysIO

文件 / 目录 / 路径操作，含 WebGL 平台专用实现。

## 文件

`Util.SysIO/` 下分部类，WebGL 实现在 `Plugins/WebGL/` 下通过 `#if UNITY_WEBGL` 切换。

## API

```csharp
Util.SysIO.File.ReadAllText(path)
Util.SysIO.File.WriteAllText(path, content)
Util.SysIO.Directory.Exists(path)
Util.SysIO.Directory.GetFiles(path)
Util.SysIO.Path.Combine(a, b)
```

## 注意事项

- `WriteAllBytesAsync` 使用 `FileMode.Create` 打开文件（自动截断旧内容），确保文件缩小时不残留旧数据。
- `ReadAllBytesAsync` 使用循环读取（`while (remaining > 0)`）确保大文件读取完整，并对超过 2GB 的文件检查抛出错误日志。
