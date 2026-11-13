# PSD2UGUI 示例

这里演示把 PSD 做成可编辑的 uGUI。开发态原目录 `Assets/Samples/PSD2UGUIDemo` 已迁到 [MainDemo 的 PSD2UGUI 子目录](./)；资源 GUID 保留，图片复用目录、预制体复用目录、导出目录和已有输出记录一起更新。消费项目以当前 MainDemo 的实际导入目录为准。

## 没有安装插件时

MainDemo 不需要安装 PSD2UGUI 才能编译。[Exports/Prefabs](Exports/Prefabs/) 中的界面和 `CommonPrefabs` 中的按钮使用普通 uGUI/TMP 组件，可以继续查看、使用；仍需项目已有的 Unity UI 和字体资源。

本示例的编辑器保护会把依赖插件的配置、编辑树、来源记录和复用索引连同 `.meta` 收到 `Authoring~`，Unity 不会继续导入这些文件。它们只是暂时收起，没有删除。安装插件后，编辑器会恢复原位置和资源标识。保护只作用于本目录，不处理项目中其它 PSD2UGUI 资源，也不会自动安装插件。

## 需要继续制作界面时

1. 从 **PlugPals → 内部云仓库** 安装 `cn.efunstudio.psd2ugui`，并完成商业插件的授权。
2. 等待 Unity 编译、导入结束，再打开 [Psd2UguiForm_UGUI_UIFormEditor.prefab](Psd2UguiForm_UGUI_UIFormEditor.prefab)。
3. PSD 改动后先同步最新 PSD，再按需要导出图片、生成或更新界面。已生成资源的关联保留，不需要重新指定图片。

移除插件前，先保存并关闭编辑树。保护不会擅自保存未保存的内容，也无法取消 Unity 的卸载；发现未保存编辑或两处已有同名数据时会停止迁移并提示，避免直接覆盖。解决冲突后重新加载脚本或重新打开工程即可再次检查。

## 文件放在哪里

| 内容 | 位置 |
|---|---|
| 原始设计与加工后的 PSD | 本目录下的两个 `.psd` |
| 可以调整类型、引用的编辑树 | [Psd2UguiForm_UGUI_UIFormEditor.prefab](Psd2UguiForm_UGUI_UIFormEditor.prefab) |
| 插件配置与来源记录 | [Psd2UIFormConfig.asset](Psd2UIFormConfig.asset)、[Editor/](Editor/) |
| 可复用图片、按钮 | [CommonPics/](CommonPics/)、[CommonPrefabs/](CommonPrefabs/) |
| 导出的图片、字体与界面 | [Exports/](Exports/) |
| 缺插件时暂存的编辑数据 | `Authoring~`，包含原始 `.meta`，不要删除 |

此目录随 Framework 的 MainDemo Sample 和公开副仓提供，包含示例自己的设计、生成产物和编辑数据，不包含 PSD2UGUI 插件源码或 DLL。插件仍通过内部云仓库另行安装和授权。示例没有自动注册为 MainDemo 的业务页面；业务接入仍通过 Nova 的 UIView、UI 配置和资源加载流程完成。

通过 Package Manager 导入 MainDemo 后，本目录随其实际导入位置移动，不固定包展示名或版本目录。导入工具会同步修改编辑树的 PSD 路径、配置中的复用目录和 `.ui.asset` 的输出记录；缺插件时收起的 `Authoring~` 数据也会改到真实位置，装回插件后无需手工替换。只转换原本属于该示例的路径，用户另外指定的公共图片、预制体和输出位置保留。源码引用和已有产物仍按资源 GUID 跟踪，不把可写路径改到只读 `Packages` 下。
