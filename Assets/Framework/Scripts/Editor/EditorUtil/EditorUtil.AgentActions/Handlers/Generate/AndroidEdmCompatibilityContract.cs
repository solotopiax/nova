/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidEdmCompatibilityContract.cs
 * author:    taoye
 * created:   2026/11/16
 * descrip:   EDM4U 官方预编译发行的 Android 解析兼容合同
 ***************************************************************/

using System;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEditor.PackageManager;
using Path = System.IO.Path;

namespace NovaFramework.Editor
{
    /// <summary>
    /// 受控 Android 解析使用的 EDM4U 官方预编译发行合同。
    /// 1.2.188 与 1.2.190 的 Android 解析、模板、Java、设置及 Version Handler 主处理实现相同；
    /// 官方 DLL 逐方法 IL 比较仅各自版本常量初始化不同，JarResolver 内嵌资源相同。
    /// 1.2.190 的 iOS 差异位于独立 Swift 包构建后支路，不扩展本合同的 Android 写入范围。
    /// 裸 DLL 是官方固定产物，摘要可跨项目复用；源码编译包不能使用此合同。
    /// </summary>
    internal static class AndroidEdmCompatibilityContract
    {
        private const string PackageName = "com.google.external-dependency-manager";

        /// <summary>只查维护方已核对的发行身份；不从当前文件或调用方输入生成信任条目。</summary>
        internal static bool TryGetAuditedIdentity(string version, string dllName, out string sha256, out string mvid)
        {
            sha256 = mvid = null;
            switch (version + "/" + dllName)
            {
                case "1.2.188/Google.JarResolver.dll":
                    sha256 = "75283de7763a24b04bf29f75fa1f90fe6113ef8cf99cf89edc67ffe8e7b3c9d0";
                    mvid = "f55ed918-6037-46ea-9954-936cacb5de0b"; break;
                case "1.2.188/Google.VersionHandlerImpl.dll":
                    sha256 = "c3a6d0419b41b0cbc7ad518b0cc2b311af043b5d71ed709ea3baa576b79862ce";
                    mvid = "57534187-1fe2-4de8-b0f0-c598228153ae"; break;
                case "1.2.188/Google.PackageManagerResolver.dll":
                    sha256 = "fba008a3876e13ec9acaff7e4dc75f88781c4ab16416648cf37ab939be6e1699";
                    mvid = "722dd370-da1f-4152-8c4e-276677b83385"; break;
                case "1.2.188/Google.IOSResolver.dll":
                    sha256 = "ca00858a5f4a657e949480015124f9c783ea80c42bb6000a9c5ec0112ffa5ca5";
                    mvid = "38a009e7-a985-40fe-b3c8-6f87b1103a7e"; break;
                case "1.2.190/Google.JarResolver.dll":
                    sha256 = "4e2505ada8d6c11bcf0b536e45bc3d02cc72b717f8440cb6c3e319410ac4dcb8";
                    mvid = "c76949a2-a9a0-41ed-9587-d079b2261ac1"; break;
                case "1.2.190/Google.VersionHandlerImpl.dll":
                    sha256 = "bbd72cc7552ff2d258b42bae7a1698982f3ba4721944f01cc3833fe1da688d48";
                    mvid = "f242991e-2577-43fa-a086-468d2aaccdf6"; break;
                case "1.2.190/Google.PackageManagerResolver.dll":
                    sha256 = "41ab4d76b4d8b8ade0a9a935c5c70e66ad1f8f3b56650a530208567d1cadb282";
                    mvid = "07414ca3-4191-4d75-9b29-fbbbfabcf188"; break;
                case "1.2.190/Google.IOSResolver.dll":
                    sha256 = "4fa2366a90725fcbca87816d6ce66dadf7b8024e017c998324aae2445a20ec48";
                    mvid = "b904e322-8696-4277-ae04-88eb067cea07"; break;
                default: return false;
            }
            return true;
        }

        /// <summary>校验真实包、唯一已加载程序集、官方文件路径、完整 DLL 与 MVID。</summary>
        internal static bool TryVerifyType(Type type, string dllName, CancellationToken token, out string error)
            => TryGetIdentity(type, dllName, token, out _, out error);

        /// <summary>返回进入当前计划摘要的实际发行身份；未适配版本、替换文件及同名歧义均阻断。</summary>
        internal static bool TryGetIdentity(Type type, string dllName, CancellationToken token,
            out string identity, out string error)
        {
            identity = null;
            error = null;
            if (type == null) { error = "EDM4U 类型尚未加载。"; return false; }
            PackageInfo package = PackageInfo.FindForAssembly(type.Assembly);
            if (package?.name != PackageName ||
                !TryGetAuditedIdentity(package.version, dllName, out string expectedSha, out string expectedMvid))
            {
                error = "EDM4U 版本尚未适配受控 Android 解析：" + (package?.version ?? "未知包来源") +
                    "；当前已审官方 UPM 版本为 1.2.188、1.2.190。";
                return false;
            }
            try
            {
                string path = Path.GetFullPath(Path.Combine(package.resolvedPath,
                    "ExternalDependencyManager", "Editor", package.version, dllName));
                string assemblyName = Path.GetFileNameWithoutExtension(dllName);
                Type[] loadedTypes = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(type.FullName, false)).Where(candidate => candidate != null).Distinct().ToArray();
                if (type.Assembly.GetName().Name != assemblyName ||
                    loadedTypes.Length != 1 || loadedTypes[0] != type ||
                    AppDomain.CurrentDomain.GetAssemblies().Count(assembly => assembly.GetName().Name == assemblyName) != 1 ||
                    Path.GetFullPath(type.Assembly.Location) != path || !File.Exists(path) ||
                    type.Module.ModuleVersionId.ToString("D") != expectedMvid ||
                    GenerateActionCommon.ComputeFileHash(path, token) != expectedSha)
                { error = "EDM4U 官方发行文件的加载位置、DLL 或 MVID 已变化：" + dllName; return false; }
                identity = package.version + "|" + dllName + "|" + expectedSha + "|" + expectedMvid + "|" + path;
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = "EDM4U 发行身份读取失败：" + exception.GetType().Name; return false; }
        }
    }
}
