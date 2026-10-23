/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PersistenceCredentialVault.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   Editor项目凭据的安全封存
 ***************************************************************/

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>Editor 宿主 macOS Keychain 的项目命名空间；不通过 shell/argv/JSON 接收或输出密码。</summary>
    internal static class PersistenceCredentialVault
    {
        internal const string HandlePrefix = "nova-keychain:";
        private const string Security = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundation = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";
        private static readonly Lazy<IntPtr> SecurityHandle = new Lazy<IntPtr>(() => dlopen(Security, 2));
        private static IntPtr SecurityLibrary => SecurityHandle.Value;
        private static readonly Lazy<IntPtr> CoreHandle = new Lazy<IntPtr>(() => dlopen(CoreFoundation, 2));
        private static IntPtr CoreLibrary => CoreHandle.Value;

        /// <summary>完整恢复秘密只在内存与 Keychain 数据项中；与当前/新 Cipher 和 Privacy 凭据独立。</summary>
        internal sealed class RecoverySecrets
        {
            internal string Before;
            internal string After;
            internal string Backup;
        }

        /// <summary>仅 macOS Editor 宿主可使用当前原生 Keychain；不描述 Player 目标平台。</summary>
        internal static bool IsSupported => Application.platform == RuntimePlatform.OSXEditor;
        /// <summary>只接受当前项目命名空间使用的 opaque N 格式 GUID handle。</summary>
        internal static bool ValidHandle(string handle) => handle != null && handle.StartsWith(HandlePrefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(handle.Substring(HandlePrefix.Length), "N", out _);
        /// <summary>生成不含秘密的唯一 handle，不创建凭据或持久文件。</summary>
        internal static string NewHandle() => HandlePrefix + Guid.NewGuid().ToString("N");
        /// <summary>用密码学安全随机数生成32字节密码；清除临时随机字节。</summary>
        internal static string GeneratePassword()
        {
            byte[] bytes = new byte[32]; using var random = RandomNumberGenerator.Create(); random.GetBytes(bytes);
            try { return Convert.ToBase64String(bytes); } finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        /// <summary>按项目根整体摘要和 credential/recovery 角色分隔 service，不接受用户 service。</summary>
        private static string Service(string kind) => "com.solotopia.nova.persistence." +
            GenerateActionCommon.ComputeTextHash(GenerateActionCommon.ProjectRoot) + "." + kind;
        /// <summary>严格核验 handle 并仅取 GUID account，拒绝任意账户名称。</summary>
        private static string Account(string handle)
        {
            if (!ValidHandle(handle)) throw new InvalidOperationException("项目凭据 handle 无效。");
            return handle.Substring(HandlePrefix.Length);
        }
        /// <summary>仅读取当前项目 credential 项并严格解码非空 UTF8；返回秘密只供内部瞬时使用。</summary>
        internal static string ReadCredential(string handle)
        {
            byte[] bytes = Read("credential", handle);
            try
            {
                string password = new UTF8Encoding(false, true).GetString(bytes);
                if (string.IsNullOrEmpty(password) || bytes.Length > 4096) throw new InvalidOperationException("凭据为空或超限。");
                return password;
            }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        /// <summary>创建唯一项目 credential 项，不覆盖已有项；密码仅经原生内存参数进入 Keychain。</summary>
        internal static void StoreCredential(string handle, string password)
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(password);
            try { Add("credential", handle, bytes); }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        /// <summary>将完整旧/新/独立备份秘密以固定二进制格式写入 recovery 项；调用方须重读后才写库。</summary>
        internal static void Seal(string handle, RecoverySecrets secrets)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            { writer.Write(1); writer.Write(secrets.Before); writer.Write(secrets.After); writer.Write(secrets.Backup); }
            byte[] bytes = stream.ToArray();
            try
            {
                Add("recovery", handle, bytes);
            }
            finally { Array.Clear(bytes, 0, bytes.Length); Array.Clear(stream.GetBuffer(), 0, (int)stream.Length); }
        }
        /// <summary>跨重载读取 recovery 项并严格核版本、全长度和三非空秘密。</summary>
        internal static RecoverySecrets Unseal(string handle)
        {
            byte[] bytes = Read("recovery", handle);
            try
            {
                using var reader = new BinaryReader(new MemoryStream(bytes), new UTF8Encoding(false, true));
                if (reader.ReadInt32() != 1) throw new InvalidOperationException("恢复秘密版本无效。");
                var secrets = new RecoverySecrets { Before = reader.ReadString(), After = reader.ReadString(), Backup = reader.ReadString() };
                if (reader.BaseStream.Position != reader.BaseStream.Length || string.IsNullOrEmpty(secrets.Before) ||
                    string.IsNullOrEmpty(secrets.After) || string.IsNullOrEmpty(secrets.Backup)) throw new InvalidOperationException("恢复秘密不完整。");
                return secrets;
            }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        /// <summary>仅删除当前项目指定角色和 opaque account；不存在也成功，不扫描其它凭据。</summary>
        internal static bool Delete(string kind, string handle)
        {
            using var query = new Query(kind, handle);
            int result = SecItemDelete(query.Dictionary);
            return result == 0 || result == -25300;
        }
        /// <summary>SecItemAdd 新建限制大小的原生数据；device-only且禁认证UI，失败不覆盖已有项。</summary>
        private static void Add(string kind, string handle, byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > 16384) throw new InvalidOperationException("封存数据无效。");
            using var query = new Query(kind, handle);
            query.Set("kSecValueData", CFDataCreate(IntPtr.Zero, bytes, bytes.Length), true);
            query.Set("kSecAttrAccessible", Symbol(SecurityLibrary, "kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly"));
            if (SecItemAdd(query.Dictionary, IntPtr.Zero) != 0) throw new InvalidOperationException("Keychain 封存失败；未授权创建或宿主状态不满足。");
        }
        /// <summary>按项目角色/account仅读单个Keychain数据项；限制大小并释放CF对象。</summary>
        private static byte[] Read(string kind, string handle)
        {
            using var query = new Query(kind, handle);
            query.Set("kSecReturnData", Symbol(CoreLibrary, "kCFBooleanTrue"));
            query.Set("kSecMatchLimit", Symbol(SecurityLibrary, "kSecMatchLimitOne"));
            int status = SecItemCopyMatching(query.Dictionary, out IntPtr data);
            if (status != 0 || data == IntPtr.Zero) throw new InvalidOperationException("项目 Keychain 凭据不可读取。");
            try
            {
                long length = CFDataGetLength(data);
                if (length <= 0 || length > 16384) throw new InvalidOperationException("封存数据大小无效。");
                byte[] bytes = new byte[(int)length]; Marshal.Copy(CFDataGetBytePtr(data), bytes, 0, bytes.Length); return bytes;
            }
            finally { CFRelease(data); }
        }
        private sealed class Query : IDisposable
        {
            internal readonly IntPtr Dictionary;
            private readonly System.Collections.Generic.List<IntPtr> owned = new System.Collections.Generic.List<IntPtr>();
            /// <summary>构造受控Keychain查询并拥有其临时CF字符串/数据；不执行外部命令。</summary>
            internal Query(string kind, string handle)
            {
                if (!IsSupported) throw new PlatformNotSupportedException("凭据封存当前仅支持 macOS Editor 宿主。");
                Dictionary = CFDictionaryCreateMutable(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero);
                if (Dictionary == IntPtr.Zero) throw new InvalidOperationException("无法创建 Keychain 查询。");
                try
                {
                    Set("kSecClass", Symbol(SecurityLibrary, "kSecClassGenericPassword"));
                    Set("kSecAttrService", CFStringCreateWithCString(IntPtr.Zero, Service(kind), 0x08000100), true);
                    Set("kSecAttrAccount", CFStringCreateWithCString(IntPtr.Zero, Account(handle), 0x08000100), true);
                    Set("kSecUseAuthenticationUI", Symbol(SecurityLibrary, "kSecUseAuthenticationUIFail"));
                }
                catch { Dispose(); throw; }
            }
            /// <summary>将已验证CF符号/值加入查询；自有临时对象在Dispose统一释放。</summary>
            internal void Set(string key, IntPtr value, bool own = false)
            {
                if (value == IntPtr.Zero) throw new InvalidOperationException("宿主凭据 API 不可用。");
                if (own) owned.Add(value); CFDictionarySetValue(Dictionary, Symbol(SecurityLibrary, key), value);
            }
            /// <summary>清除本实例拥有的临时内存或fixture状态；不删除正式恢复材料。</summary>
            public void Dispose() { foreach (IntPtr value in owned) CFRelease(value); owned.Clear(); CFRelease(Dictionary); }
        }
        /// <summary>从已加载系统framework取得真实CF对象常量，缺符号即阻断。</summary>
        private static IntPtr Symbol(IntPtr library, string name)
        {
            IntPtr address = dlsym(library, name);
            if (address == IntPtr.Zero) throw new InvalidOperationException("宿主凭据 API 符号缺失。");
            return Marshal.ReadIntPtr(address);
        }
        /// <summary>加载固定系统framework，不接受用户路径。</summary>
        [DllImport("/usr/lib/libSystem.B.dylib")]
        private static extern IntPtr dlopen(string path, int flags);
        /// <summary>取得固定framework符号地址。</summary>
        [DllImport("/usr/lib/libSystem.B.dylib")]
        private static extern IntPtr dlsym(IntPtr handle, string name);
        /// <summary>原生新建Keychain项，不覆盖已有项。</summary>
        [DllImport(Security)]
        private static extern int SecItemAdd(IntPtr attributes, IntPtr result);
        /// <summary>原生读取精确项目项，调用方释放结果。</summary>
        [DllImport(Security)]
        private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);
        /// <summary>原生删除精确项目项。</summary>
        [DllImport(Security)]
        private static extern int SecItemDelete(IntPtr query);
        /// <summary>创建本bridge独占的临时CF字典。</summary>
        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDictionaryCreateMutable(IntPtr allocator, long capacity, IntPtr keys, IntPtr values);
        /// <summary>设置受控CF对象到临时查询。</summary>
        [DllImport(CoreFoundation)]
        private static extern void CFDictionarySetValue(IntPtr dictionary, IntPtr key, IntPtr value);
        /// <summary>UTF8创建临时CF字符串，调用方释放。</summary>
        [DllImport(CoreFoundation)]
        private static extern IntPtr CFStringCreateWithCString(IntPtr allocator, string value, uint encoding);
        /// <summary>复制临时字节到CFData，调用方释放。</summary>
        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDataCreate(IntPtr allocator, byte[] bytes, long length);
        /// <summary>读取CFData长度供容量核验。</summary>
        [DllImport(CoreFoundation)]
        private static extern long CFDataGetLength(IntPtr data);
        /// <summary>取得已验CFData字节指针供内部复制。</summary>
        [DllImport(CoreFoundation)]
        private static extern IntPtr CFDataGetBytePtr(IntPtr data);
        /// <summary>释放本bridge拥有的临时CF对象。</summary>
        [DllImport(CoreFoundation)]
        private static extern void CFRelease(IntPtr value);
    }
}
