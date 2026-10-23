# EditorObjectJson

`EditorObjectJson.TryReadSnapshot<T>` 是导出 Action 内部共用的组件快照读取入口。当前调用方为 `TableSettingsResolver`、`LocalizationSettingsResolver`、`SoundSettingsResolver`、`VibrationSettingsResolver` 和 `UISettingsResolver.TryReadSettingsJson`；不增加 Action、参数或写入能力。

服务支持 `EditorJsonUtility.ToJson(component)` 的扁平组件根和 `{"MonoBehaviour":{...}}` 包装根。包装 JSON 须先解包，再交给 `JsonUtility.FromJson<Snapshot>`，避免字段层级错误与 DTO 初始值掩盖缺失字段。只接受唯一、对象型 `MonoBehaviour` 包装；拒绝重复 JSON 属性、混合包装根、非对象包装、尾随额外或畸形 JSON，以及缺失、null 或非对象型的必需设置字段。不尝试其他包装名，不递归寻找字段。`JsonTextReader.DateParseHandling=None` 保持 ISO 日期等设置字符串的原类型和值，不把它们推断成日期后再格式化。

确认字段真实存在后，再把组件正文交给 Unity `JsonUtility` 读取 DTO。组件 Getter、`SerializedObject` 写入、场景保存和源设置初始化均不执行。解析失败返回稳定的组件错误，不回显 JSON 正文或解析器异常消息；各 Resolver 保留其 DTO 设置非空检查。

Table、Sound、Vibration、UI 继续对克隆后的设置 JSON 计算既有摘要；Localization 继续对原始完整组件 JSON 计算 `ComponentHash`，同时读取设置及 `m_SupportedLanguagesDataExportPath`。根解包不缩小原计划的冻结范围。`NetworkSettingsResolver` 从 `NetworkSettings` / `ProtoSettings` 属性获得普通 DTO 后 roundtrip 克隆，没有组件 JSON 包根；`GeneratedExportCompilationState` 的记录文件读取也不使用此服务。

静态 JSON 根结构检查与编译只能证明解析边界及源码可编译。真实组件字段、当前导出 Plan、正式导出物和编译后收据仍由对应 Action 的 Unity 验证确认。
