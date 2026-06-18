/***************************************************************
 * (c) copyright 2026 - 2030, Solotopia
 * All Rights Reserved.
 * -------------------------------------------------------------
 * filename:  LauncherSplashPanel.cs
 * author:    taoye
 * created:   2026/3/26
 * descrip:   启动阶段闪屏面板
 *            全屏背景 + Logo，纯展示无交互。
 *            固化在 Resources/BuiltIn/Prefabs/ 中，不参与热更。
 ***************************************************************/

using UnityEngine;
using UnityEngine.UI;

namespace NovaFramework.Runtime
{
    /// <summary>
    /// 启动阶段闪屏面板。全屏背景 + Logo，纯展示无交互。
    /// </summary>
    public class LauncherSplashPanel : MonoBehaviour
    {
        /// <summary>
        /// 全屏背景图（预留闪屏动画使用）。
        /// </summary>
        [SerializeField]
        private Image m_Background;

        /// <summary>
        /// Logo 图（预留闪屏动画使用）。
        /// </summary>
        [SerializeField]
        private Image m_Logo;
    }
}
