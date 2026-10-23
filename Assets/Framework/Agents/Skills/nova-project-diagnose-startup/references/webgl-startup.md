# WebGL 浏览器启动诊断

仅在已导出或已部署的 WebGL 页面启动失败时读取。先冻结当前页面 URL、已有浏览器标签页、失败时间窗、本次构建身份和目标项目；只有存在可信对应关系时，才把浏览器现象归到该 Nova 产物。Unity Editor 正常启动不能证明浏览器已完成 loader、Wasm 或数据加载。

按“浏览器页面 / loader → Wasm 与 data → Unity 实例 → Nova / Manager → Procedure → Config / Asset / 网络”寻找最早直接失败阶段。先检查已有 HAR、Console/Network 导出日志，以及当前宿主已提供且 Schema 明确只读的浏览器 Console、Network、页面与响应信息；已有浏览器能力无法读取时保留 `partial`，不虚构工具、自动创建标签页、重载页面、启动服务或清理存储。需要新的复现访问或网络请求时，另冻结并取得该范围授权。

| 最早证据 | 只读核对 | 能说明的边界 |
|---|---|---|
| 页面或 loader 未加载 | 当前 URL、HTTP 状态、脚本加载失败、CSP 拒绝、第一条异常及对应文件 | 页面/loader 失败时尚不能归因 Nova Procedure |
| Wasm / data 加载失败 | 请求完整路径、状态、响应 MIME / Content-Encoding、CSP / CORS / CORP、内容长度与已有构建清单对应 | `.wasm.gz/.br` 后缀和文件存在不证明浏览器正确解压或运行 |
| WebAssembly 初始化失败 | 浏览器原文堆栈、模块和数据加载顺序；线程构建实际要求及页面已有 COOP / COEP | 缺 `SharedArrayBuffer` 或跨源隔离证据时不默认改响应头；非线程构建不强行要求隔离 |
| Unity 实例已建立，Nova 未进入 | 当前构建中可定位的 Unity / Nova 启动日志和最早异常 | 先确定实例与构建身份，不用 Editor.log 代替本浏览器执行 |
| Nova 流程已进入 | 首个失败的 Procedure / Config / Asset / 网络记录及实际请求 | 资源 URL、初始化或业务问题沿原 Operation 最窄范围查阅；静态配置不证明远端接受 |

HTTP、压缩、线程隔离和本地服务的技术边界复用 [Build Player 平台 CLI](../../nova-project-build-player/references/platform-cli.md#webgl-http-与浏览器)。本诊断只读取现有证据，不运行该页的 `serve_webgl.py`、构建、部署、刷新缓存或删除浏览器数据。只有另一个已授权运行/修复任务才使用对应执行入口。

报告当前页面/构建身份、最早阶段、完整可定位的异常或失败请求、事实与推断、缺失证据和下一最小动作。可完整定位最早直接失败点时诊断为 `success`；能力或证据不足则 `partial`，不把诊断完成写成游戏已启动或故障已修复。
