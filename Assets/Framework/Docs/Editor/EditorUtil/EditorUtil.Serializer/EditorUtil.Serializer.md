# EditorUtil.Serializer

`NovaFramework.Editor.EditorUtil.Serializer` 通过 `SerializedObject` 和 `SerializedProperty` 访问 Unity 序列化数据，提供泛型 Get/Set 和场景定位辅助方法。它不反射读取普通运行时字段，不能用来获取未序列化的 IManager 引用。

## 属性读写

`GetProperty<TTarget,TValue>(target, propertyName)` 和 `SetProperty<TTarget,TValue>(target, propertyName, value)` 要求 target 为 UnityEngine.Object，propertyName 为真实序列化路径。null 目标抛 ArgumentNullException，找不到属性抛 ArgumentException。Set 写属性后调用 ApplyModifiedProperties；不调用业务对象的运行时 setter、初始化或授权流程。

支持 int、long、float、double、bool、string、Color、Vector2/3/4、Rect、AnimationCurve、Bounds、Quaternion、枚举和 UnityEngine.Object 引用。内部依据泛型类型选择访问器，没有完整核验 SerializedPropertyType；不支持的类型抛 NotSupportedException，失效属性、类型不匹配和转换异常向上传播。枚举读取使用 intValue 和 Enum.ToObject，保留非连续整数值；写入 Convert.ToInt32 仍可能溢出。内部 SetValueToProperty 只改属性，Apply 由外层负责。

## 场景定位

`FindInScene<T>(goPath)` 使用 GameObject.Find 的名字/层级路径再 GetComponent，仅能找到活动对象，不保证限制在 Active Scene，也不能消除加法场景或同名对象歧义；不是持久对象身份。空路径、对象或组件不存在时告警并返回 null。

`FindInSceneProperty<T>(goPath, propertyName)` 继续 FindProperty，缺字段告警并返回 null。返回属性依赖其 SerializedObject 的有效生命周期，不自行核验值类型、提交修改或保存场景。

## 关联文档

- [EditorUtil](../EditorUtil.md)
- [IEditorRuntimeDrawer](../../Definitions/IEditorRuntimeDrawer.md)
