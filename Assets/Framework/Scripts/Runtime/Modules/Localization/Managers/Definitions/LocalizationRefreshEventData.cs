/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LocalizationRefreshEventData.cs
 * author:    taoye
 * created:   2026/4/10
 * descrip:   本地化语言切换刷新事件数据
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 本地化语言切换刷新事件数据。
    /// </summary>
    public sealed class LocalizationRefreshEventData : EventData
    {
        /// <summary>
        /// 切换前的语言。
        /// </summary>
        public Language OldLanguage { get; private set; }

        /// <summary>
        /// 切换后的语言。
        /// </summary>
        public Language NewLanguage { get; private set; }

        /// <summary>
        /// 从引用池获取并填充事件数据。
        /// </summary>
        /// <param name="oldLanguage">切换前的语言。</param>
        /// <param name="newLanguage">切换后的语言。</param>
        /// <returns>填充后的事件数据实例。</returns>
        public static LocalizationRefreshEventData Create(Language oldLanguage, Language newLanguage)
        {
            LocalizationRefreshEventData e = ReferencePool.Get<LocalizationRefreshEventData>();
            e.OldLanguage = oldLanguage;
            e.NewLanguage = newLanguage;
            return e;
        }

        /// <summary>
        /// 清理引用，重置为默认状态。
        /// </summary>
        public override void Clear()
        {
            OldLanguage = Language.Unspecified;
            NewLanguage = Language.Unspecified;
        }
    }
}
