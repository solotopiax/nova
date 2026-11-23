#!/usr/bin/env python3
"""从一个入口注册并启动 Nova Issues 和内网 Verdaccio。"""

from pathlib import Path
import subprocess
import sys


def main():
    """按顺序执行两个独立入口，汇总启动结果。"""
    tools = Path(__file__).resolve().parent
    scripts = (
        tools / "nova-verdaccio" / "nova-verdaccio-macos.py",
        tools / "nova-issues" / "nova-issues-server-macos.py",
    )
    failed = False
    for script in scripts:
        result = subprocess.run([sys.executable, str(script)], check=False)
        if result.returncode == 130:
            return 130
        failed = failed or result.returncode != 0
    return int(failed)


if __name__ == "__main__":
    try:
        sys.exit(main())
    except OSError as error:
        sys.exit(str(error))
    except KeyboardInterrupt:
        sys.exit(130)
