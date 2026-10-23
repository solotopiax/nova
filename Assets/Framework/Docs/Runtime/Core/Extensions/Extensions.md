# Core/Extensions

C# 和 Unity 类型的扩展方法，全部为静态类，可在任意层级直接使用。

## Foundation/（基础类型扩展）

**类名**：`NovaExtensionForFoundation`（分部类）

| 文件 | 扩展类型 | 典型方法 |
|------|---------|---------|
| `NovaExtensionForFoundation.Float.cs` | `float` | `FloatToTimeString` 秒数显示 |
| `NovaExtensionForFoundation.String.cs` | `string` | 首字符大写、富文本长度、空白折叠、时间解析与基本汉字计数 |

`FloatToTimeString` 默认只显示分钟和秒，仅支持源码列出的六种含秒组合，其余返回空串；小时隐藏后仍按 60 取分钟余数，不把小时累加进分钟。毫秒以小数点输出，`TimeStringToFloat` 却要求长度 12 的 `hh:mm:ss:SSS` 四段冒号输入，二者不是互逆。解析只逐段 TryParse，失败段计零，不验证范围；长度正确但分隔段不足仍可能越界，负数和非有限输入没有统一保护。

`UppercaseFirst` 对 null/空返回空串，只处理首个 UTF-16 字符；它与先小写再标题化的 `ToTitleCase` 都受当前文化影响，不是字素级处理。`RemoveExtraSpaces` 用 `\s+` 把空格、Tab、换行统一折叠为一个空格，不 Trim。`RichTextLength` 把精确 `<br>` 计一字符并跳过尖括号内内容，未闭合 `<` 会吞后续文本，不是完整富文本解析器。`GetChineseNum` 只统计 U+4E00～U+9FFF；除首字符大写外，上述字符串工具没有统一 null 回退。

## Unity/（Unity 类型扩展）

**类名**：`NovaExtensionForUnity`（分部类）

| 文件 | 扩展类型 |
|------|---------|
| `NovaExtensionForUnity.Transform.cs` | `Transform` |
| `NovaExtensionForUnity.GameObject.cs` | `GameObject` |
| `NovaExtensionForUnity.Component.cs` | `Component` |
| `NovaExtensionForUnity.Animator.cs` | `Animator` |
| `NovaExtensionForUnity.RectTransform.cs` | `RectTransform` |
| `NovaExtensionForUnity.UI.cs` | `UnityEngine.UI.*` |
| `NovaExtensionForUnity.Vector2.cs` | `Vector2` |
| `NovaExtensionForUnity.Vector3.cs` | `Vector3` |
| `NovaExtensionForUnity.Quaternion.cs` | `Quaternion` |

### Animator 参数

`HasParameterOfType` 对空名称返回 false，其余逐次扫描 Controller 参数，不缓存。AddAnimatorParameterIfExists 只向调用者 HashSet 追加命中项，不清旧集合；int 版空名输出 -1，非空名称即使不存在也输出 hash，不能用 out 值证明参数存在。带 HashSet 的 Update 重载默认检查 Contains，int 版 bool 结果只表示已调用 Set，不证明 Unity 最终效果；集合不验证参数类型，Controller 换后也不会自动更新。字符串 Float 直写重载忽略 performSanityCheck；IfExists 四种重载始终按名称和类型检查，也忽略这个开关。空对象、空集合和 Unity 调用错误没有统一异常隔离。

### 组件、坐标与层级

GetOrAddComponent 先查当前 GameObject，缺失才 Add；Around 版本先含 inactive 子节点，再父节点，最后才在自身添加，可能取得别的节点组件。添加会触发 Unity 生命周期，不完成业务注册或序列化绑定。GetComponentNoAlloc 的 Type 版直接 GetComponent，泛型版 TryGetComponent，没有静态缓存或跨版本零分配保证。rectTransform 只查询/转换，不添加。InScene 仅判断 scene.name 非 null，不检查场景有效性或资源归属。

Transform/RectTransform 的 Set/Add 轴向重载读原向量，只替换/累加指定轴，其他轴保留；position、localPosition、localScale、anchoredPosition 与 anchoredPosition3D 不能混用。边距 Left/Bottom 写 offsetMin，Right/Top 写负号的 offsetMax，对锚点布局的实际表现另按 Unity 约束解释。ForceRebuildLayoutImmediate 立即转发布局重建；均不校验 NaN/Infinity 或调整父坐标。GetRoute 按祖先至自身名称拼接，默认 `/`，无转义或唯一性，不能当资源地址；GetRouteNum 只数祖先，根为 0。

SetLayerRecursively 包含自身及 inactive 子节点，会分配列表，不检查层号或改 sortingLayer。SetParticleSortOrder 只查询默认活跃子粒子 Renderer；不排序时统一赋值，排序时按旧 order 分组递增，但旧 order=0 同时用作首组标记，0→非0 的边界可能未递增，同值排序也不保证稳定。

### 数学、屏幕投射与渐变

Vector2.ToVector3 将原 x/y 映射世界 X/Z，世界 Y 默认 0 或显式实参。LookAt2D 也在 XZ 平面，方向非零才 LookRotation。Quaternion.Scale 是四分量缩放，Add 按点积符号对齐后相加，Blend 是完整 q1 加 weight×q2，没有 1-weight 因子，均不自动归一，也不是旋转乘法或 Slerp。NormalizeSafe 的平方长度>1e-10 才归一，否则 identity；NaN/Infinity 不构成有效旋转保证。

Vector2 的 Physics2D Raycast 从 ScreenPointToRay 的 XY 原点以零方向查询；Line 单命中用 Ray.origin，All 版用 z=0 的 ScreenToWorldPoint，透视下不能推断二者等价。CircleCast 的 direction 实参实际是屏幕终点，转为世界 end-start 与其长度，radius 是世界单位。Vector3 的 3D 方法直接 ScreenPointToRay→Physics.Raycast 默认查询。全部未额外指定 LayerMask/深度，All 版分配集合/数组且不去重，空命中返回空数组，单命中返回 null；Camera 或物理调用异常传播。

CanvasGroup.FadeToAlpha 与 Slider.SmoothValue 返回 IEnumerator，真正迭代后才按 Time.deltaTime/Lerp 修改目标，Slider 会触发值通知。零/负/NaN duration 直接赋终值，正无穷不会结束，timeScale=0 时正常正时长不推进；没有取消或对象失效守卫，中断迭代不自动赋终值。Unity 扩展方法须由拥有者在有效对象生命周期调用，名称或静态检查不代替实际布局、命中、动画与目标平台验证。
