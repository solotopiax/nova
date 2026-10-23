/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  NativeApkInstallPath.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   独立 Android APK 安装能力
 ***************************************************************/

using System;
using System.IO;
using IoPath = System.IO.Path;

namespace NovaFramework.Runtime
{
    /// <summary>安装入口独立验证受控本地文件；不能依赖下载入口或调用方声称已校验。</summary>
    internal static class NativeApkInstallPath
    {
        /// <summary>只接受受控根目录内的既有非空完整文件，拒相对路径、根自身及符号链接。</summary>
        /// <param name="root">平台 persistentDataPath。</param>
        /// <param name="apkPath">业务显式提交的已完成文件绝对路径。</param>
        /// <returns>规范文件绝对路径；不代表 APK 身份或签名已经验证。</returns>
        internal static string Validate(string root, string apkPath)
        {
            if (string.IsNullOrWhiteSpace(root) || !IoPath.IsPathRooted(root) || string.IsNullOrWhiteSpace(apkPath) ||
                !IoPath.IsPathRooted(apkPath)) throw new ArgumentException("安装只接受受控目录内的文件绝对路径。", nameof(apkPath));
            string directory = IoPath.GetFullPath(root).TrimEnd(IoPath.DirectorySeparatorChar) + IoPath.DirectorySeparatorChar;
            string full = IoPath.GetFullPath(apkPath);
            if (!full.StartsWith(directory, StringComparison.Ordinal) || Directory.Exists(full) || !File.Exists(full))
                throw new ArgumentException("安装文件必须已经存在且位于 persistentDataPath 内。", nameof(apkPath));
            for (FileSystemInfo item = new FileInfo(full); item != null;
                 item = item is FileInfo file ? file.Directory : ((DirectoryInfo)item).Parent)
                if (item.Exists && (item.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("APK 安装路径不能包含符号链接。");
            long length = new FileInfo(full).Length;
            if (length <= 0 || length > 1024L * 1024 * 1024) throw new IOException("APK 安装文件为空或超过 1 GiB。");
            return full;
        }
    }
}
