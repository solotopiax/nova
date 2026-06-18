/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  PipifySteps.Export.Network.cs
 * author:    taoye
 * created:   2026/5/11
 * descrip:   Pipify 内置 Step 合集 —— 导出分组（Network 导出）
 ***************************************************************/

using System;
using Cysharp.Threading.Tasks;

namespace NovaFramework.Editor
{
    /// <summary>
    /// Pipify 内置 Step 合集（partial）。
    /// 本文件收录导出分组 Network 子类的原子操作：域名数据/类型、网络指令数据/类型、Proto 协议导出。
    /// 每个方法仅做薄封装，调用 EditorUtil.Network.* 对应 public API。
    /// </summary>
    internal static partial class PipifySteps
    {
        /// <summary>
        /// Step：导出域名表数据（JSON 数据文件），调用 HostKeyExporter.ExportHostKeyData。
        /// 通过 Helpers.ResolveComponentOnNova 定位 NetworkComponent，
        /// 取 NetworkSettings.HostKeySettings 后调用导出入口；失败时抛出 InvalidOperationException 中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.network.hostkey.data", "导出 Network 域名数据", "导出资源/Network")]
        internal static UniTask RunExportNetworkHostKeyData(PipifyContext ctx)
        {
            Runtime.NetworkComponent networkComponent = Helpers.ResolveComponentOnNova<Runtime.NetworkComponent>();
            Runtime.HostKeySettings hostKeySettings = networkComponent.NetworkSettings?.HostKeySettings;
            if (hostKeySettings == null)
            {
                throw new InvalidOperationException("[Pipify] NetworkComponent.NetworkSettings.HostKeySettings 未配置，请在 Inspector 中完善 Network 设置。");
            }

            bool success = EditorUtil.Network.HostKeyExporter.ExportHostKeyData(hostKeySettings);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] 域名数据导出失败，请查看控制台错误日志。");
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出域名类型代码（C# 枚举/常量类），调用 HostKeyExporter.ExportHostKeyCode。
        /// 通过 Helpers.ResolveComponentOnNova 定位 NetworkComponent，
        /// 取 NetworkSettings.HostKeySettings 后调用导出入口；失败时抛出 InvalidOperationException 中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.network.hostkey.code", "导出 Network 域名类型", "导出资源/Network")]
        internal static UniTask RunExportNetworkHostKeyCode(PipifyContext ctx)
        {
            Runtime.NetworkComponent networkComponent = Helpers.ResolveComponentOnNova<Runtime.NetworkComponent>();
            Runtime.HostKeySettings hostKeySettings = networkComponent.NetworkSettings?.HostKeySettings;
            if (hostKeySettings == null)
            {
                throw new InvalidOperationException("[Pipify] NetworkComponent.NetworkSettings.HostKeySettings 未配置，请在 Inspector 中完善 Network 设置。");
            }

            bool success = EditorUtil.Network.HostKeyExporter.ExportHostKeyCode(hostKeySettings);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] 域名类型代码导出失败，请查看控制台错误日志。");
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出网络指令数据（JSON 数据文件），调用 NetCmdExporter.ExportNetCmdData。
        /// 通过 Helpers.ResolveComponentOnNova 定位 NetworkComponent，
        /// 取 NetworkSettings.NetCmdSettings 后调用导出入口；失败时抛出 InvalidOperationException 中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.network.netcmd.data", "导出 Network 指令数据", "导出资源/Network")]
        internal static UniTask RunExportNetworkNetCmdData(PipifyContext ctx)
        {
            Runtime.NetworkComponent networkComponent = Helpers.ResolveComponentOnNova<Runtime.NetworkComponent>();
            Runtime.NetCmdSettings netCmdSettings = networkComponent.NetworkSettings?.NetCmdSettings;
            if (netCmdSettings == null)
            {
                throw new InvalidOperationException("[Pipify] NetworkComponent.NetworkSettings.NetCmdSettings 未配置，请在 Inspector 中完善 Network 设置。");
            }

            bool success = EditorUtil.Network.NetCmdExporter.ExportNetCmdData(netCmdSettings);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] 网络指令数据导出失败，请查看控制台错误日志。");
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出网络指令类型代码（C# 枚举/常量类），调用 NetCmdExporter.ExportNetCmdCode。
        /// 通过 Helpers.ResolveComponentOnNova 定位 NetworkComponent，
        /// 取 NetworkSettings.NetCmdSettings 后调用导出入口；失败时抛出 InvalidOperationException 中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.network.netcmd.code", "导出 Network 指令类型", "导出资源/Network")]
        internal static UniTask RunExportNetworkNetCmdCode(PipifyContext ctx)
        {
            Runtime.NetworkComponent networkComponent = Helpers.ResolveComponentOnNova<Runtime.NetworkComponent>();
            Runtime.NetCmdSettings netCmdSettings = networkComponent.NetworkSettings?.NetCmdSettings;
            if (netCmdSettings == null)
            {
                throw new InvalidOperationException("[Pipify] NetworkComponent.NetworkSettings.NetCmdSettings 未配置，请在 Inspector 中完善 Network 设置。");
            }

            bool success = EditorUtil.Network.NetCmdExporter.ExportNetCmdCode(netCmdSettings);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] 网络指令类型代码导出失败，请查看控制台错误日志。");
            }

            return UniTask.CompletedTask;
        }

        /// <summary>
        /// Step：导出所有 Proto 协议文件为 C# 代码，调用 ProtoExporter.ExportAllProtos。
        /// 通过 Helpers.ResolveComponentOnNova 定位 NetworkComponent，
        /// 取 ProtoSettings（#if UNITY_EDITOR 字段）后调用导出入口；失败时抛出 InvalidOperationException 中断流水线。
        /// </summary>
        /// <param name="ctx">Runner 下发的运行时上下文。</param>
        /// <returns>完成的 UniTask。</returns>
        [PipifyStep("export.network.proto", "导出 Network 所有 Proto 协议", "导出资源/Network")]
        internal static UniTask RunExportNetworkProto(PipifyContext ctx)
        {
            Runtime.NetworkComponent networkComponent = Helpers.ResolveComponentOnNova<Runtime.NetworkComponent>();
#if UNITY_EDITOR
            Runtime.ProtoSettings protoSettings = networkComponent.ProtoSettings;
            if (protoSettings == null)
            {
                throw new InvalidOperationException("[Pipify] NetworkComponent.ProtoSettings 未配置，请在 Inspector 中完善 Proto 设置。");
            }

            bool success = EditorUtil.Network.ProtoExporter.ExportAllProtos(protoSettings);
            if (!success)
            {
                throw new InvalidOperationException("[Pipify] Proto 协议导出失败，请查看控制台错误日志。");
            }
#endif
            return UniTask.CompletedTask;
        }
    }
}
