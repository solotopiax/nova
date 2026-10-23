# VibrateComponentInspector

`NovaFramework.Editor.VibrateComponentInspector` 是 `VibrateComponent` 的 `BaseComponentInspector` 子类。当前入口只绘制 Manager 选择、JSON/Binary 格式、Emphasis 导出区、Custom 导出区，最后 Apply/Repaint；没有运行振动调试、StopAll、预设测试按钮或对应调试字段，也没有本类的 Luban watcher/样式缓存生命周期。

## 两条独立导出链

Emphasis 使用 `VibrateEmphasisTemplate.xlsx`、EmphasisSourceDirPath 与 EmphasisUnitsSettings；Custom 使用 `VibrateCustomTemplate.xlsx`、CustomSourceDirPath 与 CustomUnitsSettings。两区共享 DataFormat，格式切换同步两区标准数据后缀，但不立即导出、不改变资源地址；仍须分别执行对应区域导出。

`DrawVibrateSourceDataOperations` 以准确的 Emphasis Foldout key 选择 isEmphasis，其余 key 走 Custom，不能把它当成任意扩展区域接口。目录选择是延迟刷新，部分 GUI 变量在本帧编辑之前采样；目录文本、Unit 数量或配置目录存在都不是 schema/权限/资源就绪证明。

文件树行提供数据、代码与资源地址。单文件回调不先 Apply，实际重新反射读取 target 的 VibrateSettings，再按 isEmphasis 选择区域；detailProp 与目录参数有兼容占位，不是全部被直接消费。数据目标由匹配 Unit 决定，传入 dataExportPath 只作非空门禁；代码目标使用显式 classExportPath。缺目标不能由该回调放宽为全量。全量先 Apply 后重读设置，只处理当前区域；void 包装丢弃内部 bool，不能把按钮结束当作成功验收。

正式导出在各自固定 Profile 的 `_temp/_publish` 中生成和核验，再由 `FileSystem.OutputApplier` 发布选中格式并删除对应反格式文件及 metadata；Inspector 不直接改生成物。ConfigSyncer、资源地址与运行消费仍需沿相应正式链验证。

## 模板参数

Custom 以 Name 把多行聚为组合，Intensity/Sharpness 范围 0～1，PreDuration/Duration 单位为秒，Order 控制顺序。Emphasis 的 Amplitude/Frequency 范围 0～1，PreDuration/Interval 为秒，Order 控制顺序。两模板的三段组合、强度值和末段零间隔只是样例，不是默认触感或设备验证结果。

Manager 候选只证明当前可发现的接口实现；本面板不执行原生振动，真机能力与运行播放仍由 Runtime 和对应平台单独验收。OnEnable 只绑定当前设置与类型列表，不补模板路径或建立文件监控；绘制异常可能跳过最终 Apply/标签恢复。

## 关联文档

- [BaseComponentInspector](../BaseComponentInspector.md)
- [Vibrate 导出器](../../EditorUtil/EditorUtil.Vibrate/EditorUtil.Vibrate.Exporter.md)
