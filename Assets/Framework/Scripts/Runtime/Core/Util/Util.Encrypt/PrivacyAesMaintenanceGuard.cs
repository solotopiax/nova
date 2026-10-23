/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PrivacyAesMaintenanceGuard.cs
 * author:    taoye
 * created:   2026/10/13
 * descrip:   当前Editor默认AES维护标记严格检查
 ***************************************************************/

#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>只在 Editor 宿主检查当前项目固定的默认 AES 维护标记；Runtime 不依赖任何 Editor 程序集。</summary>
    internal static class PrivacyAesMaintenanceGuard
    {
        internal const string RelativeDirectory = "Library/Nova/AgentActions/PersistenceMaintenance";
        internal const string RelativeMarker = RelativeDirectory + "/privacy-aes-active.json";
        internal const string Owner = "nova.privacy-aes-maintenance";

        /// <summary>在 Config/Persist 幂等短路之前调用；未结束维护或无法核实标记时禁止读取默认 AES 数据。</summary>
        internal static void AssertReady()
        {
            AssertProjectReady(System.IO.Path.GetDirectoryName(Application.dataPath));
        }

        /// <summary>仅检查传入工程根内固定文件；链接、坏 JSON、未知 schema 和所有有效未结束标记均阻断。</summary>
        internal static void AssertProjectReady(string projectRoot)
        {
            string marker = MarkerPath(projectRoot);
            if (!File.Exists(marker) && !Directory.Exists(marker)) return;
            ReadOperation(projectRoot);
            throw new InvalidOperationException("存档密钥维护尚未结束，请先通过 Codex 检查并恢复该操作，再启动或构建游戏。");
        }

        /// <summary>固定项目归属和严格单一 JSON 对象，返回规范操作标识；不接受日志中的自定义路径。</summary>
        internal static string ReadOperation(string projectRoot)
        {
            string marker = MarkerPath(projectRoot);
            if (!File.Exists(marker) || new FileInfo(marker).Length <= 0 || new FileInfo(marker).Length > 2048)
                throw new InvalidOperationException("存档维护标记无法核实，保留现场并停止启动。");
            using var reader = new JsonTextReader(new StreamReader(marker, new UTF8Encoding(false, true))) { MaxDepth = 4 };
            JObject value = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
            if (reader.Read() || value.Count != 4 || value["schemaVersion"]?.Type != JTokenType.Integer || value.Value<int>("schemaVersion") != 1 ||
                value["owner"]?.Type != JTokenType.String || value.Value<string>("owner") != Owner ||
                value["projectSha256"]?.Type != JTokenType.String || value.Value<string>("projectSha256") != ProjectHash(projectRoot) ||
                value["operationId"]?.Type != JTokenType.String || !Guid.TryParseExact(value.Value<string>("operationId"), "N", out _))
                throw new InvalidOperationException("存档维护标记的结构或项目归属无效，保留现场并停止启动。");
            return value.Value<string>("operationId");
        }

        /// <summary>当前工程唯一标记路径；逐级拒绝已有链接，不扫描或删除其它项目内容。</summary>
        internal static string MarkerPath(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot)) throw new InvalidOperationException("工程根无效。");
            string path = System.IO.Path.Combine(System.IO.Path.GetFullPath(projectRoot), RelativeMarker);
            for (string current = path; !string.IsNullOrEmpty(current); current = System.IO.Path.GetDirectoryName(current))
            {
                try { if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("存档维护路径含链接。"); }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }
            return path;
        }

        /// <summary>只绑定完整工程根，不包含玩家值或密钥；与 Editor 写入方使用同一计算。</summary>
        internal static string ProjectHash(string projectRoot)
        {
            string root = System.IO.Path.GetFullPath(projectRoot).TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(root))).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
#endif
