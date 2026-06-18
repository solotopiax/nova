"""
install_apk.py - 通过 adb 将 APK 文件直接安装到已连接的 Android 设备。

用法：
    python install_apk.py <apk_path> <adb_path>

参数说明：
    apk_path - 待安装的 APK 文件绝对路径
    adb_path - adb 可执行文件绝对路径
"""

import subprocess
import os
import sys


def parse_args():
    """解析并返回命令行参数，参数不足时打印帮助信息并退出。"""
    if len(sys.argv) < 3:
        print("错误：参数不足，需要 2 个参数。")
        print(__doc__)
        sys.exit(1)

    return {
        "apk_path": sys.argv[1],
        "adb_path": sys.argv[2],
    }


def install_apk(apk_path, adb_path):
    """
    列出已连接设备并通过 adb install 安装 APK。

    返回值：安装成功返回 True，否则返回 False。
    """
    try:
        subprocess.run([adb_path, "devices"], check=True)
        subprocess.run([adb_path, "install", apk_path], check=True)
        print(f"APK 安装成功：{apk_path}")
        return True
    except subprocess.CalledProcessError as e:
        print(f"APK 安装失败：{e}")
        return False


def main():
    args = parse_args()
    apk_path = args["apk_path"]
    adb_path = args["adb_path"]

    print(f"apk_path = {apk_path}")
    print(f"adb_path = {adb_path}")

    if not os.path.exists(apk_path):
        print(f"错误：APK 文件不存在：{apk_path}")
        sys.exit(1)

    if not install_apk(apk_path, adb_path):
        sys.exit(1)


if __name__ == "__main__":
    main()
