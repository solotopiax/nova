# 内网 Verdaccio

从 Nova 根目录执行下列命令，注册开机自启并启动内网 Verdaccio。

```sh
python3 Tools/nova-verdaccio/nova-verdaccio-macos.py
```

服务 Mac 需要 Python 3.10 或更新版本。使用可运行 `node` 和 `verdaccio`、且有 `sudo` 权限的部署账号。配置使用 `/opt/verdaccio-internal/config.yaml`，监听地址和端口从该文件读取。

首次执行可接管已确认的手动实例。已由 `launchd` 正常运行的实例继续运行。服务日志位于 `~/Library/Logs/nova-services/`。

要一次注册并启动 Verdaccio 和 [Nova Issues](../nova-issues/README.md)，从 Nova 根目录执行 `python3 Tools/nova-services-macos.py`。
