# Google Play 自动上传：后台配置说明

适用于首次使用 Nova Pipify 的“Google Play 上传 AAB”功能。完成这份说明中的一次性配置后，后续版本可以通过 Pipify 上传，无需每次手动选择 AAB 文件。

核对日期：2026-10-12。本文提供操作步骤，不代表已检查或修改你的实际 Google 后台。

## 1. 先分清两个后台

| 后台 | 用途 | 入口 |
|---|---|---|
| Google Play Console | 创建应用、接收 AAB、管理测试和正式版本、给上传账号授予应用权限 | [打开 Play Console](https://play.google.com/console/) |
| Google Cloud Console | 启用上传 API、创建服务账号、下载凭据 JSON | [打开 Cloud Console](https://console.cloud.google.com/) |

**凭据 JSON 从 Google Cloud 下载，上传权限在 Google Play 授予。两边都要配置。**

Play Console 的账号所有者或拥有用户管理权限的管理员负责应用授权；Cloud 项目中有权创建服务账号和密钥的人员负责凭据准备。可以由同一个人完成，也可以交给各自管理员。

Google 当前不要求把 Play 开发者账号关联到 Cloud 项目；如果旧教程让你在 Play Console 中寻找“关联 Cloud 项目”，可以直接按下面的 Cloud 入口操作。

## 2. 在 Play Console 准备应用和首包

已有应用、且已经成功手动上传过安装包时，可跳到第 3 节。

1. 登录 [Play Console](https://play.google.com/console/)，选择应用所属的开发者账号。
2. 创建目标应用，按页面填写名称、语言、应用/游戏类别、免费/付费等信息，并完成要求的声明。
3. 在目标应用中进入内部测试轨道，创建版本并手动上传第一份正确签名的 AAB。首次上传按页面要求设置 **Play 应用签名**。
4. 确认后台识别出的应用包名与工程一致，例如 `com.example.game`，并记录已经上传的 `versionCode`。后续新版本使用更大的版本号。

fastlane supply 的官方说明要求先手动上传至少一个构建；自动上传用于这之后的更新。首次准备不要求把应用直接发布到正式轨道。

如果后续要让用户下载，再按后台要求完成应用内容、隐私政策、数据安全、内容分级、目标受众、广告声明、商店资料等适用项目。保存内部测试草稿与正式上线的要求不同，以目标轨道页面列出的阻塞项为准。

签名与接口凭据的用途不同：

| 文件/密钥 | 用途 |
|---|---|
| 上传签名用的 keystore | 构建时为 AAB 签名，须与 Play 接受的上传密钥一致 |
| Google Cloud 服务账号 JSON | 让 Pipify/fastlane 以服务账号身份调用上传 API |

JSON 不能代替 AAB 签名。首次准备后，沿用该应用对应的上传签名配置。

## 3. 在 Cloud Console 启用 API

1. 打开 [Google Cloud Console](https://console.cloud.google.com/)，在顶部选择公司已有的合适项目。
2. 如果没有可用项目，打开 [创建项目](https://console.cloud.google.com/projectcreate)，完成创建后选中它。
3. 打开 [Google Play Developer API](https://console.cloud.google.com/apis/library/androidpublisher.googleapis.com)。
4. 确认顶部选中的项目正确，点击 **启用（Enable）**；如果已经启用，保留现状即可。

后面创建服务账号也使用这个 Cloud 项目。需要启用的是 Google Play Developer API，不是 Google Play Games Services。

## 4. 创建服务账号并下载 JSON

已有专用上传服务账号和可用 JSON 时，可复用，并核对第 5 节的应用权限。

1. 打开 [服务账号列表](https://console.cloud.google.com/iam-admin/serviceaccounts)。菜单路径为 **IAM 和管理 → 服务账号**。
2. 点击 **创建服务账号（Create service account）**，填写便于识别的名称，例如 `play-uploader`，完成创建。
3. 记下服务账号邮箱，例如：

   ```text
   play-uploader@YOUR_PROJECT_ID.iam.gserviceaccount.com
   ```

4. 点击刚创建的服务账号，进入 **密钥（Keys）→ 添加密钥（Add key）→ 创建新密钥（Create new key）**。
5. 选择 **JSON**，点击 **创建（Create）**。浏览器下载的 `.json` 文件就是 Pipify 所需的凭据。

创建服务账号时出现的 Cloud 项目角色设置，不能代替 Play Console 的应用授权。不要为了获得 Play 上传权限给服务账号授予 Cloud 项目 Owner/Editor。

已有私钥不能再次下载。如果原 JSON 丢失，需要由管理员创建新密钥，并更新构建机上的文件。若页面禁止创建密钥，请交给 Cloud 管理员检查操作权限和组织策略。

## 5. 在 Play Console 授予目标应用权限

**这一步最容易漏掉。仅下载 JSON，上传仍可能报 `The caller does not have permission`。**

1. 回到 [Play Console](https://play.google.com/console/)，选择目标应用所属的开发者账号。
2. 进入 **用户和权限（Users and permissions）→ 邀请新用户（Invite new users）**。
3. 在邮箱栏填写第 4 节的 **服务账号邮箱**，不是开发人员的 Google 登录邮箱。
4. 在 **应用权限 → 添加应用** 中选择需要自动上传的目标应用。
5. 按实际用途授予下表中的应用权限，保存并完成邀请。已有账号则直接检查其应用授权和权限是否有效。

| 用途 | 应用权限 |
|---|---|
| 内部、封闭或开放测试轨道上传，包括测试草稿 | **查看应用信息（只读）**；**将应用发布到测试轨道** |
| 正式轨道上传/发布 | **查看应用信息（只读）**；**发布为正式版，排除设备以及使用 Google Play 应用签名功能** |
| 同时使用测试和正式轨道 | 上述查看权限，以及对应的测试、正式版权限 |

“将应用发布到测试轨道”包含上传草稿、创建/修改版本和向测试轨道发布；不要因为当前只保存草稿而漏掉它。仅上传 AAB 不需要财务、订单或账号管理员权限。

“管理测试轨道和修改测试人员列表”是另一项权限。本上传步骤不修改测试人员名单；由管理员在后台配置名单即可。

授权时核对的是 **实际目标应用**，不能只确认服务账号出现在用户列表中。长期自动化还要检查权限到期日；应用转移到其他开发者账号后，需要在新账号下重新核对授权。

## 6. 需要测试人员下载时，再配置内部测试

只上传并保存草稿时，可以暂不配置测试名单。

需要测试人员安装时，由管理员在目标应用的内部测试轨道中配置测试人员邮箱列表或 Google 群组，保存，并提供加入测试的链接。测试人员使用被允许的 Google 账号加入测试。

上传成功不代表测试人员已经可以下载：版本仍须满足对应轨道的审核和后台发布要求。正式发布还要满足开发者账号的上线资格要求。

## 7. 把凭据路径填回 Unity

例如，把下载的 JSON 保存为：

```text
<Unity 项目根目录>/Docs/evidences/googleplay_upload/credentials.json
```

在 **Nova/Open Pipify → Google Play 上传 AAB → 参数配置** 中填写：

```text
凭据 JSON 路径（相对 Unity 项目根目录）：
Docs/evidences/googleplay_upload/credentials.json
```

项目根目录指包含 `Assets`、`Packages`、`ProjectSettings` 的目录。Step 会将相对路径转换为绝对路径后交给 fastlane；填写的是文件路径，不是 JSON 内容。不要把 JSON 交给外部协作者、放入客户端安装包或公开仓库。

本地构建机另须有可用的 fastlane。以上步骤完成后，再按 [Pipify 上传配置说明](PipifySteps.GooglePlay.md) 配置 AAB、应用包名、目标轨道和用途。

当前界面选择“保存为草稿”时，手动审核选项灰显并显示未勾选；这是本工具的使用限制。草稿不开放下载，是否触发审核由 Google 后台决定。“仅验证上传”也会真实上传，不适合作为无副作用的凭据检查。

## 8. 配置完成后交给开发人员的信息

| 信息 | 示例/要求 |
|---|---|
| Play 应用包名 | `com.example.game`，与 AAB 内包名一致 |
| 目标轨道 | 先使用内部测试 `internal` |
| 服务账号邮箱 | 已在目标开发者账号下获得该应用的权限 |
| 凭据 JSON 文件 | 已交给授权的构建机保管，并填写项目根相对路径 |
| 首包与签名 | 已成功手动上传首包，后续沿用 Play 接受的上传签名 |
| 当前最大 versionCode | 后续构建使用更大的值；已失败/仅验证上传的版本号需核对后台记录 |

这份信息准备好后即可配置自动上传；无需每次发版重新创建服务账号、下载密钥或重复邀请用户。

## 官方资料

- [Google Play Developer API 接入](https://developers.google.com/android-publisher/getting_started?hl=zh-cn)：Cloud API 与 Play 服务账号授权，以及不再要求关联 Cloud 项目的说明。
- [Play Console 用户和权限](https://support.google.com/googleplay/android-developer/answer/9844686?hl=zh-Hans)：测试、正式发布、测试名单管理等权限定义。
- [创建和删除服务账号密钥](https://docs.cloud.google.com/iam/docs/keys-create-delete?hl=zh-cn)：JSON 下载和组织策略限制。
- [fastlane supply](https://docs.fastlane.tools/actions/upload_to_play_store/)：首次手动上传要求和上传参数。
