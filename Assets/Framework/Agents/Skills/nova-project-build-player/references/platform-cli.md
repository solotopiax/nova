# 平台 CLI 与分层验收

本参考只在用户要求本地 Player 运行、AAB 安装、iOS 原生编译或 WebGL 浏览器验证时读取。先完成 [平台准备与恢复](platform-validation.md)。工具路径、设备与产物从当前宿主发现后冻结，不依赖 Nova 开发仓脚本或固定机器路径；工具 `help`、方法存在、旧产物均不算运行证据。

## 执行层与目标

| 目标 | 可复用入口 | 执行前冻结 | 成功标准 |
|---|---|---|---|
| 本地桌面/WebGL 构建 | `nova.project.player.build` | 当前 Schema、活动 Target、场景闭包、模式、精确 outputPath | 当前 BuildReport Succeeded、产物与 Verify |
| Android/iOS 本地构建/工程 | 已审 `build.package` Batch，经 `nova.project.pipify.run-batch` | 当前平台专属选项、签名、Batch 外 Scene/Master/PlayerSettings | 当前平台 BuildReport、实际包/工程与恢复；直接 Player Action 目前阻断 Android/iOS |
| APK 设备运行 | 宿主 `adb`，配合实际 SDK `aapt` / `apksigner` | 本次 APK hash、包标识/版本/签名、精确 serial、ABI/API | 安装成功、读回身份、真实入口与业务证据 |
| AAB 设备运行 | 实际 Java + `bundletool` 的 `build-apks` / `install-apks` | 本次 AAB、APKS、设备、实际 APK 签名策略 | 本次 APK 集安装与设备读回；不代表 Google Play 分发 |
| iOS 原生/设备 | 实际 Xcode 的 `xcodebuild` / `xcrun devicectl` 或 `simctl` | 工程/scheme、SDK/destination、配置、签名及明确设备 | 原生编译、签名、安装、业务运行逐层记录 |
| WebGL 本机 HTTP | 本 Skill 的 `scripts/serve_webgl.py inspect/serve` | 本次 buildRoot、inspect contentHash、端口、线程隔离 | 响应头与实际浏览器启动、资源/UI/表探针；CLI 启动本身不证明浏览器 |

本地完整运行授权可覆盖对应设备安装与启动；原有应用数据保留。降级、卸载、清数据、注册设备、修改 Apple 账号、上传商店或远端部署分别按真实目标授权，不因构建授权自动执行。缺工具、SDK、签名或设备时保留已有层级，返回 `partial` / `blocked` 并说明最小缺口。

## Android APK

当前 `DebugComponentInspector.InstallAPKToDevice` / `InstallAABToDevice` 只通过 `RunPython` 启动随包 `Scripts/Editor/Tools/Deploys/install_apk.py` / `install_aab.py`；返回值仅表示进程启动。前者列出设备后直接 `adb install`，没有 `-s` 或安装后读回；后者没有 `--device-id`，会先删除已有 APKS，密码以 `pass:` 放入参数，失败异常可能带完整命令。本 Skill 的受控安装直接复用下述当前宿主 CLI 与读回步骤，不能把 Inspector 返回 `true` 当安装成功，也不使用旧脚本替代冻结设备、保留既有 APKS和密码脱敏。

先定位当前 Unity/SDK 的 `aapt`、`apksigner`、Java 与 `adb`，读取其 `help`。只读 `adb devices -l` 发现设备；确定 serial 后所有命令使用 `-s`，不随意使用第一个设备。读取 `ro.product.cpu.abilist`、`ro.build.version.sdk`，比对 APK 的 `aapt dump badging` 与 `apksigner verify --print-certs`。签名只报告证书摘要；需要读取已安装包时用该设备的 `pm path` / `dumpsys package`，不要把相同包名当作相同版本/签名。

冻结后的命令模板如下；变量均是本轮实际发现的完整路径、serial 与入口，不让用户抄写可读取的字段：

安装分支另冻结 artifact 的绝对路径、SHA-256、包标识/版本、签名摘要和设备 serial。若同包已安装，先记录其版本与签名；包或工具、设备、签名、替换影响任一漂移后重新形成精确安装计划。普通构建分支不需要设备或安装证据。设备安装与启动串行占用本次 `serial + bundleId`，不能并行更新同一应用。

```bash
"$nova_aapt" dump badging "$nova_apk"
"$nova_apksigner" verify --print-certs "$nova_apk"
"$nova_adb" -s "$nova_serial" install -r "$nova_apk"
"$nova_adb" -s "$nova_serial" shell dumpsys package "$nova_bundle_id"
"$nova_adb" -s "$nova_serial" shell am start -W -n "$nova_launch_component"
```

`-r` 更新已有应用，不加 `-d` 降级、`-g` 一次授予全部权限，也不因签名冲突自动卸载。测试包确需 `-t` 时按 APK 的 testOnly 与授权判断。入口从当前 manifest/package manager 解析，不能固定猜 `UnityPlayerActivity`。安装输出、设备读回、启动进程、时间窗日志与真实业务探针一起归属到本次产物；日志诊断复用 `nova-project-diagnose-device-runtime` 的脱敏/时间窗约束，不能清空整台设备日志或用户存档。

## Android AAB

AAB 不能交给 `adb install`。在当前宿主定位实际 `bundletool` 或 Unity AndroidPlayer 的 `Tools/bundletool-all-*.jar`，再核对该版本 `help build-apks` / `help install-apks`；缺 Java/工具则停止该层，不能虚构 `android_install_aab` 脚本。使用新的 APKS 路径，默认不加 `--overwrite`：

```bash
"$nova_java" -jar "$nova_bundletool_jar" build-apks \
  --bundle="$nova_aab" --output="$nova_apks" \
  --connected-device --device-id="$nova_serial" --adb="$nova_adb" \
  --ks="$nova_keystore" --ks-key-alias="$nova_key_alias" \
  --ks-pass="file:$nova_keystore_password_file" --key-pass="file:$nova_key_password_file"
"$nova_java" -jar "$nova_bundletool_jar" install-apks \
  --apks="$nova_apks" --device-id="$nova_serial" --adb="$nova_adb"
```

`build-apks` 未传 keystore 时可能用宿主 debug keystore，甚至生成未签名 APK；AAB 的签名不等于设备 APK 的最终签名，不能依赖该默认值替用户决定。密码使用已授权的受保护文件或宿主交互，禁止 `pass:真实密码` 进入命令日志。APKS 按连接设备生成，不宣称通用于全部 ABI/设备；安装默认会替换同包应用。本轮临时密码文件、APKS 与提取 APK 按归属清理，正式 AAB 保留。Google Play 的应用签名、分发与登录/支付另需商店及设备证据。

### AAB 转换与设备证据

任务 `device.install.aab` 只在 `android-aab-device-install` 分支使用 `apk-set-build`；它证明既有 AAB 的本次设备 APK 集转换，不能写成 Unity BuildReport。默认 Player 构建最低证据不变。

正式 JSON wrapper 的 `apkSetBuild` 使用版本 1，包含 `inputAab / executionPlan / toolDeviceIdentity / buildResult / apkSetVerification / cleanupResult` 六份准确路径与 SHA 引用，以及 `expectedApkCount / outputApksPath`。六份引用全部进入证据的 `artifacts`；原 AAB 必须保留，临时 APKS、提取 APK 和密码文件清理后以实际转换/清场回执保留历史身份，不把已删除路径列为当前产物。

`executionPlan` 冻结 `selectedArtifact.path/sha256`、`deviceSerial`、`packageName`、`outputApks` 与 `actualSigning.certificateSha256`。`toolDeviceIdentity` 记录实际 `tools` 数组中至少 Java/bundletool/adb 的 `id/path/sha256`、`versions.bundletool` 和设备 `serial/model/api/abiList`。设备 serial 仅留受控原始证据，消费记录使用 `context.device=<model>;serial-sha256:<SHA256(serial)>`，`context.target=sha256:<AAB SHA256>`；不得输出密码、密钥或 serial。

转换回执必须有 `exitCode=0 / outputExists=true`；APK 集回执保留 `apksSha256 / apksBytes` 及完整 `apks` 清单，每项有唯一安全 `entry`、正 `size`、`sha256` 和与冻结策略相同的 `certificateSha256`，数量必须等于 `expectedApkCount`，并包含 `splits/base-master.apk`。清场回执分别确认 `inputAabAndKeystoreUnchanged / ownTemporaryFilesRemoved / passwordFilesRemoved / bundletoolExtractionDirectoryAbsent=true`。这些字段只登记实际观察，不以推测补全。

APK 集证据不替代独立 `device` 记录；后者继续要求本次全部 APK 安装字节、包/版本/证书读回、实际入口和请求的业务探针，并记录恢复原包及不卸载、不清数据的范围。输入、签名、设备或产物漂移使对应证据失效；旧 partial 不自动升级。

## iOS 工程、编译与设备

先发现 Xcode 路径、有效 developer directory 和 SDK。`/usr/bin/xcodebuild` 存在不代表完整 Xcode 被选择；命令级 `DEVELOPER_DIR="$nova_xcode_developer_dir"` 可选择已确认 Xcode，不使用 `sudo xcode-select` 改全局。只读 `xcodebuild -list -json` / `-showdestinations`、当前 Podfile/lock/Package.resolved 与 SDK 后处理产物；需要 Pods 时在本次工程执行 `pod install` 后核验结果。不能把 UPM 版本猜成 SPM tag，也不通过禁用 SDK 或替换原生 stub 掩盖失败。

按实际 workspace 或 project 选择其中一种参数，不猜 scheme；使用新的 DerivedData/resultBundle 路径：

```bash
DEVELOPER_DIR="$nova_xcode_developer_dir" xcodebuild \
  -workspace "$nova_workspace" -scheme "$nova_scheme" -configuration "$nova_configuration" \
  -sdk iphoneos -destination 'generic/platform=iOS' \
  -derivedDataPath "$nova_derived_data" -resultBundlePath "$nova_result_bundle" \
  CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO build
```

这是无签名 iphoneos 编译/链接，只在 exit code 0 且本次产物存在时通过该层。模拟器需匹配实际 `iphonesimulator` SDK/设备，不能安装 iphoneos `.app`。签名构建按已冻结团队/profile/entitlements 和 destination 单独完成；不默认加 `-allowProvisioningUpdates` 或 `-allowProvisioningDeviceRegistration` 触发 Apple 外部状态写入。

对已签名、Bundle ID 已核实的本次 `.app`，按当前 Xcode `help` 发现和冻结 device ID 后执行：

```bash
DEVELOPER_DIR="$nova_xcode_developer_dir" xcrun devicectl device install app --device "$nova_device_id" "$nova_signed_app"
DEVELOPER_DIR="$nova_xcode_developer_dir" xcrun devicectl device process launch --device "$nova_device_id" "$nova_bundle_id"
```

模拟器对应 `xcrun simctl install "$nova_simulator_udid" "$nova_simulator_app"` / `simctl launch "$nova_simulator_udid" "$nova_bundle_id"`。`devicectl list devices` 或 `simctl list devices` 仅发现环境；成功安装/启动还须真实入口/UI/表/资源探针与设备时间窗，不能冒充 SDK 真机验收。额外 `--terminate-existing` / `--terminate-running-process` 只在已授权的独立进程测试中使用。

## WebGL HTTP 与浏览器

脚本从当前受管 Skill 或安装包的本 Skill 路径定位，不依赖开发仓 `.agents/skills/nova-webgl-run`。只服务已有本次正式导出，不打开浏览器、不构建、不上传、不接管端口。先 inspect 读取 `contentHash`，再绑定同一摘要：

```bash
python3 "$nova_webgl_script" inspect --build-root "$nova_webgl_build"
python3 "$nova_webgl_script" serve --build-root "$nova_webgl_build" --expected-sha256 "$nova_content_hash" --port 8088
```

默认只有 `127.0.0.1`，端口占用直接失败。根须含 `index.html` 及 `Build/*.wasm`、`.wasm.gz` 或 `.wasm.br`；inspect 冻结目录内普通文件摘要，拒绝符号链接。服务拒绝越界、路径穿越、目录列表、未冻结/已变更文件。`.gz/.br` 分别设置 `Content-Encoding:gzip/br`，Wasm 为 `application/wasm`，JS、data、JSON 使用对应 MIME；`Cache-Control:no-store` 防止本地测试命中过期导出。文件漂移必须重新 inspect/计划，不自动接受新产物。

从当前构建设置核实线程要求。只有实际需要 `SharedArrayBuffer` / 跨源隔离时加 `--cross-origin-isolation`，它设置 COOP `same-origin` 与 COEP `require-corp`；这会限制跨源资源，必须同时核验实际 CDN 的 CORS/CORP。不开启此选项不代表所有 WebGL 构建都需要它。

核验当前 Wasm/loader/data 与 `StreamingAssets/nova-webgl-layout.txt`、HTTP 响应及浏览器 Console/Network、Nova 入口和真实消费。本机响应头不证明生产 CDN 配置正确；该服务不验 Brotli/Gzip 文件完整性或 Unity 运行。关闭本轮服务使用其进程 Ctrl+C，清理本轮测试浏览器状态；正式导出保留，用户已有浏览器存储不能清空。
