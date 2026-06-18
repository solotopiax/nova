# Util.Convert

基础类型安全转换工具（`string` → `int` / `float` / `bool` / `enum` 等）。包含纹理编码、单位转换、Hex/Base64 编码、网络字节序转换等功能。

## 文件

`Util.Convert/Util.Convert.cs`

## 注意事项

- `TextureToBytes` 使用 `catch (Exception e) when (e is UnityException || e is ArgumentException)` 合并异常处理，无不可达代码。
