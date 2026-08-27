/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  EditorUtil.Config.ActivePlatform.cs
 * author:    taoye
 * created:   2026/8/27
 * descrip:   Unity Active BuildTarget 到 Nova 编辑期平台的唯一映射入口
 ***************************************************************/

using NovaFramework.Runtime;
using UnityEditor;

namespace NovaFramework.Editor
{
    public static partial class EditorUtil
    {
        public static partial class Config
        {
            /// <summary>
            /// 提供 Unity 当前活动构建目标到 Nova 编辑期平台的唯一映射。
            /// 未建模的构建目标统一返回 <see cref="PlatformType.None"/>，由具体消费方阻止后续操作。
            /// </summary>
            public static class ActivePlatform
            {
                /// <summary>
                /// 实时读取 <see cref="EditorUserBuildSettings.activeBuildTarget"/> 并转换为 Nova 平台。
                /// </summary>
                public static PlatformType Current => FromBuildTarget(EditorUserBuildSettings.activeBuildTarget);

                /// <summary>
                /// 将 Unity 构建目标转换为 Nova 平台；当前仅 Android、iOS、WebGL 具有明确映射。
                /// </summary>
                /// <param name="target">待转换的 Unity 构建目标。</param>
                /// <returns>对应 Nova 平台；未支持时返回 None。</returns>
                public static PlatformType FromBuildTarget(BuildTarget target)
                {
                    switch (target)
                    {
                        case BuildTarget.Android:
                            return PlatformType.Android;
                        case BuildTarget.iOS:
                            return PlatformType.iOS;
                        case BuildTarget.WebGL:
                            return PlatformType.WebGL;
                        default:
                            return PlatformType.None;
                    }
                }

                /// <summary>
                /// 确保当前活动构建目标已映射到 Nova 平台，否则以明确错误阻止编辑期工具继续执行。
                /// </summary>
                /// <param name="operation">错误消息中的操作名称。</param>
                /// <returns>当前活动构建目标对应的 Nova 平台。</returns>
                /// <exception cref="System.InvalidOperationException">当前构建目标未被 Nova 建模时抛出。</exception>
                public static PlatformType RequireCurrent(string operation)
                {
                    PlatformType platform = Current;
                    if (platform != PlatformType.None) return platform;

                    throw new System.InvalidOperationException(
                        $"{operation}失败：Unity 当前 Active BuildTarget={EditorUserBuildSettings.activeBuildTarget} 没有对应的 Nova PlatformType。请先切换到 Android、iOS 或 WebGL。");
                }

                /// <summary>
                /// 确保显式 Unity 构建目标与当前 Active BuildTarget 一致，避免同一流水线的平台产物分叉。
                /// </summary>
                /// <param name="target">调用方准备使用的显式构建目标。</param>
                /// <param name="operation">错误消息中的操作名称。</param>
                /// <exception cref="System.InvalidOperationException">目标未支持或与当前活动目标不一致时抛出。</exception>
                public static void EnsureActiveBuildTarget(BuildTarget target, string operation)
                {
                    RequireCurrent(operation);
                    if (target == EditorUserBuildSettings.activeBuildTarget) return;

                    throw new System.InvalidOperationException(
                        $"{operation}失败：目标平台 {target} 与 Unity 当前 Active BuildTarget={EditorUserBuildSettings.activeBuildTarget} 不一致。请先切换 Unity BuildTarget。");
                }
            }
        }
    }
}
