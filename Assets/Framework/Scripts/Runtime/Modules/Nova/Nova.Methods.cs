/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  Nova.Methods.cs
 * author:    taoye
 * created:   2025/12/2
 * descrip:   Nova框架根节点组件-辅助方法
 *            SetCultureInfo / IsStrictCheck / OnLowMemory / ValidateComponent
 ***************************************************************/

using System;
using System.Globalization;
using System.Threading;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Nova 框架根节点组件。
    /// </summary>
    public sealed partial class Nova : FrameworkComponent
    {
        /// <summary>
        /// 设置全局文化信息，避免地区差异导致的格式化问题。
        /// </summary>
        /// <param name="cultureInfo">要设置的文化信息。</param>
        private void SetCultureInfo(CultureInfo cultureInfo)
        {
            try
            {
                CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
                CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;
                Thread.CurrentThread.CurrentCulture = cultureInfo;
                Thread.CurrentThread.CurrentUICulture = cultureInfo;
            }
            catch (Exception e)
            {
                Log.Error(LogTag.Base, "设置全局文化信息出错，错误信息：{0}。", e);
            }
        }

        /// <summary>
        /// 读取或创建框架通用安装时间。
        /// 已有有效记录时直接复用，否则以当前 UTC 时间生成 13 位毫秒时间戳。
        /// </summary>
        /// <returns>框架首次启动时记录的 UTC Unix 毫秒时间戳。</returns>
        private static long GetOrCreateInstallTimeMs()
        {
            if (IsValidInstallTimeMs(s_InstallTimeMs))
            {
                return s_InstallTimeMs;
            }

            if (TryReadInstallTimeMs(c_InstallTimeMsPersistKey, out long installTimeMs))
            {
                s_InstallTimeMs = installTimeMs;
                return s_InstallTimeMs;
            }

            installTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (!IsValidInstallTimeMs(installTimeMs))
            {
                throw new InvalidOperationException("Nova 无法生成 13 位 UTC Unix 毫秒安装时间戳。");
            }

            SaveInstallTimeMs(installTimeMs);
            s_InstallTimeMs = installTimeMs;
            return s_InstallTimeMs;
        }

        /// <summary>
        /// 尝试从启动期 PlayerPrefs 读取有效的 13 位毫秒时间戳。
        /// </summary>
        /// <param name="key">原始持久化键。</param>
        /// <param name="installTimeMs">读取到的安装时间。</param>
        /// <returns>读取且校验成功返回 true。</returns>
        private static bool TryReadInstallTimeMs(string key, out long installTimeMs)
        {
            string raw = PlatformPlayerPrefs.GetString(key, string.Empty);
            return long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out installTimeMs)
                && IsValidInstallTimeMs(installTimeMs);
        }

        /// <summary>
        /// 判断时间戳是否为 13 位正整数。
        /// </summary>
        /// <param name="installTimeMs">待校验的 UTC Unix 毫秒时间戳。</param>
        /// <returns>是 13 位正整数返回 true。</returns>
        private static bool IsValidInstallTimeMs(long installTimeMs)
        {
            return installTimeMs >= 1000000000000L && installTimeMs <= 9999999999999L;
        }

        /// <summary>
        /// 把框架通用安装时间写入跨平台启动期 PlayerPrefs 并立即落盘。
        /// </summary>
        /// <param name="installTimeMs">已校验的 13 位 UTC Unix 毫秒时间戳。</param>
        private static void SaveInstallTimeMs(long installTimeMs)
        {
            PlatformPlayerPrefs.SetString(
                c_InstallTimeMsPersistKey,
                installTimeMs.ToString(CultureInfo.InvariantCulture));
            PlatformPlayerPrefs.Save();
        }

        /// <summary>
        /// 根据当前引用严格检查类型判断是否启用严格检查。
        /// </summary>
        /// <returns>是否启用严格检查。</returns>
        private bool IsStrictCheck()
        {
            switch (m_ReferenceStrictCheckType)
            {
                case ReferenceStrictCheckType.AlwaysEnable:
                    return true;

                case ReferenceStrictCheckType.OnlyEnableWhenDevelopment:
                    return UnityEngine.Debug.isDebugBuild;

                case ReferenceStrictCheckType.OnlyEnableInEditor:
                    return Application.isEditor;

                default:
                    return false;
            }
        }

        /// <summary>
        /// 内存不足预警回调。
        /// 清空引用池、释放无用 Unity 资源并触发 GC 以缓解内存压力。
        /// </summary>
        private void OnLowMemory()
        {
            Log.Warning(LogTag.Base, "内存即将不足，开始释放无用对象池和无用资源......");
            ReferencePool.ClearAll();
            Resources.UnloadUnusedAssets();
            GC.Collect();
        }

        /// <summary>
        /// 校验框架组件是否为空，为空时输出错误日志。
        /// </summary>
        /// <param name="component">待校验的组件实例。</param>
        /// <param name="componentName">组件类型名称（用于日志输出）。</param>
        private static void ValidateComponent(FrameworkComponent component, string componentName)
        {
            if (component == null)
            {
                Log.Error(LogTag.Component, "{0} 未注册，请确保组件已挂载且 GameObject 处于 Active 状态。", componentName);
            }
        }
    }
}
