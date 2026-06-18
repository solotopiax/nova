"""
install_aab.py - 将 AAB 包体通过 bundletool 转换为 APKS 并安装到已连接的 Android 设备。

用法：
    python install_aab.py <aab_path> <apks_output_path> <bundletool_path>
                          <adb_path> <keystore_path> <keystore_alias>
                          <keystore_pass> <keystore_alias_pass>

参数说明：
    aab_path            - 待安装的 AAB 文件绝对路径
    apks_output_path    - bundletool 转换后输出的 .apks 文件路径
    bundletool_path     - bundletool.jar 绝对路径
    adb_path            - adb 可执行文件绝对路径
    keystore_path       - 签名证书（.keystore）绝对路径
    keystore_alias      - 签名证书 alias 名称
    keystore_pass       - 签名证书密钥库密码
    keystore_alias_pass - 签名证书 alias 密码
"""

import subprocess
import os
import sys


def parse_args():
    """解析并返回命令行参数，参数不足时打印帮助信息并退出。"""
    if len(sys.argv) < 9:
        print("错误：参数不足，需要 8 个参数。")
        print(__doc__)
        sys.exit(1)

    return {
        "aab_path": sys.argv[1].replace("\\", "/"),
        "apks_output_path": sys.argv[2].replace("\\", "/"),
        "bundletool_path": sys.argv[3].replace("\\", "/"),
        "adb_path": sys.argv[4].replace("\\", "/"),
        "keystore_path": sys.argv[5].replace("\\", "/"),
        "keystore_alias": sys.argv[6],
        "keystore_pass": sys.argv[7],
        "keystore_alias_pass": sys.argv[8],
    }


def convert_aab_to_apks(args):
    """
    调用 bundletool 将 AAB 转换为 APKS 文件。

    返回值：转换成功返回 True，否则返回 False。
    """
    cmd = [
        "java", "-jar", args["bundletool_path"],
        "build-apks",
        f"--bundle={args['aab_path']}",
        f"--output={args['apks_output_path']}",
        f"--ks={args['keystore_path']}",
        f"--ks-pass=pass:{args['keystore_pass']}",
        f"--ks-key-alias={args['keystore_alias']}",
        f"--key-pass=pass:{args['keystore_alias_pass']}",
    ]
    try:
        subprocess.run(cmd, check=True)
        print("AAB -> APKS 转换成功。")
        return True
    except subprocess.CalledProcessError as e:
        print(f"AAB -> APKS 转换失败：{e}")
        return False


def install_apks_to_device(args):
    """
    调用 bundletool 将 APKS 文件安装到已连接的 Android 设备。

    返回值：安装成功返回 True，否则返回 False。
    """
    cmd = [
        "java", "-jar", args["bundletool_path"],
        "install-apks",
        f"--apks={args['apks_output_path']}",
        f"--adb={args['adb_path']}",
    ]
    try:
        subprocess.run(cmd, check=True)
        print("APKS 安装到设备成功。")
        return True
    except subprocess.CalledProcessError as e:
        print(f"APKS 安装到设备失败：{e}")
        return False


def main():
    args = parse_args()

    print(f"aab_path         = {args['aab_path']}")
    print(f"apks_output_path = {args['apks_output_path']}")
    print(f"bundletool_path  = {args['bundletool_path']}")
    print(f"adb_path         = {args['adb_path']}")
    print(f"keystore_path    = {args['keystore_path']}")
    print(f"keystore_alias   = {args['keystore_alias']}")
    # 密码不打印，避免泄露到日志

    # 若旧的 .apks 文件已存在则先删除，避免 bundletool 报错
    if os.path.exists(args["apks_output_path"]):
        print(f"删除已存在的 APKS 文件：{args['apks_output_path']}")
        os.remove(args["apks_output_path"])

    if not convert_aab_to_apks(args):
        sys.exit(1)

    if not install_apks_to_device(args):
        sys.exit(1)


if __name__ == "__main__":
    main()
