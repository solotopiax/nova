# nova-issues-server

在一台局域网机器上启动服务。维护者用浏览器查看各项目的环境、使用记录和已确认的 Nova Issue。消费端的上报配置由维护者集中下发。

## 启动

需要 Python 3.10 或更新版本。将本目录复制到服务机，保留 `web/` 和私有的 `.local/accounts.json`。账号文件含两个维护账号的加盐密码摘要。

```sh
python3 -B serve.py --host 0.0.0.0 --port 8787
```

Windows 可用 `py -3 -B serve.py --host 0.0.0.0 --port 8787`。用服务机的局域网 IP 和端口打开浏览器。默认监听 `127.0.0.1`，适合本机验证。服务支持 `--tls-cert` 和 `--tls-key`。使用 HTTPS 时，把对应证书信任链下发给工作站。

网页只有 `taoye` 和 `yingzheng` 两个内置账号。新部署可用 `--init-accounts` 在服务机本地交互生成摘要。Web 提供项目登记，项目凭据与维护账号各自独立。

服务数据默认在 `data/`，可用 `--data-dir` 指定。备份示例为 `python3 -B serve.py --backup ./nova-backup.sqlite3`。恢复时将备份作为新数据目录中的 `nova-issues.sqlite3`，用 `--data-dir` 指向该目录启动。

### macOS 开机自启与更新

在服务 Mac 上，准备 `.local/accounts.json` 后，用有 `sudo` 权限的部署账号从 Nova 根目录运行。脚本会在注册时请求管理员权限。

```sh
python3 Tools/nova-issues/nova-issues-server-macos.py
```

这个入口负责 Nova Issues 的开机自启、启动和更新。首次执行会接管可确认的手动实例，由 `launchd` 管理。以后在服务机上更新 Git 后执行同一条命令。脚本发现服务代码或 Web 文件有变化时会重启 Nova Issues。显示运行版本后，刷新浏览器即可使用新页面。

一次启动两个服务时，在 Nova 根目录运行 `python3 Tools/nova-services-macos.py`。Verdaccio 的独立入口和配置见 [内网 Verdaccio](../nova-verdaccio/README.md)。

端口由无法确认归属的进程占用时，脚本会报告端口和 PID。

重启后由 macOS 自动启动，无需登录桌面。服务日志在 `~/Library/Logs/nova-services/`。如果后台服务无法读取位于桌面或文稿中的项目，请将 Nova 放在普通目录，例如 `~/Nova`。

## 接入项目

内部部署地址固定为 `http://172.16.22.175:8787`。更新服务机代码并启动服务后，安装或升级到包含自动上报的 Nova Framework，再打开 Unity Editor。第一份报告会自动登记项目和工作区。工作站无需设置环境变量或填写地址和凭据。

自动登记只接受私有 IPv4 或本机环回来源。服务按来源限制新增工作区的频率。工作区凭据仅用于上报自身数据，维护台仍使用登录账号。

项目组更新 Nova 并打开 Unity 后，可在 Codex 内审查「Nova 自动反馈机制」并自行启用。详见 [项目组使用说明](../../Assets/Framework/Docs/Onboarding/NOVA_ISSUES.md#在-codex-中启用-nova-自动反馈机制)。授权后的用量和问题报告沿用现有上报接口。

已有统一部署的工作站可用下列脚本准备 [Codex 受管 Hooks](https://learn.chatgpt.com/docs/hooks#managed-hooks-in-local-codex)，继续沿用原钩子。

```sh
python3 -B provision.py --output /absolute/managed/nova-issues
```

在单独复制的服务目录运行此命令时，用 `--agent-source` 指向 Framework 包内的 `Agents/Tools/nova_issues_agent.py`。通过 `--python` 指定钩子所用的 Python。

「总览」按数据库中的待处理问题统计总数和 P0 数量。「问题」页可按项目筛选，并同时使用搜索、状态和优先级。同一个问题涉及多个项目时，在每个相关项目的结果中都能找到。

「项目与版本」显示 Framework、Unity 版本和最近环境上报时间。环境首次识别或基础信息变化时上报，信息未变时按工作站本地自然日在同一工作区首次 Editor 编译完成后上报一次。「使用记录」按项目、模块和发生时间汇总 Codex 调用及 Skill 查阅。工作区队列从工程 Library 后台发送，服务暂不可用时按原编号补发。

## 接口

| 接口 | 用途 |
|---|---|
| `GET /health` | 查看服务、协议版本、自动上报能力及当前进程加载的版本标识 `revision`。 |
| `POST /api/v1/auto-reports` | 接收工作区上报。请求包含项目名和 `report` 信封，首次成功时自动登记项目和工作区。 |
| `POST /api/v1/reports` | 接收旧版客户端或接入方凭项目令牌提交的报告。 |
| `GET /api/v1/issues/<id>` | 用项目令牌查询该项目已上报问题的处理状态及修复版本。 |
| `POST /api/session` | 浏览器登录。维护接口使用会话 Cookie，变更请求校验来源和 CSRF。 |
| `GET /api/maintainer/issues` | 登录后按项目标识 `project`、状态 `status`、优先级 `priority` 和搜索词 `q` 查询问题。 |

上报信封包含 `version=1`、`id`、`workspace`、`observed_at`、`kind` 和 `data`。两项 ID 为 32 位小写十六进制，时间为 Unix 秒。接收成功返回 `accepted`、原 `id` 和接收时间，Issue 同时返回编号。同一项目同一上报 ID 重试返回原回执。相同 ID 内容不同返回 409。

新版工作站通过 `snapshot` 上报基础环境，并通过 `usage` 上报带 `source=codex` 和入口类型 `entry` 的实际调用。服务端继续兼容旧版 `task` 报告。旧版服务未声明 `codex_telemetry` 时，带来源的使用记录留在本地待补发。

## 验证

```sh
python3 -B -m unittest discover -s Tools/nova-issues -p 'test_*.py' -v
```

测试使用独立临时项目与环回服务。两台实际工作站的 LAN 路由、服务机证书和公司下发策略在部署时验证。
