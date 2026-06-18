/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  ProcedureDataKeys.cs
 * author:    taoye
 * created:   2026/3/26
 * descrip:   流程黑板数据键名常量
 ***************************************************************/

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 流程黑板数据键名常量。
    /// 用于 FSM 黑板 SetData / GetData 的跨流程数据传递。
    /// </summary>
    public static class ProcedureDataKeys
    {
        /// <summary>
        /// App 大版本检查结果（AppVersionResult）。
        /// 写入者：ProcedureCheckVersion | 读取者：ProcedureAppDownload
        /// </summary>
        public const string AppVersionResult = "AppVersionResult";

        /// <summary>
        /// 是否存在资源补丁（bool）。
        /// 写入者：ProcedureCheckVersion | 读取者：ProcedureHotfix
        /// </summary>
        public const string HasAssetPatch = "HasAssetPatch";
    }
}
