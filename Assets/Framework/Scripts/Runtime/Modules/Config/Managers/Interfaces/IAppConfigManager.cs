/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  IAppConfigManager.cs
 * author:    taoye
 * created:   2026/7/27
 * descrip:   框架内部应用配置读取与刷新契约
 ***************************************************************/

using Cysharp.Threading.Tasks;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 框架内部应用配置能力；由默认 ConfigManager 实现，避免扩张公开 IConfigManager
    /// 并破坏项目已有的自定义 Manager 实现。
    /// </summary>
    internal interface IAppConfigManager
    {
        /// <summary>
        /// 按 JSONPath 读取当前 Custom 配置字符串。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="defaultValue">云端与本地均未命中时的调用方默认值。</param>
        /// <returns>当前生效字符串或 defaultValue。</returns>
        string GetString(string key, string defaultValue = null);

        /// <summary>
        /// 按 JSONPath 读取 int；远端值非法时回退本地默认字符串，仍非法时返回调用方默认值。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="defaultValue">本地默认字符串也无法转换时的调用方默认值。</param>
        /// <returns>转换后的 int 或 defaultValue。</returns>
        int GetInt(string key, int defaultValue = default);

        /// <summary>
        /// 按 JSONPath 读取 float；使用固定区域格式，远端值非法时回退本地默认字符串。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="defaultValue">本地默认字符串也无法转换时的调用方默认值。</param>
        /// <returns>转换后的 float 或 defaultValue。</returns>
        float GetFloat(string key, float defaultValue = default);

        /// <summary>
        /// 按 JSONPath 读取 bool；支持 true/false 与 1/0，远端值非法时回退本地默认字符串。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="defaultValue">本地默认字符串也无法转换时的调用方默认值。</param>
        /// <returns>转换后的 bool 或 defaultValue。</returns>
        bool GetBool(string key, bool defaultValue = default);

        /// <summary>
        /// 尝试按 JSONPath 读取当前 Custom 配置字符串。
        /// </summary>
        /// <param name="key">配置路径。</param>
        /// <param name="value">命中时的当前生效字符串。</param>
        /// <returns>云端或本地路径存在且不是显式 null 时返回 true。</returns>
        bool TryGetString(string key, out string value);

        /// <summary>
        /// 显式从 GM 后台拉取并应用一轮应用配置。
        /// </summary>
        /// <returns>成功应用远端快照返回 true。</returns>
        UniTask<bool> RefreshAppConfigAsync();
    }
}
