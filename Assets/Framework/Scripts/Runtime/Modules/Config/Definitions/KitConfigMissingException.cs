/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  KitConfigMissingException.cs
 * author:    taoye
 * created:   2026/5/31
 * descrip:   Kit 配置缺失异常
 ***************************************************************/

using System;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// Kit 配置缺失异常。
    /// 当 Kit Service 方法内通过 Nova.Config.GetKitConfig 取不到对应配置（未在 ConfigWindow 配置或未启用）时抛出，属开发期部署错误，fail-fast 暴露配置漏填。
    /// </summary>
    public sealed class KitConfigMissingException : Exception
    {
        /// <summary>
        /// 构造 KitConfigMissingException 实例。
        /// </summary>
        /// <param name="configTypeName">缺失的 Kit 配置类型全名。</param>
        public KitConfigMissingException(string configTypeName)
            : base($"Kit 配置缺失：{configTypeName} 未在 ConfigWindow「Kit 配置」中配置或未启用。请检查 ConfigMaster 与导出的 ConfigRuntime.asset。")
        {
        }
    }
}
