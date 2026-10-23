/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  AndroidResolveSpeedTreeDependencyContract.cs
 * author:    taoye
 * created:   2026/10/16
 * descrip:   SpeedTree 空属性方法支路的精确派生依赖效果及现有 artifact 观察
 ***************************************************************/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using NovaFramework.Runtime;
using UnityEditor;
using UnityEngine;

namespace NovaFramework.Editor
{
    /// <summary>明确声明原生依赖前缀维护，检查当前 artifact 的可读依赖；不注册/注销依赖、不 Produce/Import/Refresh。</summary>
    internal static class AndroidResolveSpeedTreeDependencyContract
    {
        internal const string Prefix = "SpeedTree9Importer_MaterialSettings";
        private const BindingFlags Methods = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>空方法集合必须与已注册输入/现有 artifact 无相关依赖同时成立，才能接纳正规前缀清理效果。</summary>
        /// <param name="controlledPaths">正规解析自身的精确路径，用于稳定非目标 artifact 范围。</param>
        /// <param name="token">取消有界只读扫描；不会取消原生导入。</param>
        /// <param name="digest">当前非目标 artifact 与依赖 key 的稳定摘要。</param>
        /// <param name="error">实际方法、输入、依赖、容量或 API 结构不支持的精确原因。</param>
        /// <returns>当前支路没有可观察的其他 artifact 依赖受该前缀清理影响时返回 true。</returns>
        internal static bool TryCapture(string[] controlledPaths, CancellationToken token, out string digest, out string error)
        {
            digest = null;
            error = null;
            try
            {
                Type database = typeof(AssetDatabase);
                Type attribute = database.Assembly.GetType("UnityEditor.SpeedTree.Importer.MaterialSettingsCallbackAttribute", true);
                if (TypeCache.GetMethodsWithAttribute(attribute).Any(method => method.IsStatic))
                    throw new InvalidOperationException("SpeedTree 存在 MaterialSettings 属性方法，当前只接纳自然空方法支路。");
                string[] paths = AssetDatabase.GetAllAssetPaths().OrderBy(path => path, StringComparer.Ordinal).ToArray();
                if (paths.Length > 131072 || paths.Any(path => path.EndsWith(".st9", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".spm", StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("SpeedTree 存在 st9/spm 输入，或注册资产超出有界扫描容量。");
                Type infoType = database.Assembly.GetType("UnityEditor.ArtifactInfo", true);
                Type dependencyType = database.Assembly.GetType("UnityEditor.ArtifactInfoDependency", true);
                MethodInfo query = database.GetMethod("GetArtifactInfos", Methods, null, new[] { typeof(GUID) }, null);
                MethodInfo valid = infoType.GetMethod("IsValid", Methods, null, Type.EmptyTypes, null);
                MethodInfo dispose = infoType.GetMethod("Dispose", Methods, null, Type.EmptyTypes, null);
                PropertyInfo current = infoType.GetProperty("isCurrentArtifact", Methods);
                PropertyInfo dependencies = infoType.GetProperty("dependencies", Methods);
                PropertyInfo count = infoType.GetProperty("dependencyCount", Methods);
                FieldInfo rawValue = dependencyType.GetField("m_Value", BindingFlags.NonPublic | BindingFlags.Instance);
                if (query?.ReturnType != infoType.MakeArrayType() || valid?.ReturnType != typeof(bool) || dispose?.ReturnType != typeof(void) ||
                    current?.PropertyType != typeof(bool) || dependencies == null || count?.PropertyType != typeof(int) || rawValue?.FieldType != typeof(object))
                    throw new InvalidOperationException("SpeedTree artifact 只读查询 API 与已绑定 CoreModule 结构不符。");
                var controlled = new HashSet<string>(controlledPaths ?? Array.Empty<string>(), StringComparer.Ordinal);
                var rows = new List<string>();
                int observed = 0;
                long queried = 0;
                foreach (string path in paths)
                {
                    token.ThrowIfCancellationRequested();
                    if (controlled.Contains(path)) continue;
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid)) continue;
                    var infos = query.Invoke(null, new object[] { new GUID(guid) }) as Array;
                    if (infos == null) throw new InvalidOperationException("artifact 查询返回空结构。");
                    try
                    {
                        // 历史版本数量不代表当前依赖；按全项目累计数量限界，避免单资产导入历史误阻断。
                        if ((queried += infos.Length) > 131072)
                            throw new InvalidOperationException("artifact 全项目查询版本容量超限：" + queried + " > 131072。");
                        foreach (object info in infos)
                        {
                            token.ThrowIfCancellationRequested();
                            if (info?.GetType() != infoType || !(bool)valid.Invoke(info, null))
                                throw new InvalidOperationException("artifact 返回未初始化条目，不能调用会打印错误的 getter。");
                            if (!(bool)current.GetValue(info)) continue;
                            if (++observed > 131072 || (int)count.GetValue(info) > 8192)
                                throw new InvalidOperationException("当前 artifact/依赖数超出有界扫描容量。");
                            if (!(dependencies.GetValue(info) is IDictionary values)) throw new InvalidOperationException("artifact 依赖不是已审 Dictionary。");
                            var keys = new List<string>();
                            foreach (DictionaryEntry entry in values)
                            {
                                if (!(entry.Key is string key) || entry.Value?.GetType() != dependencyType)
                                    throw new InvalidOperationException("artifact 依赖 key/value 结构未知。");
                                object value = rawValue.GetValue(entry.Value);
                                if (TouchesPrefix(key, value as string)) throw new InvalidOperationException("现有 artifact 依赖命中 SpeedTree 前缀：" + path);
                                keys.Add(key);
                            }
                            rows.Add(path + "|" + guid + "|" + GenerateActionCommon.ComputeTextHash(string.Join("\n", keys.OrderBy(key => key, StringComparer.Ordinal))));
                        }
                    }
                    finally
                    {
                        // GetArtifactInfos 返回的 owned native 信息包装在使用后全部释放；不销毁 importer/资产或全局登记。
                        Exception releaseFailure = null;
                        foreach (object info in infos)
                        {
                            if (info?.GetType() != infoType) continue;
                            try { dispose.Invoke(info, null); }
                            catch (Exception exception) { if (releaseFailure == null) releaseFailure = exception; }
                        }
                        if (releaseFailure != null) throw new InvalidOperationException("artifact owned 包装释放未全部成功。", releaseFailure);
                    }
                }
                digest = GenerateActionCommon.ComputeTextHash(string.Join("\n", rows.OrderBy(row => row, StringComparer.Ordinal)));
                return true;
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            { error = exception is InvalidOperationException ? exception.Message : "SpeedTree artifact 依赖只读扫描失败：" + (exception.InnerException?.GetType().Name ?? exception.GetType().Name); return false; }
        }

        /// <summary>检查原生公开的依赖名称及字符串值；Ordinal 子串保守覆盖完整前缀，不能以零 Hash 假定没有登记。</summary>
        /// <param name="key">当前精确键或依赖名称。</param>
        /// <param name="value">需要检查或规范化的当前值；不执行其自定义方法。</param>
        /// <returns>任何可读依赖名称或字符串值命中目标前缀时返回 true。</returns>
        internal static bool TouchesPrefix(string key, string value)
        {
            return key == null || key.IndexOf(Prefix, StringComparison.Ordinal) >= 0 ||
                value != null && value.IndexOf(Prefix, StringComparison.Ordinal) >= 0;
        }
    }
}
