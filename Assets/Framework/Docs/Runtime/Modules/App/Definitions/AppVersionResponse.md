# AppVersionResponse

`AppVersionResponse` 是 CDN 版本检查 JSON 的反序列化 DTO。

当前字段有三个：

- `RecommendedDownloadVersion`
- `RecommendedDownloadPromptIntervalSeconds`
- `ForcedDownloadVersion`

## 当前语义边界

- `RecommendedDownloadVersion`：推荐更新规则的版本阈值。
- `RecommendedDownloadPromptIntervalSeconds`：用户放弃推荐更新后，再次显示提示的最小间隔秒数；缺失、为 `0` 或负数时每次命中都提示。
- `ForcedDownloadVersion`：强制更新规则的版本阈值。
- 两个字段都要求是 `System.Version` 可解析的纯数字点分版本号。

## 匹配规则

- `ForcedDownloadVersion > Application.version` 且本地启用了强制更新规则：返回 `ForcedDownload`
- `RecommendedDownloadVersion > Application.version` 且本地启用了推荐更新规则：
  - 从未放弃、间隔非正数、间隔已到或系统时钟回拨：返回 `RecommendedDownload`
  - 距离上次放弃尚未达到有效间隔：返回 `NoDownload`，启动链直接跳过推荐更新提示
- 都不命中：返回 `NoDownload`

优先级固定为：

- `ForcedDownload` > `RecommendedDownload`

## 风险点 / 易错点

- 这里对应的是 CDN 上的静态 JSON，不是 GM 后台接口返回模型。
- 任一版本字段为空或格式非法时，该字段不会命中更新规则。
- 提示间隔只抑制推荐更新；强制更新始终优先，完全不受该字段影响。
- 提示间隔对所有推荐版本共用，推荐版本阈值变化不会提前清除尚未结束的间隔。

## 继续阅读

关键源码：

- [AppVersionResponse.cs](../../../../../Scripts/Runtime/Modules/App/Definitions/AppVersionResponse.cs)

相关文档：

- [AppVersionResult.md](AppVersionResult.md)
- [../AppManager/AppManager.md](../AppManager/AppManager.md)
