/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherSettings.cs
 * author:    taoye
 * created:   2026/3/27
 * descrip:   启动阶段配置
 *            集中管理 Preload 之前所有固化资源的路径与参数，
 *            消灭 Procedure 中的硬编码常量。
 ***************************************************************/

using System;
using UnityEngine;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动阶段配置。
    /// 在 ProcedureComponent Inspector 上序列化，
    /// 通过 Nova.Procedure.LauncherSettings 供各内置流程读取。
    /// </summary>
    [Serializable]
    public class LauncherSettings
    {
        /// <summary>
        /// 闪屏最少展示时间（秒）。
        /// </summary>
        [SerializeField]
        private float m_SplashDuration = 2f;
        public float SplashDuration => m_SplashDuration;

        /// <summary>
        /// 闪屏面板 Prefab 路径（相对于 Resources/）。
        /// </summary>
        [SerializeField]
        private string m_SplashPanelPrefab = "BuiltIn/Prefabs/LauncherSplashPanel";
        public string SplashPanelPrefab => m_SplashPanelPrefab;

        /// <summary>
        /// 进度面板 Prefab 路径（相对于 Resources/）。
        /// </summary>
        [SerializeField]
        private string m_ProgressPanelPrefab = "BuiltIn/Prefabs/LauncherProgressPanel";
        public string ProgressPanelPrefab => m_ProgressPanelPrefab;

        /// <summary>
        /// 通用弹窗面板 Prefab 路径（相对于 Resources/）。
        /// </summary>
        [SerializeField]
        private string m_DialogPanelPrefab = "BuiltIn/Prefabs/LauncherDialogPanel";
        public string DialogPanelPrefab => m_DialogPanelPrefab;

        /// <summary>
        /// 启动期多语言 JSON 路径模板（相对于 Resources/，{0} 占位 Language 枚举名）。
        /// </summary>
        [SerializeField]
        private string m_LocalizationJsonPathTemplate = "BuiltIn/Jsons/LocalizationTexts_{0}";
        public string LocalizationJsonPathTemplate => m_LocalizationJsonPathTemplate;
    }
}
