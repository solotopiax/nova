# YooAssetEditorConfigs

YooAsset Editor 配置，保存 `YooAssetSettingsPath`、`BundleCollectorSettingPath`，以及设计态的 `YooFolderName`、`PackageFilePrefix` 模板。四项均按 YooAsset 面板维度保存，只用于 Editor 注入、导出与构建工具，不进入 Runtime 配置。

`YooFolderName` 与 `PackageFilePrefix` 只以 ConfigMaster 为真相源，面板不从 `YooAssetSettings.asset` 反向读取。点击 ConfigWindow 顶部“导出”时，按目标 Platform×Channel×DevelopMode 解析 `{Platform}`、`{Channel}`、`{Package}`、`{Version}`、`{Time}`，再单向写入该维度 `YooAssetSettingsPath` 指定的资产。
