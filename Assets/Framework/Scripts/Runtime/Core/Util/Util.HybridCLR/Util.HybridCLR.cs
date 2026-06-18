/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Util.HybridCLR.cs
 * author:    taoye
 * created:   2026/5/6
 * descrip:   HybridCLR 专用工具 —— AOT 元数据补充加载与业务程序集加载唯一 Facade
 ***************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace NovaFramework.Runtime
{
    public static partial class Util
    {
        /// <summary>
        /// HybridCLR 生态唯一 Facade。
        /// 封装 AOT 元数据补充加载（LoadAotMetadataAsync）与业务 DLL 注入（LoadGameAssemblyAsync）。
        /// 底层字节通过 AssetComponent.LoadAssetAsync TextAsset 获取，不走 File IO。
        /// Editor 下所有方法均为 no-op，直接跳过并输出 Debug 日志。
        /// </summary>
        public static class HybridCLR
        {
            /// <summary>
            /// 已加载 AOT 元数据程序集名称守卫集合，防止重复加载。
            /// </summary>
            private static readonly HashSet<string> s_LoadedAOTMetadata = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>
            /// 已加载业务程序集名称守卫集合，防止重复加载。
            /// </summary>
            private static readonly HashSet<string> s_LoadedGameAssemblies = new HashSet<string>(StringComparer.Ordinal);

            /// <summary>
            /// 从 Asset 异步加载 AOT 元数据 DLL 字节，
            /// 并调用 HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly 补充 AOT 泛型元数据。
            /// Editor 下为 no-op（直接返回，不执行任何加载）。
            /// 幂等：同一 location 第二次调用直接返回。
            /// </summary>
            /// <param name="location">Asset 地址，对应 AssetComponent.LoadAssetAsync 的 location 参数（与 HybridCLR assemblyName 等价）。</param>
            /// <param name="mode">同源镜像模式，默认 SuperSet（推荐值，兼容 AOT 泛型实例化超集）。</param>
            public static async UniTask LoadAotMetadataAsync(string location, global::HybridCLR.HomologousImageMode mode = global::HybridCLR.HomologousImageMode.SuperSet)
            {
#if UNITY_EDITOR
                Log.Debug(LogTag.Hotfix, "[Util.HybridCLR][Editor] 编辑器环境下跳过 LoadAotMetadata 元数据加载: {0}", location);
                return;
#else
                if (!s_LoadedAOTMetadata.Add(location))
                {
                    return;
                }

                Stopwatch swTotal = Stopwatch.StartNew();
                try
                {
                    Stopwatch swLoadBytes = Stopwatch.StartNew();
                    byte[] bytes = await LoadDllBytesAsync(location);
                    swLoadBytes.Stop();
                    if (bytes == null)
                    {
                        throw new InvalidOperationException($"[Util.HybridCLR] AOT 元数据字节加载失败：{location}");
                    }

                    Stopwatch swLoadMetadata = Stopwatch.StartNew();
                    global::HybridCLR.LoadImageErrorCode result = global::HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(bytes, mode);
                    swLoadMetadata.Stop();
                    swTotal.Stop();
                    if (result != global::HybridCLR.LoadImageErrorCode.OK)
                    {
                        throw new InvalidOperationException($"[Util.HybridCLR] 加载 AOT 元数据失败：{location}，错误码：{result}");
                    }

                    Log.Debug(LogTag.Hotfix, "[Util.HybridCLR] {0}: total={1}ms (LoadBytes={2}ms, LoadMetadata={3}ms, size={4}KB)", location, swTotal.ElapsedMilliseconds, swLoadBytes.ElapsedMilliseconds, swLoadMetadata.ElapsedMilliseconds, bytes.Length / 1024);
                }
                catch
                {
                    // 加载失败回滚守卫，允许后续重试
                    s_LoadedAOTMetadata.Remove(location);
                    throw;
                }
#endif
            }

            /// <summary>
            /// 从 Asset 异步加载业务 DLL 字节，并通过 System.Reflection.Assembly.Load 注入 AppDomain。
            /// Editor 下为 no-op，直接返回 AppDomain 中已有的源码编译产物。
            /// 幂等：同一 location 第二次调用直接返回已加载的 Assembly，不会重复 Load。
            /// 加载成功后自动调用 Util.Assembly.RefreshAssemblies 刷新反射视图。
            /// </summary>
            /// <param name="location">Asset 地址，对应 AssetComponent.LoadAssetAsync 的 location 参数（与程序集名等价）。</param>
            /// <returns>加载成功的 System.Reflection.Assembly 实例；Editor 下返回已存在的编译产物。</returns>
            public static async UniTask<System.Reflection.Assembly> LoadGameAssemblyAsync(string location)
            {
#if UNITY_EDITOR
                Log.Debug(LogTag.Hotfix, "[Util.HybridCLR][Editor] skip LoadGameAssembly: {0}", location);
                return Util.Assembly.GetAssembly(location);
#else
                if (!s_LoadedGameAssemblies.Add(location))
                {
                    return Util.Assembly.GetAssembly(location);
                }

                Stopwatch swTotal = Stopwatch.StartNew();
                try
                {
                    Stopwatch swLoadBytes = Stopwatch.StartNew();
                    byte[] bytes = await LoadDllBytesAsync(location);
                    swLoadBytes.Stop();
                    if (bytes == null)
                    {
                        throw new InvalidOperationException($"[Util.HybridCLR] 业务 DLL 字节加载失败：{location}");
                    }

                    Stopwatch swAsmLoad = Stopwatch.StartNew();
                    System.Reflection.Assembly asm = System.Reflection.Assembly.Load(bytes);
                    swAsmLoad.Stop();
                    swTotal.Stop();
                    Util.Assembly.RefreshAssemblies();
                    Log.Debug(LogTag.Hotfix, "[Util.HybridCLR] GameAssembly {0}: total={1}ms (LoadBytes={2}ms, AsmLoad={3}ms, size={4}KB)", location, swTotal.ElapsedMilliseconds, swLoadBytes.ElapsedMilliseconds, swAsmLoad.ElapsedMilliseconds, bytes.Length / 1024);
                    return asm;
                }
                catch
                {
                    // 加载失败回滚守卫，允许后续重试
                    s_LoadedGameAssemblies.Remove(location);
                    throw;
                }
#endif
            }

            /// <summary>
            /// 通过 FrameworkManagersGroup 获取 IAssetManager，按 TextAsset 加载指定 location 的 DLL 字节。
            /// 走 AB 通道（资源以 .bytes 后缀打入普通 AssetBundle，PackRule = PackOnceFile/PackDirectory 等），
            /// 不走 RawFile 通道——RawFile 通道仅在 BuildBundleType = RawBundle 时可用。
            /// 加载完成立即 Release（参考 ADR-019：Load 必须配 Release，否则资源系统引用计数无法归零）。
            /// </summary>
            /// <param name="location">Asset 地址，对应 AssetComponent.LoadAssetAsync 的 location 参数。</param>
            /// <returns>DLL 字节数组；加载失败时返回 null。</returns>
            private static async UniTask<byte[]> LoadDllBytesAsync(string location)
            {
                Stopwatch swGetMgr = Stopwatch.StartNew();
                IAssetManager assetManager = FrameworkManagersGroup.GetManager<IAssetManager>();
                swGetMgr.Stop();
                if (assetManager == null)
                {
                    Log.Error(LogTag.Hotfix, "[Util.HybridCLR] IAssetManager 不可用，无法加载 DLL：{0}", location);
                    return null;
                }

                Stopwatch swLoad = Stopwatch.StartNew();
                byte[] bytes;
                IAssetHandle<TextAsset> handle = await assetManager.LoadAsync<TextAsset>(location);
                try
                {
                    bytes = handle.Asset != null ? handle.Asset.bytes : null;
                }
                finally
                {
                    handle.Release();
                }
                swLoad.Stop();
                if (bytes == null || bytes.Length == 0)
                {
                    Log.Error(LogTag.Hotfix, "[Util.HybridCLR] DLL 字节加载失败：{0}", location);
                    return null;
                }

                Log.Debug(LogTag.Hotfix, "[Util.HybridCLR] LoadDllBytes {0}: GetMgr={1}ms, Load={2}ms, size={3}KB", location, swGetMgr.ElapsedMilliseconds, swLoad.ElapsedMilliseconds, bytes.Length / 1024);
                return bytes;
            }
        }
    }
}
