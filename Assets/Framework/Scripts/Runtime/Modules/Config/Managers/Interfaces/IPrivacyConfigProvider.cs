/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IPrivacyConfigProvider.cs
 * author:    Codex
 * created:   2026/10/21
 * descrip:   供可选框架包读取隐私运行时配置的独立能力契约
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 通用隐私配置读取能力；独立于 IConfigManager，避免要求已有自定义配置管理器增加成员。
    /// </summary>
    public interface IPrivacyConfigProvider
    {
        /// <summary>
        /// 当前已加载快照的隐私配置；加载前返回 null，可选消费者只读取扩展所需的数据。
        /// </summary>
        PrivacyConfigs PrivacyConfigs { get; }
    }
}
