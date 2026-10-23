# Util.Convert

字节与基础数值、UTF-8、Hex/Base64、纹理编码及像素单位转换工具。当前不提供通用 string→int/float/bool/enum 的安全解析门面。

## 文件

`Util.Convert/Util.Convert.cs`

## 注意事项

- `TextureToBytes` 使用 `catch (Exception e) when (e is UnityException || e is ArgumentException)` 合并异常处理，无不可达代码。

## 字节与文本转换

数值读取重载直接调用 BitConverter，省略索引的重载从 0 读，不修改数组或游标；空数组、null、越界及剩余不足按 BCL 抛异常。ToBytes 有新数组、写入调用者 buffer、指定 startIndex 三种形态，不扩容；bool 固定 1 字节，char/short/ushort 为 2，int/uint/float 为 4，long/ulong/double 为 8。float/double 保存 IEEE754 原位，char 保存 UTF-16 单元，均使用主机字节序；无符号/char 转有符号在有效 checked 环境下仍可能溢出。

short/int/long 最终通过 fixed 指针写入，当前上界校验使用 startIndex+width；unchecked 加法溢出可能绕过检查，unsafe 写没有第二层数组边界保护。调用者须提供合法索引与容量，不能把异常类型当作所有输入均安全的保证；平台非对齐/AOT 表现还需单独验证。ToNetworkOrder/ToHostOrder 对 uint 使用同一字节反转算法，各分配 4 字节，返回重排后的数值而非网络字节数组。

字符串 ToBytes/ToString 使用默认 UTF-8，不加长度前缀或 BOM，也不严格拒绝坏编码；切片范围交 Encoding 校验。Base64 编解码直接调用 System.Convert，异常传播；两个 IsBase64 只是筛选：字符串正则允许空串和非 4 倍长度，byte[] 拒绝 null/空及非 4 倍长度，但末两位的 `=` 位置检查仍可能放过 `AA=A`。它们不等同实际解码，也拒绝部分 BCL 允许的空白。

ToHex 默认逐字节 X2 大写；自选 format 直接交 byte.ToString，null/空格式可能变为十进制。数组与 offset/count 重载无统一空值、范围或整数溢出门，count=0 可能直接空返回，错误沿实际分配、格式化或索引操作传播。

## 纹理、DPI 与文件

TextureToBytes 的 isJpeg=true 固定 JPG quality100 并丢 alpha，false 为 PNG。只有 UnityException/ArgumentException 转复制路径；该路径经临时 RenderTexture→ReadPixels，finally 恢复 active 与释放临时 RT，编码后销毁副本。尺寸读取/GetTemporary 在 try 外，空/失效对象和部分准备错误仍传播；readback/编码错误会记录 Error 并返回 null，清理自身异常也可能传播。它依赖有效 Unity/GPU 生命周期。

ScreenDpi 是静态缓存，不自动读 Screen.dpi；SetScreenDpi 只赋值，不校验。四个单位转换仅拒绝 DPI≤0，使用 1 inch=2.54 cm，不四舍五入或钳制，NaN/Infinity 及负输入可继续计算。

ToNoBOMUTF8 递归匹配 searchPattern，以默认 UTF-8/BOM 读取再用无 BOM UTF-8 原地覆盖；不识别任意旧编码，也不提供整批原子恢复。目录缺失可能因 GetFiles 返回 null 而失败，前面的文件可能已经改写；应在实际授权写入集内使用。
